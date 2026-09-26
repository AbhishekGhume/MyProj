using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using XmlMiddleware.Application.Configuration;
using XmlMiddleware.Application.Exceptions;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Logging;
using XmlMiddleware.Application.Messages;
using XmlMiddleware.Application.Models;
using XmlMiddleware.Domain.Constants;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Domain.Enums;
using XmlMiddleware.Domain.Rules;
using System.Globalization;

namespace XmlMiddleware.Functions.Functions;

/// <summary>
/// The single function of the pipeline:
///   Blob (input/) -> Event Grid -> Service Bus queue -> ProcessingFunction
///
/// Message settlement rules
///   * Business outcomes (duplicate, bad extension, invalid XML, data conflict, success)
///     are FINAL results: the batch row records them and the message is COMPLETED.
///   * Any unexpected error is retried: the message is abandoned until the
///     MaxDeliveryAttempts-th delivery. Only when that last attempt fails is the
///     message dead-lettered.
///   * Cancellation (host shutdown / timeout) is never counted as a failure reason;
///     the message is abandoned so it is redelivered.
///
/// Every step is: change batch state -> save state + journey event in ONE SaveChanges
/// -> write log lines (all carrying CorrelationId, BatchId, FileName).
/// </summary>
public class ProcessingFunction
{
    private const string FunctionName = nameof(ProcessingFunction);
    private const string BlobCreatedEventType = "Microsoft.Storage.BlobCreated";

    private readonly ILogger<ProcessingFunction> _logger;
    private readonly IProcessingBatchRepository _batchRepository;
    private readonly IOutputFileRepository _outputFileRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IXmlMapperService _xmlMapperService;
    private readonly IAuditService _auditService;
    private readonly IFileGeneratorService _fileGeneratorService;
    private readonly IOrderPersistenceService _orderPersistenceService;
    private readonly IHashService _hashService;
    private readonly IXmlValidationService _xmlValidationService;
    private readonly ProcessingFolders _folders;

    public ProcessingFunction(
        ILogger<ProcessingFunction> logger,
        IProcessingBatchRepository batchRepository,
        IOutputFileRepository outputFileRepository,
        IBlobStorageService blobStorageService,
        IXmlMapperService xmlMapperService,
        IAuditService auditService,
        IFileGeneratorService fileGeneratorService,
        IOrderPersistenceService orderPersistenceService,
        IHashService hashService,
        IXmlValidationService xmlValidationService,
        ProcessingFolders folders)
    {
        _logger = logger;
        _batchRepository = batchRepository;
        _outputFileRepository = outputFileRepository;
        _blobStorageService = blobStorageService;
        _xmlMapperService = xmlMapperService;
        _auditService = auditService;
        _fileGeneratorService = fileGeneratorService;
        _orderPersistenceService = orderPersistenceService;
        _hashService = hashService;
        _xmlValidationService = xmlValidationService;
        _folders = folders;
    }

    // =====================================================================
    // ENTRY POINT
    // =====================================================================

