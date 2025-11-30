using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;

/// <summary>
/// 数递鸟设备状态码（信息一第5字节）
/// </summary>
public enum ShuDiNiaoDeviceState : byte
{
    /// <summary>
    /// 待机
    /// </summary>
    [Description("待机")]
    Standby = 0x50,

    /// <summary>
    /// 运行
    /// </summary>
    [Description("运行")]
    Running = 0x51,

    /// <summary>
    /// 急停
    /// </summary>
    [Description("急停")]
    EmergencyStop = 0x52,

    /// <summary>
    /// 故障
    /// </summary>
    [Description("故障")]
    Fault = 0x53
}
