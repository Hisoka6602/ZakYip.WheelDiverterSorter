using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Events.Sorting;

/// <summary>
/// 包裹超时事件载荷
/// </summary>
/// <remarks>
/// 当包裹在特定阶段超过允许的时间窗口时触发此事件。
/// 
/// <para><b>超时类型</b>：</para>
/// <list type="bullet">
///   <item>分配超时：Status == Detected 且超过 DetectionToAssignmentTimeout</item>
///   <item>落格超时：Status == Assigned/Routing 且超过 AssignmentToSortingTimeout</item>
/// </list>
/// 
/// <para><b>处理流程</b>：</para>
/// <list type="number">
///   <item>ParcelLifetimeMonitor 检测到超时</item>
///   <item>更新 ParcelTrackingRecord 状态为 TimedOut</item>
///   <item>触发此事件通知订阅者</item>
///   <item>尝试路由到异常格口</item>
///   <item>向上游发送 Outcome=Timeout 的 SortingCompletedNotification</item>
/// </list>
/// </remarks>
public readonly record struct ParcelTimedOutEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 超时前的状态
    /// </summary>
    /// <remarks>
    /// 指示在哪个阶段发生超时：
    /// <list type="bullet">
    ///   <item>Detected - 分配超时</item>
    ///   <item>Assigned/Routing - 落格超时</item>
    /// </list>
    /// </remarks>
    public required ParcelLifecycleStatus StatusBeforeTimeout { get; init; }

    /// <summary>
    /// 包裹检测时间
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 超时判定时间
    /// </summary>
    public required DateTimeOffset TimeoutAt { get; init; }

    /// <summary>
    /// 超时持续时间（从检测到超时）
    /// </summary>
    public TimeSpan TimeoutDuration => TimeoutAt - DetectedAt;
}
