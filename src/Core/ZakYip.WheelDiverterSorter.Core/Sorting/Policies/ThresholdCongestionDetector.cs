using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 基于阈值的拥堵检测器
/// Threshold-based congestion detector
/// </summary>
/// <remarks>
/// PR-S1: 统一的拥堵检测器实现，支持 CongestionMetrics 和 CongestionSnapshot 两种输入格式。
/// </remarks>
public class ThresholdCongestionDetector : ICongestionDetector
{
    private readonly ReleaseThrottleConfiguration _config;

    public ThresholdCongestionDetector(ReleaseThrottleConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 检测当前拥堵级别（使用 CongestionMetrics）
    /// </summary>
    public CongestionLevel DetectCongestionLevel(CongestionMetrics metrics)
    {
        if (metrics == null)
        {
            return CongestionLevel.Normal;
        }

        // 检查是否达到严重级别（任一指标达到严重阈值）
        if (IsSevereMetrics(metrics))
        {
            return CongestionLevel.Severe;
        }

        // 检查是否达到警告级别（任一指标达到警告阈值）
        if (IsWarningMetrics(metrics))
        {
            return CongestionLevel.Warning;
        }

        return CongestionLevel.Normal;
    }

    /// <summary>
    /// 检测当前拥堵级别（使用 CongestionSnapshot，高性能版本）
    /// </summary>
    public CongestionLevel Detect(in CongestionSnapshot snapshot)
    {
        // 检查是否达到严重级别
        if (IsSevereSnapshot(in snapshot))
        {
            return CongestionLevel.Severe;
        }

        // 检查是否达到警告级别
        if (IsWarningSnapshot(in snapshot))
        {
            return CongestionLevel.Warning;
        }

        return CongestionLevel.Normal;
    }

    private bool IsSevereMetrics(CongestionMetrics metrics)
    {
        // 检查延迟是否超过严重阈值
        if (metrics.AverageLatencyMs >= _config.SevereThresholdLatencyMs)
        {
            return true;
        }

        // 检查成功率是否低于严重阈值
        if (metrics.TotalParcels > 0 && metrics.SuccessRate < _config.SevereThresholdSuccessRate)
        {
            return true;
        }

        // 检查在途包裹数是否超过严重阈值
        if (metrics.InFlightParcels >= _config.SevereThresholdInFlightParcels)
        {
            return true;
        }

        return false;
    }

    private bool IsWarningMetrics(CongestionMetrics metrics)
    {
        // 检查延迟是否超过警告阈值
        if (metrics.AverageLatencyMs >= _config.WarningThresholdLatencyMs)
        {
            return true;
        }

        // 检查成功率是否低于警告阈值
        if (metrics.TotalParcels > 0 && metrics.SuccessRate < _config.WarningThresholdSuccessRate)
        {
            return true;
        }

        // 检查在途包裹数是否超过警告阈值
        if (metrics.InFlightParcels >= _config.WarningThresholdInFlightParcels)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 将成功率阈值转换为失败率阈值
    /// Convert success rate threshold to failure rate threshold
    /// </summary>
    /// <param name="successRate">成功率阈值 (0.0 ~ 1.0)</param>
    /// <returns>对应的失败率阈值</returns>
    private static double ConvertSuccessRateToFailureRate(double successRate) => 1.0 - successRate;

    private bool IsSevereSnapshot(in CongestionSnapshot snapshot)
    {
        // 在途包裹数超标
        if (snapshot.InFlightParcels >= _config.SevereThresholdInFlightParcels)
        {
            return true;
        }

        // 平均延迟超标
        if (snapshot.AverageLatencyMs >= _config.SevereThresholdLatencyMs)
        {
            return true;
        }

        // 失败率超标（将 SuccessRate 阈值转换为失败率）
        double severeFailureRatio = ConvertSuccessRateToFailureRate(_config.SevereThresholdSuccessRate);
        if (snapshot.FailureRatio >= severeFailureRatio)
        {
            return true;
        }

        return false;
    }

    private bool IsWarningSnapshot(in CongestionSnapshot snapshot)
    {
        // 在途包裹数接近上限
        if (snapshot.InFlightParcels >= _config.WarningThresholdInFlightParcels)
        {
            return true;
        }

        // 平均延迟偏高
        if (snapshot.AverageLatencyMs >= _config.WarningThresholdLatencyMs)
        {
            return true;
        }

        // 失败率偏高（将 SuccessRate 阈值转换为失败率）
        double warningFailureRatio = ConvertSuccessRateToFailureRate(_config.WarningThresholdSuccessRate);
        if (snapshot.FailureRatio >= warningFailureRatio)
        {
            return true;
        }

        return false;
    }
}
