using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Domain.Constants;

/// <summary>
/// Vocabulary for ProcessingBatch.CurrentStep and ProcessingEvent.FromState / ToState.
/// Using constants keeps the journey consistent (no typos, easy to search).
/// </summary>
public static class BatchSteps
{
    // ---- happy path, in order ----
    public const string FileDetected = "FileDetected";
    public const string ExtensionChecked = "ExtensionChecked";
    public const string HashCalculated = "HashCalculated";
    public const string DuplicateChecked = "DuplicateChecked";
    public const string Validating = "Validating";
    public const string XmlValidated = "XmlValidated";
    public const string OrdersMapped = "OrdersMapped";
    public const string OrdersPersisted = "OrdersPersisted";
    public const string OutputRecordsCreated = "OutputRecordsCreated";
    public const string ProcessingStarted = "ProcessingStarted";
    public const string Completed = "Completed";
    public const string Archived = "Archived";

    // ---- retry ----
    public const string RetryPending = "RetryPending";
    public const string RetryStarted = "RetryStarted";

    // ---- terminal / side exits ----
    public const string Duplicate = "Duplicate";
    public const string ValidationFailed = "ValidationFailed";
    public const string Rejected = "Rejected";
    public const string DeadLettered = "DeadLettered";
    public const string Ignored = "Ignored";

    /// <summary>e.g. GeneratingPdf</summary>
    public static string Generating(OutputType outputType) => $"Generating{outputType}";

    /// <summary>e.g. PdfCompleted</summary>
    public static string OutputCompleted(OutputType outputType) => $"{outputType}Completed";

    /// <summary>e.g. PdfFailed</summary>
    public static string OutputFailed(OutputType outputType) => $"{outputType}Failed";
}