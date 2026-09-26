namespace XmlMiddleware.Application.Models;

public class CanonicalOrderItemModel
{
    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}