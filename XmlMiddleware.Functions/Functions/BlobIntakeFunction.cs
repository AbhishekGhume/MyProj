//using Microsoft.Azure.Functions.Worker;
//using Microsoft.Extensions.Logging;
//using XmlMiddleware.Application.Interfaces.Repositories;
//using XmlMiddleware.Application.Interfaces.Services;
//using XmlMiddleware.Application.Messages;
//using XmlMiddleware.Domain.Entities;
//using XmlMiddleware.Domain.Enums;
//using XmlMiddleware.Infrastructure.Mapping;

//namespace XmlMiddleware.Functions.Functions;

//public class BlobIntakeFunction
//{
//    private readonly ILogger<BlobIntakeFunction> _logger;
//    private readonly IHashService _hashService;
//    private readonly IXmlValidationService _xmlValidationService;
//    private readonly IProcessingBatchRepository _batchRepository;
//    private readonly IOutputFileRepository _outputFileRepository;
//    private readonly IAuditService _auditService;
//    private readonly IMessagePublisherService _messagePublisher;
//    private readonly IBlobStorageService _blobStorageService;
//    private readonly IXmlMapperService _xmlMapperService;

//    public BlobIntakeFunction(
//        ILogger<BlobIntakeFunction> logger,
//        IHashService hashService,
//        IXmlValidationService xmlValidationService,
//        IProcessingBatchRepository batchRepository,
//        IOutputFileRepository outputFileRepository,
//        IAuditService auditService,
//        IMessagePublisherService messagePublisher,
//        IBlobStorageService blobStorageService,
//        IXmlMapperService xmlMapperService)
//    {
//        _logger = logger;
//        _hashService = hashService;
//        _xmlValidationService = xmlValidationService;
//        _batchRepository = batchRepository;
//        _outputFileRepository = outputFileRepository;
//        _auditService = auditService;
//        _messagePublisher = messagePublisher;
//        _blobStorageService = blobStorageService;
//        _xmlMapperService = xmlMapperService;
//    }

//    [Function(nameof(BlobIntakeFunction))]
//    public async Task Run(
//        [BlobTrigger(
//            "%ContainerName%/%InputFolder%/{name}",
//            Connection = "AzureWebJobsStorage")]
//        Stream stream,
//        string name)
//    {
//        var correlationId = Guid.NewGuid();

//        using var scope = _logger.BeginScope(
//            new Dictionary<string, object>
//            {
//                ["CorrelationId"] = correlationId,
//                ["FileName"] = name
//            });

//        try
//        {
//            _logger.LogInformation(
//                "New File Detected | CorrelationId={CorrelationId} | FileName={FileName}",
//                correlationId,
//                name);

//            // --------------------------------------------------
//            // STORAGE VALIDATION
//            // --------------------------------------------------

//            if (!await _blobStorageService.ContainerExistsAsync())
//            {
//                _logger.LogError(
//                "Blob container unavailable. Processing aborted. | CorrelationId={CorrelationId}",
//                correlationId);

//                return;
//            }

//            // --------------------------------------------------
//            // FILE FORMAT VALIDATION
//            // --------------------------------------------------

//            if (!Path.GetExtension(name)
//                    .Equals(".xml",
//                        StringComparison.OrdinalIgnoreCase))
//            {
//                _logger.LogError(
//                    "Invalid File Format | FileName={FileName}",
//                    name);

//                var errorBlobPath =
//                    $"error/{name}";

//                await _blobStorageService.MoveAsync(
//                    $"input/{name}",
//                    errorBlobPath);

//                _logger.LogWarning(
//                    "Invalid File Format. File moved to error folder | FileName={FileName}",
//                    name);

//                return;
//            }

//            // --------------------------------------------------
//            // STEP 1 : GENERATE HASH
//            // --------------------------------------------------

//            _logger.LogInformation(
//                "Hash Generation Started | CorrelationId={CorrelationId} | FileName={FileName}",
//                correlationId,
//                name);

