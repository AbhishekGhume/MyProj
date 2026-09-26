using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Application.Interfaces.Repositories;

public interface IProcessingEventRepository
{
    Task AddAsync(
        ProcessingEvent processingEvent,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<ProcessingEvent> processingEvents,
        CancellationToken cancellationToken = default);

    Task<List<ProcessingEvent>> GetByBatchIdAsync(
        long batchId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}