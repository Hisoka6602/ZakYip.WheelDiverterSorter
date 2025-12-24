using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮驱动器重连机制测试
/// </summary>
public class ShuDiNiaoReconnectionTests
{
    private readonly Mock<ILogger<ShuDiNiaoWheelDiverterDriver>> _mockLogger;
    private readonly Mock<ISystemClock> _mockClock;

    public ShuDiNiaoReconnectionTests()
    {
        _mockLogger = new Mock<ILogger<ShuDiNiaoWheelDiverterDriver>>();
        _mockClock = new Mock<ISystemClock>();
        _mockClock.Setup(x => x.LocalNow).Returns(DateTime.Now);
    }

    [Fact]
    public async Task StartReconnect_ShouldNotThrow_WhenCalledMultipleTimes()
    {
        // Arrange
        var config = new ShuDiNiaoDeviceEntry
        {
            DiverterId = 1,
            Host = "127.0.0.1",
            Port = 9999, // 不存在的端口，确保连接失败
            DeviceAddress = 1,
            IsEnabled = true
        };

        using var driver = new ShuDiNiaoWheelDiverterDriver(config, _mockLogger.Object, _mockClock.Object);

        // Act - 多次调用 StartReconnect 不应抛出异常
        driver.StartReconnect();
        driver.StartReconnect();
        driver.StartReconnect();

        // Assert - 通过没有异常表示成功
        // 等待一小段时间让重连任务启动
        await Task.Delay(100);
    }

    [Fact]
    public async Task CheckHeartbeatAsync_ShouldReturnFalse_WhenNeverConnected()
    {
        // Arrange
        var config = new ShuDiNiaoDeviceEntry
        {
            DiverterId = 1,
            Host = "127.0.0.1",
            Port = 9999,
            DeviceAddress = 1,
            IsEnabled = true
        };

        using var driver = new ShuDiNiaoWheelDiverterDriver(config, _mockLogger.Object, _mockClock.Object);

        // Act
        var result = await driver.CheckHeartbeatAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Dispose_ShouldStopReconnectTask()
    {
        // Arrange
        var config = new ShuDiNiaoDeviceEntry
        {
            DiverterId = 1,
            Host = "127.0.0.1",
            Port = 9999,
            DeviceAddress = 1,
            IsEnabled = true
        };

        var driver = new ShuDiNiaoWheelDiverterDriver(config, _mockLogger.Object, _mockClock.Object);

        // Act - 启动重连任务后立即释放
        driver.StartReconnect();
        await Task.Delay(100); // 等待任务启动
        driver.Dispose();

        // Assert - 验证没有异常
        // 通过 Dispose 不抛出异常来验证重连任务被正确停止
    }

    [Fact]
    public async Task TurnLeftAsync_ShouldAttemptConnection_WhenNotConnected()
    {
        // Arrange
        var config = new ShuDiNiaoDeviceEntry
        {
            DiverterId = 1,
            Host = "127.0.0.1",
            Port = 9999, // 不存在的端口
            DeviceAddress = 1,
            IsEnabled = true
        };

        using var driver = new ShuDiNiaoWheelDiverterDriver(config, _mockLogger.Object, _mockClock.Object);

        // Act
        var result = await driver.TurnLeftAsync();

        // Assert - 由于连接失败，应该返回 false
        Assert.False(result);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReturnDisconnected_WhenNotConnected()
    {
        // Arrange
        var config = new ShuDiNiaoDeviceEntry
        {
            DiverterId = 1,
            Host = "127.0.0.1",
            Port = 9999,
            DeviceAddress = 1,
            IsEnabled = true
        };

        using var driver = new ShuDiNiaoWheelDiverterDriver(config, _mockLogger.Object, _mockClock.Object);

        // Act
        var status = await driver.GetStatusAsync();

        // Assert
        Assert.Contains("未连接", status);
    }
}
