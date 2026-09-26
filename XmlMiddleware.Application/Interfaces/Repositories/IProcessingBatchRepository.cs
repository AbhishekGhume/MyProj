using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Application.Interfaces.Repositories;

public interface IProcessingBatchRepository
{
    Task<ProcessingBatch?> GetByIdAsync(
        long batchId,
        CancellationToken cancellationToken = default);

    Task<ProcessingBatch?> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Earlier batches (lower BatchId) with the same file hash, excluding rows that are
    /// themselves Duplicates. Read-only (not tracked).
    /// </summary>
    Task<List<ProcessingBatch>> GetHashCandidatesAsync(
        string fileHash,
        long beforeBatchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the EF change tracker and loads the batch (with output files) fresh from the
    /// database. Used after a failure so half-applied in-memory changes can never be
    /// written by the next SaveChanges.
    /// </summary>
    Task<ProcessingBatch?> ReloadAsync(
        long batchId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ProcessingBatch batch,
        CancellationToken cancellationToken = default);

    void Update(ProcessingBatch batch);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<ProcessingBatch?> GetLatestDeadLetteredByHashAsync(
        string fileHash,
        CancellationToken cancellationToken = default);
}