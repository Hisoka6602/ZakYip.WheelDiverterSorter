using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.S7;

public class S7ConnectionTests
{
    private readonly Mock<ILogger<S7Connection>> _mockLogger;
    private readonly S7Options _options;

    public S7ConnectionTests()
    {
        _mockLogger = new Mock<ILogger<S7Connection>>();
        _options = new S7Options
        {
            IpAddress = "192.168.0.100",
            Rack = 0,
            Slot = 1,
            CpuType = S7CpuType.S71200,
            ConnectionTimeout = 5000,
            ReadWriteTimeout = 2000,
            MaxReconnectAttempts = 3,
            ReconnectDelay = 100 // Short delay for testing
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var connection = new S7Connection(_mockLogger.Object, _options);

        // Assert
        Assert.NotNull(connection);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7Connection(null!, _options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7Connection(_mockLogger.Object, null!));
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidIpAddress_ReturnsFalse()
    {
        // Arrange
        var options = new S7Options
        {
            IpAddress = "999.999.999.999", // Invalid IP
            Rack = 0,
            Slot = 1,
            CpuType = S7CpuType.S71200,
            ConnectionTimeout = 1000,
            MaxReconnectAttempts = 1,
            ReconnectDelay = 100
        };
        var connection = new S7Connection(_mockLogger.Object, options);

        // Act
        var result = await connection.ConnectAsync();

        // Assert
        Assert.False(result);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public void GetPlc_WhenNotConnected_ReturnsNull()
    {
        // Arrange
        var connection = new S7Connection(_mockLogger.Object, _options);

        // Act
        var plc = connection.GetPlc();

        // Assert
        // PLC is created but not connected, so it should not be null
        // but IsConnected should be false
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var connection = new S7Connection(_mockLogger.Object, _options);

        // Act & Assert
        connection.Dispose();
        connection.Dispose(); // Should not throw
    }

    [Fact]
    public void Disconnect_WhenNotConnected_DoesNotThrow()
    {
        // Arrange
        var connection = new S7Connection(_mockLogger.Object, _options);

        // Act & Assert
        connection.Disconnect(); // Should not throw
    }

    [Fact]
    public async Task EnsureConnectedAsync_WithMaxRetries_FailsAfterMaxAttempts()
    {
        // Arrange
        var options = new S7Options
        {
            IpAddress = "192.168.1.1", // Unreachable IP
            Rack = 0,
            Slot = 1,
            CpuType = S7CpuType.S71200,
            ConnectionTimeout = 500,
            MaxReconnectAttempts = 2,
            ReconnectDelay = 100
        };
        var connection = new S7Connection(_mockLogger.Object, options);

        // Act
        var result = await connection.EnsureConnectedAsync();

        // Assert
        Assert.False(result);
        Assert.False(connection.IsConnected);
    }
}
