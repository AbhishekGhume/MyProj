using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Application.Interfaces.Repositories;

public interface IProcessingBatchRepository
{
    Task<ProcessingBatch?> GetByIdAsync(long batchId);

    Task<ProcessingBatch?> GetByHashAsync(string fileHash);

    Task<ProcessingBatch?> GetByCorrelationIdAsync(Guid correlationId);

    Task AddAsync(ProcessingBatch batch);

    void Update(ProcessingBatch batch);

    Task SaveChangesAsync();
}
