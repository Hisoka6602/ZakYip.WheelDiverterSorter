using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;

/// <summary>
/// 数递鸟协议消息类型（第4字节）
/// </summary>
public enum ShuDiNiaoMessageType : byte
{
    /// <summary>
    /// 信息一：设备状态上报（设备端 → 服务端）
    /// </summary>
    [Description("设备状态上报")]
    DeviceStatus = 0x51,

    /// <summary>
    /// 信息二：控制命令（服务端 → 设备端）
    /// </summary>
    [Description("控制命令")]
    ControlCommand = 0x52,

    /// <summary>
    /// 信息三：应答与完成（设备端 → 服务端）
    /// </summary>
    [Description("应答与完成")]
    ResponseAndCompletion = 0x53
}
