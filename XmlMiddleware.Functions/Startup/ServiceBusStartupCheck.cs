using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XmlMiddleware.Application.Interfaces.Services;

namespace XmlMiddleware.Functions.Startup;

public class ServiceBusStartupCheck
{
    private readonly IServiceBusHealthService _serviceBusHealthService;
    private readonly ILogger<ServiceBusStartupCheck> _logger;
    private readonly string _queueName;

    public ServiceBusStartupCheck(
        IServiceBusHealthService serviceBusHealthService,
        ILogger<ServiceBusStartupCheck> logger,
        IConfiguration configuration)
    {
        _serviceBusHealthService =
            serviceBusHealthService;

        _logger =
            logger;

        _queueName =
            configuration["QueueName"]
            ?? "Unknown";
    }

    public async Task CheckAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            await _serviceBusHealthService.GetHealthAsync(
                cancellationToken);

        // --------------------------------------------------
        // STRUCTURED LOG
        // Application Insights / configured logging pipeline
        // --------------------------------------------------

        switch (result.Status)
        {
            case "Active":

                _logger.LogInformation(
                    "SERVICE BUS STATUS | Queue={Queue} | Status={Status} | Health={Health} | ActiveMessages={ActiveMessages} | DeadLetterMessages={DeadLetterMessages} | Message={Message}",
                    _queueName,
                    result.Status,
                    result.Health,
                    result.ActiveMessageCount,
                    result.DeadLetterMessageCount,
                    result.Message);

                break;

            case "SendDisabled":

            case "ReceiveDisabled":

                _logger.LogWarning(
                    "SERVICE BUS STATUS | Queue={Queue} | Status={Status} | Health={Health} | ActiveMessages={ActiveMessages} | DeadLetterMessages={DeadLetterMessages} | Message={Message}",
                    _queueName,
                    result.Status,
                    result.Health,
                    result.ActiveMessageCount,
                    result.DeadLetterMessageCount,
                    result.Message);

                break;

            default:

                _logger.LogError(
                    "SERVICE BUS STATUS | Queue={Queue} | Status={Status} | Health={Health} | ActiveMessages={ActiveMessages} | DeadLetterMessages={DeadLetterMessages} | Message={Message}",
                    _queueName,
                    result.Status,
                    result.Health,
                    result.ActiveMessageCount,
                    result.DeadLetterMessageCount,
                    result.Message);

                break;
        }

    }
}