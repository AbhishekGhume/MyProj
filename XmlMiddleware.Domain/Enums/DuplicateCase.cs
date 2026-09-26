namespace XmlMiddleware.Domain.Enums;

/// <summary>
/// What the duplicate check found when an identical file (same SHA-256) had
/// already been seen by an earlier batch.
/// </summary>
public enum DuplicateCase
{
    /// <summary>An earlier batch with the same hash Completed. New batch = Duplicate, file skipped.</summary>
    AlreadyCompleted = 1,

    /// <summary>An earlier batch with the same hash is still in flight. New batch = Duplicate, file skipped.</summary>
    AlreadyInProgress = 2,

    /// <summary>Every earlier batch with the same hash failed. New batch is processed again.</summary>
    PreviouslyFailed = 3
}