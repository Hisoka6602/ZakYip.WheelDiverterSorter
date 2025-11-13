namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 传感器恢复事件参数
/// </summary>
public class SensorRecoveryEventArgs : EventArgs
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
    /// 恢复时间
    /// </summary>
    public DateTimeOffset RecoveryTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 故障持续时间（秒）
    /// </summary>
    public double FaultDurationSeconds { get; set; }
}
