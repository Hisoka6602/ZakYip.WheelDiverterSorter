using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.S7;

public class S7OutputPortTests
{
    private readonly Mock<ILogger<S7OutputPort>> _mockLogger;
    private readonly Mock<S7Connection> _mockConnection;
    private const int DbNumber = 1;

    public S7OutputPortTests()
    {
        _mockLogger = new Mock<ILogger<S7OutputPort>>();
        
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
        var outputPort = new S7OutputPort(_mockLogger.Object, _mockConnection.Object, DbNumber);

        // Assert
        Assert.NotNull(outputPort);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7OutputPort(null!, _mockConnection.Object, DbNumber));
    }

    [Fact]
    public void Constructor_WithNullConnection_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7OutputPort(_mockLogger.Object, null!, DbNumber));
    }

    [Fact]
    public async Task WriteAsync_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        _mockConnection.Setup(c => c.EnsureConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var outputPort = new S7OutputPort(_mockLogger.Object, _mockConnection.Object, DbNumber);

        // Act
        var result = await outputPort.WriteAsync(0, true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task WriteAsync_WhenPlcIsNull_ReturnsFalse()
    {
        // Arrange
        _mockConnection.Setup(c => c.EnsureConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockConnection.Setup(c => c.GetPlc()).Returns((global::S7.Net.Plc?)null);
        
        var outputPort = new S7OutputPort(_mockLogger.Object, _mockConnection.Object, DbNumber);

        // Act
        var result = await outputPort.WriteAsync(0, true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task WriteBatchAsync_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        _mockConnection.Setup(c => c.EnsureConnectedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var outputPort = new S7OutputPort(_mockLogger.Object, _mockConnection.Object, DbNumber);

        // Act
        var result = await outputPort.WriteBatchAsync(0, new[] { true, false, true });

        // Assert
        Assert.False(result);
    }
}