//            var hash = await _hashService.GenerateHashAsync(stream);

//            stream.Position = 0;

//            _logger.LogInformation(
//            "Hash Generated | CorrelationId={CorrelationId} | FileName={FileName} | Hash={Hash}",
//            correlationId,
//            name,
//            hash);

//            // --------------------------------------------------
//            // STEP 2 : DUPLICATE CHECK
//            // --------------------------------------------------

//            _logger.LogInformation(
//                "Duplicate Check Started | CorrelationId={CorrelationId} | FileName={FileName}",
//                correlationId,
//                name);

//            var existingBatch =
//                await _batchRepository.GetByHashAsync(hash);

//            // Case 1: Completed Batch
//            if (existingBatch is not null &&
//                existingBatch.Status == BatchStatus.Completed)
//            {
//                _logger.LogWarning(
//                    "Duplicate Completed File Detected | CorrelationId={CorrelationId} | ExistingBatchId={BatchId} | FileName={FileName}",
//                    correlationId,
//                    existingBatch.BatchId,
//                    name);

//                return;
//            }

//            // Case 2: Processing Batch
//            if (existingBatch is not null &&
//                existingBatch.Status == BatchStatus.Processing)
//            {
//                _logger.LogWarning(
//                    "Duplicate File Already Processing | CorrelationId={CorrelationId} | ExistingBatchId={BatchId} | FileName={FileName}",
//                    correlationId,
//                    existingBatch.BatchId,
//                    name);

//                return;
//            }

//            // Case 3: Failed Batch
//            if (existingBatch is not null &&
//                existingBatch.Status == BatchStatus.DeadLettered)
//            {
//                _logger.LogInformation(
//                    "Failed Batch Found. Republishing Existing Batch | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
//                    correlationId,
//                    existingBatch.BatchId,
//                    name);

//                await _messagePublisher.PublishAsync(
//                    new FileProcessingMessage
//                    {
//                        BatchId = existingBatch.BatchId,
//                        CorrelationId = existingBatch.CorrelationId,
//                        BlobPath = existingBatch.BlobPath
//                    });

//                return;
//            }

//            var batch = new ProcessingBatch
//            {
//                CorrelationId = correlationId,
//                FileName = name,
//                BlobPath = $"input/{name}",
//                FileHash = hash,
//                RecordCount = 0,
//                Status = BatchStatus.Received,
//                ReceivedDateTime = DateTime.UtcNow,
//                CurrentStep = "Received"
//            };


//            await _batchRepository.AddAsync(batch);
//            await _batchRepository.SaveChangesAsync();

//            await _auditService.LogEventAsync(
//                batch.BatchId,
//                correlationId,
//                EventType.BatchCreated,
//                "Success",
//                "Batch created successfully",
//                nameof(BlobIntakeFunction),
//                fromState: null,
//                toState: "Received");

//            _logger.LogInformation(
//                "Batch Created | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
//                correlationId,
//                batch.BatchId,
//                name);

//            // --------------------------------------------------
//            // STEP 3 : XML VALIDATION
//            // --------------------------------------------------

//            _logger.LogInformation(
//                "Validation Started | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
//                correlationId,
//                batch.BatchId,
//                name);

//            var validationResult =
//                await _xmlValidationService.ValidateAsync(stream);

//            batch.RecordCount = validationResult.RecordCount;

//            if (!validationResult.IsValid)
//            {
//                var errorMessage =
//                    string.Join(" | ", validationResult.Errors);

//                batch.Status = BatchStatus.ValidationFailed;
//                batch.ErrorMessage = errorMessage;
//                batch.CurrentStep = "ValidationFailed";

//                _batchRepository.Update(batch);

//                await _batchRepository.SaveChangesAsync();

//                var errorBlobPath = $"error/{name}";

//                await _blobStorageService.MoveAsync(
//                    batch.BlobPath,
//                    errorBlobPath);

//                batch.BlobPath = errorBlobPath;

