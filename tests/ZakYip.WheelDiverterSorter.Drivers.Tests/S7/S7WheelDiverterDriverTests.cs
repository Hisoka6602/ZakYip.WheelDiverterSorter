using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.S7;

public class S7WheelDiverterDriverTests
{
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockOutputPort = CreateMockOutputPort();
        var config = CreateTestConfig();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7WheelDiverterDriver(null!, mockOutputPort, config));
    }

    [Fact]
    public void Constructor_WithNullOutputPort_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = Mock.Of<ILogger<S7WheelDiverterDriver>>();
        var config = CreateTestConfig();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7WheelDiverterDriver(mockLogger, null!, config));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = Mock.Of<ILogger<S7WheelDiverterDriver>>();
        var mockOutputPort = CreateMockOutputPort();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7WheelDiverterDriver(mockLogger, mockOutputPort, null!));
    }

    [Fact]
    public async Task GetStatusAsync_InitialValue_ReturnsUnknown()
    {
        // Arrange
        var driver = CreateDriver();

        // Act
        var status = await driver.GetStatusAsync();

        // Assert
        Assert.Equal("未知", status);
    }

    [Fact]
    public void DiverterId_ReturnsConfiguredId()
    {
        // Arrange
        var driver = CreateDriver("TestDiverter123");

        // Act
        var id = driver.DiverterId;

        // Assert
        Assert.Equal("TestDiverter123", id);
    }

    private static S7WheelDiverterDriver CreateDriver(string diverterId = "D1")
    {
        var mockLogger = Mock.Of<ILogger<S7WheelDiverterDriver>>();
        var mockOutputPort = CreateMockOutputPort();
        var config = CreateTestConfig(diverterId);
        
        return new S7WheelDiverterDriver(mockLogger, mockOutputPort, config);
    }

    private static S7OutputPort CreateMockOutputPort()
    {
        var mockLogger = Mock.Of<ILogger<S7OutputPort>>();
        var mockConnectionLogger = Mock.Of<ILogger<S7Connection>>();
        var options = new S7Options { IpAddress = "127.0.0.1" };
        var connection = new S7Connection(mockConnectionLogger, options);
        
        return new S7OutputPort(mockLogger, connection, 1);
    }

    private static S7DiverterConfig CreateTestConfig(string diverterId = "D1")
    {
        return new S7DiverterConfig
        {
            DiverterId = diverterId,
            OutputDbNumber = 1,
            OutputStartByte = 0,
            OutputStartBit = 0
        };
    }
}
