using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
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


}
