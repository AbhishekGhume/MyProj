using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Domain.Rules;

/// <summary>
/// Single source of truth for which BatchStatus changes are legal.
/// Pure logic (no I/O) so it can be unit tested.
/// </summary>
public static class BatchStateMachine
{
    private static readonly IReadOnlyDictionary<BatchStatus, BatchStatus[]> Allowed =
        new Dictionary<BatchStatus, BatchStatus[]>
        {
            [BatchStatus.Received] = new[]
            {
                BatchStatus.Validated,
                BatchStatus.ValidationFailed,
                BatchStatus.Duplicate,
                BatchStatus.Rejected,
                BatchStatus.RetryPending,
                BatchStatus.DeadLettered
            },

            // Validated -> Received is a resume after a crash / retry when no output rows exist yet.
            [BatchStatus.Validated] = new[]
            {
                BatchStatus.Received,
                BatchStatus.Processing,
                BatchStatus.Rejected,
                BatchStatus.RetryPending,
                BatchStatus.DeadLettered
            },

            [BatchStatus.Processing] = new[]
            {
                BatchStatus.Completed,
                BatchStatus.PartialSuccess,
                BatchStatus.RetryPending,
                BatchStatus.DeadLettered
            },

            // A retried delivery resumes at Received (intake not finished) or Processing (outputs exist).
            [BatchStatus.RetryPending] = new[]
            {
                BatchStatus.Received,
                BatchStatus.Processing,
                BatchStatus.DeadLettered
            },

            // A manually resent DLQ message is a new delivery and may resume the
            // stored checkpoint. Automatic redelivery never reaches this state.
            [BatchStatus.DeadLettered] = new[]
            {
                BatchStatus.Received,
                BatchStatus.Processing
            }
        };

    public static bool IsTerminal(BatchStatus status) =>
        status is BatchStatus.Completed
            or BatchStatus.Failed
            or BatchStatus.Duplicate
            or BatchStatus.Rejected
            or BatchStatus.ValidationFailed
            or BatchStatus.DeadLettered
            or BatchStatus.PartialSuccess;

    public static bool CanTransition(BatchStatus from, BatchStatus to)
    {
        if (from == to)
        {
            return !IsTerminal(from);
        }

        return Allowed.TryGetValue(from, out var targets)
               && Array.IndexOf(targets, to) >= 0;
    }

    public static void EnsureCanTransition(BatchStatus from, BatchStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Illegal batch status transition {from} -> {to}.");
        }
    }
}