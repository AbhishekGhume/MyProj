using Microsoft.Extensions.Logging;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Domain.Constants;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Infrastructure.Auditing;

public class AuditService : IAuditService
{
    private const int MaxMessageLength = 4000;
    private const int MaxErrorMessageLength = 4000;

    private readonly IProcessingEventRepository _repository;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IProcessingEventRepository repository,
        ILogger<AuditService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task LogEventAsync(
        long batchId,
        Guid correlationId,
        string fileName,
        EventType eventType,
        string status,
        string message,
        string functionName,
        string? fromState = null,
        string? toState = null,
        int attemptNumber = 0,
        long? outputFileId = null,
        string? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new ProcessingEvent
        {
            BatchId = batchId,
            CorrelationId = correlationId,
            FileName = fileName,
            EventType = eventType,
            Status = status,

            FromState = fromState,
            ToState = toState,

            Message = Truncate(message, MaxMessageLength) ?? string.Empty,
            ErrorCode = Truncate(errorCode, 100),
            ErrorMessage = Truncate(errorMessage, MaxErrorMessageLength),

            FunctionName = functionName,
            AttemptNumber = attemptNumber,
            OutputFileId = outputFileId
        };

        await _repository.AddAsync(
            auditEvent,
            cancellationToken);

        // SaveChanges on the shared DbContext also commits any batch / output-file
        // changes the caller made just before this call -> one atomic write.
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Audit event saved | EventId={EventId} | EventType={EventType} | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
            auditEvent.EventId,
            eventType,
            correlationId,
            batchId,
            fileName);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}