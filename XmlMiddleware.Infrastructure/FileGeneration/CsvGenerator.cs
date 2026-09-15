using System.Text;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public class CsvGenerator : IGenerator
{
    public MemoryStream Generate(
        List<CanonicalOrderModel> orders)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "OrderId,OrderDate,CustomerId,FirstName,LastName,Email,AddressLine1,City,PostCode,Country,ProductCode,ProductName,Quantity,UnitPrice");

        foreach (var order in orders)
        {
            builder.AppendLine(
                $"{order.OrderId}," +
                $"{order.OrderDate:yyyy-MM-dd}," +
                $"{order.CustomerId}," +
                $"{order.FirstName}," +
                $"{order.LastName}," +
                $"{order.Email}," +
                $"{order.AddressLine1}," +
                $"{order.City}," +
                $"{order.PostCode}," +
                $"{order.Country}," +
                $"{order.ProductCode}," +
                $"{order.ProductName}," +
                $"{order.Quantity}," +
                $"{order.UnitPrice:F2}");
        }

        return new MemoryStream(
            Encoding.UTF8.GetBytes(builder.ToString()));
    }
}