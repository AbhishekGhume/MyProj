using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using XmlMiddleware.Application.Exceptions;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Messages;
using XmlMiddleware.Domain.Constants;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Functions.Functions;

public class ProcessingFunction
{
    private readonly ILogger<ProcessingFunction> _logger;
    private readonly IProcessingBatchRepository _batchRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IXmlMapperService _xmlMapperService;
    private readonly IAuditService _auditService;
    private readonly IFileGeneratorService _fileGeneratorService;

    public ProcessingFunction(
        ILogger<ProcessingFunction> logger,
        IProcessingBatchRepository batchRepository,
        IBlobStorageService blobStorageService,
        IXmlMapperService xmlMapperService,
        IAuditService auditService,
        IFileGeneratorService fileGeneratorService)
    {
        _logger = logger;
        _batchRepository = batchRepository;
        _blobStorageService = blobStorageService;
        _xmlMapperService = xmlMapperService;
        _auditService = auditService;
        _fileGeneratorService = fileGeneratorService;
    }

    [Function(nameof(ProcessingFunction))]
    public async Task Run(
        [ServiceBusTrigger(
            "%QueueName%",
            Connection = "ServiceBusConnectionString")]
        ServiceBusReceivedMessage sbMessage,
        ServiceBusMessageActions messageActions)
    {
        // DeliveryCount is the broker's own, authoritative attempt counter.
        // It is 1 on the first delivery, 2 on the first redelivery, etc.
        var deliveryAttempt = sbMessage.DeliveryCount;

        FileProcessingMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<FileProcessingMessage>(sbMessage.Body.ToString());
        }
        catch (JsonException)
        {
            message = null;
        }

        if (message is null)
        {
            _logger.LogError(
                "Malformed Message - Dead-Lettering | MessageId={MessageId}",
                sbMessage.MessageId);

            await messageActions.DeadLetterMessageAsync(
                sbMessage,
                deadLetterReason: "MalformedMessage",
                deadLetterErrorDescription: "Message body could not be deserialized to FileProcessingMessage.");

            return;
        }
        
        try
        {
            var batch =
                await _batchRepository.GetByIdAsync(
                    message.BatchId);

            if (batch is null)
            {
                _logger.LogWarning(
                    "Batch Not Found | CorrelationId={CorrelationId} | BatchId={BatchId} | Attempt={Attempt}",
                    message.CorrelationId,
                    message.BatchId,
                    deliveryAttempt);

                // Nothing to retry against - dead-letter immediately rather than
                // burning delivery attempts on a message that can never succeed.
                await MoveInputToErrorAsync(message.BlobPath, message.CorrelationId, message.BatchId);

                await messageActions.DeadLetterMessageAsync(
                    sbMessage,
                    deadLetterReason: "BatchNotFound",
                    deadLetterErrorDescription: $"No ProcessingBatch found for BatchId={message.BatchId}");

                return;
            }

            _logger.LogInformation(
                "Message Received | CorrelationId={CorrelationId} | BatchId={BatchId} | Attempt={Attempt}/{MaxAttempts} | RetryCount={RetryCount} | FileName={FileName}",
                batch.CorrelationId,
                batch.BatchId,
                deliveryAttempt,
                SystemConstants.MaxDeliveryAttempts,
                batch.RetryCount,
                batch.FileName);

            _logger.LogInformation(
                "Processing Started | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                message.CorrelationId,
                message.BatchId,
                batch.FileName);

            if (batch.Status == BatchStatus.Completed)
            {
                _logger.LogInformation(
                    "Batch Already Completed. Skipping. | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                    batch.CorrelationId,
                    batch.BatchId,
                    batch.FileName);

                return;
            }

            batch.Status = BatchStatus.Processing;
            batch.CurrentStep = "ProcessingStarted";
            batch.ProcessingStartDateTime = DateTime.UtcNow;

            _batchRepository.Update(batch);

            await _batchRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                batch.BatchId,
                batch.CorrelationId,
                EventType.OutputStarted,
                "Success",
                "Processing started",
                nameof(ProcessingFunction),
                attemptNumber: deliveryAttempt);

