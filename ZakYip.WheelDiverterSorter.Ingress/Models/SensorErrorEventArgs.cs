namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 传感器错误事件参数
/// </summary>
public class SensorErrorEventArgs : EventArgs
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType Type { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public required string ErrorMessage { get; set; }

    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 错误时间
    /// </summary>
    public DateTimeOffset ErrorTime { get; set; } = DateTimeOffset.UtcNow;
}
