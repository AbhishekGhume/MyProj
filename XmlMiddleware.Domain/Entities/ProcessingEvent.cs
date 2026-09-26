using XmlMiddleware.Domain.Common;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Domain.Entities;

/// <summary>
/// One step of a batch's journey. Every row carries CorrelationId, BatchId and FileName
/// so the journey can be found by any of the three.
/// </summary>
public class ProcessingEvent : BaseEntity
{
    public long EventId { get; set; }

    public long BatchId { get; set; }

    public long? OutputFileId { get; set; }

    public Guid CorrelationId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public EventType EventType { get; set; }

    /// <summary>Outcome of the step: Success, Started, Skipped, Rejected, Failed, Retry ...</summary>
    public string Status { get; set; } = string.Empty;

    public string? FromState { get; set; }

    public string? ToState { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string FunctionName { get; set; } = string.Empty;

    /// <summary>Service Bus delivery attempt (1..MaxDeliveryAttempts).</summary>
    public int AttemptNumber { get; set; }

    public ProcessingBatch ProcessingBatch { get; set; } = null!;

    public OutputFile? OutputFile { get; set; }
}