using System.Globalization;
using System.Net.Mail;
using System.Xml.Linq;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.Validation;

public class XmlValidationService : IXmlValidationService
{
    public async Task<XmlValidationResult> ValidateAsync(Stream stream)
    {
        var result = new XmlValidationResult();

        try
        {
            stream.Position = 0;

            var document = await XDocument.LoadAsync(
                stream,
                LoadOptions.None,
                CancellationToken.None);

            ValidateOrders(document, result);
        }
        catch (Exception ex)
        {
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

        for (int i = 0; i < orders.Count; i++)
        {
            ValidateOrder(
                orders[i],
                i + 1,
                result);
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
        var details = order.Element("OrderDetails");

        if (details == null)
        {
            result.Errors.Add(
                $"Record {recordNumber}: OrderDetails section is required.");

            return;
        }

        ValidateRequired(
            details,
            "ProductCode",
            recordNumber,
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