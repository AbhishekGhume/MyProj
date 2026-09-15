using XmlMiddleware.Domain.Common;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Domain.Entities;

public class OutputFile : BaseEntity
{
    public long OutputFileId { get; set; }

    public long BatchId { get; set; }

    public OutputType OutputType { get; set; }

    public OutputStatus Status { get; set; }

    // Retry Tracking
    public int AttemptCount { get; set; }

    public string? OutputBlobPath { get; set; }

    public DateTime? StartedDateTime { get; set; }

    public DateTime? CompletedDateTime { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    // Navigation Properties
    public ProcessingBatch ProcessingBatch { get; set; } = null!;
}