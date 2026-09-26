namespace XmlMiddleware.Domain.Enums;

/// <summary>
/// Kinds of entries written to ProcessingEvents (the per-batch journey).
/// Numeric values 1-13 are unchanged from the previous version.
/// </summary>
public enum EventType
{
    BatchCreated = 1,

    HashCalculated = 2,

    ValidationStarted = 3,

    ValidationPassed = 4,

    ValidationFailed = 5,

    /// <summary>Legacy (old two-function design). No longer written.</summary>
    MessagePublished = 6,

    OutputStarted = 7,

    OutputCompleted = 8,

    OutputFailed = 9,

    RetryStarted = 10,

    BatchCompleted = 11,

    DuplicateDetected = 12,

    MaxRetriesExceeded = 13,

    FileDetected = 14,

    ExtensionCheckPassed = 15,

    ExtensionCheckFailed = 16,

    DuplicateCheckPassed = 17,

    OrdersMapped = 18,

    OrdersPersisted = 19,

    DataIntegrityFailed = 20,

    OutputRecordsCreated = 21,

    ProcessingStarted = 22,

    SourceFileArchived = 23,

    SourceFileMovedToError = 24,

    SourceFileMovedToDuplicate = 25,

    SourceFileMoveFailed = 26,

    RetryScheduled = 27,

    DeadLettered = 28,

    ProcessingCancelled = 29,

    MessageIgnored = 30,

    SourceFileCopiedToInProcessing = 31,

    OutputsPublished = 32
}