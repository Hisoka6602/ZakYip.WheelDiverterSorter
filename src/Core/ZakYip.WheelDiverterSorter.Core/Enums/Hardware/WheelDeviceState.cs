using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 摆轮设备状态（领域层枚举）
/// </summary>
/// <remarks>
/// 定义与协议层状态码解耦的设备状态。
/// 此枚举属于 Core 层的 Hardware 枚举目录。
/// </remarks>
public enum WheelDeviceState
{
    /// <summary>
    /// 未知状态
    /// </summary>
    [Description("未知")]
    Unknown = 0,

    /// <summary>
    /// 待机状态
    /// </summary>
    [Description("待机")]
    Standby = 1,

    /// <summary>
    /// 运行中
    /// </summary>
    [Description("运行中")]
    Running = 2,

    /// <summary>
    /// 急停状态
    /// </summary>
    [Description("急停")]
    EmergencyStop = 3,

    /// <summary>
    /// 故障状态
    /// </summary>
    [Description("故障")]
    Fault = 4
}
