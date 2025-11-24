using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 传感器故障类型
/// </summary>
public enum SensorFaultType
{
    /// <summary>
    /// 通信超时
    /// </summary>
    [Description("通信超时")]
    CommunicationTimeout,

    /// <summary>
    /// 长时间无响应
    /// </summary>
    [Description("长时间无响应")]
    NoResponse,

    /// <summary>
    /// 读取错误
    /// </summary>
    [Description("读取错误")]
    ReadError,

    /// <summary>
    /// 设备离线
    /// </summary>
    [Description("设备离线")]
    DeviceOffline,

    /// <summary>
    /// 配置错误
    /// </summary>
    [Description("配置错误")]
    ConfigurationError,

    /// <summary>
    /// 未知错误
    /// </summary>
    [Description("未知错误")]
    Unknown
}
