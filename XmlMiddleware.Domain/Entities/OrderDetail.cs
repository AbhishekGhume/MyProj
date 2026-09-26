namespace XmlMiddleware.Domain.Entities;

public class OrderDetail
{
    public long OrderDetailSK { get; set; }

    public long OrderSK { get; set; }

    public Order Order { get; set; } = null!;

    public int LineNumber { get; set; }

    public long ProductSK { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; private set; }
}
