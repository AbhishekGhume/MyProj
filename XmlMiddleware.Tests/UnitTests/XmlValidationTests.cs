using System.Text;
using FluentAssertions;
using XmlMiddleware.Infrastructure.Validation;

namespace XmlMiddleware.Tests.UnitTests;

public class XmlValidationTests
{
    private readonly XmlValidationService _service;

    public XmlValidationTests()
    {
        _service = new XmlValidationService();
    }

    [Fact]
    public async Task ValidateAsync_ShouldPass_WhenXmlIsValid()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD10001</OrderId>
                <OrderDate>2026-09-01</OrderDate>

                <Customer>
                    <CustomerId>CUST001</CustomerId>
                    <FirstName>John</FirstName>
                    <LastName>Smith</LastName>
                    john.smith@example.com
                </Customer>

                <Address>
                    <AddressLine1>Main Road</AddressLine1>
                    <City>London</City>
                    <PostCode>SW1A1AA</PostCode>
                    <Country>UK</Country>
                </Address>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>2</Quantity>
                    <UnitPrice>100</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _service.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenRootElementIsInvalid()
    {
        var xml =
        """
        <InvalidRoot>
        </InvalidRoot>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.IsValid.Should().BeFalse();

        result.Errors.Should()
            .Contain("Root element must be Orders.");
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenNoOrdersExist()
    {
        var xml =
        """
        <Orders>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.IsValid.Should().BeFalse();

        result.Errors.Should()
            .Contain("At least one Order is required.");
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenOrderIdMissing()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderDate>2026-09-01</OrderDate>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.IsValid.Should().BeFalse();

        result.Errors.Should()
            .Contain(x => x.Contains("OrderId"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenCustomerSectionMissing()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>
                <OrderDate>2026-09-01</OrderDate>

                <Address>
                    <AddressLine1>Main</AddressLine1>
                    <City>London</City>
                    <PostCode>ABC</PostCode>
                    <Country>UK</Country>
                </Address>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>1</Quantity>
                    <UnitPrice>10</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.Errors.Should()
            .Contain(x => x.Contains("Customer section is required"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenAddressMissing()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>
                <OrderDate>2026-09-01</OrderDate>

                <Customer>
                    <CustomerId>C1</CustomerId>
                    <FirstName>John</FirstName>
                    <LastName>Smith</LastName>
                    john@test.com
                </Customer>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>1</Quantity>
                    <UnitPrice>100</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.Errors.Should()
            .Contain(x => x.Contains("Address section is required"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenOrderDetailsMissing()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>
                <OrderDate>2026-09-01</OrderDate>

                <Customer>
                    <CustomerId>C1</CustomerId>
                    <FirstName>John</FirstName>
                    <LastName>Smith</LastName>
                    john@test.com
                </Customer>

                <Address>
                    <AddressLine1>Main</AddressLine1>
                    <City>London</City>
                    <PostCode>ABC</PostCode>
                    <Country>UK</Country>
                </Address>

            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.Errors.Should()
            .Contain(x => x.Contains("OrderDetails section is required"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenEmailInvalid()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>
                <OrderDate>2026-09-01</OrderDate>

                <Customer>
                    <CustomerId>C1</CustomerId>
                    <FirstName>John</FirstName>
                    <LastName>Smith</LastName>
                    invalid-email
                </Customer>

                <Address>
                    <AddressLine1>Main</AddressLine1>
                    <City>London</City>
                    <PostCode>ABC</PostCode>
                    <Country>UK</Country>
                </Address>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>1</Quantity>
                    <UnitPrice>100</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.Errors.Should()
            .Contain(x => x.Contains("Email format is invalid"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenOrderDateFormatInvalid()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>
                <OrderDate>01-09-2026</OrderDate>

                <Customer>
                    <CustomerId>C1</CustomerId>
                    <FirstName>John</FirstName>
                    <LastName>Smith</LastName>
                    john@test.com
                </Customer>

                <Address>
                    <AddressLine1>Main</AddressLine1>
                    <City>London</City>
                    <PostCode>ABC</PostCode>
                    <Country>UK</Country>
                </Address>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>1</Quantity>
                    <UnitPrice>100</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.Errors.Should()
            .Contain(x => x.Contains("yyyy-MM-dd"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenQuantityIsZero()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>
                <OrderDate>2026-09-01</OrderDate>

                <Customer>
                    <CustomerId>C1</CustomerId>
                    <FirstName>John</FirstName>
                    <LastName>Smith</LastName>
                    john@test.com
                </Customer>

                <Address>
                    <AddressLine1>Main</AddressLine1>
                    <City>London</City>
                    <PostCode>ABC</PostCode>
                    <Country>UK</Country>
                </Address>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>0</Quantity>
                    <UnitPrice>100</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.Errors.Should()
            .Contain(x => x.Contains("Quantity must be greater than zero"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenUnitPriceNegative()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>
                <OrderDate>2026-09-01</OrderDate>

                <Customer>
                    <CustomerId>C1</CustomerId>
                    <FirstName>John</FirstName>
                    <LastName>Smith</LastName>
                    john@test.com
                </Customer>

                <Address>
                    <AddressLine1>Main</AddressLine1>
                    <City>London</City>
                    <PostCode>ABC</PostCode>
                    <Country>UK</Country>
                </Address>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>1</Quantity>
                    <UnitPrice>-100</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.Errors.Should()
            .Contain(x => x.Contains("UnitPrice must be greater than zero"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenXmlMalformed()
    {
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>1</OrderId>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        var result =
            await _service.ValidateAsync(stream);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}