using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;

/// <summary>
/// 数递鸟通信模式
/// </summary>
public enum ShuDiNiaoMode
{
    /// <summary>
    /// 客户端模式：系统主动连接到摆轮设备（默认）
    /// </summary>
    [Description("客户端模式")]
    Client = 0,

    /// <summary>
    /// 服务端模式：系统监听设备连接
    /// </summary>
    [Description("服务端模式")]
    Server = 1
}
