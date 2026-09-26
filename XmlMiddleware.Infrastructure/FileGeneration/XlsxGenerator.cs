using ClosedXML.Excel;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public class XlsxGenerator : IGenerator
{
    public MemoryStream Generate(
        List<CanonicalOrderModel> orders)
    {
        using var workbook =
            new XLWorkbook();

        foreach (var order in orders)
        {
            var sheetName =
                CreateSafeSheetName(
                    order.OrderId,
                    workbook);

            var worksheet =
                workbook.Worksheets.Add(
                    sheetName);

            CreateOrderWorksheet(
                worksheet,
                order);
        }

        var stream =
            new MemoryStream();

        workbook.SaveAs(stream);

        stream.Position = 0;

        return stream;
    }

    private static void CreateOrderWorksheet(
        IXLWorksheet worksheet,
        CanonicalOrderModel order)
    {
        // ==================================================
        // TITLE
        // ==================================================

        worksheet.Cell(1, 1).Value =
            "ORDER DETAILS";

        worksheet.Range(1, 1, 1, 10)
            .Merge();

        worksheet.Cell(1, 1)
            .Style.Font.Bold = true;

        worksheet.Cell(1, 1)
            .Style.Font.FontSize = 16;

        worksheet.Cell(1, 1)
            .Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;

        worksheet.Cell(1, 1)
            .Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;

        worksheet.Cell(1, 1)
            .Style.Fill.BackgroundColor =
            XLColor.LightBlue;

        worksheet.Row(1).Height = 25;

        // ==================================================
        // ORDER INFORMATION
        // ==================================================

        worksheet.Cell(3, 1).Value =
            "ORDER INFORMATION";

        worksheet.Range(3, 1, 3, 5)
            .Merge();

        worksheet.Cell(3, 1)
            .Style.Font.Bold = true;

        worksheet.Cell(3, 1)
            .Style.Fill.BackgroundColor =
            XLColor.LightGray;

        worksheet.Cell(4, 1).Value =
            "Order Id";

        worksheet.Cell(4, 2).Value =
            order.OrderId;

        worksheet.Cell(4, 4).Value =
            "Order Date";

        worksheet.Cell(4, 5).Value =
            order.OrderDate;

        worksheet.Cell(4, 5)
            .Style.DateFormat.Format =
            "dd-MM-yyyy";

        worksheet.Cell(4, 1)
            .Style.Font.Bold = true;

        worksheet.Cell(4, 4)
            .Style.Font.Bold = true;

        // ==================================================
        // CUSTOMER INFORMATION
        // ==================================================

        worksheet.Cell(6, 1).Value =
            "CUSTOMER INFORMATION";

        worksheet.Range(6, 1, 6, 4)
            .Merge();

        worksheet.Cell(6, 1)
            .Style.Font.Bold = true;

        worksheet.Cell(6, 1)
            .Style.Fill.BackgroundColor =
            XLColor.LightGray;

        worksheet.Cell(7, 1).Value =
            "Customer Id";

        worksheet.Cell(7, 2).Value =
            order.CustomerId;

        worksheet.Cell(8, 1).Value =
            "First Name";

        worksheet.Cell(8, 2).Value =
            order.FirstName;

        worksheet.Cell(9, 1).Value =
            "Last Name";

        worksheet.Cell(9, 2).Value =
            order.LastName;

        worksheet.Cell(10, 1).Value =
            "Email";

        worksheet.Cell(10, 2).Value =
            order.Email;

        worksheet.Cell(7, 1)
            .Style.Font.Bold = true;

        worksheet.Cell(8, 1)
            .Style.Font.Bold = true;

        worksheet.Cell(9, 1)
            .Style.Font.Bold = true;

        worksheet.Cell(10, 1)
            .Style.Font.Bold = true;

        // ==================================================
        // SHIPPING ADDRESS
        // ==================================================

        worksheet.Cell(6, 6).Value =
            "SHIPPING ADDRESS";

        worksheet.Range(6, 6, 6, 10)
            .Merge();

        worksheet.Cell(6, 6)
            .Style.Font.Bold = true;

        worksheet.Cell(6, 6)
            .Style.Fill.BackgroundColor =
            XLColor.LightGray;

        worksheet.Cell(7, 6).Value =
            "Address";

        worksheet.Cell(7, 7).Value =
            order.AddressLine1;

        worksheet.Cell(8, 6).Value =
            "City";

        worksheet.Cell(8, 7).Value =
            order.City;

        worksheet.Cell(9, 6).Value =
            "Post Code";

        worksheet.Cell(9, 7).Value =
            order.PostCode;

        worksheet.Cell(10, 6).Value =
            "Country";

        worksheet.Cell(10, 7).Value =
            order.Country;

        worksheet.Cell(7, 6)
            .Style.Font.Bold = true;

        worksheet.Cell(8, 6)
            .Style.Font.Bold = true;

        worksheet.Cell(9, 6)
            .Style.Font.Bold = true;

        worksheet.Cell(10, 6)
            .Style.Font.Bold = true;

        // ==================================================
        // ORDER ITEMS
        // ==================================================

        const int sectionRow = 12;
        const int headerRow = 13;

        worksheet.Cell(sectionRow, 1).Value =
            "ORDER ITEMS";

        worksheet.Range(
                sectionRow,
                1,
                sectionRow,
                6)
            .Merge();

        worksheet.Cell(sectionRow, 1)
            .Style.Font.Bold = true;

        worksheet.Cell(sectionRow, 1)
            .Style.Fill.BackgroundColor =
            XLColor.LightGray;

        // ==================================================
        // ORDER ITEM HEADERS
        // ==================================================

        worksheet.Cell(headerRow, 1).Value =
            "#";

        worksheet.Cell(headerRow, 2).Value =
            "Product Code";

        worksheet.Cell(headerRow, 3).Value =
            "Product Name";

        worksheet.Cell(headerRow, 4).Value =
            "Quantity";

        worksheet.Cell(headerRow, 5).Value =
            "Unit Price";

        worksheet.Cell(headerRow, 6).Value =
            "Line Total";

        var header =
            worksheet.Range(
                headerRow,
                1,
                headerRow,
                6);

        header.Style.Font.Bold = true;

        header.Style.Fill.BackgroundColor =
            XLColor.FromHtml("#343434");

        header.Style.Font.FontColor =
            XLColor.White;

        header.Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;

        header.Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;

        // ==================================================
        // ORDER ITEM DATA
        // ==================================================

        var row =
            headerRow + 1;

        var lineNumber = 1;

        foreach (var item in order.Items)
        {
            worksheet.Cell(row, 1).Value =
                lineNumber++;

            worksheet.Cell(row, 2).Value =
                item.ProductCode;

            worksheet.Cell(row, 3).Value =
                item.ProductName;

            worksheet.Cell(row, 4).Value =
                item.Quantity;

            worksheet.Cell(row, 5).Value =
                item.UnitPrice;

            // Calculate Line Total using an Excel formula.
            worksheet.Cell(row, 6).FormulaA1 =
                $"D{row}*E{row}";

            // Numerical alignment.
            worksheet.Cell(row, 1)
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Cell(row, 4)
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Cell(row, 5)
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Right;

            worksheet.Cell(row, 6)
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Right;

            row++;
        }

        // ==================================================
        // ORDER TOTAL
        // ==================================================

        var totalRow =
            row + 1;

        worksheet.Cell(totalRow, 5).Value =
            "ORDER TOTAL";

        worksheet.Cell(totalRow, 5)
            .Style.Font.Bold = true;

        if (order.Items.Count > 0)
        {
            worksheet.Cell(totalRow, 6)
                .FormulaA1 =
                $"SUM(F{headerRow + 1}:F{row - 1})";
        }
        else
        {
            worksheet.Cell(totalRow, 6).Value =
                0;
        }

        worksheet.Cell(totalRow, 6)
            .Style.Font.Bold = true;

        worksheet.Cell(totalRow, 6)
            .Style.Font.FontSize = 12;

        worksheet.Cell(totalRow, 6)
            .Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Right;

        // ==================================================
        // CURRENCY / DECIMAL FORMATTING
        // ==================================================

        if (order.Items.Count > 0)
        {
            worksheet.Range(
                    headerRow + 1,
                    5,
                    row - 1,
                    6)
                .Style.NumberFormat.Format =
                "#,##0.00";
        }

        worksheet.Cell(totalRow, 6)
            .Style.NumberFormat.Format =
            "#,##0.00";

        // ==================================================
        // BORDERS
        // ==================================================

        if (order.Items.Count > 0)
        {
            var itemRange =
                worksheet.Range(
                    headerRow,
                    1,
                    row - 1,
                    6);

            itemRange.Style.Border
                .BottomBorder =
                XLBorderStyleValues.Thin;

            itemRange.Style.Border
                .BottomBorderColor =
                XLColor.LightGray;
        }

        // ==================================================
        // GENERAL FORMATTING
        // ==================================================

        worksheet.Columns()
            .AdjustToContents();

        // Maintain minimum useful widths.
        worksheet.Column(1).Width =
            Math.Max(
                worksheet.Column(1).Width,
                12);

        worksheet.Column(2).Width =
            Math.Max(
                worksheet.Column(2).Width,
                18);

        worksheet.Column(3).Width =
            Math.Max(
                worksheet.Column(3).Width,
                25);

        worksheet.Column(4).Width =
            Math.Max(
                worksheet.Column(4).Width,
                12);

        worksheet.Column(5).Width =
            Math.Max(
                worksheet.Column(5).Width,
                15);

        worksheet.Column(6).Width =
            Math.Max(
                worksheet.Column(6).Width,
                16);

        worksheet.Column(7).Width =
            Math.Max(
                worksheet.Column(7).Width,
                25);

        // Make email/address readable.
        worksheet.Cell(10, 2)
            .Style.Alignment.WrapText = true;

        worksheet.Cell(7, 7)
            .Style.Alignment.WrapText = true;

        // ==================================================
        // FREEZE
        // ==================================================

        worksheet.SheetView
            .FreezeRows(headerRow);

        // ==================================================
        // PAGE SETTINGS
        // ==================================================

        worksheet.PageSetup
            .PageOrientation =
            XLPageOrientation.Landscape;

        worksheet.PageSetup
            .FitToPages(1, 1);

        worksheet.PageSetup
            .Margins.Top = 0.5;

        worksheet.PageSetup
            .Margins.Bottom = 0.5;

        worksheet.PageSetup
            .Margins.Left = 0.5;

        worksheet.PageSetup
            .Margins.Right = 0.5;
    }

    private static string CreateSafeSheetName(
        string orderId,
        XLWorkbook workbook)
    {
        var invalidCharacters =
            new[]
            {
                ':',
                '\\',
                '/',
                '?',
                '*',
                '[',
                ']'
            };

        var safeName =
            orderId;

        foreach (var character in invalidCharacters)
        {
            safeName =
                safeName.Replace(
                    character,
                    '_');
        }

        // Excel limits worksheet names to 31 characters.
        if (safeName.Length > 31)
        {
            safeName =
                safeName[..31];
        }

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName =
                "Order";
        }

        var originalName =
            safeName;

        var number = 1;

        while (workbook.Worksheets
            .Any(x =>
                x.Name.Equals(
                    safeName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            var suffix =
                $"_{number++}";

            var maximumBaseLength =
                31 - suffix.Length;

            safeName =
                originalName[
                    ..Math.Min(
                        originalName.Length,
                        maximumBaseLength)]
                + suffix;
        }

        return safeName;
    }
}