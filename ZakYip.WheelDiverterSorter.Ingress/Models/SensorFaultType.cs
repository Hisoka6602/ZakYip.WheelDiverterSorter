namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 传感器故障类型
/// </summary>
public enum SensorFaultType
{
    /// <summary>
    /// 通信超时
    /// </summary>
    CommunicationTimeout,

    /// <summary>
    /// 长时间无响应
    /// </summary>
    NoResponse,

    /// <summary>
    /// 读取错误
    /// </summary>
    ReadError,

    /// <summary>
    /// 设备离线
    /// </summary>
    DeviceOffline,

    /// <summary>
    /// 配置错误
    /// </summary>
    ConfigurationError,

    /// <summary>
    /// 未知错误
    /// </summary>
    Unknown
}
