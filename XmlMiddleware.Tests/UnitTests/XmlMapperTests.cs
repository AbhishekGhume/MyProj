using System.Text;
using FluentAssertions;
using XmlMiddleware.Infrastructure.Mapping;

namespace XmlMiddleware.Tests.UnitTests;

public class XmlMapperTests
{
    private readonly XmlMapperService _mapper;

    public XmlMapperTests()
    {
        _mapper = new XmlMapperService();
    }

    [Fact]
    public async Task MapAsync_ShouldMapSingleOrder()
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
                    john.smith@test.com
                </Customer>

                <Address>
                    <AddressLine1>Main Street</AddressLine1>
                    <City>London</City>
                    <PostCode>SW1A1AA</PostCode>
                    <Country>UK</Country>
                </Address>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>2</Quantity>
                    <UnitPrice>750.00</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result.Should().HaveCount(1);

        result[0].OrderId.Should().Be("ORD10001");
        result[0].CustomerId.Should().Be("CUST001");
        result[0].ProductCode.Should().Be("P100");
    }

    [Fact]
    public async Task MapAsync_ShouldMapMultipleOrders()
    {
        // Arrange
        var xml =
        """
        <Orders>

            <Order>
                <OrderId>ORD1</OrderId>
                <OrderDate>2026-09-01</OrderDate>
            </Order>

            <Order>
                <OrderId>ORD2</OrderId>
                <OrderDate>2026-09-02</OrderDate>
            </Order>

        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task MapAsync_ShouldMapCustomerInformation()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>

                <Customer>
                    <CustomerId>C001</CustomerId>
                    <FirstName>John</FirstName>
                    <LastName>Smith</LastName>
                    john@test.com
                </Customer>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result[0].CustomerId.Should().Be("C001");
        result[0].FirstName.Should().Be("John");
        result[0].LastName.Should().Be("Smith");
        result[0].Email.Should().Be("john@test.com");
    }

    [Fact]
    public async Task MapAsync_ShouldMapAddressInformation()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>

                <Address>
                    <AddressLine1>Main Street</AddressLine1>
                    <City>London</City>
                    <PostCode>ABC123</PostCode>
                    <Country>UK</Country>
                </Address>

            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result[0].AddressLine1.Should().Be("Main Street");
        result[0].City.Should().Be("London");
        result[0].PostCode.Should().Be("ABC123");
        result[0].Country.Should().Be("UK");
    }

    [Fact]
    public async Task MapAsync_ShouldMapOrderDetails()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>

                <OrderDetails>
                    <ProductCode>P100</ProductCode>
                    <ProductName>Laptop</ProductName>
                    <Quantity>3</Quantity>
                    <UnitPrice>250.50</UnitPrice>
                </OrderDetails>

            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result[0].ProductCode.Should().Be("P100");
        result[0].ProductName.Should().Be("Laptop");
        result[0].Quantity.Should().Be(3);
        result[0].UnitPrice.Should().Be(250.50m);
    }

    [Fact]
    public async Task MapAsync_ShouldReturnEmptyList_WhenNoOrdersExist()
    {
        // Arrange
        var xml =
        """
        <Orders>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MapAsync_ShouldReturnEmptyStrings_WhenOptionalNodesMissing()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>
                <OrderId>ORD1</OrderId>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result[0].CustomerId.Should().BeEmpty();
        result[0].FirstName.Should().BeEmpty();
        result[0].City.Should().BeEmpty();
        result[0].ProductCode.Should().BeEmpty();
    }

    [Fact]
    public async Task MapAsync_ShouldReturnZero_WhenQuantityInvalid()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>
                <OrderDetails>
                    <Quantity>ABC</Quantity>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result[0].Quantity.Should().Be(0);
    }

    [Fact]
    public async Task MapAsync_ShouldReturnZero_WhenUnitPriceInvalid()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>
                <OrderDetails>
                    <UnitPrice>ABC</UnitPrice>
                </OrderDetails>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result[0].UnitPrice.Should().Be(0);
    }

    [Fact]
    public async Task MapAsync_ShouldReturnMinDate_WhenOrderDateInvalid()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>
                <OrderDate>INVALID-DATE</OrderDate>
            </Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        var result =
            await _mapper.MapAsync(stream);

        // Assert
        result[0].OrderDate.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public async Task MapAsync_ShouldThrowException_WhenXmlMalformed()
    {
        // Arrange
        var xml =
        """
        <Orders>
            <Order>
        </Orders>
        """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(xml));

        // Act
        Func<Task> action =
            async () => await _mapper.MapAsync(stream);

        // Assert
        await action.Should()
            .ThrowAsync<Exception>();
    }
}
