namespace XmlMiddleware.Application.Interfaces.Services;

public interface ILeaseService
{
    Task<bool> AcquireLeaseAsync(long batchId);

    Task ReleaseLeaseAsync(long batchId);
}