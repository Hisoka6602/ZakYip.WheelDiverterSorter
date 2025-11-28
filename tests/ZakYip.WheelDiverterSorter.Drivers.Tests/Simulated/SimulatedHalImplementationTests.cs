using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using HalWheelCommand = ZakYip.WheelDiverterSorter.Core.Hardware.WheelCommand;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Simulated;

/// <summary>
/// 模拟离散IO端口 HAL 实现测试
/// Tests for Simulated Discrete IO HAL implementations
/// </summary>
public class SimulatedDiscreteIoTests
{
    [Fact]
    public async Task SimulatedDiscreteIoPort_SetAndGet_ShouldWorkCorrectly()
    {
        // Arrange
        var port = new SimulatedDiscreteIoPort(10, initialState: false);

        // Act & Assert - Initial state
        Assert.Equal(10, port.PortNumber);
        Assert.False(await port.GetAsync());

        // Act & Assert - Set to true
        await port.SetAsync(true);
        Assert.True(await port.GetAsync());

        // Act & Assert - Set to false
        await port.SetAsync(false);
        Assert.False(await port.GetAsync());
    }

    [Fact]
    public async Task SimulatedDiscreteIoPort_InitialState_ShouldBeConfigurable()
    {
        // Arrange
        var port = new SimulatedDiscreteIoPort(5, initialState: true);

        // Assert
        Assert.True(await port.GetAsync());
    }

    [Fact]
    public void SimulatedDiscreteIoGroup_ShouldCreateCorrectNumberOfPorts()
    {
        // Arrange & Act
        var group = new SimulatedDiscreteIoGroup("TestCard", portCount: 16);

        // Assert
        Assert.Equal("TestCard", group.Name);
        Assert.Equal(16, group.Ports.Count);
    }

    [Fact]
    public void SimulatedDiscreteIoGroup_GetPort_ShouldReturnCorrectPort()
    {
        // Arrange
        var group = new SimulatedDiscreteIoGroup("TestCard", portCount: 8);

        // Act
        var port5 = group.GetPort(5);
        var invalidPort = group.GetPort(100);

        // Assert
        Assert.NotNull(port5);
        Assert.Equal(5, port5!.PortNumber);
        Assert.Null(invalidPort);
    }

    [Fact]
    public async Task SimulatedDiscreteIoGroup_ReadBatchAsync_ShouldReadMultiplePorts()
    {
        // Arrange
        var group = new SimulatedDiscreteIoGroup("TestCard", portCount: 8);
        await group.GetPort(2)!.SetAsync(true);
        await group.GetPort(5)!.SetAsync(true);

        // Act
        var results = await group.ReadBatchAsync(new[] { 1, 2, 5, 7 });

        // Assert
        Assert.Equal(4, results.Count);
        Assert.False(results[1]);
        Assert.True(results[2]);
        Assert.True(results[5]);
        Assert.False(results[7]);
    }

    [Fact]
    public async Task SimulatedDiscreteIoGroup_WriteBatchAsync_ShouldWriteMultiplePorts()
    {
        // Arrange
        var group = new SimulatedDiscreteIoGroup("TestCard", portCount: 8);

        // Act
        await group.WriteBatchAsync(new Dictionary<int, bool>
        {
            { 0, true },
            { 3, true },
            { 7, true }
        });

        // Assert
        Assert.True(await group.GetPort(0)!.GetAsync());
        Assert.False(await group.GetPort(1)!.GetAsync());
        Assert.True(await group.GetPort(3)!.GetAsync());
        Assert.True(await group.GetPort(7)!.GetAsync());
    }
}

/// <summary>
/// 模拟摆轮设备 HAL 实现测试
/// Tests for Simulated Wheel Diverter Device HAL implementation
/// </summary>
public class SimulatedWheelDiverterDeviceTests
{
    [Fact]
    public void Constructor_ShouldSetDeviceId()
    {
        // Arrange & Act
        var device = new SimulatedWheelDiverterDevice("D001");

        // Assert
        Assert.Equal("D001", device.DeviceId);
    }

    [Fact]
    public async Task ExecuteAsync_LeftDirection_ShouldSetStateToAtLeft()
    {
        // Arrange
        var device = new SimulatedWheelDiverterDevice("D001");
        var command = new HalWheelCommand
        {
            Direction = DiverterDirection.Left
        };

        // Act
        var result = await device.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(WheelDiverterState.AtLeft, await device.GetStateAsync());
        Assert.Equal(DiverterDirection.Left, device.LastDirection);
    }

    [Fact]
    public async Task ExecuteAsync_RightDirection_ShouldSetStateToAtRight()
    {
        // Arrange
        var device = new SimulatedWheelDiverterDevice("D001");
        var command = new HalWheelCommand
        {
            Direction = DiverterDirection.Right
        };

        // Act
        var result = await device.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(WheelDiverterState.AtRight, await device.GetStateAsync());
    }

