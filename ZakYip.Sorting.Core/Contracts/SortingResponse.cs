namespace ZakYip.Sorting.Core.Contracts;

/// <summary>
/// 分拣响应
/// </summary>
/// <remarks>
/// 包含目标格口ID、异常标志、原因代码等
/// </remarks>
public record SortingResponse
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required int TargetChuteId { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// 是否为异常路由
    /// </summary>
    /// <remarks>
    /// 当分拣失败或需要特殊处理时，标记为异常路由
    /// </remarks>
    public bool IsException { get; init; }

    /// <summary>
    /// 原因代码
    /// </summary>
    /// <remarks>
    /// 记录分拣结果的原因代码，例如：SUCCESS、TIMEOUT、TOPOLOGY_UNREACHABLE 等
    /// </remarks>
    public string? ReasonCode { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    /// <remarks>
    /// 当 IsSuccess 为 false 时，此字段记录错误详情
    /// </remarks>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 响应时间
    /// </summary>
    public DateTimeOffset ResponseTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 来源
    /// </summary>
    /// <remarks>
    /// 记录响应来源，例如：RuleEngine、LocalDecision、FallbackPolicy 等
    /// </remarks>
    public string? Source { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    /// <remarks>
    /// 用于传递额外的业务信息
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
