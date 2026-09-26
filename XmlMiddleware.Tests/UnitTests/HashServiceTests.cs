using FluentAssertions;
using System.Text;
using XmlMiddleware.Infrastructure.Hashing;

namespace XmlMiddleware.Tests.UnitTests;

public class HashServiceTests
{
    private readonly HashService _hashService;

    public HashServiceTests()
    {
        _hashService = new HashService();
    }

    [Fact]
    public async Task GenerateHashAsync_ShouldReturnHash_WhenStreamIsValid()
    {
        // Arrange
        var content = "Hello World";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(content));

        // Act
        var hash =
            await _hashService.GenerateHashAsync(stream);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateHashAsync_ShouldReturnSameHash_ForSameContent()
    {
        // Arrange
        const string content = "Same Content";

        using var stream1 =
            new MemoryStream(
                Encoding.UTF8.GetBytes(content));

        using var stream2 =
            new MemoryStream(
                Encoding.UTF8.GetBytes(content));

        // Act
        var hash1 =
            await _hashService.GenerateHashAsync(stream1);

        var hash2 =
            await _hashService.GenerateHashAsync(stream2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public async Task GenerateHashAsync_ShouldReturnDifferentHash_ForDifferentContent()
    {
        // Arrange
        using var stream1 =
            new MemoryStream(
                Encoding.UTF8.GetBytes("File One"));

        using var stream2 =
            new MemoryStream(
                Encoding.UTF8.GetBytes("File Two"));

        // Act
        var hash1 =
            await _hashService.GenerateHashAsync(stream1);

        var hash2 =
            await _hashService.GenerateHashAsync(stream2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public async Task GenerateHashAsync_ShouldReturn64CharacterHexHash()
    {
        // Arrange
        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes("Test Content"));

        // Act
        var hash =
            await _hashService.GenerateHashAsync(stream);

        // Assert
        hash.Length.Should().Be(64);
    }

    [Fact]
    public async Task GenerateHashAsync_ShouldResetStreamPosition_AfterHashGeneration()
    {
        // Arrange
        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes("Reset Position Test"));

        // Act
        await _hashService.GenerateHashAsync(stream);

        // Assert
        stream.Position.Should().Be(0);
    }

    [Fact]
    public async Task GenerateHashAsync_ShouldGenerateHash_ForEmptyStream()
    {
        // Arrange
        using var stream =
            new MemoryStream();

        // Act
        var hash =
            await _hashService.GenerateHashAsync(stream);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64);
    }

    [Fact]
    public async Task GenerateHashAsync_ShouldGenerateConsistentHash_WhenCalledMultipleTimesOnSameStream()
    {
        // Arrange
        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes("Consistent Hash"));

        // Act
        var hash1 =
            await _hashService.GenerateHashAsync(stream);

        var hash2 =
            await _hashService.GenerateHashAsync(stream);

        // Assert
        hash1.Should().Be(hash2);
    }
}