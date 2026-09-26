using System;
using System.Collections.Generic;
using System.Text;

namespace XmlMiddleware.Application.Models;

public class CanonicalOrderModel
{
    public string OrderId { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string PostCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public List<CanonicalOrderItemModel> Items { get; set; } = new();
}
