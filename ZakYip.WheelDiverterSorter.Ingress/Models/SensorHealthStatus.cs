using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Ingress.Models;

/// <summary>
/// 传感器健康状态
/// </summary>
public class SensorHealthStatus {

    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public SensorType Type { get; set; }

    /// <summary>
    /// 是否健康
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// 最后触发时间
    /// </summary>
    public DateTimeOffset? LastTriggerTime { get; set; }

    /// <summary>
    /// 最后检查时间
    /// </summary>
    public DateTimeOffset LastCheckTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 错误计数
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// 最后错误信息
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 最后错误时间
    /// </summary>
    public DateTimeOffset? LastErrorTime { get; set; }

    /// <summary>
    /// 总触发次数
    /// </summary>
    public long TotalTriggerCount { get; set; }

    /// <summary>
    /// 运行时长（秒）
    /// </summary>
    public double UptimeSeconds { get; set; }

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }
}