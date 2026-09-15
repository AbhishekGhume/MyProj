using XmlMiddleware.Domain.Common;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Domain.Entities;

public class ProcessingEvent : BaseEntity
{
    public long EventId { get; set; }

    public long BatchId { get; set; }

    public long? OutputFileId { get; set; }

    public Guid CorrelationId { get; set; }

    public EventType EventType { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string FunctionName { get; set; } = string.Empty;

    public int AttemptNumber { get; set; }

    // Navigation Properties
    public ProcessingBatch ProcessingBatch { get; set; } = null!;

    public OutputFile? OutputFile { get; set; }
}