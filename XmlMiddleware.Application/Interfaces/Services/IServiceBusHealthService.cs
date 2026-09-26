using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Application.Interfaces.Services;

public interface IServiceBusHealthService
{
    Task<ServiceBusHealthResult>
        GetHealthAsync(
            CancellationToken cancellationToken = default);
}