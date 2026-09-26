using System.Text;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public class DatGenerator : IGenerator
{
    public MemoryStream Generate(
        List<CanonicalOrderModel> orders)
    {
        var builder = new StringBuilder();

        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                builder.AppendLine(
                    $"{order.OrderId}|" +
                    $"{order.OrderDate:yyyy-MM-dd}|" +
                    $"{order.CustomerId}|" +
                    $"{order.FirstName}|" +
                    $"{order.LastName}|" +
                    $"{order.AddressLine1}|" +
                    $"{order.City}|" +
                    $"{order.PostCode}|" +
                    $"{order.Country}|" +
                    $"{item.ProductCode}|" +
                    $"{item.ProductName}|" +
                    $"{item.Quantity}|" +
                    $"{item.UnitPrice:F2}");
            }
        }

        return new MemoryStream(
            Encoding.UTF8.GetBytes(builder.ToString()));
    }
}