    [Function(nameof(ProcessingFunction))]
    public async Task Run(
        [ServiceBusTrigger(
            "%QueueName%",
            Connection = "ServiceBusConnectionString")]
        ServiceBusReceivedMessage sbMessage,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        // DeliveryCount is the broker's own attempt counter: 1 on the first delivery.
        var attempt = sbMessage.DeliveryCount;

        // ---- 1. read the Event Grid envelope --------------------------------

        EventGridBlobCreatedMessage? eventMessage = null;
        string? parseError = null;

        try
        {
            eventMessage =
                JsonSerializer.Deserialize<EventGridBlobCreatedMessage>(
                    sbMessage.Body.ToString());

            if (eventMessage is null)
            {
                parseError = "Message body is empty.";
            }
        }
        catch (JsonException ex)
        {
            parseError = $"Message body is not valid JSON: {ex.Message}";
        }

        // Same Event Grid event id => same CorrelationId on every redelivery.
        var correlationId =
            ResolveCorrelationId(
                eventMessage?.Id,
                sbMessage.MessageId);

        var ctx = new ProcessingContext(
            correlationId,
            "unknown",
            string.Empty,
            attempt);

        if (parseError is not null || eventMessage is null)
        {
            // No batch can exist for a message we cannot read. It follows the same
            // retry rule as everything else: dead-lettered only after the last attempt.
            await HandleFailureAsync(
                ctx,
                new InvalidDataException(parseError ?? "Message could not be read."),
                sbMessage,
                messageActions,
                cancellationToken);

            return;
        }

        var (kind, blobPath, reason) = ResolveBlob(eventMessage);

        if (!string.IsNullOrEmpty(blobPath))
        {
            ctx.BlobPath = _folders.ToFullPath(blobPath);
            ctx.FileName = Path.GetFileName(blobPath);
        }

        if (kind == BlobResolution.Ignore)
        {
            Log(ctx, LogLevel.Information, $"Message ignored: {reason}");

            await messageActions.CompleteMessageAsync(
                sbMessage,
                CancellationToken.None);

            return;
        }

        if (kind == BlobResolution.Malformed)
        {
            await HandleFailureAsync(
                ctx,
                new InvalidDataException(reason),
                sbMessage,
                messageActions,
                cancellationToken);

            return;
        }

        using var scope = _logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = ctx.CorrelationId,
                ["FileName"] = ctx.FileName
            });

        Log(
            ctx,
            LogLevel.Information,
            $"Message received | Attempt={attempt}/{SystemConstants.MaxDeliveryAttempts} | MessageId={sbMessage.MessageId} | BlobPath={ctx.BlobPath}");

        // ---- 2. process ------------------------------------------------------

        Outcome outcome;

        try
        {
            outcome = await ProcessAsync(ctx, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await HandleCancellationAsync(
                ctx,
                sbMessage,
                messageActions);

            return;
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(
                ctx,
                ex,
                sbMessage,
                messageActions,
                cancellationToken);

            return;
        }

        // ---- 3. settle -------------------------------------------------------

        await SettleAsync(
            ctx,
            outcome,
            sbMessage,
            messageActions);
    }

    // =====================================================================
    // PIPELINE
    // =====================================================================

    private async Task<Outcome> ProcessAsync(
        ProcessingContext ctx,
        CancellationToken ct)
    {
        // ---- load or create the batch ----------------------------------------

        var existing =
            await _batchRepository.GetByCorrelationIdAsync(
                ctx.CorrelationId,
                ct);

        if (existing is not null)
        {
            ctx.Batch = existing;
            var manuallyResumed = false;

            Log(
                ctx,
                LogLevel.Information,
                $"DB: batch loaded | Status={existing.Status} | Step={existing.CurrentStep} | RetryCount={existing.RetryCount}");

            if (BatchStateMachine.IsTerminal(existing.Status))
            {
                if (existing.Status == BatchStatus.DeadLettered && ctx.Attempt <= 1)
                {
                    manuallyResumed = true;
                    ctx.Batch = existing;
                    existing.RetryCount = 0;
                    existing.LastRetryDateTime = DateTime.UtcNow;

                    var resumeFromOutput = existing.OutputFiles.Count > 0;

                    await TransitionAsync(
                        ctx,
                        EventType.RetryStarted,
                        "ManualResend",
                        $"Manual DLQ resend accepted - resuming {(resumeFromOutput ? "at output generation" : "from intake")}",
                        BatchSteps.RetryStarted,
                        resumeFromOutput ? BatchStatus.Processing : BatchStatus.Received,
                        ct: ct);
                }
                else
                {
                    // The message is being redelivered although its outcome is already stored
                    // (e.g. the process stopped after the DB commit but before the settle).
                    if (existing.Status == BatchStatus.DeadLettered)
                    {
                        Log(
                            ctx,
                            LogLevel.Warning,
                            "Batch was already dead-lettered - dead-lettering the redelivered message");

                        return new Outcome(
                            true,
                            existing.ErrorMessage ?? "Batch was already dead-lettered.");
                    }

                    await RecordEventAsync(
                        ctx,
                        EventType.MessageIgnored,
                        "Ignored",
                        $"Redelivered message ignored: batch is already {existing.Status}",
                        existing.CurrentStep,
                        existing.CurrentStep,
                        ct: ct);

                    Log(
                        ctx,
                        LogLevel.Information,
                        $"Redelivered message ignored: batch is already {existing.Status}");

                    return Outcome.Complete;
                }
            }

            if (!manuallyResumed && ctx.Attempt <= 1)
            {
                // First delivery of this message, but a batch for the same Event Grid event
                // is still in flight: a second copy of the event. Do not run it twice.
                await RecordEventAsync(
                    ctx,
                    EventType.MessageIgnored,
                    "Ignored",
                    $"Duplicate event ignored: batch is already {existing.Status} (step {existing.CurrentStep})",
                    existing.CurrentStep,
                    existing.CurrentStep,
                    ct: ct);

                Log(
                    ctx,
                    LogLevel.Warning,
                    $"Duplicate event ignored: batch is already {existing.Status}");

                return Outcome.Complete;
            }

            if (!manuallyResumed)
            {
                // ---- retry: resume the existing batch ------------------------------

                var outputsExist = existing.OutputFiles.Count > 0;

                existing.RetryCount = Math.Max(0, ctx.Attempt - 1);
                existing.LastRetryDateTime = DateTime.UtcNow;

                await TransitionAsync(
                    ctx,
                    EventType.RetryStarted,
                    "Retry",
                    $"Retry attempt {ctx.Attempt}/{SystemConstants.MaxDeliveryAttempts} started - resuming {(outputsExist ? "at output generation" : "from intake")}",
                    BatchSteps.RetryStarted,
                    outputsExist ? BatchStatus.Processing : BatchStatus.Received,
                    ct: ct);
            }
        }
        else
        {
            // --------------------------------------------------
            // NEW EVENT / FRESH UPLOAD
            // --------------------------------------------------
            //
            // A fresh upload always has a new Event Grid event
            // and therefore normally a new CorrelationId.
            //
            // Before creating a new ProcessingBatch, calculate
            // the incoming file hash and check whether this is
            // actually a re-upload of a previously DeadLettered
            // batch.
            // --------------------------------------------------

            using var incomingStream =
                await _blobStorageService.ReadAsync(
                    _folders.ToRelativePath(
                        ctx.BlobPath),
                    ct);

            var incomingHash =
                await CalculateIncomingHashAsync(
                    incomingStream,
                    ct);

            var recoverableBatch =
                await _batchRepository
                    .GetLatestDeadLetteredByHashAsync(
                        incomingHash,
                        ct);

            if (recoverableBatch is not null)
            {
                // --------------------------------------------------
                // RE-UPLOAD RECOVERY
                // --------------------------------------------------
                //
                // Same content was previously DeadLettered.
                //
                // Do NOT create a new ProcessingBatch.
                // Resume the previous batch instead.
                // --------------------------------------------------

                ctx.Batch =
                    recoverableBatch;

                // The newly uploaded Event Grid message belongs to a
                // new event, so its CorrelationId is different.
                //
                // We intentionally keep the OLD batch CorrelationId
                // because this recovery continues the old processing
                // journey.
                ctx.CorrelationId =
                    recoverableBatch.CorrelationId;

                ctx.RecoveryPreviousBlobPath = recoverableBatch.BlobPath;

                var previousFailedPath =
                        recoverableBatch.BlobPath;

                // The new input blob becomes the current physical
                // source for this recovery attempt.
                recoverableBatch.BlobPath =
                        ctx.BlobPath;

                // Keep OriginalBlobPath as historical information.
                // Do NOT replace it with the new upload path.

                recoverableBatch.RetryCount =
                    0;

                recoverableBatch.LastRetryDateTime =
                    DateTime.UtcNow;

                recoverableBatch.ProcessingEndDateTime =
                    null;

                var resumeFromOutput =
                    recoverableBatch.OutputFiles.Count > 0;

                await TransitionAsync(
                    ctx,
                    EventType.RetryStarted,
                    "ReUploadRecovery",
                    $"Previously dead-lettered file re-uploaded - " +
                    $"recovering BatchId={recoverableBatch.BatchId} " +
                    $"and resuming " +
                    $"{(resumeFromOutput
                        ? "at output generation"
                        : "from intake")}",
                    BatchSteps.RetryStarted,
                    resumeFromOutput
                        ? BatchStatus.Processing
                        : BatchStatus.Received,
                    ct: ct);

                Log(
                    ctx,
                    LogLevel.Information,
                    $"Re-upload recovery accepted | " +
                    $"BatchId={recoverableBatch.BatchId} | " +
                    $"FileHash={incomingHash} | " +
                    $"NewSource={ctx.BlobPath}");
            }
            else
            {
                // --------------------------------------------------
                // ORDINARY NEW FILE
                // --------------------------------------------------

                await CreateBatchAsync(
                    ctx,
                    ct);
            }
        }

        var batch = ctx.Batch!;

        ctx.HasPartialSuccess = batch.InvalidRecordCount > 0;

        await EnsureInProcessingCopyAsync(ctx, ct);

        // The output rows only exist once the data has been accepted and persisted,
        // so "outputs exist" tells us intake is finished (also the resume checkpoint).
        var outputs =
            await _outputFileRepository.GetByBatchIdAsync(
                batch.BatchId,
                ct);

        var intakeDone = outputs.Count > 0;

        // ---- intake: extension ---------------------------------------------

        XmlValidationResult? validation = null;

        if (!intakeDone)
        {
            if (!await CheckExtensionAsync(ctx, ct))
            {
                return Outcome.Complete;
            }
        }

        // ---- read the blob ---------------------------------------------------

        using var stream =
            await _blobStorageService.ReadAsync(
                _folders.ToRelativePath(batch.BlobPath),
                ct);

        Log(
            ctx,
            LogLevel.Information,
            $"Source blob read | Path={batch.BlobPath} | Bytes={stream.Length}");

        // ---- intake: hash, duplicate check, XML validation -------------------

        if (!intakeDone)
        {
            var hash = await CalculateHashAsync(ctx, stream, ct);

            if (!await CheckDuplicateAsync(ctx, hash, ct))
            {
                return Outcome.Complete;
            }

            validation = await ValidateXmlAsync(ctx, stream, ct);

            if (validation is null)
            {
                return Outcome.Complete;
            }
        }

        // ---- map ---------------------------------------------------------------

        var orders =
            await _xmlMapperService.MapAsync(
                stream,
                ct);

        if (validation is null && batch.InvalidRecordCount > 0)
        {
            validation = await _xmlValidationService.ValidateAsync(stream, ct);
            stream.Position = 0;
        }

        if (validation is not null &&
            validation.ValidRecordNumbers.Count < orders.Count)
        {
            ctx.HasPartialSuccess = validation.ValidRecordNumbers.Count > 0;

            foreach (var invalidOrder in validation.OrderErrors)
            {
                Log(
                    ctx,
                    LogLevel.Warning,
                    $"Order rejected | Record={invalidOrder.Key} | Errors={string.Join("; ", invalidOrder.Value)}");
            }

            orders = orders
                .Where((_, index) => validation.ValidRecordNumbers.Contains(index + 1))
                .ToList();
        }

        if (!intakeDone)
        {
            await TransitionAsync(
                ctx,
                EventType.OrdersMapped,
                "Success",
                $"XML mapped to canonical model ({orders.Count} order(s))",
                BatchSteps.OrdersMapped,
                ct: ct);

            // ---- persist (business rules live here) --------------------------

            PersistOrdersResult persisted;

            try
            {
                persisted =
                    await _orderPersistenceService.PersistOrdersAsync(
                        batch.BatchId,
                        ctx.CorrelationId,
                        ctx.FileName,
                        batch.FileHash,
                        orders,
                        ct);
            }
            catch (DataIntegrityException ex)
            {
                batch.ProcessingEndDateTime = DateTime.UtcNow;

                await TransitionAsync(
                    ctx,
                    EventType.DataIntegrityFailed,
                    "Rejected",
                    $"Business data rejected: {ex.Message}",
                    BatchSteps.Rejected,
                    BatchStatus.Rejected,
                    errorCode: nameof(DataIntegrityException),
                    errorMessage: ex.Message,
                    level: LogLevel.Warning,
                    ct: ct);

                // No output rows were created, so there is nothing to mark as failed.
                await MoveSourceAsync(
                    ctx,
                    _folders.Error,
                    EventType.SourceFileMovedToError,
                    null,
                    ct);

                return Outcome.Complete;
            }

            if (persisted.FailedOrders.Count > 0)
            {
                ctx.HasPartialSuccess = true;

                foreach (var failedOrder in persisted.FailedOrders)
                {
                    Log(
                        ctx,
                        LogLevel.Warning,
                        $"Order rejected during persistence | OrderId={failedOrder.OrderId} | Error={failedOrder.Reason}");
                }

                orders = orders
                    .Where(order => !persisted.FailedOrders.Any(
                        failed => string.Equals(
                            failed.OrderId,
                            order.OrderId,
                            StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (orders.Count == 0)
                {
                    var error = "All orders failed business persistence checks.";
                    batch.ProcessingEndDateTime = DateTime.UtcNow;

                    await TransitionAsync(
                        ctx,
                        EventType.DataIntegrityFailed,
                        "Rejected",
                        error,
                        BatchSteps.Rejected,
                        BatchStatus.Rejected,
                        errorCode: nameof(DataIntegrityException),
                        errorMessage: error,
                        level: LogLevel.Warning,
                        ct: ct);

                    await MoveSourceAsync(
                        ctx,
                        _folders.Error,
                        EventType.SourceFileMovedToError,
                        null,
                        ct);

                    return Outcome.Complete;
                }
            }

            await TransitionAsync(
                ctx,
                EventType.OrdersPersisted,
                "Success",
                persisted.Skipped
                    ? $"Order skipped: {persisted.SkipReason}"
                    : $"Saved: {persisted.OrdersInserted} order(s), {persisted.OrderLinesInserted} line(s), {persisted.CustomersInserted} new customer(s), {persisted.ProductsInserted} new product(s)",
                BatchSteps.OrdersPersisted,
                ct: ct);

            // ---- output rows: only now, when generation will really happen ----

            outputs = Enum.GetValues<OutputType>()
                .OrderBy(t => (int)t)
                .Select(t => new OutputFile
                {
                    BatchId = batch.BatchId,
                    OutputType = t,
                    Status = OutputStatus.Pending,
                    AttemptCount = 0
                })
                .ToList();

            await _outputFileRepository.AddRangeAsync(
                outputs,
                ct);

            // Saved together with the batch step and the journey event.
            await TransitionAsync(
                ctx,
                EventType.OutputRecordsCreated,
                "Success",
                $"{outputs.Count} output record(s) created: {string.Join(", ", outputs.Select(o => o.OutputType))}",
                BatchSteps.OutputRecordsCreated,
                ct: ct);
        }
        else
        {
            Log(
                ctx,
                LogLevel.Information,
                $"Intake already completed in an earlier attempt - {orders.Count} order(s) mapped again for output generation");
        }

        // ---- processing ----------------------------------------------------------

        batch.ProcessingStartDateTime ??= DateTime.UtcNow;

        if (batch.Status != BatchStatus.Processing)
        {
            await TransitionAsync(
                ctx,
                EventType.ProcessingStarted,
                "Success",
                "Output generation started",
                BatchSteps.ProcessingStarted,
                BatchStatus.Processing,
                ct: ct);
        }

        foreach (var output in outputs.OrderBy(o => (int)o.OutputType))
        {
            // XLSX remains one physical workbook containing
            // one worksheet per Order.
            if (output.OutputType == OutputType.Xlsx)
            {
                await GenerateOutputAsync(
                    ctx,
                    output,
                    orders,
                    ct);

                continue;
            }

            // TXT, CSV, JSON, DAT and PDF generate
            // one physical file per Order.
            await GeneratePerOrderOutputsAsync(
                ctx,
                output,
                orders,
                ct);
        }

        Log(
            ctx,
            LogLevel.Information,
            $"All output formats generated | Formats={string.Join(", ", outputs.Where(o => o.Status == OutputStatus.Success).OrderBy(o => (int)o.OutputType).Select(o => o.OutputType))}");

        await PublishOutputsAsync(ctx, outputs, ct);

        // ---- complete ---------------------------------------------------------------

        batch.ProcessingEndDateTime = DateTime.UtcNow;

        var finalStatus = ctx.HasPartialSuccess
            ? BatchStatus.PartialSuccess
            : BatchStatus.Completed;

        await TransitionAsync(
            ctx,
            EventType.BatchCompleted,
            ctx.HasPartialSuccess ? "PartialSuccess" : "Success",
            ctx.HasPartialSuccess
                ? "Batch completed with valid orders processed and invalid orders recorded in the journey log"
                : "Batch completed - all outputs generated",
            BatchSteps.Completed,
            finalStatus,
            ct: ct);

        // The outcome is stored. Archiving is best-effort and never fails the batch.
        await MoveSourceAsync(
            ctx,
            _folders.Archive,
            EventType.SourceFileArchived,
            BatchSteps.Archived,
            ct);

        return Outcome.Complete;
    }

    // =====================================================================
    // STEPS
    // =====================================================================

    private async Task CreateBatchAsync(
        ProcessingContext ctx,
        CancellationToken ct)
    {
        var batch = new ProcessingBatch
        {
            CorrelationId = ctx.CorrelationId,
            FileName = ctx.FileName,
            BlobPath = ctx.BlobPath,
            OriginalBlobPath = ctx.BlobPath,
            RecordCount = 0,
            Status = BatchStatus.Received,
            CurrentStep = BatchSteps.FileDetected,
            ReceivedDateTime = DateTime.UtcNow
        };

        await _batchRepository.AddAsync(batch, ct);
        await _batchRepository.SaveChangesAsync(ct);

        ctx.Batch = batch;

        Log(
            ctx,
            LogLevel.Information,
            "DB: batch row created | Status=Received | Step=FileDetected");

        await RecordEventAsync(
            ctx,
            EventType.FileDetected,
            "Success",
            $"File detected in '{_folders.Input}' folder: {ctx.BlobPath}. Batch created.",
            null,
            BatchSteps.FileDetected,
            ct: ct);

        Log(
            ctx,
            LogLevel.Information,
            $"File detected: {ctx.BlobPath}");
    }

    private async Task EnsureInProcessingCopyAsync(
    ProcessingContext ctx,
    CancellationToken ct)
    {
        var batch = ctx.Batch!;

        var stagedRelative =
            _folders.BuildPath(
                _folders.InProcessing,
                batch.ReceivedDateTime,
                ctx.FileName);

        var stagedFullPath =
            _folders.ToFullPath(
                stagedRelative);

        // --------------------------------------------------
        // 1. In-processing copy already exists
        // --------------------------------------------------

        if (await _blobStorageService.ExistsAsync(
                stagedRelative,
                ct))
        {
            if (!string.Equals(
                    batch.BlobPath,
                    stagedFullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                batch.BlobPath =
                    stagedFullPath;

                _batchRepository.Update(batch);

                await _batchRepository.SaveChangesAsync(ct);
            }

            Log(
                ctx,
                LogLevel.Information,
                $"In-processing source already exists | Path={stagedFullPath}");

            return;
        }

        // --------------------------------------------------
        // 2. Check original input location
        // --------------------------------------------------

        string? originalRelative = null;

        if (!string.IsNullOrWhiteSpace(
                batch.OriginalBlobPath))
        {
            originalRelative =
                _folders.ToRelativePath(
                    batch.OriginalBlobPath);
        }

        if (!string.IsNullOrWhiteSpace(
                originalRelative)
            &&
            await _blobStorageService.ExistsAsync(
                originalRelative,
                ct))
        {
            // Normal processing:
            //
            // input/file.xml
            //       ↓ COPY
            // inprocessing/file.xml
            //
            // Original input remains available until the
            // batch reaches a final outcome.

            await _blobStorageService.CopyAsync(
                originalRelative,
                stagedRelative,
                ct);

            batch.BlobPath =
                stagedFullPath;

            _batchRepository.Update(batch);

            await _batchRepository.SaveChangesAsync(ct);

            // --------------------------------------------------
            // RE-UPLOAD RECOVERY CLEANUP
            // --------------------------------------------------
            //
            // The newly uploaded input file is now safely available
            // in in-processing.
            //
            // If this batch was recovered by re-upload, remove the
            // old failed copy from error/ so it does not remain as
            // a stale failed file after recovery has started.

            if (!string.IsNullOrWhiteSpace(
                    ctx.RecoveryPreviousBlobPath))
            {
                var previousRelative =
                    _folders.ToRelativePath(
                        ctx.RecoveryPreviousBlobPath);

                if (!string.Equals(
                        previousRelative,
                        stagedRelative,
                        StringComparison.OrdinalIgnoreCase))
                {
                    await _blobStorageService.DeleteIfExistsAsync(
                        previousRelative,
                        ct);

                    Log(
                        ctx,
                        LogLevel.Information,
                        $"Previous failed source removed after re-upload recovery | " +
                        $"Path={ctx.RecoveryPreviousBlobPath}");
                }

                ctx.RecoveryPreviousBlobPath = null;
            }

            await RecordEventAsync(
                ctx,
                EventType.SourceFileCopiedToInProcessing,
                "Success",
                $"Source file copied from '{batch.OriginalBlobPath}' " +
                $"to '{stagedFullPath}'",
                batch.CurrentStep,
                batch.CurrentStep,
                ct: ct);

            Log(
                ctx,
                LogLevel.Information,
                $"Processing copy created | " +
                $"Source={batch.OriginalBlobPath} | " +
                $"StagedPath={stagedFullPath}");

            return;
        }

        // --------------------------------------------------
        // 3. Original input no longer exists.
        //
        // This is the manual DLQ recovery scenario:
        //
        // OriginalBlobPath -> input/... (historical)
        // BlobPath         -> error/... (actual file)
        // --------------------------------------------------

        if (!string.IsNullOrWhiteSpace(
                batch.BlobPath))
        {
            var currentRelative =
                _folders.ToRelativePath(
                    batch.BlobPath);

            if (await _blobStorageService.ExistsAsync(
                    currentRelative,
                    ct))
            {
                var currentFullPath =
                    batch.BlobPath;

                // Recovery:
                //
                // error/file.xml
                //       ↓ MOVE
                // inprocessing/file.xml
                //
                // Move rather than copy so the file is no
                // longer shown in error/ during recovery.

                await _blobStorageService.MoveAsync(
                    currentRelative,
                    stagedRelative,
                    ct);

                batch.BlobPath =
                    stagedFullPath;

                _batchRepository.Update(batch);

                await _batchRepository.SaveChangesAsync(ct);

                await RecordEventAsync(
                    ctx,
                    EventType.SourceFileCopiedToInProcessing,
                    "Success",
                    $"Recovery source moved from '{currentFullPath}' " +
                    $"to '{stagedFullPath}' for reprocessing",
                    batch.CurrentStep,
                    batch.CurrentStep,
                    ct: ct);

                Log(
                    ctx,
                    LogLevel.Information,
                    $"Recovery processing source prepared | " +
                    $"Source={currentFullPath} | " +
                    $"StagedPath={stagedFullPath}");

                return;
            }
        }

        // --------------------------------------------------
        // 4. Source cannot be found anywhere
        // --------------------------------------------------

        throw new FileNotFoundException(
            $"Source XML could not be located for BatchId={batch.BatchId}. " +
            $"OriginalPath='{batch.OriginalBlobPath}', " +
            $"CurrentPath='{batch.BlobPath}', " +
            $"ExpectedStagedPath='{stagedFullPath}'.");
    }

    /// <returns>true when processing may continue.</returns>
    private async Task<bool> CheckExtensionAsync(
        ProcessingContext ctx,
        CancellationToken ct)
    {
        var extension = Path.GetExtension(ctx.FileName);

        if (string.Equals(
                extension,
                ".xml",
                StringComparison.OrdinalIgnoreCase))
        {
            await TransitionAsync(
                ctx,
                EventType.ExtensionCheckPassed,
                "Success",
                "File extension check passed (.xml)",
                BatchSteps.ExtensionChecked,
                ct: ct);

            return true;
        }

        var error =
            $"Unsupported file extension '{extension}'. Only .xml files are accepted.";

        ctx.Batch!.ProcessingEndDateTime = DateTime.UtcNow;

        await TransitionAsync(
            ctx,
            EventType.ExtensionCheckFailed,
            "Rejected",
            error,
            BatchSteps.Rejected,
            BatchStatus.Rejected,
            errorCode: "InvalidFileExtension",
            errorMessage: error,
            level: LogLevel.Warning,
            ct: ct);

        await MoveSourceAsync(
            ctx,
            _folders.Error,
            EventType.SourceFileMovedToError,
            null,
            ct);

        return false;
    }

    private async Task<string> CalculateHashAsync(
        ProcessingContext ctx,
        Stream stream,
        CancellationToken ct)
    {
        var hash =
            await _hashService.GenerateHashAsync(
                stream,
                ct);

        stream.Position = 0;

        ctx.Batch!.FileHash = hash;

        await TransitionAsync(
            ctx,
            EventType.HashCalculated,
            "Success",
            $"SHA-256 hash calculated: {hash}",
            BatchSteps.HashCalculated,
            ct: ct);

        return hash;
    }

    private async Task<string> CalculateIncomingHashAsync(
    Stream stream,
    CancellationToken ct)
    {
        var hash =
            await _hashService.GenerateHashAsync(
                stream,
                ct);

        stream.Position = 0;

        return hash;
    }

    /// <returns>true when processing may continue (not a skipped duplicate).</returns>
    private async Task<bool> CheckDuplicateAsync(
        ProcessingContext ctx,
        string hash,
        CancellationToken ct)
    {
        var batch = ctx.Batch!;

        var earlierBatches =
            await _batchRepository.GetHashCandidatesAsync(
                hash,
                batch.BatchId,
                ct);

        Log(
            ctx,
            LogLevel.Information,
            $"DB: duplicate lookup by file hash returned {earlierBatches.Count} earlier batch(es)");

        var decision = DuplicateResolver.Decide(earlierBatches);

        if (!decision.IsDuplicate)
        {
            batch.OriginalBatchId = null;
            batch.DuplicateCase = null;

            await TransitionAsync(
                ctx,
                EventType.DuplicateCheckPassed,
                "Success",
                "Duplicate check passed - no earlier batch with the same file hash",
                BatchSteps.DuplicateChecked,
                ct: ct);

            return true;
        }

        var original = decision.Original!;

        batch.OriginalBatchId = original.BatchId;
        batch.DuplicateCase = decision.Case;

        // Case 3: every earlier copy failed -> process this one again.
        if (decision.Case == DuplicateCase.PreviouslyFailed)
        {
            await TransitionAsync(
                ctx,
                EventType.DuplicateDetected,
                "Reprocess",
                $"Identical file was seen before in batch {original.BatchId} ({original.Status}). The earlier attempt failed, so this file is processed again.",
                BatchSteps.DuplicateChecked,
                level: LogLevel.Warning,
                ct: ct);

            return true;
        }

        // Case 1 and 2: skip. The batch row is kept as a Duplicate.
        var message =
            decision.Case == DuplicateCase.AlreadyCompleted
                ? $"Duplicate file: an identical file already completed in batch {original.BatchId}. File skipped."
                : $"Duplicate file: an identical file is currently being processed by batch {original.BatchId} ({original.Status}). File skipped.";

        batch.ProcessingEndDateTime = DateTime.UtcNow;

        await TransitionAsync(
            ctx,
            EventType.DuplicateDetected,
            "Skipped",
            message,
            BatchSteps.Duplicate,
            BatchStatus.Duplicate,
            level: LogLevel.Warning,
            ct: ct);

        if (string.Equals(
                original.BlobPath,
                batch.BlobPath,
                StringComparison.OrdinalIgnoreCase))
        {
            // Same blob name re-uploaded while the original still uses it:
            // moving it would pull the file out from under the original batch.
            Log(
                ctx,
                LogLevel.Warning,
                $"Duplicate shares its blob path with batch {original.BatchId} - file left in place");
        }
        else
        {
            await MoveSourceAsync(
                ctx,
                _folders.Archive,
                EventType.SourceFileArchived,
                null,
                ct);
        }

        return false;
    }

    /// <returns>Validation details when at least one order can continue.</returns>
    private async Task<XmlValidationResult?> ValidateXmlAsync(
        ProcessingContext ctx,
        Stream stream,
        CancellationToken ct)
    {
        var batch = ctx.Batch!;

        await TransitionAsync(
            ctx,
            EventType.ValidationStarted,
            "Started",
            "XML validation started",
            BatchSteps.Validating,
            ct: ct);

        var validation =
            await _xmlValidationService.ValidateAsync(
                stream,
                ct);

        stream.Position = 0;

        batch.RecordCount = validation.RecordCount;

        batch.InvalidRecordCount = validation.OrderErrors.Count;

        if (!validation.IsValid && validation.ValidRecordNumbers.Count == 0)
        {
            var errors = string.Join(" | ", validation.Errors);

            batch.ProcessingEndDateTime = DateTime.UtcNow;

            await TransitionAsync(
                ctx,
                EventType.ValidationFailed,
                "Rejected",
                $"XML validation failed ({validation.Errors.Count} error(s)): {errors}",
                BatchSteps.ValidationFailed,
                BatchStatus.ValidationFailed,
                errorCode: "XmlValidationFailed",
                errorMessage: errors,
                level: LogLevel.Warning,
                ct: ct);

            await MoveSourceAsync(
                ctx,
                _folders.Error,
                EventType.SourceFileMovedToError,
                null,
                ct);

            return null;
        }

        await TransitionAsync(
            ctx,
            EventType.ValidationPassed,
            validation.IsValid ? "Success" : "PartialSuccess",
            validation.IsValid
                ? $"XML validation passed ({batch.RecordCount} record(s))"
                : $"XML validation partially passed: {validation.ValidRecordNumbers.Count} valid order(s), {batch.InvalidRecordCount} invalid order(s)",
            BatchSteps.XmlValidated,
            BatchStatus.Validated,
            ct: ct);

        return validation;
    }

    private async Task GeneratePerOrderOutputsAsync(
    ProcessingContext ctx,
    OutputFile output,
    List<CanonicalOrderModel> orders,
    CancellationToken ct)
    {
        var outputType =
            output.OutputType;

        // --------------------------------------------------
        // IDEMPOTENCY
        // --------------------------------------------------

        if (output.Status == OutputStatus.Success)
        {
            Log(
                ctx,
                LogLevel.Information,
                $"{outputType} outputs already generated - skipping");

            return;
        }

        output.Status =
            OutputStatus.Processing;

        output.StartedDateTime =
            DateTime.UtcNow;

        output.CompletedDateTime =
            null;

        output.AttemptCount++;

        output.ErrorCode =
            null;

        output.ErrorMessage =
            null;

        await TransitionAsync(
            ctx,
            EventType.OutputStarted,
            "Started",
            $"{outputType} generation started for " +
            $"{orders.Count} order(s) " +
            $"(output attempt {output.AttemptCount})",
            BatchSteps.Generating(outputType),
            outputFileId: output.OutputFileId,
            writeApplicationLog: false,
            ct: ct);

        try
        {
            // if (outputType == OutputType.Dat)
            // {
            //    throw new TimeoutException(
            //    "TEST ONLY - Forced DAT generation failure.");
            // }

            var inputFileName =
                Path.GetFileNameWithoutExtension(
                    ctx.FileName);

            var safeInputFileName =
                MakeSafeFileName(
                    inputFileName);

            var timestamp =
                ctx.Batch!
                    .ReceivedDateTime
                    .ToString(
                        "yyyyMMddHHmmssfff",
                        CultureInfo.InvariantCulture);

            // Parent:
            //
            // 01-valid-multiple-orders_20260922071419631
            var outputFolderName =
                $"{safeInputFileName}_{timestamp}";

            // Format subfolder:
            //
            // TXT_01-valid-multiple-orders_20260922071419631
            // CSV_01-valid-multiple-orders_20260922071419631
            // JSON_01-valid-multiple-orders_20260922071419631
            // DAT_01-valid-multiple-orders_20260922071419631
            // PDF_01-valid-multiple-orders_20260922071419631

            var formatFolderName =
                $"{outputType.ToString().ToUpperInvariant()}_" +
                $"{safeInputFileName}_{timestamp}";

            var formatFolderPath =
                $"{_folders.OutputStaging.Trim('/')}/" +
                $"{outputFolderName}/" +
                $"{formatFolderName}";

            var extension =
                outputType
                    .ToString()
                    .ToLowerInvariant();

            var generatedPaths =
                new List<string>();

            // --------------------------------------------------
            // GENERATE ONE PHYSICAL FILE PER ORDER
            // --------------------------------------------------

            foreach (var order in orders)
            {
                // Generator receives exactly one order.
                var singleOrder =
                    new List<CanonicalOrderModel>
                    {
                    order
                    };

                using var generated =
                    await _fileGeneratorService.GenerateAsync(
                        singleOrder,
                        outputType,
                        ct);

                var safeOrderId =
                    MakeSafeFileName(
                        order.OrderId);

                // Example:
                //
                // ORD10078_valid4_20260918130232660.txt
                // ORD10078_valid4_20260918130232660.csv
                // ORD10078_valid4_20260918130232660.json
                // ORD10078_valid4_20260918130232660.dat
                // ORD10078_valid4_20260918130232660.pdf

                var outputFileName =
                    $"{safeOrderId}_" +
                    $"{safeInputFileName}_" +
                    $"{timestamp}." +
                    $"{extension}";

                var relativePath =
                    $"{formatFolderPath}/" +
                    $"{outputFileName}";

                await _blobStorageService.UploadAsync(
                    generated,
                    relativePath,
                    ct);

                var fullPath =
                    _folders.ToFullPath(
                        relativePath);

                generatedPaths.Add(
                    fullPath);

            }

            // --------------------------------------------------
            // FORMAT-LEVEL OUTPUT RECORD
            // --------------------------------------------------
            //
            // The existing OutputFile row represents the
            // complete folder for this format.

            output.OutputBlobPath =
                _folders.ToFullPath(
                    $"{formatFolderPath}/");

            output.Status =
                OutputStatus.Success;

            output.CompletedDateTime =
                DateTime.UtcNow;

            await TransitionAsync(
                ctx,
                EventType.OutputCompleted,
                "Success",
                $"{orders.Count} {outputType} file(s) " +
                $"generated successfully",
                BatchSteps.OutputCompleted(
                    outputType),
                outputFileId: output.OutputFileId,
                writeApplicationLog: false,
                ct: ct);
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException)
        {
            var isFinalAttempt =
                ctx.Attempt >=
                SystemConstants.MaxDeliveryAttempts;

            output.ErrorCode =
                ex.GetType().Name;

            output.ErrorMessage =
                Truncate(
                    ex.Message,
                    SystemConstants.MaxErrorLength);

            output.Status =
                isFinalAttempt
                    ? OutputStatus.Failed
                    : OutputStatus.RetryPending;

            if (isFinalAttempt)
            {
                output.CompletedDateTime =
                    DateTime.UtcNow;
            }

            try
            {
                await TransitionAsync(
                    ctx,
                    EventType.OutputFailed,
                    isFinalAttempt
                        ? "Failed"
                        : "RetryableFailure",
                    $"{outputType} generation failed " +
                    $"(output attempt {output.AttemptCount}): " +
                    $"{ex.Message}",
                    BatchSteps.OutputFailed(
                        outputType),
                    errorCode:
                        ex.GetType().Name,
                    errorMessage:
                        ex.Message,
                    outputFileId:
                        output.OutputFileId,
                    level:
                        LogLevel.Warning,
                    writeApplicationLog: true,
                    exception: ex,
                    ct:
                        CancellationToken.None);
            }
            catch (Exception recordEx)
            {
                Log(
                    ctx,
                    LogLevel.Error,
                    $"Could not record {outputType} " +
                    "generation failure",
                    exception: recordEx);
            }

            throw;
        }
    }


    private async Task GenerateOutputAsync(
        ProcessingContext ctx,
        OutputFile output,
        List<CanonicalOrderModel> orders,
        CancellationToken ct)
    {
        var outputType = output.OutputType;

        // Idempotency: an output generated by an earlier attempt is never generated twice.
        if (output.Status == OutputStatus.Success)
        {
            Log(
                ctx,
                LogLevel.Information,
                $"{outputType} already generated - skipping");

            return;
        }

        output.Status = OutputStatus.Processing;
        output.StartedDateTime = DateTime.UtcNow;
        output.CompletedDateTime = null;
        output.AttemptCount++;
        output.ErrorCode = null;
        output.ErrorMessage = null;

        // Saves the output row, the batch step and the journey event together.
        await TransitionAsync(
            ctx,
            EventType.OutputStarted,
            "Started",
            $"{outputType} generation started (output attempt {output.AttemptCount})",
            BatchSteps.Generating(outputType),
            outputFileId: output.OutputFileId,
            writeApplicationLog: false,
            ct: ct);

        try
        {
            using var generated =
                await _fileGeneratorService.GenerateAsync(
                    orders,
                    outputType,
                    ct);

            var baseFileName = Path.GetFileNameWithoutExtension(ctx.FileName);
            var extension = outputType.ToString().ToLowerInvariant();

            //var blobPath =
            //    $"{_folders.Output.Trim('/')}/{ctx.CorrelationId}_{baseFileName}/{baseFileName}.{extension}";

            var timestamp = ctx.Batch!.ReceivedDateTime.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            var outputName = $"{baseFileName}_{timestamp}";

            var relativePath = $"{_folders.OutputStaging.Trim('/')}/{outputName}/{outputName}.{extension}";

            await _blobStorageService.UploadAsync(
                generated,
                relativePath,
                ct);

            var blobPath = _folders.ToFullPath(relativePath);

            output.OutputBlobPath = blobPath;
            output.Status = OutputStatus.Success;
            output.CompletedDateTime = DateTime.UtcNow;

            await TransitionAsync(
                ctx,
                EventType.OutputCompleted,
                "Success",
                $"{outputType} generated and uploaded: {blobPath}",
                BatchSteps.OutputCompleted(outputType),
                outputFileId: output.OutputFileId,
                writeApplicationLog: false,
                ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var isFinalAttempt =
                ctx.Attempt >= SystemConstants.MaxDeliveryAttempts;

            output.ErrorCode = ex.GetType().Name;
            output.ErrorMessage = Truncate(ex.Message, SystemConstants.MaxErrorLength);
            output.Status = isFinalAttempt
                ? OutputStatus.Failed
                : OutputStatus.RetryPending;

            if (isFinalAttempt)
            {
                output.CompletedDateTime = DateTime.UtcNow;
            }

            try
            {
                await TransitionAsync(
                    ctx,
                    EventType.OutputFailed,
                    isFinalAttempt ? "Failed" : "RetryableFailure",
                    $"{outputType} generation failed (output attempt {output.AttemptCount}): {ex.Message}",
                    BatchSteps.OutputFailed(outputType),
                    errorCode: ex.GetType().Name,
                    errorMessage: ex.Message,
                    outputFileId: output.OutputFileId,
                    level: LogLevel.Warning,
                    writeApplicationLog: true,
                    exception: ex,
                    ct: CancellationToken.None);
            }
            catch (Exception recordEx)
            {
                Log(
                    ctx,
                    LogLevel.Error,
                    $"Could not record {outputType} failure in the database",
                    exception: recordEx);
            }

            // The batch-level retry / dead-letter decision is made in HandleFailureAsync.
            throw;
        }
    }

    // =====================================================================
    // STATE + JOURNEY + LOG (the one place where state changes are written)
    // =====================================================================

    /// <summary>
    /// Applies a state change to the batch, saves it together with the journey event in
    /// one SaveChanges (so state and journey can never disagree), then logs the DB write
    /// and the step itself.
    /// </summary>
    private async Task TransitionAsync(
        ProcessingContext ctx,
        EventType eventType,
        string eventStatus,
        string message,
        string toStep,
        BatchStatus? toStatus = null,
        string? errorCode = null,
        string? errorMessage = null,
        long? outputFileId = null,
        LogLevel level = LogLevel.Information,
        bool writeApplicationLog = true,
        Exception? exception = null,
        CancellationToken ct = default)
    {
        var batch = ctx.Batch
            ?? throw new InvalidOperationException(
                "Cannot change state before the batch exists.");

        var fromStep = batch.CurrentStep;
        var fromStatus = batch.Status;

        if (toStatus.HasValue && toStatus.Value != fromStatus)
        {
            BatchStateMachine.EnsureCanTransition(
                fromStatus,
                toStatus.Value);

            batch.Status = toStatus.Value;
        }

        batch.CurrentStep = toStep;
        batch.ErrorCode = Truncate(errorCode, 100);
        batch.ErrorMessage = Truncate(errorMessage, SystemConstants.MaxErrorLength);

        await _auditService.LogEventAsync(
            batch.BatchId,
            ctx.CorrelationId,
            ctx.FileName,
            eventType,
            eventStatus,
            message,
            FunctionName,
            fromStep,
            toStep,
            ctx.Attempt,
            outputFileId,
            errorCode,
            errorMessage,
            ct);

        if (writeApplicationLog)
        {
            Log(
                ctx,
                level,
                message,
                error: errorMessage,
                exception: exception);
        }
    }

    /// <summary>Writes a journey event without changing the batch state.</summary>
    private async Task RecordEventAsync(
        ProcessingContext ctx,
        EventType eventType,
        string eventStatus,
        string message,
        string? fromState,
        string? toState,
        string? errorCode = null,
        string? errorMessage = null,
        long? outputFileId = null,
        CancellationToken ct = default)
    {
        var batch = ctx.Batch
            ?? throw new InvalidOperationException(
                "Cannot record an event before the batch exists.");

        await _auditService.LogEventAsync(
            batch.BatchId,
            ctx.CorrelationId,
            ctx.FileName,
            eventType,
            eventStatus,
            message,
            FunctionName,
            fromState,
            toState,
            ctx.Attempt,
            outputFileId,
            errorCode,
            errorMessage,
            ct);

    }

    /// <summary>
    /// Moves the source blob out of input/ (archive, error or duplicate).
    /// Best-effort: a failed move is logged and recorded, but never changes the outcome
    /// that is already stored and never triggers a retry.
    /// </summary>
    private async Task MoveSourceAsync(
    ProcessingContext ctx,
    string folder,
    EventType successEvent,
    string? newStep,
    CancellationToken ct)
    {
        var batch =
            ctx.Batch!;

        string? sourceRelative = null;
        string? sourceFullPath = null;

        // --------------------------------------------------
        // 1. Prefer original upload when it still exists
        // --------------------------------------------------

        if (!string.IsNullOrWhiteSpace(
                batch.OriginalBlobPath))
        {
            var originalRelative =
                _folders.ToRelativePath(
                    batch.OriginalBlobPath);

            if (await _blobStorageService.ExistsAsync(
                    originalRelative,
                    ct))
            {
                sourceRelative =
                    originalRelative;

                sourceFullPath =
                    batch.OriginalBlobPath;
            }
        }

        // --------------------------------------------------
        // 2. Otherwise use current physical location
        //
        // Important for DLQ recovery:
        // BlobPath may now point to inprocessing/ or error/
        // --------------------------------------------------

        if (sourceRelative is null &&
            !string.IsNullOrWhiteSpace(
                batch.BlobPath))
        {
            var currentRelative =
                _folders.ToRelativePath(
                    batch.BlobPath);

            if (await _blobStorageService.ExistsAsync(
                    currentRelative,
                    ct))
            {
                sourceRelative =
                    currentRelative;

                sourceFullPath =
                    batch.BlobPath;
            }
        }

        // --------------------------------------------------
        // 3. Nothing exists
        // --------------------------------------------------

        if (sourceRelative is null ||
            sourceFullPath is null)
        {
            Log(
                ctx,
                LogLevel.Error,
                $"Source file could not be located for move | " +
                $"OriginalPath={batch.OriginalBlobPath} | " +
                $"CurrentPath={batch.BlobPath}");

            try
            {
                await RecordEventAsync(
                    ctx,
                    EventType.SourceFileMoveFailed,
                    "Failed",
                    $"Source file could not be located. " +
                    $"OriginalPath='{batch.OriginalBlobPath}', " +
                    $"CurrentPath='{batch.BlobPath}'",
                    batch.CurrentStep,
                    batch.CurrentStep,
                    errorCode: "SourceFileNotFound",
                    errorMessage:
                        "No physical source blob was found.",
                    ct: CancellationToken.None);
            }
            catch (Exception recordEx)
            {
                Log(
                    ctx,
                    LogLevel.Error,
                    "Could not record source-file lookup failure",
                    exception: recordEx);
            }

            return;
        }

        // --------------------------------------------------
        // 4. Build destination
        // --------------------------------------------------

        var destinationRelative =
            _folders.BuildPath(
                folder,
                batch.ReceivedDateTime,
                ctx.FileName);

        var destinationFullPath =
            _folders.ToFullPath(
                destinationRelative);

        try
        {
            // --------------------------------------------------
            // 5. Move actual available source
            // --------------------------------------------------

            await _blobStorageService.MoveAsync(
                sourceRelative,
                destinationRelative,
                ct);

            // --------------------------------------------------
            // 6. Delete staged working copy if the source that
            //    was moved was a different blob.
            //
            // Normal success/failure:
            //
            // input → archive/error
            // then delete inprocessing copy.
            // --------------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                    batch.BlobPath))
            {
                var workingRelative =
                    _folders.ToRelativePath(
                        batch.BlobPath);

                if (!string.Equals(
                        sourceRelative,
                        workingRelative,
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    await _blobStorageService.ExistsAsync(
                        workingRelative,
                        ct))
                {
                    await _blobStorageService.DeleteIfExistsAsync(
                        workingRelative,
                        ct);
                }
            }

            // --------------------------------------------------
            // 7. Store actual current location
            // --------------------------------------------------

            var fromStep =
                batch.CurrentStep;

            batch.BlobPath =
                destinationFullPath;

            if (newStep is not null)
            {
                batch.CurrentStep =
                    newStep;
            }

            // IMPORTANT:
            // Persist the new location.
            _batchRepository.Update(batch);

            await _batchRepository.SaveChangesAsync(ct);

            await RecordEventAsync(
                ctx,
                successEvent,
                "Success",
                $"Source file moved from '{sourceFullPath}' " +
                $"to '{destinationFullPath}'",
                fromStep,
                batch.CurrentStep,
                ct: ct);

            Log(
                ctx,
                LogLevel.Information,
                $"Source file moved | " +
                $"From={sourceFullPath} | " +
                $"To={destinationFullPath}");
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException)
        {
            Log(
                ctx,
                LogLevel.Error,
                $"Failed to move source file from " +
                $"'{sourceFullPath}' to '{destinationFullPath}'",
                exception: ex);

            try
            {
                await RecordEventAsync(
                    ctx,
                    EventType.SourceFileMoveFailed,
                    "Failed",
                    $"Failed to move source file from " +
                    $"'{sourceFullPath}' to '{destinationFullPath}'",
                    batch.CurrentStep,
                    batch.CurrentStep,
                    ex.GetType().Name,
                    ex.Message,
                    ct: CancellationToken.None);
            }
            catch (Exception recordEx)
            {
                Log(
                    ctx,
                    LogLevel.Error,
                    "Could not record the failed file move in the database",
                    exception: recordEx);
            }
        }
    }

    private async Task PublishOutputsAsync(
    ProcessingContext ctx,
    IReadOnlyCollection<OutputFile> outputs,
    CancellationToken ct)
    {
        // --------------------------------------------------
        // VERIFY ALL FORMATS SUCCEEDED
        // --------------------------------------------------

        if (outputs.Count !=
                Enum.GetValues<OutputType>().Length ||
            outputs.Any(
                output =>
                    output.Status != OutputStatus.Success))
        {
            throw new InvalidOperationException(
                "Outputs cannot be published until " +
                "every configured format succeeds.");
        }

        foreach (var output in
            outputs.OrderBy(
                o => (int)o.OutputType))
        {
            if (string.IsNullOrWhiteSpace(
                    output.OutputBlobPath))
            {
                throw new InvalidOperationException(
                    $"Output path is missing for " +
                    $"{output.OutputType}.");
            }

            var stagedPath =
                _folders.ToRelativePath(
                    output.OutputBlobPath);

            var stagingPrefix =
                _folders.OutputStaging.Trim('/') + "/";

            var outputPrefix =
                _folders.Output.Trim('/') + "/";

            // --------------------------------------------------
            // XLSX
            //
            // One physical file containing all Order worksheets.
            // --------------------------------------------------

            if (output.OutputType == OutputType.Xlsx)
            {
                var destination =
                    outputPrefix +
                    stagedPath[
                        stagingPrefix.Length..];

                await MoveIfNeededAsync(
                    stagedPath,
                    destination,
                    ct);

                output.OutputBlobPath =
                    _folders.ToFullPath(
                        destination);

                await _outputFileRepository
                    .SaveChangesAsync(ct);

                continue;
            }

            // --------------------------------------------------
            // TXT / CSV / JSON / DAT / PDF
            //
            // OutputBlobPath represents a folder containing
            // one physical file per Order.
            // --------------------------------------------------

            var stagedFiles =
                await _blobStorageService.ListAsync(
                    stagedPath,
                    ct);

            if (stagedFiles.Count == 0)
            {
                throw new FileNotFoundException(
                    $"No staged {output.OutputType} " +
                    $"files were found under " +
                    $"'{stagedPath}'.");
            }

            foreach (var stagedFile in stagedFiles)
            {
                var suffix =
                    stagedFile.StartsWith(
                        stagingPrefix,
                        StringComparison.OrdinalIgnoreCase)
                        ? stagedFile[
                            stagingPrefix.Length..]
                        : Path.GetFileName(
                            stagedFile);

                var destination =
                    outputPrefix +
                    suffix;

                await MoveIfNeededAsync(
                    stagedFile,
                    destination,
                    ct);
            }

            // Store the final format folder in OutputFile.
            output.OutputBlobPath =
                _folders.ToFullPath(
                    outputPrefix +
                    stagedPath[
                        stagingPrefix.Length..]);

            await _outputFileRepository
                .SaveChangesAsync(ct);
        }

        await RecordEventAsync(
            ctx,
            EventType.OutputsPublished,
            "Success",
            "All output formats were published " +
            "after successful generation.",
            ctx.Batch!.CurrentStep,
            ctx.Batch.CurrentStep,
            ct: ct);
    }

    private async Task MoveIfNeededAsync(
        string source,
        string destination,
        CancellationToken ct)
    {
        if (await _blobStorageService.ExistsAsync(destination, ct))
        {
            return;
        }

        if (!await _blobStorageService.ExistsAsync(source, ct))
        {
            throw new FileNotFoundException(
                $"Staged output '{source}' was not found.");
        }

        await _blobStorageService.MoveAsync(source, destination, ct);
    }

    // =====================================================================
    // FAILURE / RETRY / DEAD-LETTER / CANCELLATION
    // =====================================================================

    /// <summary>
    /// Any unexpected error lands here. Attempts 1..N-1: record RetryPending and abandon.
    /// Attempt N (the last): record DeadLettered and dead-letter the message.
    /// </summary>
    private async Task HandleFailureAsync(
        ProcessingContext ctx,
        Exception ex,
        ServiceBusReceivedMessage sbMessage,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
    {
        var isFinalAttempt =
            ctx.Attempt >= SystemConstants.MaxDeliveryAttempts;

        Log(
            ctx,
            isFinalAttempt ? LogLevel.Error : LogLevel.Warning,
            $"Processing failed | Attempt={ctx.Attempt}/{SystemConstants.MaxDeliveryAttempts} | ErrorType={ex.GetType().Name}",
            exception: ex);

        var alreadyTerminal = false;

        if (ctx.Batch is not null)
        {
            try
            {
                // The DbContext may hold half-applied changes from the failed attempt.
                // Start from what is really in the database.
                var fresh =
                    await _batchRepository.ReloadAsync(
                        ctx.Batch.BatchId,
                        CancellationToken.None);

                if (fresh is not null)
                {
                    ctx.Batch = fresh;
                }

                var status = ctx.Batch.Status;

                if (BatchStateMachine.IsTerminal(status) &&
                    status != BatchStatus.DeadLettered)
                {
                    // The outcome was already stored (e.g. the failure happened after
                    // Completed was saved). Retrying would repeat finished work.
                    alreadyTerminal = true;
                }
                else if (isFinalAttempt)
                {
                    if (status != BatchStatus.DeadLettered)
                    {
                        await RecordDeadLetterStateAsync(ctx, ex);
                    }
                }
                else
                {
                    await RecordRetryStateAsync(ctx, ex);
                }
            }
            catch (Exception recordEx)
            {
                // Settling the message must not depend on the database being reachable.
                Log(
                    ctx,
                    LogLevel.Error,
                    "Could not record the failure in the database - settling the message anyway",
                    exception: recordEx);
            }
        }

        if (alreadyTerminal)
        {
            Log(
                ctx,
                LogLevel.Information,
                $"Batch is already {ctx.Batch!.Status}; the failure happened after the outcome was saved - completing the message");

            await messageActions.CompleteMessageAsync(
                sbMessage,
                CancellationToken.None);

            return;
        }

        if (isFinalAttempt)
        {
            await messageActions.DeadLetterMessageAsync(
                sbMessage,
                deadLetterReason: "MaxRetriesExceeded",
                deadLetterErrorDescription: Truncate($"{ex.GetType().Name}: {ex.Message}", 1000),
                cancellationToken: CancellationToken.None);

            Log(
                ctx,
                LogLevel.Error,
                $"Message dead-lettered after {ctx.Attempt} attempt(s)",
                exception: ex);

            if (ctx.Batch is not null)
            {
                try
                {
                    await RecordEventAsync(
                        ctx,
                        EventType.DeadLettered,
                        "Failed",
                        "Message moved to the dead-letter queue (reason: MaxRetriesExceeded)",
                        BatchSteps.DeadLettered,
                        BatchSteps.DeadLettered,
                        ex.GetType().Name,
                        ex.Message,
                        ct: CancellationToken.None);
                }
                catch (Exception recordEx)
                {
                    Log(
                        ctx,
                        LogLevel.Error,
                        "Could not record the dead-letter event in the database",
                        exception: recordEx);
                }
            }

            return;
        }

        // Retry: wait a little so a short outage can clear, then hand the message back.
        var delay =
            TimeSpan.FromSeconds(
                SystemConstants.RetryBackoffSeconds * ctx.Attempt);

        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutting down: abandon immediately.
        }

        await messageActions.AbandonMessageAsync(
            sbMessage,
            cancellationToken: CancellationToken.None);

        Log(
            ctx,
            LogLevel.Warning,
            $"Message abandoned - redelivery expected (attempt {ctx.Attempt + 1}/{SystemConstants.MaxDeliveryAttempts})");
    }

    private async Task RecordRetryStateAsync(
        ProcessingContext ctx,
        Exception ex)
    {
        var batch = ctx.Batch!;

        batch.RetryCount = Math.Max(0, ctx.Attempt - 1);
        batch.LastRetryDateTime = DateTime.UtcNow;

        foreach (var output in batch.OutputFiles
                     .Where(o => o.Status == OutputStatus.Processing))
        {
            output.Status = OutputStatus.RetryPending;
            output.ErrorCode ??= ex.GetType().Name;
            output.ErrorMessage ??= Truncate(ex.Message, SystemConstants.MaxErrorLength);
        }

        await TransitionAsync(
            ctx,
            EventType.RetryScheduled,
            "RetryPending",
            $"Attempt {ctx.Attempt}/{SystemConstants.MaxDeliveryAttempts} failed - retry scheduled",
            BatchSteps.RetryPending,
            BatchStatus.RetryPending,
            errorCode: ex.GetType().Name,
            errorMessage: ex.Message,
            level: LogLevel.Warning,
            exception: ex,
            ct: CancellationToken.None);
    }

    private async Task RecordDeadLetterStateAsync(
        ProcessingContext ctx,
        Exception ex)
    {
        var batch = ctx.Batch!;

        // Retries actually performed = attempts - 1.
        batch.RetryCount = Math.Max(0, ctx.Attempt - 1);
        batch.LastRetryDateTime = DateTime.UtcNow;
        batch.ProcessingEndDateTime = DateTime.UtcNow;

        foreach (var output in batch.OutputFiles
                     .Where(o => o.Status != OutputStatus.Success &&
                                 o.Status != OutputStatus.Failed))
        {
            output.Status = OutputStatus.Failed;
            output.CompletedDateTime = DateTime.UtcNow;
            output.ErrorCode ??= ex.GetType().Name;
            output.ErrorMessage ??= Truncate(ex.Message, SystemConstants.MaxErrorLength);
        }

        await TransitionAsync(
            ctx,
            EventType.MaxRetriesExceeded,
            "Failed",
            $"Attempt {ctx.Attempt}/{SystemConstants.MaxDeliveryAttempts} failed - retries exhausted, message will be dead-lettered",
            BatchSteps.DeadLettered,
            BatchStatus.DeadLettered,
            errorCode: ex.GetType().Name,
            errorMessage: ex.Message,
            level: LogLevel.Error,
            exception: ex,
            ct: CancellationToken.None);

        await MoveSourceAsync(
            ctx,
            _folders.Error,
            EventType.SourceFileMovedToError,
            null,
            CancellationToken.None);
    }

    /// <summary>
    /// The host is shutting down or the function timed out. This says nothing about the
    /// file, so it is not treated as a failure: park the batch as RetryPending and abandon
    /// the message so it is picked up again.
    /// </summary>
    private async Task HandleCancellationAsync(
        ProcessingContext ctx,
        ServiceBusReceivedMessage sbMessage,
        ServiceBusMessageActions messageActions)
    {
        Log(
            ctx,
            LogLevel.Warning,
            "Processing cancelled (host shutdown or timeout) - message will be redelivered");

        var complete = false;

        if (ctx.Batch is not null)
        {
            try
            {
                var fresh =
                    await _batchRepository.ReloadAsync(
                        ctx.Batch.BatchId,
                        CancellationToken.None);

                if (fresh is not null)
                {
                    ctx.Batch = fresh;
                }

                var batch = ctx.Batch;

                if (BatchStateMachine.IsTerminal(batch.Status))
                {
                    // Outcome already stored: nothing left to redo.
                    complete = batch.Status != BatchStatus.DeadLettered;
                }
                else
                {
                    batch.RetryCount = ctx.Attempt;
                    batch.LastRetryDateTime = DateTime.UtcNow;

                    foreach (var output in batch.OutputFiles
                                 .Where(o => o.Status == OutputStatus.Processing))
                    {
                        output.Status = OutputStatus.RetryPending;
                    }

                    await TransitionAsync(
                        ctx,
                        EventType.ProcessingCancelled,
                        "Cancelled",
                        "Processing cancelled (host shutdown or timeout) - message abandoned for redelivery",
                        BatchSteps.RetryPending,
                        BatchStatus.RetryPending,
                        errorCode: nameof(OperationCanceledException),
                        errorMessage: "Processing was cancelled.",
                        level: LogLevel.Warning,
                        ct: CancellationToken.None);
                }
            }
            catch (Exception recordEx)
            {
                Log(
                    ctx,
                    LogLevel.Error,
                    "Could not record the cancellation in the database",
                    exception: recordEx);
            }
        }

        if (complete)
        {
            await messageActions.CompleteMessageAsync(
                sbMessage,
                CancellationToken.None);

            return;
        }

        await messageActions.AbandonMessageAsync(
            sbMessage,
            cancellationToken: CancellationToken.None);
    }

    private async Task SettleAsync(
        ProcessingContext ctx,
        Outcome outcome,
        ServiceBusReceivedMessage sbMessage,
        ServiceBusMessageActions messageActions)
    {
        if (outcome.DeadLetter)
        {
            await messageActions.DeadLetterMessageAsync(
                sbMessage,
                deadLetterReason: "MaxRetriesExceeded",
                deadLetterErrorDescription: Truncate(outcome.Description, 1000),
                cancellationToken: CancellationToken.None);

            Log(
                ctx,
                LogLevel.Error,
                "Message dead-lettered (batch had already exhausted its retries)");

            return;
        }

        await messageActions.CompleteMessageAsync(
            sbMessage,
            CancellationToken.None);

        Log(
            ctx,
            LogLevel.Information,
            $"Message completed | FinalStatus={ctx.Batch?.Status.ToString() ?? "n/a"} | FinalStep={ctx.Batch?.CurrentStep ?? "n/a"}");
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private void Log(
        ProcessingContext ctx,
        LogLevel level,
        string message,
        string? error = null,
        Exception? exception = null)
    {
        _logger.LogProcessing(
            ctx,
            level,
            message,
            error,
            exception);
    }

    private (BlobResolution Kind, string BlobPath, string Reason) ResolveBlob(
        EventGridBlobCreatedMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.EventType) &&
            !string.Equals(
                message.EventType,
                BlobCreatedEventType,
                StringComparison.OrdinalIgnoreCase))
        {
            return (
                BlobResolution.Ignore,
                string.Empty,
                $"Event type '{message.EventType}' is not {BlobCreatedEventType}.");
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            return (
                BlobResolution.Malformed,
                string.Empty,
                "Event Grid subject is missing.");
        }

        const string marker = "/blobs/";

        var markerIndex =
            message.Subject.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
        {
            return (
                BlobResolution.Malformed,
                string.Empty,
                $"Unable to resolve blob path from subject '{message.Subject}'.");
        }

        var blobPath =
            message.Subject[(markerIndex + marker.Length)..];

        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return (
                BlobResolution.Malformed,
                string.Empty,
                $"Subject '{message.Subject}' does not contain a blob name.");
        }

        // Our own moves (archive/, error/, duplicate/) and generated outputs (output/)
        // also raise BlobCreated events when the subscription is not filtered.
        if (!blobPath.StartsWith(
                _folders.InputPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return (
                BlobResolution.Ignore,
                blobPath,
                $"Blob '{blobPath}' is not in the '{_folders.Input}' folder.");
        }

        return (BlobResolution.Ok, blobPath, string.Empty);
    }

    /// <summary>
    /// Stable id for one message: the Event Grid event id (a GUID) if present, so every
    /// redelivery of the same message logs and stores the same CorrelationId.
    /// </summary>
    private static Guid ResolveCorrelationId(
        string? eventId,
        string? messageId)
    {
        if (Guid.TryParse(eventId, out var fromEvent))
        {
            return fromEvent;
        }

        if (Guid.TryParse(messageId, out var fromMessage))
        {
            return fromMessage;
        }

        var seed =
            !string.IsNullOrWhiteSpace(eventId) ? eventId
            : !string.IsNullOrWhiteSpace(messageId) ? messageId
            : Guid.NewGuid().ToString();

        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(seed));

        return new Guid(hash.AsSpan(0, 16));  // take first 16 bytes and create GUID
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }

    private static string MakeSafeFileName(
    string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Order";
        }

        var invalidCharacters =
            Path.GetInvalidFileNameChars();

        var safeValue =
            new string(
                value
                    .Select(character =>
                        invalidCharacters.Contains(character)
                            ? '_'
                            : character)
                    .ToArray());

        return safeValue;
    }

    private enum BlobResolution
    {
        Ok,
        Ignore,
        Malformed
    }

    private sealed record Outcome(
        bool DeadLetter,
        string? Description = null)
    {
        public static readonly Outcome Complete = new(false);
    }
}