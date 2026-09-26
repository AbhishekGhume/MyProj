using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.ServiceBus;

public class ServiceBusHealthService
    : IServiceBusHealthService
{
    private readonly ServiceBusAdministrationClient
        _administrationClient;

    private readonly string _queueName;

    private readonly ILogger<ServiceBusHealthService>
        _logger;

    public ServiceBusHealthService(
        IConfiguration configuration,
        ILogger<ServiceBusHealthService> logger)
    {
        var connectionString =
            configuration[
                "ServiceBusConnectionString"]
            ?? throw new InvalidOperationException(
                "ServiceBusConnectionString is not configured.");

        _queueName =
            configuration["QueueName"]
            ?? throw new InvalidOperationException(
                "QueueName is not configured.");

        _administrationClient =
            new ServiceBusAdministrationClient(
                connectionString);

        _logger = logger;
    }

    public async Task<ServiceBusHealthResult>
        GetHealthAsync(
            CancellationToken cancellationToken = default)
    {
        try
        {
            var queueResponse =
                await _administrationClient
                    .GetQueueAsync(
                        _queueName,
                        cancellationToken);

            var queue =
                queueResponse.Value;

            var runtimeResponse =
                await _administrationClient
                    .GetQueueRuntimePropertiesAsync(
                        _queueName,
                        cancellationToken);

            var runtime =
                runtimeResponse.Value;

            var status =
                queue.Status.ToString();

            var result =
                new ServiceBusHealthResult
                {
                    Status = status,

                    ActiveMessageCount =
                        runtime.ActiveMessageCount,

                    DeadLetterMessageCount =
                        runtime.DeadLetterMessageCount
                };

            switch (status)
            {
                case "Active":

                    result.IsHealthy = true;
                    result.Health = "Healthy";
                    result.Status = "Active";

                    result.Message =
                        "Queue is active and available for sending and receiving.";

                    break;

                case "SendDisabled":

                    result.IsHealthy = false;
                    result.Health = "Degraded";
                    result.Status = "SendDisabled";

                    result.Message =
                        "Sending is disabled. New Event Grid messages cannot enter the queue. Existing queued messages can still be processed.";

                    break;

                case "ReceiveDisabled":

                    result.IsHealthy = false;
                    result.Health = "Degraded";
                    result.Status = "ReceiveDisabled";

                    result.Message =
                        "Receiving is disabled. ProcessingFunction cannot consume messages. Messages may accumulate in the queue.";

                    break;

                case "Disabled":

                    result.IsHealthy = false;
                    result.Health = "Unavailable";
                    result.Status = "Disabled";

                    result.Message =
                        "Queue is disabled. Sending and receiving are unavailable. ProcessingFunction cannot consume messages.";

                    break;

                default:

                    result.IsHealthy = false;

                    result.Message =
                        $"Queue is in '{status}' state.";

                    break;
            }

            _logger.LogInformation(
                "Service Bus Status | Queue={QueueName} | Status={Status} | ActiveMessages={ActiveMessages} | DeadLetterMessages={DeadLetterMessages}",
                _queueName,
                result.Status,
                result.ActiveMessageCount,
                result.DeadLetterMessageCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Service Bus health check failed | Queue={QueueName} | Status=Unavailable",
                _queueName);

            return new ServiceBusHealthResult
            {
                IsHealthy = false,
                Status = "Unavailable",
                Message =
                    "Unable to retrieve Service Bus queue status."
            };
        }
    }
}