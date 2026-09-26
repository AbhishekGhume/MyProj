namespace XmlMiddleware.Domain.Entities;

public class Product
{
    public long ProductSK { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; }
        = new List<OrderDetail>();
}