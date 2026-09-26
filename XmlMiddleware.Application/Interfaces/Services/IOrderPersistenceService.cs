using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Application.Interfaces.Services;

public interface IOrderPersistenceService
{
    /// <summary>
    /// Persists customers, products, orders and order lines in one transaction.
    /// Idempotent: returns a Skipped result if this batch (or an earlier batch with the same
    /// file hash) has already persisted the data.
    /// Throws DataIntegrityException when the data conflicts with what is already stored.
    /// </summary>
    Task<PersistOrdersResult> PersistOrdersAsync(
        long batchId,
        Guid correlationId,
        string fileName,
        string? fileHash,
        IReadOnlyCollection<CanonicalOrderModel> orders,
        CancellationToken cancellationToken = default);
}