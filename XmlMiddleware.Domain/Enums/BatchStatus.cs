namespace XmlMiddleware.Domain.Enums;

/// <summary>
/// Coarse lifecycle state of a ProcessingBatch. The fine-grained position inside
/// a state is tracked by ProcessingBatch.CurrentStep (see BatchSteps).
///
/// Allowed transitions are enforced by BatchStateMachine:
///
///   Received ──► Validated ──► Processing ──► Completed
///      │             │              │
///      │             └──► Rejected  │            (business data rejected)
///      ├──► ValidationFailed        │
///      ├──► Duplicate               │
///      └──► Rejected                │            (unsupported file extension)
///
///   Received / Validated / Processing ──► RetryPending ──► (resume) Received | Processing
///   Any non-terminal state           ──► DeadLettered      (3rd delivery attempt failed)
/// </summary>
public enum BatchStatus
{
    /// <summary>File detected, batch row created, intake checks running.</summary>
    Received = 1,

    /// <summary>XML passed structural validation.</summary>
    Validated = 2,

    /// <summary>Legacy value (old two-function design). No longer used.</summary>
    Published = 3,

    /// <summary>Orders persisted, output files are being generated.</summary>
    Processing = 4,

    /// <summary>All outputs generated. Terminal.</summary>
    Completed = 5,

    /// <summary>Legacy value. Unexpected failures now go RetryPending -> DeadLettered. Terminal.</summary>
    Failed = 6,

    /// <summary>Identical file already completed or in progress. Terminal.</summary>
    Duplicate = 7,

    /// <summary>Business rejection: unsupported extension or data-integrity conflict. Terminal.</summary>
    Rejected = 8,

    /// <summary>XML failed structural validation. Terminal.</summary>
    ValidationFailed = 9,

    /// <summary>Delivery attempts exhausted; message moved to the dead-letter queue. Terminal.</summary>
    DeadLettered = 10,

    /// <summary>An attempt failed and the message was abandoned for redelivery.</summary>
    RetryPending = 11,

    PartialSuccess = 12
}