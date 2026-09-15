namespace XmlMiddleware.Application.Messages;

public class FileProcessingMessage
{
    public long BatchId { get; set; }

    public Guid CorrelationId { get; set; }

    public string BlobPath { get; set; } = string.Empty;
}