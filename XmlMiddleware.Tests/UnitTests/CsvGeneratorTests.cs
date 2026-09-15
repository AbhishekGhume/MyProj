using System.Text;
using FluentAssertions;
using XmlMiddleware.Application.Models;
using XmlMiddleware.Infrastructure.FileGeneration;

namespace XmlMiddleware.Tests.UnitTests;

public class CsvGeneratorTests
{
    private readonly CsvGenerator _generator;

    public CsvGeneratorTests()
    {
        _generator = new CsvGenerator();
    }

    [Fact]
    public void Generate_ShouldReturnMemoryStream()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MemoryStream>();
    }

    [Fact]
    public void Generate_ShouldNotReturnEmptyStream()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        // Assert
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_ShouldContainCsvHeader()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain(
            "OrderId,OrderDate,CustomerId,FirstName,LastName,Email,AddressLine1,City,PostCode,Country,ProductCode,ProductName,Quantity,UnitPrice");
    }

    [Fact]
    public void Generate_ShouldContainOrderId()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain("ORD10001");
    }

    [Fact]
    public void Generate_ShouldContainCustomerInformation()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain("CUST001");
        content.Should().Contain("John");
        content.Should().Contain("Smith");
        content.Should().Contain("john.smith@example.com");
    }

    [Fact]
    public void Generate_ShouldContainAddressInformation()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain("10 Main Street");
        content.Should().Contain("London");
        content.Should().Contain("SW1A 1AA");
        content.Should().Contain("UK");
    }

    [Fact]
    public void Generate_ShouldContainProductInformation()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain("P100");
        content.Should().Contain("Laptop");
    }

    [Fact]
    public void Generate_ShouldContainQuantity()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain(",2,");
    }

    [Fact]
    public void Generate_ShouldContainFormattedUnitPrice()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain("750.00");
    }

    [Fact]
    public void Generate_ShouldContainFormattedOrderDate()
    {
        // Arrange
        var orders = CreateOrders();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain("2026-09-01");
    }

    [Fact]
    public void Generate_ShouldGenerateMultipleRows_WhenMultipleOrdersExist()
    {
        // Arrange
        var orders =
        new List<CanonicalOrderModel>
        {
            CreateOrder("ORD10001"),
            CreateOrder("ORD10002")
        };

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain("ORD10001");
        content.Should().Contain("ORD10002");
    }

    [Fact]
    public void Generate_ShouldGenerateOnlyHeader_WhenOrderListIsEmpty()
    {
        // Arrange
        var orders =
            new List<CanonicalOrderModel>();

        // Act
        var result = _generator.Generate(orders);

        var content =
            Encoding.UTF8.GetString(result.ToArray());

        // Assert
        content.Should().Contain("OrderId,OrderDate");
    }

    private static List<CanonicalOrderModel> CreateOrders()
    {
        return
        [
            CreateOrder("ORD10001")
        ];
    }

    private static CanonicalOrderModel CreateOrder(
        string orderId)
    {
        return new CanonicalOrderModel
        {
            OrderId = orderId,
            OrderDate = new DateTime(2026, 09, 01),

            CustomerId = "CUST001",
            FirstName = "John",
            LastName = "Smith",
            Email = "john.smith@example.com",

            AddressLine1 = "10 Main Street",
            City = "London",
            PostCode = "SW1A 1AA",
            Country = "UK",

            ProductCode = "P100",
            ProductName = "Laptop",

            Quantity = 2,
            UnitPrice = 750.00m
        };
    }
}
