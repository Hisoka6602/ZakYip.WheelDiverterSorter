using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟协议消息类型（第4字节）
/// </summary>
internal enum ShuDiNiaoMessageType : byte
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

/// <summary>
/// 数递鸟设备状态码（信息一第5字节）
/// </summary>
internal enum ShuDiNiaoDeviceState : byte
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

/// <summary>
/// 数递鸟控制命令码（信息二第5字节）
/// </summary>
internal enum ShuDiNiaoControlCommand : byte
{
    /// <summary>
    /// 运行
    /// </summary>
    [Description("运行")]
    Run = 0x51,

    /// <summary>
    /// 停止
    /// </summary>
    [Description("停止")]
    Stop = 0x52,

    /// <summary>
    /// 左摆
    /// </summary>
    [Description("左摆")]
    TurnLeft = 0x53,

    /// <summary>
    /// 回中（直通）
    /// </summary>
    [Description("回中")]
    ReturnCenter = 0x54,

    /// <summary>
    /// 右摆
    /// </summary>
    [Description("右摆")]
    TurnRight = 0x55
}

/// <summary>
/// 数递鸟应答/完成状态码（信息三第5字节）
/// </summary>
internal enum ShuDiNiaoResponseCode : byte
{
    /// <summary>
    /// 运行应答
    /// </summary>
    [Description("运行应答")]
    RunAck = 0x51,

    /// <summary>
    /// 停止应答
    /// </summary>
    [Description("停止应答")]
    StopAck = 0x52,

    /// <summary>
    /// 左摆应答
    /// </summary>
    [Description("左摆应答")]
    TurnLeftAck = 0x53,

    /// <summary>
    /// 中摆应答
    /// </summary>
    [Description("中摆应答")]
    ReturnCenterAck = 0x54,

    /// <summary>
    /// 右摆应答
    /// </summary>
    [Description("右摆应答")]
    TurnRightAck = 0x55,

    /// <summary>
    /// 左摆完成
    /// </summary>
    [Description("左摆完成")]
    TurnLeftComplete = 0x56,

    /// <summary>
    /// 中摆完成
    /// </summary>
    [Description("中摆完成")]
    ReturnCenterComplete = 0x57,

    /// <summary>
    /// 右摆完成
    /// </summary>
    [Description("右摆完成")]
    TurnRightComplete = 0x58
}
