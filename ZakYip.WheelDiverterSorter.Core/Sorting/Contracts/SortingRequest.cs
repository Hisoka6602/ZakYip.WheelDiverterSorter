namespace ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;

/// <summary>
/// 分拣请求
/// </summary>
/// <remarks>
/// 包含包裹信息、入口时间、可能的候选格口等
/// </remarks>
public record SortingRequest
{
    /// <summary>
    /// 包裹ID（通常为毫秒时间戳）
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹条码
    /// </summary>
    /// <remarks>
    /// 可选字段，用于与上游系统关联
    /// </remarks>
    public string? Barcode { get; init; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTimeOffset RequestTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 候选格口ID列表
    /// </summary>
    /// <remarks>
    /// 可选字段，用于提示上游系统可用的格口列表
    /// </remarks>
    public IReadOnlyList<int>? CandidateChuteIds { get; init; }

    /// <summary>
    /// 检测传感器ID
    /// </summary>
    /// <remarks>
    /// 记录检测到该包裹的传感器标识
    /// </remarks>
    public string? SensorId { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    /// <remarks>
    /// 用于传递额外的业务信息
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