//                _batchRepository.Update(batch);

//                await _batchRepository.SaveChangesAsync();

//                _logger.LogWarning(
//                    "Validation Failed. File moved to error folder | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
//                    correlationId,
//                    batch.BatchId,
//                    name);

//                await _auditService.LogEventAsync(
//                    batch.BatchId,
//                    correlationId,
//                    EventType.ValidationFailed,
//                    "Failed",
//                    errorMessage,
//                    nameof(BlobIntakeFunction),
//                    fromState: "Received",
//                    toState: "ValidationFailed");

//                _logger.LogWarning(
//                    "Validation Failed | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName} | Errors={Errors}",
//                    correlationId,
//                    batch.BatchId,
//                    name,
//                    errorMessage);

//                return;
//            }

//            batch.Status = BatchStatus.Validated;
//            batch.CurrentStep = "Validated";

//            _batchRepository.Update(batch);

//            await _batchRepository.SaveChangesAsync();

//            await _auditService.LogEventAsync(
//                batch.BatchId,
//                correlationId,
//                EventType.ValidationPassed,
//                "Success",
//                "XML validation successful",
//                nameof(BlobIntakeFunction),
//                fromState: "Received",
//                toState: "Validated");

//            _logger.LogInformation(
//                "Validation Passed | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
//                correlationId,
//                batch.BatchId,
//                name);

//            // --------------------------------------------------
//            // STEP 5 : CREATE OUTPUT FILE RECORDS
//            // --------------------------------------------------

//            var outputFiles = new List<OutputFile>
//            {
//                CreateOutputFile(batch.BatchId, OutputType.Txt),
//                CreateOutputFile(batch.BatchId, OutputType.Csv),
//                CreateOutputFile(batch.BatchId, OutputType.Json),
//                CreateOutputFile(batch.BatchId, OutputType.Dat),
//                CreateOutputFile(batch.BatchId, OutputType.Pdf),
//                CreateOutputFile(batch.BatchId, OutputType.Xlsx)
//            };

//            await _outputFileRepository.AddRangeAsync(outputFiles);
//            await _outputFileRepository.SaveChangesAsync();

//            _logger.LogInformation(
//                "Output Records Created | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName} | OutputCount={OutputCount}",
//                correlationId,
//                batch.BatchId,
//                name,
//                outputFiles.Count);


//            // --------------------------------------------------
//            // STEP 7 : PUBLISH QUEUE MESSAGE
//            // --------------------------------------------------

//            var message = new FileProcessingMessage
//            {
//                BatchId = batch.BatchId,
//                CorrelationId = correlationId,
//                BlobPath = batch.BlobPath
//            };

//            await _messagePublisher.PublishAsync(message);

//            _logger.LogInformation(
//                "Queue Message Published | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
//                correlationId,
//                batch.BatchId,
//                name);

//            await _auditService.LogEventAsync(
//                batch.BatchId,
//                correlationId,
//                EventType.MessagePublished,
//                "Success",
//                "Message published to queue",
//                nameof(BlobIntakeFunction),
//                fromState: "Validated",
//                toState: "Queued");

//            _logger.LogInformation(
//                "Blob Intake Completed | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
//                correlationId,
//                batch.BatchId,
//                name);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(
//                ex.Message,
//                "Blob Intake Failed | CorrelationId={CorrelationId} | FileName={FileName} ",
//                correlationId,
//                name);

//            //_logger.LogError(
//            //    ex,
//            //    "Blob Intake Failed | CorrelationId={CorrelationId} | FileName={FileName}",
//            //    correlationId,
//            //    name);

//            throw;
//        }
//    }

//    private static OutputFile CreateOutputFile(
//        long batchId,
//        OutputType outputType)
//    {
//        return new OutputFile
//        {
//            BatchId = batchId,
//            OutputType = outputType,
//            Status = OutputStatus.Pending,
//            AttemptCount = 0
//        };
//    }
//}