using XmlMiddleware.Domain.Common;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Domain.Entities;

/// <summary>
/// One row per file ARRIVAL (including duplicates and rejected files).
/// </summary>
public class ProcessingBatch : BaseEntity
{
    public long BatchId { get; set; }

    public Guid CorrelationId { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>Where the source blob currently lives (input/, archive/, error/ or duplicate/).</summary>
    public string BlobPath { get; set; } = string.Empty;

    public string OriginalBlobPath { get; set; } = string.Empty;

    /// <summary>SHA-256 of the file. Null until the hash step has run (e.g. rejected extension).</summary>
    public string? FileHash { get; set; }

    public BatchStatus Status { get; set; }

    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>Retries scheduled so far (0 on the first attempt, max = MaxDeliveryAttempts - 1).</summary>
    public int RetryCount { get; set; }

    public DateTime? LastRetryDateTime { get; set; }

    public int RecordCount { get; set; }

    public int InvalidRecordCount { get; set; }

    public DateTime ReceivedDateTime { get; set; }

    public DateTime? ProcessingStartDateTime { get; set; }

    public DateTime? ProcessingEndDateTime { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    // Duplicate tracking
    // For AlreadyCompleted / AlreadyInProgress the batch is a Duplicate row.
    // For PreviouslyFailed the batch is processed again and points at the failed batch.
    public long? OriginalBatchId { get; set; }

    public ProcessingBatch? OriginalBatch { get; set; }

    public DuplicateCase? DuplicateCase { get; set; }

    // Navigation Properties
    public ICollection<OutputFile> OutputFiles { get; set; }
        = new List<OutputFile>();

    public ICollection<ProcessingEvent> ProcessingEvents { get; set; }
        = new List<ProcessingEvent>();

    public ICollection<Order> Orders { get; set; }
        = new List<Order>();
}