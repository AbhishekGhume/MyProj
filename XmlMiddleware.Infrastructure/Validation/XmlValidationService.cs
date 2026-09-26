using System.Globalization;
using System.Net.Mail;
using System.Xml.Linq;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Models;
using System.Text.RegularExpressions;

namespace XmlMiddleware.Infrastructure.Validation;

public class XmlValidationService : IXmlValidationService
{
    private static readonly Regex OrderIdPattern =
    new(
        @"^ORD\d+$",
        RegexOptions.Compiled);

    private static readonly Regex CustomerIdPattern =
        new(
            @"^CUST\d+$",
            RegexOptions.Compiled);

    private static readonly Regex ProductCodePattern =
        new(
            @"^P\d+$",
            RegexOptions.Compiled);

    public async Task<XmlValidationResult> ValidateAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var result = new XmlValidationResult();

        try
        {
            stream.Position = 0;

            var document = await XDocument.LoadAsync(
                stream,
                LoadOptions.None,
                cancellationToken);

            ValidateOrders(document, result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Malformed XML is a validation error, but a cancelled request is not.
            result.Errors.Add(ex.Message);
        }

        result.IsValid = !result.Errors.Any();

        return result;
    }

    private static void ValidateOrders(
        XDocument document,
        XmlValidationResult result)
    {
        var root = document.Root;

        if (root == null || root.Name != "Orders")
        {
            result.Errors.Add(
                "Root element must be Orders.");

            return;
        }

        var orders = root.Elements("Order").ToList();

        result.RecordCount = orders.Count;

        if (!orders.Any())
        {
            result.Errors.Add(
                "At least one Order is required.");

            return;
        }

        ValidateDuplicateOrderIds(
            orders,
            result);

        for (int i = 0; i < orders.Count; i++)
        {
            ValidateOrder(
                orders[i],
                i + 1,
                result);
        }

        var duplicateIds = orders
            .Select(order => order.Element("OrderId")?.Value.Trim())
            .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
            .GroupBy(orderId => orderId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < orders.Count; index++)
        {
            var recordNumber = index + 1;
            var orderId = orders[index].Element("OrderId")?.Value.Trim();
            var orderErrors = result.Errors
                .Where(error =>
                    error.StartsWith($"Record {recordNumber}:", StringComparison.Ordinal) ||   // Ordinal is for exact character to character comparison 
                    error.StartsWith($"Record {recordNumber},", StringComparison.Ordinal))
                .ToList();

            if (orderId is not null && duplicateIds.Contains(orderId))
            {
                orderErrors.Add($"Record {recordNumber}: OrderId '{orderId}' is duplicated in the XML file.");
            }

            if (orderErrors.Count == 0)
            {
                result.ValidRecordNumbers.Add(recordNumber);
            }
            else
            {
                result.OrderErrors[recordNumber] = orderErrors;
            }
        }
    }

    private static void ValidateOrder(
        XElement order,
        int recordNumber,
        XmlValidationResult result)
    {
        ValidateRequired(
            order,
            "OrderId",
            recordNumber,
            result);

        ValidateOrderId(
            order,
            recordNumber,
            result);

        ValidateRequired(
            order,
            "OrderDate",
            recordNumber,
            result);

        ValidateOrderDate(
            order,
            recordNumber,
            result);

        ValidateCustomer(
            order,
            recordNumber,
            result);

        ValidateAddress(
            order,
            recordNumber,
            result);

        ValidateOrderDetails(
            order,
            recordNumber,
            result);
    }

    private static void ValidateCustomer(
        XElement order,
        int recordNumber,
        XmlValidationResult result)
    {
        var customer = order.Element("Customer");

        if (customer == null)
        {
            result.Errors.Add(
                $"Record {recordNumber}: Customer section is required.");

            return;
        }

        ValidateRequired(
            customer,
            "CustomerId",
            recordNumber,
            result);

        ValidateCustomerId(
            customer,
            recordNumber,
            result);

        ValidateRequired(
            customer,
            "FirstName",
            recordNumber,
            result);

        ValidateRequired(
            customer,
            "LastName",
            recordNumber,
            result);

        ValidateRequired(
            customer,
            "Email",
            recordNumber,
            result);

        var email = customer.Element("Email");

        if (email != null &&
            !string.IsNullOrWhiteSpace(email.Value))
        {
            ValidateEmail(
                email.Value,
                recordNumber,
                result);
        }
    }

    private static void ValidateAddress(
        XElement order,
        int recordNumber,
        XmlValidationResult result)
    {
        var address = order.Element("Address");

        if (address == null)
        {
            result.Errors.Add(
                $"Record {recordNumber}: Address section is required.");

            return;
        }

        ValidateRequired(
            address,
            "AddressLine1",
            recordNumber,
            result);

        ValidateRequired(
            address,
            "City",
            recordNumber,
            result);

        ValidateRequired(
            address,
            "PostCode",
            recordNumber,
            result);

        ValidateRequired(
            address,
            "Country",
            recordNumber,
            result);
    }

