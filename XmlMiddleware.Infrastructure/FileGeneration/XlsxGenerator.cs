using ClosedXML.Excel;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public class XlsxGenerator : IGenerator
{
    public MemoryStream Generate(
        List<CanonicalOrderModel> orders)
    {
        using var workbook = new XLWorkbook();

        var worksheet =
            workbook.Worksheets.Add("Orders");

        worksheet.Cell(1, 1).Value = "Order Id";
        worksheet.Cell(1, 2).Value = "Order Date";
        worksheet.Cell(1, 3).Value = "Customer Id";
        worksheet.Cell(1, 4).Value = "First Name";
        worksheet.Cell(1, 5).Value = "Last Name";
        worksheet.Cell(1, 6).Value = "Email";
        worksheet.Cell(1, 7).Value = "Address";
        worksheet.Cell(1, 8).Value = "City";
        worksheet.Cell(1, 9).Value = "Post Code";
        worksheet.Cell(1, 10).Value = "Country";
        worksheet.Cell(1, 11).Value = "Product Code";
        worksheet.Cell(1, 12).Value = "Product Name";
        worksheet.Cell(1, 13).Value = "Quantity";
        worksheet.Cell(1, 14).Value = "Unit Price";
        worksheet.Cell(1, 15).Value = "Order Value";

        var headerRange =
            worksheet.Range(1, 1, 1, 15);

        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor =
            XLColor.LightBlue;

        int row = 2;

        foreach (var order in orders)
        {
            worksheet.Cell(row, 1).Value =
                order.OrderId;

            worksheet.Cell(row, 2).Value =
                order.OrderDate;

            worksheet.Cell(row, 3).Value =
                order.CustomerId;

            worksheet.Cell(row, 4).Value =
                order.FirstName;

            worksheet.Cell(row, 5).Value =
                order.LastName;

            worksheet.Cell(row, 6).Value =
                order.Email;

            worksheet.Cell(row, 7).Value =
                order.AddressLine1;

            worksheet.Cell(row, 8).Value =
                order.City;

            worksheet.Cell(row, 9).Value =
                order.PostCode;

            worksheet.Cell(row, 10).Value =
                order.Country;

            worksheet.Cell(row, 11).Value =
                order.ProductCode;

            worksheet.Cell(row, 12).Value =
                order.ProductName;

            worksheet.Cell(row, 13).Value =
                order.Quantity;

            worksheet.Cell(row, 14).Value =
                (double)order.UnitPrice;

            worksheet.Cell(row, 15).Value =
                (double)(order.Quantity * order.UnitPrice);

            row++;
        }

        worksheet.Columns().AdjustToContents();

        worksheet.SheetView.FreezeRows(1);

        var stream = new MemoryStream();

        workbook.SaveAs(stream);

        stream.Position = 0;

        return stream;
    }
}