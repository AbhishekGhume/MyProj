using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Application.Interfaces.Repositories;

public interface IProcessingEventRepository
{
    Task AddAsync(ProcessingEvent processingEvent);

    Task AddRangeAsync(IEnumerable<ProcessingEvent> processingEvents);

    Task<List<ProcessingEvent>> GetByBatchIdAsync(long batchId);

    Task SaveChangesAsync();
}