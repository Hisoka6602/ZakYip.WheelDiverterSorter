using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 中段皮带运行状态枚举。
/// 定义中段皮带在不同阶段的工作状态。
/// </summary>
public enum ConveyorSegmentState
{
    /// <summary>已停止：皮带已完全停止</summary>
    [Description("已停止")]
    Stopped,

    /// <summary>启动中：皮带正在启动</summary>
    [Description("启动中")]
    Starting,

    /// <summary>运行中：皮带正常运行</summary>
    [Description("运行中")]
    Running,

    /// <summary>停止中：皮带正在停止</summary>
    [Description("停止中")]
    Stopping,

    /// <summary>故障：皮带发生故障</summary>
    [Description("故障")]
    Fault
}
