using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Application.Interfaces.Services;

public interface IAuditService
{
    Task LogEventAsync(
        long batchId,
        Guid correlationId,
        EventType eventType,
        string status,
        string message,
        string functionName,
        int attemptNumber = 0,
        long? outputFileId = null);
}