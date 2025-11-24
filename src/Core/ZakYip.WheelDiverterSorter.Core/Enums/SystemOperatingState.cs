using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums;

/// <summary>
/// 系统运行状态枚举。
/// 定义系统在不同阶段的工作状态，用于面板联动与三色灯控制。
/// </summary>
public enum SystemOperatingState
{
    /// <summary>初始化中：系统正在启动和初始化</summary>
    [Description("初始化中")]
    Initializing,

    /// <summary>待机：系统已就绪，等待启动</summary>
    [Description("待机")]
    Standby,

    /// <summary>运行中：系统正常运行，执行分拣任务</summary>
    [Description("运行中")]
    Running,

    /// <summary>暂停：系统已暂停，可恢复运行</summary>
    [Description("暂停")]
    Paused,

    /// <summary>停止中：系统正在安全停机</summary>
    [Description("停止中")]
    Stopping,

    /// <summary>已停止：系统已完全停止</summary>
    [Description("已停止")]
    Stopped,

    /// <summary>故障：系统发生故障，需要复位</summary>
    [Description("故障")]
    Faulted,

    /// <summary>急停：触发急停按钮，系统紧急停止</summary>
    [Description("急停")]
    EmergencyStopped,

    /// <summary>等待上游：等待上游系统响应或连接</summary>
    [Description("等待上游")]
    WaitingUpstream
}
