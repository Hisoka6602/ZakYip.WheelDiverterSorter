using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 面板按钮类型枚举。
/// </summary>
public enum PanelButtonType
{
    /// <summary>启动按钮</summary>
    [Description("启动")]
    Start,

    /// <summary>停止按钮</summary>
    [Description("停止")]
    Stop,

    /// <summary>复位按钮</summary>
    [Description("复位")]
    Reset,

    /// <summary>急停按钮</summary>
    [Description("急停")]
    EmergencyStop,

    /// <summary>自动模式选择</summary>
    [Description("自动模式")]
    ModeAuto,

    /// <summary>手动模式选择</summary>
    [Description("手动模式")]
    ModeManual
}
