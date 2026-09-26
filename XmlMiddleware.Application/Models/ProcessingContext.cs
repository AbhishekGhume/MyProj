using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Application.Models;

/// <summary>
/// Everything that identifies one delivery of one file. Every log line and every
/// ProcessingEvent row is stamped with CorrelationId + BatchId + FileName from here.
/// </summary>
public sealed class ProcessingContext
{
    public ProcessingContext(
        Guid correlationId,
        string fileName,
        string blobPath,
        int attempt)
    {
        CorrelationId = correlationId;
        FileName = fileName;
        BlobPath = blobPath;
        Attempt = attempt;
    }

    public Guid CorrelationId { get; set; }

    public string FileName { get; set; }

    /// <summary>Blob path from the Event Grid event (input/...).</summary>
    public string BlobPath { get; set; }

    /// <summary>Service Bus delivery attempt, 1-based.</summary>
    public int Attempt { get; }

    /// <summary>The batch row for this file. Null until it has been created / loaded.</summary>
    public ProcessingBatch? Batch { get; set; }

    public bool HasPartialSuccess { get; set; }

    public string? RecoveryPreviousBlobPath { get; set; }

    /// <summary>Null until the batch row exists.</summary>
    public long? BatchId =>
        Batch is { BatchId: > 0 } ? Batch.BatchId : null;
}