    [Fact]
    public async Task ExecuteAsync_StraightDirection_ShouldSetStateToAtStraight()
    {
        // Arrange
        var device = new SimulatedWheelDiverterDevice("D001");
        var command = new HalWheelCommand
        {
            Direction = DiverterDirection.Straight
        };

        // Act
        var result = await device.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(WheelDiverterState.AtStraight, await device.GetStateAsync());
    }

    [Fact]
    public async Task StopAsync_ShouldSetStateToIdle()
    {
        // Arrange
        var device = new SimulatedWheelDiverterDevice("D001");
        await device.ExecuteAsync(new HalWheelCommand { Direction = DiverterDirection.Left });

        // Act
        var result = await device.StopAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(WheelDiverterState.Idle, await device.GetStateAsync());
    }

    [Fact]
    public async Task InjectFault_ShouldSetStateToFault()
    {
        // Arrange
        var device = new SimulatedWheelDiverterDevice("D001");

        // Act
        device.InjectFault();

        // Assert
        Assert.Equal(WheelDiverterState.Fault, await device.GetStateAsync());
    }

    [Fact]
    public async Task ClearFault_ShouldSetStateToIdle()
    {
        // Arrange
        var device = new SimulatedWheelDiverterDevice("D001");
        device.InjectFault();

        // Act
        device.ClearFault();

        // Assert
        Assert.Equal(WheelDiverterState.Idle, await device.GetStateAsync());
    }
}

/// <summary>
/// 模拟线体段设备 HAL 实现测试
/// Tests for Simulated Conveyor Line Segment Device HAL implementation
/// </summary>
public class SimulatedConveyorLineSegmentDeviceTests
{
    [Fact]
    public void Constructor_ShouldSetSegmentId()
    {
        // Arrange & Act
        var device = new SimulatedConveyorLineSegmentDevice("SEG001");

        // Assert
        Assert.Equal("SEG001", device.SegmentId);
    }

    [Fact]
    public async Task SetSpeedAsync_ShouldSetSpeedAndStartRunning()
    {
        // Arrange
        var device = new SimulatedConveyorLineSegmentDevice("SEG001");

        // Act
        var result = await device.SetSpeedAsync(1500m);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Running, await device.GetStateAsync());
        Assert.Equal(1500m, await device.GetCurrentSpeedAsync());
    }

    [Fact]
    public async Task SetSpeedAsync_ZeroSpeed_ShouldStop()
    {
        // Arrange
        var device = new SimulatedConveyorLineSegmentDevice("SEG001");
        await device.SetSpeedAsync(1500m);

        // Act
        var result = await device.SetSpeedAsync(0m);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Stopped, await device.GetStateAsync());
    }

    [Fact]
    public async Task StopAsync_ShouldStopAndResetSpeed()
    {
        // Arrange
        var device = new SimulatedConveyorLineSegmentDevice("SEG001");
        await device.SetSpeedAsync(1500m);

        // Act
        var result = await device.StopAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Stopped, await device.GetStateAsync());
        Assert.Equal(0m, await device.GetCurrentSpeedAsync());
    }

    [Fact]
    public async Task StartAsync_ShouldUseDefaultSpeed()
    {
        // Arrange
        var device = new SimulatedConveyorLineSegmentDevice("SEG001");

        // Act
        var result = await device.StartAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConveyorSegmentState.Running, await device.GetStateAsync());
        Assert.True(await device.GetCurrentSpeedAsync() > 0);
    }

    [Fact]
    public async Task StartAsync_AfterSettingSpeed_ShouldUsePreviousSpeed()
    {
        // Arrange
        var device = new SimulatedConveyorLineSegmentDevice("SEG001");
        await device.SetSpeedAsync(2000m);
        await device.StopAsync();

        // Act
        await device.StartAsync();

        // Assert
        Assert.Equal(2000m, await device.GetCurrentSpeedAsync());
    }

    [Fact]
    public async Task InjectFault_ShouldSetFaultState()
    {
        // Arrange
        var device = new SimulatedConveyorLineSegmentDevice("SEG001");
        await device.SetSpeedAsync(1500m);

        // Act
        device.InjectFault();

        // Assert
        Assert.Equal(ConveyorSegmentState.Fault, await device.GetStateAsync());
        Assert.Equal(0m, await device.GetCurrentSpeedAsync());
    }

    [Fact]
    public async Task ClearFault_ShouldSetStoppedState()
    {
        // Arrange
        var device = new SimulatedConveyorLineSegmentDevice("SEG001");
        device.InjectFault();

        // Act
        device.ClearFault();

        // Assert
        Assert.Equal(ConveyorSegmentState.Stopped, await device.GetStateAsync());
    }
}
