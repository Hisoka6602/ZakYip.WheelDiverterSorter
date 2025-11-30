using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;

/// <summary>
/// 数递鸟控制命令码（信息二第5字节）
/// </summary>
public enum ShuDiNiaoControlCommand : byte
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
