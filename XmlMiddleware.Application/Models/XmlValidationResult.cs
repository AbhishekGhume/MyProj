namespace XmlMiddleware.Application.Models;

public class XmlValidationResult
{
    public bool IsValid { get; set; }
    public int RecordCount { get; set; }

    public List<string> Errors { get; set; } = new();
}