using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;

/// <summary>
/// 莫迪摆轮控制命令枚举
/// </summary>
public enum ModiControlCommand : byte
{
    /// <summary>
    /// 停止
    /// </summary>
    [Description("停止")]
    Stop = 0x00,
    
    /// <summary>
    /// 左转
    /// </summary>
    [Description("左转")]
    TurnLeft = 0x01,
    
    /// <summary>
    /// 右转
    /// </summary>
    [Description("右转")]
    TurnRight = 0x02,
    
    /// <summary>
    /// 回中（直通）
    /// </summary>
    [Description("回中")]
    ReturnCenter = 0x03
}
