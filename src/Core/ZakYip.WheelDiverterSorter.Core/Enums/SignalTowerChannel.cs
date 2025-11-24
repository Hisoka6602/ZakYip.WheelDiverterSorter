using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums;

/// <summary>
/// 信号塔通道枚举（三色灯与蜂鸣器）。
/// </summary>
public enum SignalTowerChannel
{
    /// <summary>红色灯</summary>
    [Description("红灯")]
    Red,

    /// <summary>黄色灯</summary>
    [Description("黄灯")]
    Yellow,

    /// <summary>绿色灯</summary>
    [Description("绿灯")]
    Green,

    /// <summary>蜂鸣器</summary>
    [Description("蜂鸣器")]
    Buzzer
}
