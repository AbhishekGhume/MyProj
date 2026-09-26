namespace XmlMiddleware.Application.Configuration;

/// <summary>
/// Blob folder names inside the container. Bound from app settings
/// (InputFolder, InProcessingFolder, OutputFolder, OutputStagingFolder, ArchiveFolder, ErrorFolder).
/// </summary>
public sealed class ProcessingFolders
{
    public string Input { get; init; } = "input";

    public string InProcessing { get; init; } = "inprocessing";

    public string Output { get; init; } = "output";

    public string OutputStaging { get; init; } = "output-staging";

    public string Archive { get; init; } = "archive";

    public string Error { get; init; } = "error";

    /// <summary>"input/"</summary>
    public string InputPrefix => $"{Input.Trim('/')}/";

    /// <summary>Blob ontainer name (app setting ContainerName).</summary>
    public string Container { get; init; } = string.Empty;

    /// <summary>"mycontainer/input/x.xml (for DB values and logs).</summary>
    public string ToFullPath(string relativePath) =>
        string.IsNullOrEmpty(Container) ? relativePath : $"{Container.Trim('/')}/{relativePath.TrimStart('/')}";

    /// <summary>"mycontainer/input/x.xml (for Blob storage calls).</summary>
	public string ToRelativePath(string storedPath)
    {
        if (string.IsNullOrEmpty(Container))
        {
            return storedPath;
        }

        var prefix = $"{Container.Trim('/')}/";

        return storedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? storedPath[prefix.Length..] : storedPath;
    }

    /// <summary>
    /// Builds "{folder}/{correlationId}_{fileName}". The correlation id keeps two files
    /// with the same name from overwriting each other in archive/error/duplicate.
    /// </summary>

    public string BuildPath(string folder, DateTime reveivedUtc, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        return $"{folder.Trim('/')}/{name}_{reveivedUtc.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture)}{extension}";
    }
}