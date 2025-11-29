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
    Unknown = 0,

    /// <summary>
    /// 待机状态
    /// </summary>
    Standby = 1,

    /// <summary>
    /// 运行中
    /// </summary>
    Running = 2,

    /// <summary>
    /// 急停状态
    /// </summary>
    EmergencyStop = 3,

    /// <summary>
    /// 故障状态
    /// </summary>
    Fault = 4
}
