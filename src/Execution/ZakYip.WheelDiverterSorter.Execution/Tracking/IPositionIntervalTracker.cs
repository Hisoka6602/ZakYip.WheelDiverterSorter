namespace ZakYip.WheelDiverterSorter.Execution.Tracking;

/// <summary>
/// Position 间隔追踪器接口
/// </summary>
/// <remarks>
/// 用于记录和统计每个 position 的传感器触发间隔，支持包裹丢失检测方案A+（中位数自适应超时检测）
/// </remarks>
public interface IPositionIntervalTracker
{
    /// <summary>
    /// 记录触发间隔
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    /// <param name="intervalMs">触发间隔（毫秒）</param>
    void RecordInterval(int positionIndex, double intervalMs);
    
    /// <summary>
    /// 记录传感器触发事件（旧方法，已弃用）
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    /// <param name="triggeredAt">触发时间</param>
    /// <remarks>
    /// 此方法会自动计算与上次触发的间隔并记录。
    /// 注意：此方法计算的是同一position的重复触发间隔，不是包裹在相邻position间的间隔。
    /// 推荐使用 RecordParcelPosition 方法来跟踪包裹在position间的移动。
    /// </remarks>
    [Obsolete("Use RecordParcelPosition instead for tracking parcel movement between positions")]
    void RecordTrigger(int positionIndex, DateTime triggeredAt);
    
    /// <summary>
    /// 记录包裹到达某个Position
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="positionIndex">Position 索引</param>
    /// <param name="arrivedAt">到达时间</param>
    /// <remarks>
    /// 此方法会跟踪包裹在各个position的时间，并计算相邻position间的间隔。
    /// 例如：包裹从 position 1 → position 2 的间隔 = position2触发时间 - position1触发时间
    /// </remarks>
    void RecordParcelPosition(long parcelId, int positionIndex, DateTime arrivedAt);
    
    /// <summary>
    /// 获取指定 Position 的统计信息
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    /// <returns>统计信息(positionIndex, medianMs, sampleCount, minMs, maxMs, lastUpdated)，如果该 Position 没有数据则返回 null</returns>
    (int PositionIndex, double? MedianIntervalMs, int SampleCount, double? MinIntervalMs, double? MaxIntervalMs, DateTime? LastUpdatedAt)? GetStatistics(int positionIndex);
    
    /// <summary>
    /// 获取所有 Position 的统计信息
    /// </summary>
    /// <returns>所有 Position 的统计信息列表</returns>
    IReadOnlyList<(int PositionIndex, double? MedianIntervalMs, int SampleCount, double? MinIntervalMs, double? MaxIntervalMs, DateTime? LastUpdatedAt)> GetAllStatistics();
    
    /// <summary>
    /// 获取当前 Position 的动态超时阈值（用于包裹丢失检测）
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    /// <returns>动态超时阈值（毫秒），如果数据不足则返回 null</returns>
    double? GetDynamicThreshold(int positionIndex);
    
    /// <summary>
    /// 清空指定 Position 的统计数据
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    void ClearStatistics(int positionIndex);
    
    /// <summary>
    /// 清空所有统计数据
    /// </summary>
    void ClearAllStatistics();
}
