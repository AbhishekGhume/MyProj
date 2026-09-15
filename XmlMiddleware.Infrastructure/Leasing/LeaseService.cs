using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Application.Interfaces.Services;

namespace XmlMiddleware.Infrastructure.Leasing;

public class LeaseService : ILeaseService
{
    private readonly IProcessingBatchRepository _repository;

    public LeaseService(
        IProcessingBatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> AcquireLeaseAsync(long batchId)
    {
        var batch = await _repository.GetByIdAsync(batchId);

        if (batch is null)
        {
            return false;
        }

        if (batch.LeaseExpiry.HasValue &&
            batch.LeaseExpiry > DateTime.UtcNow)
        {
            return false;
        }

        batch.LeaseOwner = Environment.MachineName;

        batch.LeaseExpiry = DateTime.UtcNow.AddMinutes(10);

        _repository.Update(batch);

        await _repository.SaveChangesAsync();

        return true;
    }

    public async Task ReleaseLeaseAsync(long batchId)
    {
        var batch = await _repository.GetByIdAsync(batchId);

        if (batch is null)
        {
            return;
        }

        batch.LeaseOwner = null;
        batch.LeaseExpiry = null;

        _repository.Update(batch);

        await _repository.SaveChangesAsync();
    }
}