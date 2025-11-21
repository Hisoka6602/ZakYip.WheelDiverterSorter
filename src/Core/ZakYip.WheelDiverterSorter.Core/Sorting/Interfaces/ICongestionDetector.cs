using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;

/// <summary>
/// 拥堵检测器接口
/// Interface for detecting system congestion based on metrics
/// </summary>
public interface ICongestionDetector
{
    /// <summary>
    /// 检测当前拥堵级别
    /// Detect current congestion level based on metrics
    /// </summary>
    /// <param name="metrics">拥堵指标 / Congestion metrics</param>
    /// <returns>当前拥堵级别 / Current congestion level</returns>
    CongestionLevel DetectCongestionLevel(CongestionMetrics metrics);
}