    private static void ValidateOrderDetails(
        XElement order,
        int recordNumber,
        XmlValidationResult result)
    {
        var detailsList = order.Elements("OrderDetails").ToList();

        if (detailsList.Count == 0)
        {
            result.Errors.Add(
                $"Record {recordNumber}: OrderDetails section is required.");

            return;
        }

        // An order can carry several OrderDetails lines - validate every one of them,
        // not just the first.
        for (var i = 0; i < detailsList.Count; i++)
        {
            var details =
                detailsList[i];

            var lineNumber =
                i + 1;

            ValidateRequired(
                details,
                "ProductCode",
                recordNumber,
                result);

            ValidateProductCode(
                details,
                recordNumber,
                lineNumber,
                result);

            ValidateRequired(
                details,
                "ProductName",
                recordNumber,
                result);

            ValidateQuantity(
                details,
                recordNumber,
                result);

            ValidateUnitPrice(
                details,
                recordNumber,
                result);
        }
    }

    private static void ValidateRequired(
        XElement parent,
        string elementName,
        int recordNumber,
        XmlValidationResult result)
    {
        var element = parent.Element(elementName);

        if (element == null)
        {
            result.Errors.Add(
                $"Record {recordNumber}: {elementName} is required.");

            return;
        }

        if (string.IsNullOrWhiteSpace(element.Value))
        {
            result.Errors.Add(
                $"Record {recordNumber}: {elementName} cannot be empty.");
        }
    }

    private static void ValidateOrderId(
    XElement order,
    int recordNumber,
    XmlValidationResult result)
    {
        var orderId =
            order.Element("OrderId");

        if (orderId == null ||
            string.IsNullOrWhiteSpace(orderId.Value))
        {
            return;
        }

        var value =
            orderId.Value.Trim();

        if (!OrderIdPattern.IsMatch(value))
        {
            result.Errors.Add(
                $"Record {recordNumber}: OrderId '{value}' is invalid. " +
                "OrderId must start with 'ORD' followed by numbers only.");
        }
    }

    private static void ValidateCustomerId(
    XElement customer,
    int recordNumber,
    XmlValidationResult result)
    {
        var customerId =
            customer.Element("CustomerId");

        if (customerId == null ||
            string.IsNullOrWhiteSpace(customerId.Value))
        {
            return;
        }

        var value =
            customerId.Value.Trim();

        if (!CustomerIdPattern.IsMatch(value))
        {
            result.Errors.Add(
                $"Record {recordNumber}: CustomerId '{value}' is invalid. " +
                "CustomerId must start with 'CUST' followed by numbers only.");
        }
    }

    private static void ValidateProductCode(
    XElement details,
    int recordNumber,
    int lineNumber,
    XmlValidationResult result)
    {
        var productCode =
            details.Element("ProductCode");

        if (productCode == null ||
            string.IsNullOrWhiteSpace(productCode.Value))
        {
            return;
        }

        var value =
            productCode.Value.Trim();

        if (!ProductCodePattern.IsMatch(value))
        {
            result.Errors.Add(
                $"Record {recordNumber}, OrderDetails line {lineNumber}: " +
                $"ProductCode '{value}' is invalid. " +
                "ProductCode must start with 'P' followed by numbers only.");
        }
    }

    private static void ValidateDuplicateOrderIds(
    List<XElement> orders,
    XmlValidationResult result)
    {
        var duplicateOrderIds =
            orders
                .Select(x =>
                    x.Element("OrderId")?.Value.Trim())
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .GroupBy(
                    x => x!,
                    StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key);

        foreach (var orderId in duplicateOrderIds)
        {
            result.Errors.Add(
                $"Duplicate OrderId '{orderId}' found within the XML file.");
        }
    }

    private static void ValidateQuantity(
        XElement details,
        int recordNumber,
        XmlValidationResult result)
    {
        var quantity = details.Element("Quantity");

        if (quantity == null)
        {
            result.Errors.Add(
                $"Record {recordNumber}: Quantity is required.");

            return;
        }

        if (!int.TryParse(quantity.Value, out var value))
        {
            result.Errors.Add(
                $"Record {recordNumber}: Quantity must be numeric.");

            return;
        }

        if (value <= 0)
        {
            result.Errors.Add(
                $"Record {recordNumber}: Quantity must be greater than zero.");
        }
    }

    private static void ValidateUnitPrice(
        XElement details,
        int recordNumber,
        XmlValidationResult result)
    {
        var unitPrice = details.Element("UnitPrice");

        if (unitPrice == null)
        {
            result.Errors.Add(
                $"Record {recordNumber}: UnitPrice is required.");

            return;
        }

        if (!decimal.TryParse(unitPrice.Value, out var value))
        {
            result.Errors.Add(
                $"Record {recordNumber}: UnitPrice must be numeric.");

            return;
        }

        if (value <= 0)
        {
            result.Errors.Add(
                $"Record {recordNumber}: UnitPrice must be greater than zero.");
        }
    }

    private static void ValidateOrderDate(
        XElement order,
        int recordNumber,
        XmlValidationResult result)
    {
        var orderDate = order.Element("OrderDate");

        if (orderDate == null ||
            string.IsNullOrWhiteSpace(orderDate.Value))
        {
            return;
        }

        var isValid = DateTime.TryParseExact(
            orderDate.Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

        if (!isValid)
        {
            result.Errors.Add(
                $"Record {recordNumber}: OrderDate must be in yyyy-MM-dd format.");
        }
    }

    private static void ValidateEmail(
        string email,
        int recordNumber,
        XmlValidationResult result)
    {
        try
        {
            var mailAddress = new MailAddress(email);

            if (mailAddress.Address != email)
            {
                result.Errors.Add(
                    $"Record {recordNumber}: Email format is invalid.");
            }
        }
        catch
        {
            result.Errors.Add(
                $"Record {recordNumber}: Email format is invalid.");
        }
    }
}