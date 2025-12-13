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
    /// 记录传感器触发事件
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    /// <param name="triggeredAt">触发时间</param>
    /// <remarks>
    /// 此方法会自动计算与上次触发的间隔并记录
    /// </remarks>
    void RecordTrigger(int positionIndex, DateTime triggeredAt);
    
    /// <summary>
    /// 获取指定 Position 的统计信息
    /// </summary>
    /// <param name="positionIndex">Position 索引</param>
    /// <returns>统计信息，如果该 Position 没有数据则返回 null</returns>
    PositionIntervalStatistics? GetStatistics(int positionIndex);
    
    /// <summary>
    /// 获取所有 Position 的统计信息
    /// </summary>
    /// <returns>所有 Position 的统计信息列表</returns>
    IReadOnlyList<PositionIntervalStatistics> GetAllStatistics();
    
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
