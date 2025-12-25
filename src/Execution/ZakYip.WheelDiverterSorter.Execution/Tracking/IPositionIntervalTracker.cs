namespace ZakYip.WheelDiverterSorter.Execution.Tracking;

/// <summary>
/// Position 间隔追踪器接口
/// </summary>
/// <remarks>
/// <para>用于记录和统计每个 position 的传感器触发间隔，提供观测数据。</para>
/// <para>⚠️ 重要：中位数统计值<b>仅用于观测</b>，不用于任何分拣逻辑判断。</para>
/// <para>所有超时判断、丢失判定均应基于输送线配置（ConveyorSegmentConfiguration）。</para>
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
    /// <remarks>
    /// ⚠️ 已废弃：此方法返回的中位数阈值仅用于观测统计，不应用于分拣逻辑判断。
    /// 所有超时判断应基于输送线配置（ConveyorSegmentConfiguration）。
    /// 请使用 GetStatistics() 获取中位数统计信息用于观测。
    /// </remarks>
    [Obsolete("中位数阈值仅用于观测统计，不应用于分拣逻辑判断。请使用输送线配置进行判断。")]
    double? GetDynamicThreshold(int positionIndex);
    
    /// <summary>
    /// 获取当前 Position 的丢失判定阈值
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    /// <returns>丢失判定阈值（毫秒），如果数据不足则返回 null</returns>
    /// <remarks>
    /// ⚠️ 已废弃：此方法返回的中位数阈值仅用于观测统计，不应用于分拣逻辑判断。
    /// 所有丢失判定应基于输送线配置（ConveyorSegmentConfiguration.TimeToleranceMs × 1.5）。
    /// 请使用 GetStatistics() 获取中位数统计信息用于观测。
    /// </remarks>
    [Obsolete("中位数阈值仅用于分拣逻辑判断。请使用输送线配置进行判断。")]
    double? GetLostDetectionThreshold(int positionIndex);
    
    /// <summary>
    /// 清空指定 Position 的统计数据
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    void ClearStatistics(int positionIndex);
    
    /// <summary>
    /// 清空所有统计数据
    /// </summary>
    void ClearAllStatistics();
    
    /// <summary>
    /// 清除指定包裹的位置追踪记录
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <remarks>
    /// 当包裹完成分拣或检测到丢失时调用，防止残留数据影响后续包裹
    /// </remarks>
    void ClearParcelTracking(long parcelId);
    
    /// <summary>
    /// 获取最后一次包裹记录时间
    /// </summary>
    /// <returns>最后一次记录包裹位置的时间，如果从未记录则返回 null</returns>
    DateTime? GetLastParcelRecordTime();
    
    /// <summary>
    /// 检查是否应该自动清空统计数据
    /// </summary>
    /// <param name="autoClearIntervalMs">自动清空间隔（毫秒），0表示不自动清空</param>
    /// <returns>如果应该清空返回 true，否则返回 false</returns>
    bool ShouldAutoClear(int autoClearIntervalMs);
}
