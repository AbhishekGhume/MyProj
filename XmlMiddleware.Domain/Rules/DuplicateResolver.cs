using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Domain.Rules;

public sealed record DuplicateDecision(DuplicateCase? Case, ProcessingBatch? Original)
{
    public static readonly DuplicateDecision None = new(null, null);

    public bool IsDuplicate => Case.HasValue;
}

/// <summary>
/// Decides what to do with a file whose hash matches earlier batches.
/// Pass only EARLIER batches (lower BatchId) that are not themselves Duplicate rows.
/// Using "earlier only" makes two identical files that arrive at the same moment
/// deterministic: the lower BatchId wins, the other becomes the duplicate.
/// </summary>
public static class DuplicateResolver
{
    public static DuplicateDecision Decide(IEnumerable<ProcessingBatch> earlierBatches)
    {
        var candidates = earlierBatches
            .Where(b => b.Status != BatchStatus.Duplicate)
            .ToList();

        // Case 1 - an earlier identical file already completed.
        var completed = candidates
            .Where(b => b.Status == BatchStatus.Completed)
            .OrderBy(b => b.BatchId)
            .FirstOrDefault();

        if (completed is not null)
        {
            return new DuplicateDecision(DuplicateCase.AlreadyCompleted, completed);
        }

        // Case 2 - an earlier identical file is still being worked on.
        var inFlight = candidates
            .Where(b => !BatchStateMachine.IsTerminal(b.Status))
            .OrderBy(b => b.BatchId)
            .FirstOrDefault();

        if (inFlight is not null)
        {
            return new DuplicateDecision(DuplicateCase.AlreadyInProgress, inFlight);
        }

        // Case 3 - every earlier identical file failed; allow a fresh attempt.
        var failed = candidates
            .OrderByDescending(b => b.BatchId)
            .FirstOrDefault();

        return failed is null
            ? DuplicateDecision.None
            : new DuplicateDecision(DuplicateCase.PreviouslyFailed, failed);
    }
}