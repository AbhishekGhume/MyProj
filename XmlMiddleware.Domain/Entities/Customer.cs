namespace XmlMiddleware.Domain.Entities;

public class Customer
{
    public long CustomerSK { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public ICollection<Order> Orders { get; set; }
        = new List<Order>();
}