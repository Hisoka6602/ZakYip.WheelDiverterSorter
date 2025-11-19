using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Simulated;

public class SimulatedConveyorSegmentDriverTests
{
    private readonly ConveyorIoMapping _mapping = new()
    {
        SegmentKey = "Middle1",
        DisplayName = "中段皮带1",
        StartOutputChannel = 100,
        StopOutputChannel = 101,
        FaultInputChannel = 200,
        RunningInputChannel = 201,
        Priority = 1,
        StartTimeoutMs = 5000,
        StopTimeoutMs = 5000
    };

    [Fact]
    public async Task WriteStartSignal_ShouldSetRunningStateAfterDelay()
    {
        // Arrange
        var driver = new SimulatedConveyorSegmentDriver(_mapping, NullLogger<SimulatedConveyorSegmentDriver>.Instance);

        // Act
        await driver.WriteStartSignalAsync();
        await Task.Delay(200); // 等待模拟启动完成

        // Assert
        var isRunning = await driver.ReadRunningStatusAsync();
        Assert.True(isRunning);
    }

    [Fact]
    public async Task WriteStopSignal_ShouldClearRunningState()
    {
        // Arrange
        var driver = new SimulatedConveyorSegmentDriver(_mapping, NullLogger<SimulatedConveyorSegmentDriver>.Instance);
        await driver.WriteStartSignalAsync();
        await Task.Delay(200);

        // Act
        await driver.WriteStopSignalAsync();
        await Task.Delay(200); // 等待模拟停止完成

        // Assert
        var isRunning = await driver.ReadRunningStatusAsync();
        Assert.False(isRunning);
    }

    [Fact]
    public async Task InjectFault_ShouldSetFaultStatus()
    {
        // Arrange
        var driver = new SimulatedConveyorSegmentDriver(_mapping, NullLogger<SimulatedConveyorSegmentDriver>.Instance);
        await driver.WriteStartSignalAsync();
        await Task.Delay(200);

        // Act
        driver.InjectFault(true);

        // Assert
        var hasFault = await driver.ReadFaultStatusAsync();
        var isRunning = await driver.ReadRunningStatusAsync();
        Assert.True(hasFault);
        Assert.False(isRunning); // 故障时应停止运行
    }

    [Fact]
    public async Task InjectFault_ShouldPreventStarting()
    {
        // Arrange
        var driver = new SimulatedConveyorSegmentDriver(_mapping, NullLogger<SimulatedConveyorSegmentDriver>.Instance);
        driver.InjectFault(true);

        // Act
        await driver.WriteStartSignalAsync();
        await Task.Delay(200);

        // Assert
        var isRunning = await driver.ReadRunningStatusAsync();
        Assert.False(isRunning); // 有故障时不应启动
    }

    [Fact]
    public async Task Reset_ShouldClearAllStates()
    {
        // Arrange
        var driver = new SimulatedConveyorSegmentDriver(_mapping, NullLogger<SimulatedConveyorSegmentDriver>.Instance);
        await driver.WriteStartSignalAsync();
        await Task.Delay(200);

        // Act
        await driver.ResetAsync();

        // Assert
        var isRunning = await driver.ReadRunningStatusAsync();
        Assert.False(isRunning);
    }

    [Fact]
    public async Task ReadFaultStatus_WithoutFault_ShouldReturnFalse()
    {
        // Arrange
        var driver = new SimulatedConveyorSegmentDriver(_mapping, NullLogger<SimulatedConveyorSegmentDriver>.Instance);

        // Act
        var hasFault = await driver.ReadFaultStatusAsync();

        // Assert
        Assert.False(hasFault);
    }

    [Fact]
    public async Task SetRunningState_ShouldUpdateRunningStatus()
    {
        // Arrange
        var driver = new SimulatedConveyorSegmentDriver(_mapping, NullLogger<SimulatedConveyorSegmentDriver>.Instance);

        // Act
        driver.SetRunningState(true);

        // Assert
        var isRunning = await driver.ReadRunningStatusAsync();
        Assert.True(isRunning);
    }

    [Fact]
    public void Mapping_ShouldReturnConfiguredMapping()
    {
        // Arrange & Act
        var driver = new SimulatedConveyorSegmentDriver(_mapping, NullLogger<SimulatedConveyorSegmentDriver>.Instance);

        // Assert
        Assert.Equal(_mapping, driver.Mapping);
        Assert.Equal("Middle1", driver.Mapping.SegmentKey);
        Assert.Equal("中段皮带1", driver.Mapping.DisplayName);
    }
}
