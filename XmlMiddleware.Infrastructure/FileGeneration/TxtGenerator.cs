using System.Text;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public class TxtGenerator : IGenerator
{
    public MemoryStream Generate(
        List<CanonicalOrderModel> orders)
    {
        var builder = new StringBuilder();

        builder.AppendLine("========================================================================");
        builder.AppendLine("ORDER DETAILS");
        builder.AppendLine("========================================================================");
        builder.AppendLine();

        foreach (var order in orders)
        {
            builder.AppendLine(
                $"Order Id       : {order.OrderId}");

            builder.AppendLine(
                $"Order Date     : {order.OrderDate:yyyy-MM-dd}");

            builder.AppendLine();

            builder.AppendLine(
                $"Customer Id    : {order.CustomerId}");

            builder.AppendLine(
                $"Customer Name  : {order.FirstName} {order.LastName}");

            builder.AppendLine(
                $"Email          : {order.Email}");

            builder.AppendLine();

            builder.AppendLine(
                $"Address Line 1 : {order.AddressLine1}");

            builder.AppendLine(
                $"City           : {order.City}");

            builder.AppendLine(
                $"Post Code      : {order.PostCode}");

            builder.AppendLine(
                $"Country        : {order.Country}");

            builder.AppendLine();

            builder.AppendLine(
                $"Product Code   : {order.ProductCode}");

            builder.AppendLine(
                $"Product Name   : {order.ProductName}");

            builder.AppendLine();

            builder.AppendLine(
                $"Quantity       : {order.Quantity}");

            builder.AppendLine(
                $"Unit Price     : {order.UnitPrice:F2}");

            builder.AppendLine();

            builder.AppendLine(
                "========================================================================");

            builder.AppendLine();
        }

        return new MemoryStream(
            Encoding.UTF8.GetBytes(builder.ToString()));
    }
}