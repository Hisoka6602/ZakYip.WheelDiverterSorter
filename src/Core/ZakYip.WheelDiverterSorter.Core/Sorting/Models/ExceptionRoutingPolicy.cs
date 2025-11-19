namespace ZakYip.WheelDiverterSorter.Core.Sorting.Models;

/// <summary>
/// 异常路由策略
/// </summary>
/// <remarks>
/// 定义当分拣失败时的处理策略
/// </remarks>
public record ExceptionRoutingPolicy
{
    /// <summary>
    /// 异常格口ID
    /// </summary>
    public required int ExceptionChuteId { get; init; }

    /// <summary>
    /// 上游超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 等待上游 RuleEngine 响应的最大时间，超时后使用异常格口
    /// </remarks>
    public int UpstreamTimeoutMs { get; init; } = 10000;

    /// <summary>
    /// 是否在超时后重试
    /// </summary>
    public bool RetryOnTimeout { get; init; } = false;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; } = 0;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    public int RetryDelayMs { get; init; } = 1000;

    /// <summary>
    /// 拓扑不可达时是否使用异常格口
    /// </summary>
    /// <remarks>
    /// 当目标格口在拓扑中不可达时，是否自动路由到异常格口
    /// </remarks>
    public bool UseExceptionOnTopologyUnreachable { get; init; } = true;

    /// <summary>
    /// TTL 失败时是否使用异常格口
    /// </summary>
    /// <remarks>
    /// 当包裹在系统中停留时间过长（TTL 超时）时，是否路由到异常格口
    /// </remarks>
    public bool UseExceptionOnTtlFailure { get; init; } = true;
}
