using ZakYip.WheelDiverterSorter.Core.Sorting.Models;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;

/// <summary>
/// 放包节流策略接口
/// Interface for determining parcel release throttling based on congestion level
/// </summary>
public interface IReleaseThrottlePolicy
{
    /// <summary>
    /// 获取当前放包间隔（毫秒）
    /// Get current parcel release interval in milliseconds
    /// </summary>
    /// <param name="congestionLevel">拥堵级别 / Congestion level</param>
    /// <returns>放包间隔（毫秒）/ Release interval in milliseconds</returns>
    int GetReleaseIntervalMs(CongestionLevel congestionLevel);

    /// <summary>
    /// 判断是否允许创建新包裹
    /// Determine if new parcel creation is allowed
    /// </summary>
    /// <param name="congestionLevel">拥堵级别 / Congestion level</param>
    /// <returns>是否允许创建新包裹 / Whether new parcel creation is allowed</returns>
    bool AllowNewParcel(CongestionLevel congestionLevel);

    /// <summary>
    /// 判断当前是否处于暂停状态
    /// Determine if system is currently paused
    /// </summary>
    /// <param name="congestionLevel">拥堵级别 / Congestion level</param>
    /// <returns>是否暂停 / Whether system is paused</returns>
    bool IsPaused(CongestionLevel congestionLevel);
}
