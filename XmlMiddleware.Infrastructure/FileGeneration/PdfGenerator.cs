using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public class PdfGenerator : IGenerator
{
    public MemoryStream Generate(
        List<CanonicalOrderModel> orders)
    {
        QuestPDF.Settings.License =
            LicenseType.Community;

        if (orders.Count != 1)
        {
            throw new InvalidOperationException(
                "PdfGenerator expects exactly one order.");
        }

        var order = orders[0];

        var orderTotal =
            order.Items.Sum(
                x => x.Quantity * x.UnitPrice);

        var stream =
            new MemoryStream();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.MarginHorizontal(30);
                page.MarginVertical(24);

                page.DefaultTextStyle(
                    style => style.FontSize(8));

                page.Header()
                    .Column(header =>
                    {
                        header.Item()
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(left =>
                                    {
                                        left.Item()
                                            .Text("XML MIDDLEWARE")
                                            .FontSize(16)
                                            .Bold();

                                        left.Item()
                                            .Text("Order Processing")
                                            .FontSize(8)
                                            .FontColor("#666666");
                                    });

                                row.ConstantItem(100)
                                    .Column(right =>
                                    {
                                        right.Item()
                                            .Text("ORDER")
                                            .FontSize(7)
                                            .Bold();

                                        right.Item()
                                            .Text(order.OrderId)
                                            .FontSize(12)
                                            .Bold();
                                    });
                            });

                        header.Item()
                            .PaddingTop(8)
                            .LineHorizontal(1)
                            .LineColor("#777777");
                    });

                page.Content()
                    .PaddingTop(14)
                    .Column(column =>
                    {
                        // -------------------------
                        // ORDER INFORMATION
                        // -------------------------

                        column.Item()
                            .Text("ORDER INFORMATION")
                            .FontSize(8)
                            .Bold();

                        column.Item()
                            .PaddingTop(6)
                            .Border(1)
                            .BorderColor("#DDDDDD")
                            .Padding(10)
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(item =>
                                    {
                                        item.Item()
                                            .Text("Order Number")
                                            .FontSize(7)
                                            .FontColor("#666666");

                                        item.Item()
                                            .PaddingTop(2)
                                            .Text(order.OrderId)
                                            .Bold();
                                    });

                                row.RelativeItem()
                                    .Column(item =>
                                    {
                                        item.Item()
                                            .Text("Order Date")
                                            .FontSize(7)
                                            .FontColor("#666666");

                                        item.Item()
                                            .PaddingTop(2)
                                            .Text(
                                                order.OrderDate
                                                    .ToString(
                                                        "dd MMM yyyy"))
                                            .Bold();
                                    });
                            });

                        // -------------------------
                        // CUSTOMER + ADDRESS
                        // -------------------------

                        column.Item()
                            .PaddingTop(16)
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(customer =>
                                    {
                                        customer.Item()
                                            .Text("CUSTOMER")
                                            .FontSize(8)
                                            .Bold();

                                        customer.Item()
                                            .PaddingTop(6)
                                            .Border(1)
                                            .BorderColor("#DDDDDD")
                                            .Padding(10)
                                            .Column(info =>
                                            {
                                                info.Item()
                                                    .Text(
                                                        $"{order.FirstName} {order.LastName}")
                                                    .Bold();

                                                info.Item()
                                                    .PaddingTop(3)
                                                    .Text(
                                                        $"Customer ID: {order.CustomerId}");

                                                info.Item()
                                                    .PaddingTop(2)
                                                    .Text(order.Email);
                                            });
                                    });

                                row.ConstantItem(12);

                                row.RelativeItem()
                                    .Column(address =>
                                    {
                                        address.Item()
                                            .Text("SHIPPING ADDRESS")
                                            .FontSize(8)
                                            .Bold();

                                        address.Item()
                                            .PaddingTop(6)
                                            .Border(1)
                                            .BorderColor("#DDDDDD")
                                            .Padding(10)
                                            .Column(info =>
                                            {
                                                info.Item()
                                                    .Text(
                                                        order.AddressLine1);

                                                info.Item()
                                                    .Text(order.City);

                                                info.Item()
                                                    .Text(
                                                        $"{order.PostCode}, {order.Country}");
                                            });
                                    });
                            });

                        // -------------------------
                        // ORDER ITEMS
                        // -------------------------

                        column.Item()
                            .PaddingTop(18)
                            .Text("ORDER ITEMS")
                            .FontSize(8)
                            .Bold();

                        column.Item()
                            .PaddingTop(6)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(25);
                                    columns.ConstantColumn(65);
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(40);
                                    columns.ConstantColumn(65);
                                    columns.ConstantColumn(65);
                                });

                                table.Header(header =>
                                {
                                    static IContainer HeaderCell(
                                        IContainer container)
                                    {
                                        return container
                                            .Background("#343434")
                                            .PaddingVertical(6)
                                            .PaddingHorizontal(4);
                                    }

                                    header.Cell()
                                        .Element(HeaderCell)
                                        .Text("#")
                                        .FontColor("#FFFFFF")
                                        .Bold();

                                    header.Cell()
                                        .Element(HeaderCell)
                                        .Text("CODE")
                                        .FontColor("#FFFFFF")
                                        .Bold();

                                    header.Cell()
                                        .Element(HeaderCell)
                                        .Text("PRODUCT")
                                        .FontColor("#FFFFFF")
                                        .Bold();

                                    header.Cell()
                                        .Element(HeaderCell)
                                        .AlignRight()
                                        .Text("QTY")
                                        .FontColor("#FFFFFF")
                                        .Bold();

                                    header.Cell()
                                        .Element(HeaderCell)
                                        .AlignRight()
                                        .Text("UNIT PRICE")
                                        .FontColor("#FFFFFF")
                                        .Bold();

                                    header.Cell()
                                        .Element(HeaderCell)
                                        .AlignRight()
                                        .Text("TOTAL")
                                        .FontColor("#FFFFFF")
                                        .Bold();
                                });

                                var lineNumber = 1;

                                foreach (var item in order.Items)
                                {
                                    var lineTotal =
                                        item.Quantity *
                                        item.UnitPrice;

                                    table.Cell()
                                        .BorderBottom(1)
                                        .BorderColor("#DDDDDD")
                                        .Padding(5)
                                        .Text(
                                            lineNumber
                                                .ToString());

                                    table.Cell()
                                        .BorderBottom(1)
                                        .BorderColor("#DDDDDD")
                                        .Padding(5)
                                        .Text(
                                            item.ProductCode);

                                    table.Cell()
                                        .BorderBottom(1)
                                        .BorderColor("#DDDDDD")
                                        .Padding(5)
                                        .Text(
                                            item.ProductName);

                                    table.Cell()
                                        .BorderBottom(1)
                                        .BorderColor("#DDDDDD")
                                        .Padding(5)
                                        .AlignRight()
                                        .Text(
                                            item.Quantity
                                                .ToString());

                                    table.Cell()
                                        .BorderBottom(1)
                                        .BorderColor("#DDDDDD")
                                        .Padding(5)
                                        .AlignRight()
                                        .Text(
                                            item.UnitPrice
                                                .ToString("F2"));

                                    table.Cell()
                                        .BorderBottom(1)
                                        .BorderColor("#DDDDDD")
                                        .Padding(5)
                                        .AlignRight()
                                        .Text(
                                            lineTotal
                                                .ToString("F2"));

                                    lineNumber++;
                                }
                            });

                        // -------------------------
                        // TOTAL
                        // -------------------------

                        column.Item()
                            .PaddingTop(10)
                            .AlignRight()
                            .Row(row =>
                            {
                                row.AutoItem()
                                    .PaddingRight(30)
                                    .Text("ORDER TOTAL")
                                    .FontSize(9)
                                    .Bold();

                                row.AutoItem()
                                    .Text(
                                        orderTotal
                                            .ToString("F2"))
                                    .FontSize(13)
                                    .Bold();
                            });
                    });

                page.Footer()
                    .PaddingTop(10)
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .AlignLeft()
                            .Text(
                                $"Generated: {DateTime.UtcNow:dd MMM yyyy HH:mm:ss} UTC")
                            .FontSize(7)
                            .FontColor("#888888");

                        row.RelativeItem()
                            .AlignRight()
                            .Text(
                                "Generated by XML Middleware")
                            .FontSize(7)
                            .FontColor("#888888");
                    });
            });
        })
        .GeneratePdf(stream);

        stream.Position = 0;

        return stream;
    }
}