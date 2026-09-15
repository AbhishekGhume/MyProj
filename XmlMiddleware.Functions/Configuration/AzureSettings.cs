namespace XmlMiddleware.Functions.Configuration;

public sealed class AzureSettings
{
    public string StorageConnectionString { get; set; } = string.Empty;

    public string ServiceBusConnectionString { get; set; } = string.Empty;

    public string QueueName { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;

    public string InputFolder { get; set; } = string.Empty;

    public string OutputFolder { get; set; } = string.Empty;

    public string ArchiveFolder { get; set; } = string.Empty;

    public string ErrorFolder { get; set; } = string.Empty;
}