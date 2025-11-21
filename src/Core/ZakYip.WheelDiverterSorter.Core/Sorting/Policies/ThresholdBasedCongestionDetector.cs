using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 基于阈值的拥堵检测器实现。
/// </summary>
public class ThresholdBasedCongestionDetector : ICongestionDetector
{
    private readonly CongestionThresholds _thresholds;

    public ThresholdBasedCongestionDetector(CongestionThresholds thresholds)
    {
        _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
    }

    public CongestionLevel Detect(in CongestionSnapshot snapshot)
    {
        // 检查是否达到严重级别
        if (IsSevere(snapshot))
        {
            return CongestionLevel.Severe;
        }

        // 检查是否达到警告级别
        if (IsWarning(snapshot))
        {
            return CongestionLevel.Warning;
        }

        return CongestionLevel.Normal;
    }

    private bool IsSevere(in CongestionSnapshot snapshot)
    {
        // 在途包裹数超标
        if (snapshot.InFlightParcels >= _thresholds.SevereInFlightParcels)
        {
            return true;
        }

        // 平均延迟超标
        if (snapshot.AverageLatencyMs >= _thresholds.SevereAverageLatencyMs)
        {
            return true;
        }

        // 失败率超标
        if (snapshot.FailureRatio >= _thresholds.SevereFailureRatio)
        {
            return true;
        }

        return false;
    }

    private bool IsWarning(in CongestionSnapshot snapshot)
    {
        // 在途包裹数接近上限
        if (snapshot.InFlightParcels >= _thresholds.WarningInFlightParcels)
        {
            return true;
        }

        // 平均延迟偏高
        if (snapshot.AverageLatencyMs >= _thresholds.WarningAverageLatencyMs)
        {
            return true;
        }

        // 失败率偏高
        if (snapshot.FailureRatio >= _thresholds.WarningFailureRatio)
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// 拥堵检测阈值配置。
/// </summary>
public class CongestionThresholds
{
    /// <summary>
    /// 警告级别：在途包裹数阈值（默认：50）。
    /// </summary>
    public int WarningInFlightParcels { get; set; } = 50;

    /// <summary>
    /// 严重级别：在途包裹数阈值（默认：100）。
    /// </summary>
    public int SevereInFlightParcels { get; set; } = 100;

    /// <summary>
    /// 警告级别：平均延迟阈值（毫秒，默认：3000）。
    /// </summary>
    public double WarningAverageLatencyMs { get; set; } = 3000;

    /// <summary>
    /// 严重级别：平均延迟阈值（毫秒，默认：5000）。
    /// </summary>
    public double SevereAverageLatencyMs { get; set; } = 5000;

    /// <summary>
    /// 警告级别：失败率阈值（0.0 ~ 1.0，默认：0.1）。
    /// </summary>
    public double WarningFailureRatio { get; set; } = 0.1;

    /// <summary>
    /// 严重级别：失败率阈值（0.0 ~ 1.0，默认：0.3）。
    /// </summary>
    public double SevereFailureRatio { get; set; } = 0.3;
}
