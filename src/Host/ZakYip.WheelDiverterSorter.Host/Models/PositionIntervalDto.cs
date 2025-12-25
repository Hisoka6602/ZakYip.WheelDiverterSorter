namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// Position间隔统计DTO
/// </summary>
/// <remarks>
/// ⚠️ 重要：此DTO提供的中位数统计数据仅用于观测和监控，不用于任何分拣逻辑判断。
/// 所有超时判断、丢失判定均基于输送线配置（ConveyorSegmentConfiguration）。
/// </remarks>
public record PositionIntervalDto
{
    /// <summary>
    /// Position索引
    /// </summary>
    /// <example>1</example>
    public int PositionIndex { get; init; }

    /// <summary>
    /// 触发间隔中位数（毫秒）
    /// </summary>
    /// <example>352.5</example>
    public double? MedianIntervalMs { get; init; }

    /// <summary>
    /// 样本数量
    /// </summary>
    /// <example>10</example>
    public int SampleCount { get; init; }

    /// <summary>
    /// 最小间隔（毫秒）
    /// </summary>
    /// <example>320.0</example>
    public double? MinIntervalMs { get; init; }

    /// <summary>
    /// 最大间隔（毫秒）
    /// </summary>
    /// <example>380.0</example>
    public double? MaxIntervalMs { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? LastUpdatedAt { get; init; }
}
