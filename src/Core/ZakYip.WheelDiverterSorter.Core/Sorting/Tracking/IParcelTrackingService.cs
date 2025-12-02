namespace ZakYip.WheelDiverterSorter.Core.Sorting.Tracking;

/// <summary>
/// 包裹跟踪服务接口
/// </summary>
/// <remarks>
/// 负责包裹生命周期状态的存储和查询，是状态跟踪的唯一数据源。
/// 
/// <para><b>职责边界</b>：</para>
/// <list type="bullet">
///   <item>创建/更新/查询 ParcelTrackingRecord</item>
///   <item>提供状态列表供 ParcelLifetimeMonitor 扫描</item>
///   <item><b>不参与</b>路径计算或分拣决策（那是 ISortingOrchestrator 的职责）</item>
/// </list>
/// 
/// <para><b>调用时机</b>：</para>
/// <list type="bullet">
///   <item>入口检测时：CreateTrackingRecordAsync</item>
///   <item>收到格口分配时：UpdateAssignedAsync</item>
///   <item>落格确认时：UpdateSortedAsync</item>
///   <item>监控扫描时：GetActiveRecordsAsync</item>
///   <item>超时/丢失时：UpdateTimedOutAsync / UpdateLostAsync</item>
/// </list>
/// 
/// <para><b>与 ISortingOrchestrator 的关系</b>：</para>
/// <list type="bullet">
///   <item>ISortingOrchestrator 是业务编排入口，负责完整分拣流程</item>
///   <item>IParcelTrackingService 只是状态存储服务，不构成新的编排"影分身"</item>
/// </list>
/// </remarks>
public interface IParcelTrackingService
{
    /// <summary>
    /// 创建包裹跟踪记录（入口检测时调用）
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="detectedAt">检测时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的跟踪记录</returns>
    /// <remarks>
    /// 在入口传感器检测到包裹时调用，创建初始状态为 Detected 的跟踪记录。
    /// </remarks>
    Task<ParcelTrackingRecord> CreateTrackingRecordAsync(
        long parcelId,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新为已分配状态（收到格口分配时调用）
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <param name="assignedAt">分配时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的跟踪记录，如果记录不存在则返回 null</returns>
    Task<ParcelTrackingRecord?> UpdateAssignedAsync(
        long parcelId,
        long targetChuteId,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新为路径执行中状态
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="lastSeenAt">最后确认时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的跟踪记录，如果记录不存在则返回 null</returns>
    Task<ParcelTrackingRecord?> UpdateRoutingAsync(
        long parcelId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新为已落格状态（落格确认时调用）
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="actualChuteId">实际落格格口ID</param>
    /// <param name="sortedAt">落格时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的跟踪记录，如果记录不存在则返回 null</returns>
    Task<ParcelTrackingRecord?> UpdateSortedAsync(
        long parcelId,
        long actualChuteId,
        DateTimeOffset sortedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新为已超时状态
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的跟踪记录，如果记录不存在则返回 null</returns>
    Task<ParcelTrackingRecord?> UpdateTimedOutAsync(
        long parcelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新为已丢失状态
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的跟踪记录，如果记录不存在则返回 null</returns>
    Task<ParcelTrackingRecord?> UpdateLostAsync(
        long parcelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据包裹ID获取跟踪记录
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>跟踪记录，如果不存在则返回 null</returns>
    Task<ParcelTrackingRecord?> GetByParcelIdAsync(
        long parcelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有活跃的跟踪记录（未完成落格且未丢失）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>活跃的跟踪记录列表</returns>
    /// <remarks>
    /// 返回状态为 Detected/Assigned/Routing/TimedOut 的记录，
    /// 供 ParcelLifetimeMonitor 周期性扫描判定超时/丢失。
    /// </remarks>
    Task<IReadOnlyList<ParcelTrackingRecord>> GetActiveRecordsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理过期的跟踪记录
    /// </summary>
    /// <param name="olderThan">清理早于此时间的已完成/已丢失记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清理的记录数量</returns>
    /// <remarks>
    /// 定期调用以清理已完成（Sorted）或已丢失（Lost）的旧记录，
    /// 避免内存无限增长。
    /// </remarks>
    Task<int> CleanupExpiredRecordsAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default);
}
