using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Host.StateMachine;

/// <summary>
/// 系统状态枚举
/// </summary>
/// <remarks>
/// 定义系统的各种运行状态，用于状态机管理和面板控制。
/// 所有状态转移必须遵循预定义的规则，不允许任意跳转。
/// </remarks>
public enum SystemState
{
    /// <summary>启动中：系统正在启动和初始化</summary>
    [Description("启动中")]
    Booting = 0,

    /// <summary>就绪：系统已就绪，可以开始运行</summary>
    [Description("就绪")]
    Ready = 1,

    /// <summary>运行中：系统正常运行，执行分拣任务</summary>
    [Description("运行中")]
    Running = 2,

    /// <summary>暂停：系统已暂停，可恢复运行</summary>
    [Description("暂停")]
    Paused = 3,

    /// <summary>故障：系统发生故障，需要处理</summary>
    [Description("故障")]
    Faulted = 4,

    /// <summary>急停：触发急停按钮，系统紧急停止</summary>
    [Description("急停")]
    EmergencyStop = 5
}
