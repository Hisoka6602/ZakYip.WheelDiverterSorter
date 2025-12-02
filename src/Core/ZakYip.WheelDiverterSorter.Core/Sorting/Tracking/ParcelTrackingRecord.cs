using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Tracking;

/// <summary>
/// 包裹生命周期跟踪记录
/// </summary>
/// <remarks>
/// 记录包裹从检测到最终状态（落格/超时/丢失）的完整生命周期数据。
/// 
/// <para><b>状态转换时间戳</b>：</para>
/// <list type="bullet">
///   <item>DetectedAt：入口传感器检测时间（必填，创建时设置）</item>
///   <item>AssignedAt：收到格口分配的时间（可选）</item>
///   <item>SortedAt：落格确认时间（可选）</item>
///   <item>LastSeenAt：最后一次确认包裹位置的时间</item>
/// </list>
/// 
/// <para><b>使用场景</b>：</para>
/// <list type="bullet">
///   <item>实时跟踪：ParcelLifetimeMonitor 周期性扫描判定超时/丢失</item>
///   <item>统计分析：计算各阶段耗时、成功率、超时率等指标</item>
///   <item>问题追溯：排查分拣问题时查看包裹生命周期轨迹</item>
/// </list>
/// </remarks>
public sealed record class ParcelTrackingRecord
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 入口传感器检测时间
    /// </summary>
    /// <remarks>
    /// 创建跟踪记录时设置，是判断超时和丢失的起始时间点。
    /// </remarks>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 收到格口分配的时间
    /// </summary>
    /// <remarks>
    /// 当上游推送格口分配或本地策略确定目标格口时设置。
    /// 为 null 表示尚未收到格口分配。
    /// </remarks>
    public DateTimeOffset? AssignedAt { get; init; }

    /// <summary>
    /// 落格确认时间
    /// </summary>
    /// <remarks>
    /// 当包裹成功落入目标格口时设置。
    /// 为 null 表示尚未完成落格。
    /// </remarks>
    public DateTimeOffset? SortedAt { get; init; }

    /// <summary>
    /// 最后确认包裹位置的时间
    /// </summary>
    /// <remarks>
    /// 用于判断包裹是否丢失。每次传感器检测或状态更新时刷新。
    /// </remarks>
    public DateTimeOffset? LastSeenAt { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    /// <remarks>
    /// 上游分配的目标格口ID，为 null 表示尚未分配。
    /// </remarks>
    public long? TargetChuteId { get; init; }

    /// <summary>
    /// 实际落格格口ID
    /// </summary>
    /// <remarks>
    /// 包裹实际落入的格口ID。
    /// 当发生异常路由时，可能与 TargetChuteId 不同。
    /// </remarks>
    public long? ActualChuteId { get; init; }

    /// <summary>
    /// 当前生命周期状态
    /// </summary>
    public required ParcelLifecycleStatus Status { get; init; }

    /// <summary>
    /// 创建一个新的检测状态记录
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="detectedAt">检测时间</param>
    /// <returns>新的跟踪记录</returns>
    public static ParcelTrackingRecord CreateDetected(long parcelId, DateTimeOffset detectedAt) => new()
    {
        ParcelId = parcelId,
        DetectedAt = detectedAt,
        LastSeenAt = detectedAt,
        Status = ParcelLifecycleStatus.Detected
    };

    /// <summary>
    /// 更新为已分配状态
    /// </summary>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <param name="assignedAt">分配时间</param>
    /// <returns>更新后的记录</returns>
    public ParcelTrackingRecord WithAssigned(long targetChuteId, DateTimeOffset assignedAt) =>
        this with
        {
            TargetChuteId = targetChuteId,
            AssignedAt = assignedAt,
            LastSeenAt = assignedAt,
            Status = ParcelLifecycleStatus.Assigned
        };

    /// <summary>
    /// 更新为路径执行中状态
    /// </summary>
    /// <param name="lastSeenAt">最后确认时间</param>
    /// <returns>更新后的记录</returns>
    public ParcelTrackingRecord WithRouting(DateTimeOffset lastSeenAt) =>
        this with
        {
            LastSeenAt = lastSeenAt,
            Status = ParcelLifecycleStatus.Routing
        };

    /// <summary>
    /// 更新为已完成落格状态
    /// </summary>
    /// <param name="actualChuteId">实际落格格口ID</param>
    /// <param name="sortedAt">落格时间</param>
    /// <returns>更新后的记录</returns>
    public ParcelTrackingRecord WithSorted(long actualChuteId, DateTimeOffset sortedAt) =>
        this with
        {
            ActualChuteId = actualChuteId,
            SortedAt = sortedAt,
            LastSeenAt = sortedAt,
            Status = ParcelLifecycleStatus.Sorted
        };

    /// <summary>
    /// 更新为已超时状态
    /// </summary>
    /// <returns>更新后的记录</returns>
    public ParcelTrackingRecord WithTimedOut() =>
        this with { Status = ParcelLifecycleStatus.TimedOut };

    /// <summary>
    /// 更新为已丢失状态
    /// </summary>
    /// <returns>更新后的记录</returns>
    public ParcelTrackingRecord WithLost() =>
        this with { Status = ParcelLifecycleStatus.Lost };
}
