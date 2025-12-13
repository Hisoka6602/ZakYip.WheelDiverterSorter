namespace ZakYip.WheelDiverterSorter.Execution.Tracking;

/// <summary>
/// Position 间隔统计信息
/// </summary>
public sealed record PositionIntervalStatistics
{
    /// <summary>
    /// Position 索引
    /// </summary>
    public required int PositionIndex { get; init; }
    
    /// <summary>
    /// 触发间隔中位数（毫秒）
    /// </summary>
    public double? MedianIntervalMs { get; init; }
    
    /// <summary>
    /// 样本数量
    /// </summary>
    public int SampleCount { get; init; }
    
    /// <summary>
    /// 最小间隔（毫秒）
    /// </summary>
    public double? MinIntervalMs { get; init; }
    
    /// <summary>
    /// 最大间隔（毫秒）
    /// </summary>
    public double? MaxIntervalMs { get; init; }
    
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? LastUpdatedAt { get; init; }
}
