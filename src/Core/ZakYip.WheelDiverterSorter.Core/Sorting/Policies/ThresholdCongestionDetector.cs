using ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 基于阈值的拥堵检测器
/// Threshold-based congestion detector
/// </summary>
public class ThresholdCongestionDetector : ICongestionDetector
{
    private readonly ReleaseThrottleConfiguration _config;

    public ThresholdCongestionDetector(ReleaseThrottleConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 检测当前拥堵级别
    /// </summary>
    public CongestionLevel DetectCongestionLevel(CongestionMetrics metrics)
    {
        if (metrics == null)
        {
            return CongestionLevel.Normal;
        }

        // 检查是否达到严重级别（任一指标达到严重阈值）
        if (IsSevere(metrics))
        {
            return CongestionLevel.Severe;
        }

        // 检查是否达到警告级别（任一指标达到警告阈值）
        if (IsWarning(metrics))
        {
            return CongestionLevel.Warning;
        }

        return CongestionLevel.Normal;
    }

    private bool IsSevere(CongestionMetrics metrics)
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

    private bool IsWarning(CongestionMetrics metrics)
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
}
