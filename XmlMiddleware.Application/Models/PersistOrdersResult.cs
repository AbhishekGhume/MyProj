namespace XmlMiddleware.Application.Models;

public sealed class PersistOrdersResult
{
    public int OrdersInserted { get; set; }

    public int CustomersInserted { get; set; }

    public int ProductsInserted { get; set; }

    public int OrderLinesInserted { get; set; }

    /// <summary>True when nothing was written because the data was already persisted.</summary>
    public bool Skipped { get; set; }

    public string? SkipReason { get; set; }

    public List<PersistOrderIssue> FailedOrders { get; } = [];

    public static PersistOrdersResult SkippedBecause(string reason) =>
        new() { Skipped = true, SkipReason = reason };
}

public sealed record PersistOrderIssue(string OrderId, string Reason);