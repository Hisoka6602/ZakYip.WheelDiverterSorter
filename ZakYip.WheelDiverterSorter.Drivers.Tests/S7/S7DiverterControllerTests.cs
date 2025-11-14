using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Drivers.S7;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.S7;

public class S7DiverterControllerTests
{
    private readonly Mock<ILogger<S7DiverterController>> _mockLogger;
    private readonly Mock<S7OutputPort> _mockOutputPort;
    private readonly S7DiverterConfig _config;

    public S7DiverterControllerTests()
    {
        _mockLogger = new Mock<ILogger<S7DiverterController>>();
        
        var mockOutputPortLogger = new Mock<ILogger<S7OutputPort>>();
        var mockConnectionLogger = new Mock<ILogger<S7Connection>>();
        var options = new S7Options
        {
            IpAddress = "192.168.0.100",
            Rack = 0,
            Slot = 1
        };
        var mockConnection = new Mock<S7Connection>(mockConnectionLogger.Object, options);
        _mockOutputPort = new Mock<S7OutputPort>(mockOutputPortLogger.Object, mockConnection.Object, 1);
        
        _config = new S7DiverterConfig
        {
            DiverterId = "D1",
            OutputDbNumber = 1,
            OutputStartByte = 0,
            OutputStartBit = 0
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);

        // Assert
        Assert.NotNull(controller);
        Assert.Equal("D1", controller.DiverterId);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7DiverterController(null!, _mockOutputPort.Object, _config));
    }

    [Fact]
    public void Constructor_WithNullOutputPort_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7DiverterController(_mockLogger.Object, null!, _config));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, null!));
    }

    [Fact]
    public async Task SetAngleAsync_With0Degrees_WritesCorrectBits()
    {
        // Arrange
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);

        // Act
        var result = await controller.SetAngleAsync(0);

        // Assert
        Assert.True(result);
        _mockOutputPort.Verify(p => p.WriteAsync(0, false), Times.Once); // Bit 0 = false
        _mockOutputPort.Verify(p => p.WriteAsync(1, false), Times.Once); // Bit 1 = false
    }

    [Fact]
    public async Task SetAngleAsync_With30Degrees_WritesCorrectBits()
    {
        // Arrange
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);

        // Act
        var result = await controller.SetAngleAsync(30);

        // Assert
        Assert.True(result);
        _mockOutputPort.Verify(p => p.WriteAsync(0, true), Times.Once); // Bit 0 = true
        _mockOutputPort.Verify(p => p.WriteAsync(1, false), Times.Once); // Bit 1 = false
    }

    [Fact]
    public async Task SetAngleAsync_With45Degrees_WritesCorrectBits()
    {
        // Arrange
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);

        // Act
        var result = await controller.SetAngleAsync(45);

        // Assert
        Assert.True(result);
        _mockOutputPort.Verify(p => p.WriteAsync(0, false), Times.Once); // Bit 0 = false
        _mockOutputPort.Verify(p => p.WriteAsync(1, true), Times.Once); // Bit 1 = true
    }

    [Fact]
    public async Task SetAngleAsync_With90Degrees_WritesCorrectBits()
    {
        // Arrange
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);

        // Act
        var result = await controller.SetAngleAsync(90);

        // Assert
        Assert.True(result);
        _mockOutputPort.Verify(p => p.WriteAsync(0, true), Times.Once); // Bit 0 = true
        _mockOutputPort.Verify(p => p.WriteAsync(1, true), Times.Once); // Bit 1 = true
    }

    [Fact]
    public async Task SetAngleAsync_WithUnsupportedAngle_DefaultsTo0Degrees()
    {
        // Arrange
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);

        // Act
        var result = await controller.SetAngleAsync(60); // Unsupported angle

        // Assert
        Assert.True(result);
        _mockOutputPort.Verify(p => p.WriteAsync(0, false), Times.Once); // Bit 0 = false
        _mockOutputPort.Verify(p => p.WriteAsync(1, false), Times.Once); // Bit 1 = false
    }

    [Fact]
    public async Task SetAngleAsync_WhenWriteFails_ReturnsFalse()
    {
        // Arrange
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(false);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);

        // Act
        var result = await controller.SetAngleAsync(45);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCurrentAngleAsync_AfterSetAngle_ReturnsSetAngle()
    {
        // Arrange
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);
        await controller.SetAngleAsync(45);

        // Act
        var currentAngle = await controller.GetCurrentAngleAsync();

        // Assert
        Assert.Equal(45, currentAngle);
    }

    [Fact]
    public async Task ResetAsync_CallsSetAngleWith0()
    {
        // Arrange
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, _config);

        // Act
        var result = await controller.ResetAsync();

        // Assert
        Assert.True(result);
        _mockOutputPort.Verify(p => p.WriteAsync(0, false), Times.Once);
        _mockOutputPort.Verify(p => p.WriteAsync(1, false), Times.Once);
    }

    [Fact]
    public async Task SetAngleAsync_WithNonZeroStartByte_CalculatesCorrectBitIndex()
    {
        // Arrange
        var config = new S7DiverterConfig
        {
            DiverterId = "D2",
            OutputDbNumber = 1,
            OutputStartByte = 2, // Start at byte 2
            OutputStartBit = 3   // Start at bit 3
        };
        _mockOutputPort.Setup(p => p.WriteAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        
        var controller = new S7DiverterController(_mockLogger.Object, _mockOutputPort.Object, config);

        // Act
        var result = await controller.SetAngleAsync(0);

        // Assert
        Assert.True(result);
        // Byte 2 * 8 + Bit 3 = 16 + 3 = 19
        _mockOutputPort.Verify(p => p.WriteAsync(19, false), Times.Once); // Bit 0
        _mockOutputPort.Verify(p => p.WriteAsync(20, false), Times.Once); // Bit 1
    }
}