            using var stream =
                await _blobStorageService.ReadAsync(
                    batch.BlobPath);

            var orders =
                await _xmlMapperService.MapAsync(stream);

            _logger.LogInformation(
                "Mapped {Count} Orders | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                orders.Count,
                message.CorrelationId,
                batch.BatchId,
                batch.FileName);

            await GenerateOutputAsync(batch, orders, OutputType.Txt, "txt", deliveryAttempt);
            await GenerateOutputAsync(batch, orders, OutputType.Csv, "csv", deliveryAttempt);
            await GenerateOutputAsync(batch, orders, OutputType.Json, "json", deliveryAttempt);
            await GenerateOutputAsync(batch, orders, OutputType.Dat, "dat", deliveryAttempt);
            await GenerateOutputAsync(batch, orders, OutputType.Pdf, "pdf", deliveryAttempt);
            await GenerateOutputAsync(batch, orders, OutputType.Xlsx, "xlsx", deliveryAttempt);

            // --------------------------------------------------
            // COMPLETE BATCH
            // --------------------------------------------------

            batch.Status = BatchStatus.Completed;
            batch.CurrentStep = "Completed";
            batch.ProcessingEndDateTime = DateTime.UtcNow;

            await _batchRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                batch.BatchId,
                batch.CorrelationId,
                EventType.BatchCompleted,
                "Success",
                "Batch processing completed successfully",
                nameof(ProcessingFunction),
                attemptNumber: deliveryAttempt);

            _logger.LogInformation(
                "Batch Completed | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                message.CorrelationId,
                batch.BatchId,
                batch.FileName);

            // --------------------------------------------------
            // ARCHIVE SOURCE XML
            // --------------------------------------------------

            var fileName = Path.GetFileName(batch.BlobPath);
            var archivePath = $"archive/{fileName}";

            await _blobStorageService.MoveAsync(
                batch.BlobPath,
                archivePath);

            _logger.LogInformation(
                "Source File Archived | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName} | ArchivePath={ArchivePath}",
                message.CorrelationId,
                batch.BatchId,
                batch.FileName,
                archivePath);

            // Message completes automatically on clean return (auto-complete).
        }
        catch (Exception ex)
        {
            var isTransient = IsTransient(ex);

            // Final attempt = either a non-retryable error, or we've hit the same
            // max delivery count that's configured on the Service Bus queue itself.
            var isFinalAttempt = !isTransient || deliveryAttempt >= SystemConstants.MaxDeliveryAttempts;

            if (isTransient)
            {
                _logger.LogWarning(
                    "Transient Failure | CorrelationId={CorrelationId} | BatchId={BatchId} | Attempt={Attempt}/{MaxAttempts} | Error={Error}",
                    message.CorrelationId, message.BatchId, deliveryAttempt, SystemConstants.MaxDeliveryAttempts, ex.Message);
            }
            else
            {
                _logger.LogError(
                    "Permanent Failure | CorrelationId={CorrelationId} | BatchId={BatchId} | Attempt={Attempt}/{MaxAttempts} | Error={Error}",
                    message.CorrelationId, message.BatchId, deliveryAttempt, SystemConstants.MaxDeliveryAttempts, ex.Message);
            }

            var batch =
                await _batchRepository.GetByIdAsync(
                    message.BatchId);

            if (batch is not null)
            {
                _logger.LogWarning(
                    "Failure Classification | Type={FailureType} | Final={IsFinalAttempt} | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                    isTransient ? "Transient" : "Permanent",
                    isFinalAttempt,
                    batch.CorrelationId,
                    batch.BatchId,
                    batch.FileName);

                batch.RetryCount = deliveryAttempt;
                batch.LastRetryDateTime = DateTime.UtcNow;
                batch.ProcessingEndDateTime = DateTime.UtcNow;
                batch.ErrorCode = ex.GetType().Name;
                batch.ErrorMessage = ex.Message;
                batch.Status = isFinalAttempt ? BatchStatus.Failed : BatchStatus.Processing;
                batch.CurrentStep = isFinalAttempt ? "DeadLettered" : "RetryPending";

                await _batchRepository.SaveChangesAsync();

                await _auditService.LogEventAsync(
                    batch.BatchId,
                    batch.CorrelationId,
                    isFinalAttempt ? EventType.MaxRetriesExceeded : EventType.OutputFailed,
                    isTransient ? "Transient" : "Permanent",
                    ex.Message,
                    nameof(ProcessingFunction),
                    attemptNumber: deliveryAttempt);

                if (isFinalAttempt)
                {
                    // Own the DLQ transition explicitly instead of relying on the
                    // broker's implicit max-delivery-count behavior, so we get a
                    // clear, queryable reason on the dead-lettered message itself
                    // and a matching terminal record in our own DB.
                    batch.Status = BatchStatus.DeadLettered;
                    await _batchRepository.SaveChangesAsync();

                    _logger.LogError(
                        "Max Attempts Reached - Dead-Lettering Message | CorrelationId={CorrelationId} | BatchId={BatchId} | Attempt={Attempt}/{MaxAttempts} | FileName={FileName} | Error={Error}",
                        batch.CorrelationId,
                        batch.BatchId,
                        deliveryAttempt,
                        SystemConstants.MaxDeliveryAttempts,
                        batch.FileName,
                        ex.Message);

                    await MoveInputToErrorAsync(batch.BlobPath, batch.CorrelationId, batch.BatchId);

                    await messageActions.DeadLetterMessageAsync(
                        sbMessage,
                        deadLetterReason: isTransient ? "MaxRetriesExceeded" : ex.GetType().Name,
                        deadLetterErrorDescription: ex.Message);

                    return; // explicitly handled - do not rethrow
                }
            }

            await messageActions.AbandonMessageAsync(sbMessage);
            return;
        }
    }

    /// <summary>
    /// Moves the original input blob to the error/ folder once a batch is being
    /// dead-lettered, mirroring the existing archive/ move used on success.
    /// Best-effort: a failure here must never stop the DLQ handling itself, since
    /// the message has already been (or is about to be) settled either way.
    /// </summary>
    private async Task MoveInputToErrorAsync(
        string blobPath,
        Guid correlationId,
        long batchId)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return;
        }

        try
        {
            var fileName = Path.GetFileName(blobPath);
            var errorPath = $"error/{fileName}";

            await _blobStorageService.MoveAsync(
                blobPath,
                errorPath);

            _logger.LogInformation(
                "Source File Moved To Error Folder | CorrelationId={CorrelationId} | BatchId={BatchId} | SourcePath={SourcePath} | ErrorPath={ErrorPath}",
                correlationId,
                batchId,
                blobPath,
                errorPath);
        }
        catch (Exception moveEx)
        {
            // The blob may already have been moved/archived by a prior run, or the
            // move itself may fail transiently - either way, don't let this block
            // the DLQ transition that's already been decided.
            _logger.LogError(
                "Failed To Move Source File To Error Folder | CorrelationId={CorrelationId} | BatchId={BatchId} | SourcePath={SourcePath} | Error={Error}",
                correlationId,
                batchId,
                blobPath,
                moveEx.Message);
        }
    }

    private async Task GenerateOutputAsync(
        ProcessingBatch batch,
        List<Application.Models.CanonicalOrderModel> orders,
        OutputType outputType,
        string extension,
        int deliveryAttempt)
    {
        var outputFile = batch.OutputFiles
            .FirstOrDefault(x => x.OutputType == outputType);

        if (outputFile is null)
        {
            _logger.LogError(
                "{OutputType} Output Record Not Found | CorrelationId={CorrelationId} | BatchId={BatchId}",
                outputType,
                batch.CorrelationId,
                batch.BatchId);

            return;
        }

        // Idempotency (Skip Already Generated Outputs)
        if (outputFile.Status == OutputStatus.Success)
        {
            _logger.LogInformation(
                "{OutputType} Already Generated. Skipping. | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                outputType,
                batch.CorrelationId,
                batch.BatchId,
                batch.FileName);

            return;
        }

        try
        {
            outputFile.Status = OutputStatus.Processing;
            outputFile.StartedDateTime = DateTime.UtcNow;
            outputFile.AttemptCount++;

            _logger.LogInformation(
                "Generating {OutputType} | Attempt={AttemptCount} (Delivery {DeliveryAttempt}/{MaxAttempts}) | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                outputType,
                outputFile.AttemptCount,
                deliveryAttempt,
                SystemConstants.MaxDeliveryAttempts,
                batch.CorrelationId,
                batch.BatchId,
                batch.FileName);

            batch.CurrentStep = $"Generating{outputType}";

            await _batchRepository.SaveChangesAsync();

            if (outputType == OutputType.Pdf)
            {
               throw new TransientException(
                   "Forced PDF Timeout");
            }

            var generatedStream =
                await _fileGeneratorService.GenerateAsync(
                    orders,
                    outputType);

            var baseFileName = Path.GetFileNameWithoutExtension(batch.FileName);
            var folderName = $"{batch.CorrelationId}_{baseFileName}";
            var outputFileName = $"{baseFileName}.{extension}";
            var blobPath = $"output/{folderName}/{outputFileName}";

            await _blobStorageService.UploadAsync(
                generatedStream,
                blobPath);

            outputFile.OutputBlobPath = blobPath;
            outputFile.Status = OutputStatus.Success;
            outputFile.CompletedDateTime = DateTime.UtcNow;
            outputFile.ErrorCode = null;
            outputFile.ErrorMessage = null;

            batch.CurrentStep = $"{outputType}Completed";

            await _batchRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                batch.BatchId,
                batch.CorrelationId,
                EventType.OutputCompleted,
                "Success",
                $"{outputType} generated successfully",
                nameof(ProcessingFunction),
                attemptNumber: outputFile.AttemptCount,
                outputFileId: outputFile.OutputFileId);

            _logger.LogInformation(
                "{OutputType} Generated | CorrelationId={CorrelationId} | BatchId={BatchId} | BlobPath={BlobPath} | FileName={FileName}",
                outputType,
                batch.CorrelationId,
                batch.BatchId,
                blobPath,
                batch.FileName);
        }
        catch (Exception ex)
        {
            var isTransient = IsTransient(ex);

            // Always record what happened on this output, even for a transient
            // error - only the terminal (Failed) status is reserved for permanent
            // failures. This is what was missing before: ErrorCode was never set,
            // and a transient failure here left no trace at all on the OutputFile.
            outputFile.ErrorCode = ex.GetType().Name;
            outputFile.ErrorMessage = ex.Message;

            if (!isTransient)
            {
                outputFile.Status = OutputStatus.Failed;
                outputFile.CompletedDateTime = DateTime.UtcNow;
                batch.CurrentStep = $"{outputType}Failed";
            }
            else
            {
                outputFile.Status = OutputStatus.RetryPending;
                batch.CurrentStep = $"{outputType}RetryPending";

                _logger.LogWarning(
                    "Transient Failure During {OutputType} Generation | AttemptCount={AttemptCount} | BatchId={BatchId} | FileName={FileName}",
                    outputType,
                    outputFile.AttemptCount,
                    batch.BatchId,
                    batch.FileName);
            }

            await _batchRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                batch.BatchId,
                batch.CorrelationId,
                EventType.OutputFailed,
                isTransient ? "Transient" : "Permanent",
                ex.Message,
                nameof(ProcessingFunction),
                attemptNumber: outputFile.AttemptCount,
                outputFileId: outputFile.OutputFileId);

            throw;
        }
    }

    private static bool IsTransient(Exception ex)
    {
        return ex switch
        {
            TimeoutException => true,
            IOException => true,
            TransientException => true,
            _ => false
        };
    }
}