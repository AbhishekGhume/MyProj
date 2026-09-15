using XmlMiddleware.Domain.Common;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Domain.Entities;

public class ProcessingBatch : BaseEntity
{
    public long BatchId { get; set; }

    public Guid CorrelationId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string BlobPath { get; set; } = string.Empty;

    public string FileHash { get; set; } = string.Empty;

    public BatchStatus Status { get; set; }

    public string CurrentStep { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public DateTime? LastRetryDateTime { get; set; }
    public int RecordCount { get; set; }

    public DateTime ReceivedDateTime { get; set; }

    public DateTime? ProcessingStartDateTime { get; set; }

    public DateTime? ProcessingEndDateTime { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    // Lease Management
    public string? LeaseOwner { get; set; }

    public DateTime? LeaseExpiry { get; set; }

    // Navigation Properties
    public ICollection<OutputFile> OutputFiles { get; set; }
        = new List<OutputFile>();

    public ICollection<ProcessingEvent> ProcessingEvents { get; set; }
        = new List<ProcessingEvent>();
}