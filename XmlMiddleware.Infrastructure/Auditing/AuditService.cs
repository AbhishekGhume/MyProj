using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Infrastructure.Auditing;

public class AuditService : IAuditService
{
    private readonly IProcessingEventRepository _repository;

    public AuditService(
        IProcessingEventRepository repository)
    {
        _repository = repository;
    }

    public async Task LogEventAsync(
        long batchId,
        Guid correlationId,
        EventType eventType,
        string status,
        string message,
        string functionName,
        int attemptNumber = 0,
        long? outputFileId = null)
    {
        var auditEvent = new ProcessingEvent
        {
            BatchId = batchId,
            CorrelationId = correlationId,
            EventType = eventType,
            Status = status,
            Message = message,
            FunctionName = functionName,
            AttemptNumber = attemptNumber,
            OutputFileId = outputFileId
        };

        await _repository.AddAsync(auditEvent);

        await _repository.SaveChangesAsync();
    }
}