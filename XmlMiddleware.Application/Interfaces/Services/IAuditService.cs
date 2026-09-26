using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Application.Interfaces.Services;

public interface IAuditService
{
    /// <summary>
    /// Adds a ProcessingEvent and calls SaveChanges on the shared DbContext, so any batch /
    /// output-file changes made just before the call are committed in the SAME transaction
    /// as the event (state change and journey entry can never disagree).
    /// </summary>
    Task LogEventAsync(
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
        CancellationToken cancellationToken = default);
}