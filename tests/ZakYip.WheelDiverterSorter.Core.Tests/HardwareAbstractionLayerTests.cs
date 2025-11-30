using ZakYip.WheelDiverterSorter.Core.Events.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using HalWheelCommand = ZakYip.WheelDiverterSorter.Core.Hardware.WheelCommand;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// HAL 接口定义测试
/// Tests for HAL interface definitions
/// </summary>
public class HardwareAbstractionLayerTests
{
    /// <summary>
    /// 验证 WheelDiverterState 枚举包含所有必需的状态
    /// </summary>
    [Fact]
    public void WheelDiverterState_ShouldContainAllRequiredStates()
    {
        // Verify all expected states exist
        Assert.True(Enum.IsDefined(typeof(WheelDiverterState), WheelDiverterState.Unknown));
        Assert.True(Enum.IsDefined(typeof(WheelDiverterState), WheelDiverterState.Idle));
        Assert.True(Enum.IsDefined(typeof(WheelDiverterState), WheelDiverterState.Executing));
        Assert.True(Enum.IsDefined(typeof(WheelDiverterState), WheelDiverterState.AtLeft));
        Assert.True(Enum.IsDefined(typeof(WheelDiverterState), WheelDiverterState.AtRight));
        Assert.True(Enum.IsDefined(typeof(WheelDiverterState), WheelDiverterState.AtStraight));
        Assert.True(Enum.IsDefined(typeof(WheelDiverterState), WheelDiverterState.Fault));
    }

    /// <summary>
    /// 验证 WheelCommand 是不可变的 record struct
    /// </summary>
    [Fact]
    public void WheelCommand_ShouldBeImmutableRecordStruct()
    {
        var command = new HalWheelCommand
        {
            Direction = DiverterDirection.Left,
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Verify properties
        Assert.Equal(DiverterDirection.Left, command.Direction);
        Assert.Equal(TimeSpan.FromSeconds(5), command.Timeout);
    }

    /// <summary>
    /// 验证 WheelCommand 可以有可选的 Timeout
    /// </summary>
    [Fact]
    public void WheelCommand_TimeoutIsOptional()
    {
        var command = new HalWheelCommand
        {
            Direction = DiverterDirection.Right
        };

        Assert.Equal(DiverterDirection.Right, command.Direction);
        Assert.Null(command.Timeout);
    }

    /// <summary>
    /// 验证 IoPortChangedEventArgs 是 readonly record struct
    /// </summary>
    [Fact]
    public void IoPortChangedEventArgs_ShouldBeReadonlyRecordStruct()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var eventArgs = new IoPortChangedEventArgs
        {
            GroupName = "Card0",
            PortNumber = 10,
            IsOn = true,
            Timestamp = timestamp
        };

        Assert.Equal("Card0", eventArgs.GroupName);
        Assert.Equal(10, eventArgs.PortNumber);
        Assert.True(eventArgs.IsOn);
        Assert.Equal(timestamp, eventArgs.Timestamp);
    }

    /// <summary>
    /// 验证 WheelDiverterStateChangedEventArgs 是 readonly record struct
    /// </summary>
    [Fact]
    public void WheelDiverterStateChangedEventArgs_ShouldBeReadonlyRecordStruct()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var eventArgs = new WheelDiverterStateChangedEventArgs
        {
            DeviceId = "D001",
            NewState = WheelDiverterState.AtLeft,
            PreviousState = WheelDiverterState.Idle,
            Timestamp = timestamp
        };

        Assert.Equal("D001", eventArgs.DeviceId);
        Assert.Equal(WheelDiverterState.AtLeft, eventArgs.NewState);
        Assert.Equal(WheelDiverterState.Idle, eventArgs.PreviousState);
        Assert.Equal(timestamp, eventArgs.Timestamp);
    }

    /// <summary>
    /// 验证 WheelDiverterStateChangedEventArgs 的 PreviousState 是可选的
    /// </summary>
    [Fact]
    public void WheelDiverterStateChangedEventArgs_PreviousStateIsOptional()
    {
        var eventArgs = new WheelDiverterStateChangedEventArgs
        {
            DeviceId = "D001",
            NewState = WheelDiverterState.AtRight
        };

        Assert.Null(eventArgs.PreviousState);
    }

    /// <summary>
    /// 验证 ConveyorSegmentStateChangedEventArgs 是 readonly record struct
    /// </summary>
    [Fact]
    public void ConveyorSegmentStateChangedEventArgs_ShouldBeReadonlyRecordStruct()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var eventArgs = new ConveyorSegmentStateChangedEventArgs
        {
            SegmentId = "SEG001",
            NewState = ConveyorSegmentState.Running,
            PreviousState = ConveyorSegmentState.Stopped,
            CurrentSpeedMmPerSec = 1500m,
            Timestamp = timestamp
        };

        Assert.Equal("SEG001", eventArgs.SegmentId);
        Assert.Equal(ConveyorSegmentState.Running, eventArgs.NewState);
        Assert.Equal(ConveyorSegmentState.Stopped, eventArgs.PreviousState);
        Assert.Equal(1500m, eventArgs.CurrentSpeedMmPerSec);
        Assert.Equal(timestamp, eventArgs.Timestamp);
    }

    /// <summary>
    /// 验证 ConveyorSegmentStateChangedEventArgs 的可选字段
    /// </summary>
    [Fact]
    public void ConveyorSegmentStateChangedEventArgs_OptionalFieldsAreNullable()
    {
        var eventArgs = new ConveyorSegmentStateChangedEventArgs
        {
            SegmentId = "SEG001",
            NewState = ConveyorSegmentState.Fault
        };

        Assert.Null(eventArgs.PreviousState);
        Assert.Null(eventArgs.CurrentSpeedMmPerSec);
    }
}
