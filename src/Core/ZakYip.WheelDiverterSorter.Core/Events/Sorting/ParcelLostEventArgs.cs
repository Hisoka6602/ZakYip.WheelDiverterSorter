namespace ZakYip.WheelDiverterSorter.Core.Events.Sorting;

/// <summary>
/// 包裹丢失事件载荷
/// </summary>
/// <remarks>
/// 当包裹超出系统允许的最大存活时间，仍未完成落格且无法确定位置时触发此事件。
/// 
/// <para><b>丢失判定条件</b>：</para>
/// <list type="bullet">
///   <item>状态为 Detected/Assigned/Routing/TimedOut（非 Sorted）</item>
///   <item>SortedAt 为 null（未完成落格）</item>
///   <item>当前时间 - DetectedAt &gt; MaxLifetimeBeforeLost</item>
/// </list>
/// 
/// <para><b>丢失与超时的区别</b>：</para>
/// <list type="bullet">
///   <item>超时：特定阶段时间窗口内未完成对应步骤，仍可能被找回</item>
///   <item>丢失：超出最大存活时间，无法确定位置，视为永久丢失</item>
/// </list>
/// 
/// <para><b>处理流程</b>：</para>
/// <list type="number">
///   <item>ParcelLifetimeMonitor 检测到丢失</item>
///   <item>更新 ParcelTrackingRecord 状态为 Lost</item>
///   <item>触发此事件通知订阅者</item>
///   <item>记录 parcel_lost_total 等统计指标</item>
///   <item>向上游发送 Outcome=Lost 的 SortingCompletedNotification</item>
/// </list>
/// </remarks>
public readonly record struct ParcelLostEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹检测时间
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 丢失判定时间
    /// </summary>
    public required DateTimeOffset LostAt { get; init; }

    /// <summary>
    /// 最后确认包裹位置的时间
    /// </summary>
    /// <remarks>
    /// 如果有值，表示在此时间后失去包裹踪迹。
    /// </remarks>
    public DateTimeOffset? LastSeenAt { get; init; }

    /// <summary>
    /// 包裹存活时长（从检测到丢失判定）
    /// </summary>
    public TimeSpan LifetimeDuration => LostAt - DetectedAt;
}
