namespace XmlMiddleware.Application.Models;

public class ServiceBusHealthResult
{
    public bool IsHealthy { get; set; }

    public string Health { get; set; } =
        string.Empty;

    public string Status { get; set; } =
        string.Empty;

    public string Message { get; set; } =
        string.Empty;

    public long ActiveMessageCount { get; set; }

    public long DeadLetterMessageCount { get; set; }
}