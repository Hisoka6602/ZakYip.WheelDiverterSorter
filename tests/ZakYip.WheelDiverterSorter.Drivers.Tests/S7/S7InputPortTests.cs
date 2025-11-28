using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.S7;

public class S7InputPortTests
{
    private readonly Mock<ILogger<S7InputPort>> _mockLogger;
    private readonly Mock<S7Connection> _mockConnection;
    private const int DbNumber = 1;

    public S7InputPortTests()
    {
        _mockLogger = new Mock<ILogger<S7InputPort>>();
        
        var mockConnectionLogger = new Mock<ILogger<S7Connection>>();
        var options = new S7Options
        {
            IpAddress = "192.168.0.100",
            Rack = 0,
            Slot = 1
        };
        _mockConnection = new Mock<S7Connection>(mockConnectionLogger.Object, options);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var inputPort = new S7InputPort(_mockLogger.Object, _mockConnection.Object, DbNumber);

        // Assert
        Assert.NotNull(inputPort);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7InputPort(null!, _mockConnection.Object, DbNumber));
    }

    [Fact]
    public void Constructor_WithNullConnection_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7InputPort(_mockLogger.Object, null!, DbNumber));
    }

    [Fact]
    public async Task ReadAsync_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        _mockConnection.Setup(c => c.EnsureConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var inputPort = new S7InputPort(_mockLogger.Object, _mockConnection.Object, DbNumber);

        // Act
        var result = await inputPort.ReadAsync(0);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReadAsync_WhenPlcIsNull_ReturnsFalse()
    {
        // Arrange
        _mockConnection.Setup(c => c.EnsureConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockConnection.Setup(c => c.GetPlc()).Returns((global::S7.Net.Plc?)null);
        
        var inputPort = new S7InputPort(_mockLogger.Object, _mockConnection.Object, DbNumber);

        // Act
        var result = await inputPort.ReadAsync(0);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReadBatchAsync_WhenNotConnected_ReturnsAllFalse()
    {
        // Arrange
        _mockConnection.Setup(c => c.EnsureConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var inputPort = new S7InputPort(_mockLogger.Object, _mockConnection.Object, DbNumber);

        // Act
        var results = await inputPort.ReadBatchAsync(0, 5);

        // Assert
        Assert.Equal(5, results.Length);
        Assert.All(results, r => Assert.False(r));
    }
}
