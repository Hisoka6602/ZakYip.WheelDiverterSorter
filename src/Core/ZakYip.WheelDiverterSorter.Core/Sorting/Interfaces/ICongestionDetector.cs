using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;

/// <summary>
/// 拥堵检测器接口
/// Interface for detecting system congestion based on metrics
/// </summary>
/// <remarks>
/// PR-S1: 统一的拥堵检测器契约，支持两种输入格式。
/// - <see cref="DetectCongestionLevel"/> 方法使用 <see cref="CongestionMetrics"/> 类
/// - <see cref="Detect"/> 方法使用 <see cref="CongestionSnapshot"/> 结构体（高性能版本）
/// </remarks>
public interface ICongestionDetector
{
    /// <summary>
    /// 检测当前拥堵级别（使用 CongestionMetrics）
    /// Detect current congestion level based on metrics class
    /// </summary>
    /// <param name="metrics">拥堵指标 / Congestion metrics</param>
    /// <returns>当前拥堵级别 / Current congestion level</returns>
    CongestionLevel DetectCongestionLevel(CongestionMetrics metrics);

    /// <summary>
    /// 检测当前拥堵级别（使用 CongestionSnapshot，高性能版本）
    /// Detect current congestion level based on snapshot struct (high-performance version)
    /// </summary>
    /// <param name="snapshot">当前指标快照 / Current metrics snapshot</param>
    /// <returns>拥堵等级 / Congestion level</returns>
    CongestionLevel Detect(in CongestionSnapshot snapshot);
}
