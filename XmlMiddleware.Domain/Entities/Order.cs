namespace XmlMiddleware.Domain.Entities;

public class Order
{
    public long OrderSK { get; set; }

    public string OrderId { get; set; } = string.Empty;

    public long BatchId { get; set; }

    public ProcessingBatch Batch { get; set; } = null!;

    public long CustomerSK { get; set; }

    public Customer Customer { get; set; } = null!;

    public DateOnly OrderDate { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string PostCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; }
        = new List<OrderDetail>();
}