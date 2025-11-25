namespace ZakYip.WheelDiverterSorter.Core.Sorting.Models;

/// <summary>
/// 格口分配结果
/// </summary>
/// <remarks>
/// 记录目标格口、异常原因、分配时间等信息
/// </remarks>
public record ChuteAssignment
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 是否为异常路由
    /// </summary>
    public bool IsException { get; init; }

    /// <summary>
    /// 异常原因
    /// </summary>
    /// <remarks>
    /// 当 IsException 为 true 时，此字段记录异常原因
    /// 例如：上游超时、TTL 失败、拓扑不可达等
    /// </remarks>
    public string? ExceptionReason { get; init; }

    /// <summary>
    /// 分配时间
    /// </summary>
    public DateTimeOffset AssignmentTime { get; init; }

    /// <summary>
    /// 来源
    /// </summary>
    /// <remarks>
    /// 记录分配来源，例如：RuleEngine、FixedChute、RoundRobin、Exception 等
    /// </remarks>
    public string? Source { get; init; }
}
