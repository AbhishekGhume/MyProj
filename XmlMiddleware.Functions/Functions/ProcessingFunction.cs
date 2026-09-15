using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using XmlMiddleware.Application.Exceptions;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Messages;
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
        FileProcessingMessage message)
    {
        try
        {
            
            var batch =
                await _batchRepository.GetByIdAsync(
                    message.BatchId);

            

            if (batch is null)
            {
                _logger.LogWarning(
                    "Batch Not Found | CorrelationId={CorrelationId} | BatchId={BatchId}",
                    message.CorrelationId,
                    message.BatchId);

                return;
            }

            _logger.LogInformation(
                "Message Received | CorrelationId={CorrelationId} | BatchId={BatchId} | RetryCount={RetryCount} | FileName={FileName}",
                batch.CorrelationId,
                batch.BatchId,
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
                nameof(ProcessingFunction));

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

            await GenerateOutputAsync(
                batch,
                orders,
                OutputType.Txt,
                "txt");

            await GenerateOutputAsync(
                batch,
                orders,
                OutputType.Csv,
                "csv");

            await GenerateOutputAsync(
                batch,
                orders,
                OutputType.Json,
                "json");

            await GenerateOutputAsync(
                batch,
                orders,
                OutputType.Dat,
                "dat");

            await GenerateOutputAsync(
                batch,
                orders,
                OutputType.Pdf,
                "pdf");

            await GenerateOutputAsync(
                batch,
                orders,
                OutputType.Xlsx,
                "xlsx");

            // --------------------------------------------------
            // COMPLETE BATCH
            // --------------------------------------------------

            batch.Status = BatchStatus.Completed;

            batch.CurrentStep = "Completed";

            batch.ProcessingEndDateTime =
                DateTime.UtcNow;

            await _batchRepository.SaveChangesAsync();

            await _auditService.LogEventAsync(
                batch.BatchId,
                batch.CorrelationId,
                EventType.BatchCompleted,
                "Success",
                "Batch processing completed successfully",
                nameof(ProcessingFunction));

            _logger.LogInformation(
                "Batch Completed | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                message.CorrelationId,
                batch.BatchId,
                batch.FileName);

            // --------------------------------------------------
            // ARCHIVE SOURCE XML
            // --------------------------------------------------

            var fileName =
                Path.GetFileName(batch.BlobPath);

            var archivePath =
                $"archive/{fileName}";

            await _blobStorageService.MoveAsync(
                batch.BlobPath,
                archivePath);

            _logger.LogInformation(
                "Source File Archived | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName} | ArchivePath={ArchivePath}",
                message.CorrelationId,
                batch.BatchId,
                batch.FileName,
                archivePath);
        }
        catch (Exception ex)
        {
            if (IsTransient(ex))
            {
                _logger.LogWarning(
                    
                    "Transient Failure | CorrelationId={CorrelationId} | BatchId={BatchId} | Error={Error}", message.CorrelationId,
                    message.BatchId, ex.Message);
            }
            else
            {
                _logger.LogError(
                    "Permanent Failure | CorrelationId={CorrelationId} | BatchId={BatchId} | Error={Error}", message.CorrelationId,
                    message.BatchId, ex.Message);
            }

            var batch =
                await _batchRepository.GetByIdAsync(
                    message.BatchId);

            if (batch is not null)
            {
                var isTransient =
                    IsTransient(ex);

                _logger.LogWarning(
                    "Failure Classification | Type={FailureType} | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                    isTransient ? "Transient" : "Permanent",
                    batch.CorrelationId,
                    batch.BatchId,
                    batch.FileName);

          

                if (isTransient)
                {
                    batch.Status =
                    BatchStatus.Processing;
                }
                else
                {
                    batch.Status =
                    BatchStatus.Failed;
                }

                batch.RetryCount++;

                batch.LastRetryDateTime =
                    DateTime.UtcNow;

                batch.ProcessingEndDateTime =
                    DateTime.UtcNow;

                await _batchRepository.SaveChangesAsync();

                await _auditService.LogEventAsync(
                    batch.BatchId,
                    batch.CorrelationId,
                    EventType.OutputFailed,
                    isTransient ? "Transient" : "Permanent",
                    ex.Message,
                    nameof(ProcessingFunction));
            }

            throw;
        }
    }

    private async Task GenerateOutputAsync(
        ProcessingBatch batch,
        List<Application.Models.CanonicalOrderModel> orders,
        OutputType outputType,
        string extension)
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
            _logger.LogInformation(
                "Generating {OutputType} | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                outputType,
                batch.CorrelationId,
                batch.BatchId,
                batch.FileName);

            outputFile.Status = OutputStatus.Processing;
            outputFile.StartedDateTime = DateTime.UtcNow;
            outputFile.AttemptCount++;

            //_logger.LogInformation(
            //    "{OutputType} Attempt #{AttemptCount} | BatchId={BatchId}",
            //    outputType,
            //    outputFile.AttemptCount,
            //    batch.BatchId);

            batch.CurrentStep = $"Generating{outputType}";

            await _batchRepository.SaveChangesAsync();

            //if (outputType == OutputType.Pdf)
            //{
            //    throw new TransientException(
            //        "Forced PDF Timeout");
            //}

            var generatedStream =
                await _fileGeneratorService.GenerateAsync(
                    orders,
                    outputType);

            var baseFileName =
                Path.GetFileNameWithoutExtension(
                batch.FileName);

            var folderName =
                $"{batch.CorrelationId}_{baseFileName}";

            var outputFileName =
                $"{baseFileName}.{extension}";

            var blobPath =
                $"output/{folderName}/{outputFileName}";

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
            var isTransient =
                IsTransient(ex);

            if (!isTransient)
            {
                outputFile.Status =
                    OutputStatus.Failed;

                outputFile.ErrorMessage =
                    ex.Message;

                outputFile.CompletedDateTime =
                    DateTime.UtcNow;

                batch.CurrentStep =
                    $"{outputType}Failed";

                await _batchRepository.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning(
                    "Transient Failure During {OutputType} Generation | BatchId={BatchId} | FileName={FileName}",
                    outputType,
                    batch.BatchId,
                    batch.FileName);
            }

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