using System.Xml.Linq;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.Mapping;

public class XmlMapperService : IXmlMapperService
{
    public async Task<List<CanonicalOrderModel>> MapAsync(Stream xmlStream)
    {
        xmlStream.Position = 0;

        var document = await XDocument.LoadAsync(
            xmlStream,
            LoadOptions.None,
            CancellationToken.None);

        var orders = document
            .Root?
            .Elements("Order")
            .ToList()
            ?? [];

        var result = new List<CanonicalOrderModel>();

        foreach (var order in orders)
        {
            result.Add(new CanonicalOrderModel
            {
                OrderId = order.Element("OrderId")?.Value ?? string.Empty,

                OrderDate = DateTime.TryParse(
                    order.Element("OrderDate")?.Value,
                    out var orderDate)
                    ? orderDate
                    : DateTime.MinValue,

                CustomerId = order.Element("Customer")
                    ?.Element("CustomerId")
                    ?.Value ?? string.Empty,

                FirstName = order.Element("Customer")
                    ?.Element("FirstName")
                    ?.Value ?? string.Empty,

                LastName = order.Element("Customer")
                    ?.Element("LastName")
                    ?.Value ?? string.Empty,

                Email = order.Element("Customer")
                    ?.Element("Email")
                    ?.Value ?? string.Empty,

                AddressLine1 = order.Element("Address")
                    ?.Element("AddressLine1")
                    ?.Value ?? string.Empty,

                City = order.Element("Address")
                    ?.Element("City")
                    ?.Value ?? string.Empty,

                PostCode = order.Element("Address")
                    ?.Element("PostCode")
                    ?.Value ?? string.Empty,

                Country = order.Element("Address")
                    ?.Element("Country")
                    ?.Value ?? string.Empty,

                ProductCode = order.Element("OrderDetails")
                    ?.Element("ProductCode")
                    ?.Value ?? string.Empty,

                ProductName = order.Element("OrderDetails")
                    ?.Element("ProductName")
                    ?.Value ?? string.Empty,

                Quantity = int.TryParse(
                    order.Element("OrderDetails")
                        ?.Element("Quantity")
                        ?.Value,
                    out var quantity)
                    ? quantity
                    : 0,

                UnitPrice = decimal.TryParse(
                    order.Element("OrderDetails")
                        ?.Element("UnitPrice")
                        ?.Value,
                    out var unitPrice)
                    ? unitPrice
                    : 0
            });
        }

        return result;
    }
}