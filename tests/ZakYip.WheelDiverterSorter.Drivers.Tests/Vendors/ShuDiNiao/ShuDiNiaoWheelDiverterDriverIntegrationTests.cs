using Microsoft.Extensions.Logging;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮驱动器集成测试
/// </summary>
/// <remarks>
/// 使用仿真设备测试驱动器的完整通信流程
/// </remarks>
public class ShuDiNiaoWheelDiverterDriverIntegrationTests : IDisposable
{
    private readonly ILogger<ShuDiNiaoWheelDiverterDriver> _driverLogger;
    private readonly ILogger<ShuDiNiaoSimulatedDevice> _deviceLogger;
    private readonly ISystemClock _clock;
    private readonly ShuDiNiaoSimulatedDevice _simulatedDevice;
    private readonly ShuDiNiaoWheelDiverterDriver _driver;

    private const string TestHost = "127.0.0.1";
    private const int TestPort = 15000; // 使用非标准端口避免冲突
    private const byte TestDeviceAddress = 0x51;
    private const int ActionDelayMs = 50; // 短延时，加快测试速度

    public ShuDiNiaoWheelDiverterDriverIntegrationTests()
    {
        // 创建日志记录器
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _driverLogger = loggerFactory.CreateLogger<ShuDiNiaoWheelDiverterDriver>();
        _deviceLogger = loggerFactory.CreateLogger<ShuDiNiaoSimulatedDevice>();

        // 创建系统时钟
        _clock = new LocalSystemClock();

        // 创建并启动仿真设备
        _simulatedDevice = new ShuDiNiaoSimulatedDevice(
            TestHost,
            TestPort,
            TestDeviceAddress,
            ActionDelayMs,
            _clock,
            _deviceLogger);
        _simulatedDevice.Start();

        // 等待设备启动
        Thread.Sleep(100);

        // 创建驱动器配置
        var config = new ShuDiNiaoDeviceEntry
        {
            DiverterId = 1,
            Host = TestHost,
            Port = TestPort,
            DeviceAddress = TestDeviceAddress,
            IsEnabled = true
        };

        // 创建驱动器
        _driver = new ShuDiNiaoWheelDiverterDriver(config, _driverLogger);
    }

    [Fact(DisplayName = "驱动器应能成功发送左转命令")]
    public async Task TurnLeftAsync_ShouldSucceed()
    {
        // Act
        var result = await _driver.TurnLeftAsync();

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "驱动器应能成功发送右转命令")]
    public async Task TurnRightAsync_ShouldSucceed()
    {
        // Act
        var result = await _driver.TurnRightAsync();

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "驱动器应能成功发送直通命令")]
    public async Task PassThroughAsync_ShouldSucceed()
    {
        // Act
        var result = await _driver.PassThroughAsync();

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "驱动器应能成功发送停止命令")]
    public async Task StopAsync_ShouldSucceed()
    {
        // Act
        var result = await _driver.StopAsync();

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "驱动器应能连续发送多个命令")]
    public async Task SendMultipleCommands_ShouldSucceed()
    {
        // Act & Assert
        Assert.True(await _driver.TurnLeftAsync());
        await Task.Delay(ActionDelayMs + 50); // 等待动作完成

        Assert.True(await _driver.PassThroughAsync());
        await Task.Delay(ActionDelayMs + 50);

        Assert.True(await _driver.TurnRightAsync());
        await Task.Delay(ActionDelayMs + 50);

        Assert.True(await _driver.StopAsync());
    }

    [Fact(DisplayName = "驱动器应能报告状态")]
    public async Task GetStatusAsync_ShouldReturnStatus()
    {
        // Act
        var status = await _driver.GetStatusAsync();

        // Assert
        Assert.NotNull(status);
        Assert.NotEmpty(status);
    }

    [Fact(DisplayName = "驱动器连接错误端口应返回失败")]
    public async Task ConnectToWrongPort_ShouldReturnFalse()
    {
        // Arrange - 创建连接到错误端口的驱动器
        var wrongConfig = new ShuDiNiaoDeviceEntry
        {
            DiverterId = 2,
            Host = TestHost,
            Port = TestPort + 1, // 错误的端口
            DeviceAddress = TestDeviceAddress,
            IsEnabled = true
        };

        var wrongDriver = new ShuDiNiaoWheelDiverterDriver(wrongConfig, _driverLogger);

        try
        {
            // Act - 尝试发送命令
            var result = await wrongDriver.TurnLeftAsync();

            // Assert - 应该失败
            Assert.False(result);
        }
        finally
        {
            wrongDriver.Dispose();
        }
    }

    public void Dispose()
    {
        _driver.Dispose();
        _simulatedDevice.StopAsync().GetAwaiter().GetResult();
        _simulatedDevice.Dispose();
    }
}
