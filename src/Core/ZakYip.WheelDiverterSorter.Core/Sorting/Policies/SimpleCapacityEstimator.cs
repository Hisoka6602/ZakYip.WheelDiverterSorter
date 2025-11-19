using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 简单产能估算器实现：基于成功率和延迟计算安全产能区间。
/// </summary>
public class SimpleCapacityEstimator : ICapacityEstimator
{
    private readonly CapacityEstimationThresholds _thresholds;

    public SimpleCapacityEstimator(CapacityEstimationThresholds thresholds)
    {
        _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
    }

    public CapacityEstimationResult Estimate(in CapacityHistory history)
    {
        if (history.TestResults == null || history.TestResults.Count == 0)
        {
            return new CapacityEstimationResult
            {
                SafeMinParcelsPerMinute = 0,
                SafeMaxParcelsPerMinute = 0,
                DangerousThresholdParcelsPerMinute = 0,
                Confidence = 0,
                DataPointCount = 0
            };
        }

        // 计算每个测试点的包裹/分钟
        var dataPoints = history.TestResults
            .Select(r => new
            {
                ParcelsPerMinute = 60000.0 / r.IntervalMs, // 60秒 / 间隔(ms)
                SuccessRate = r.SuccessRate,
                AvgLatency = r.AverageLatencyMs,
                ExceptionRate = r.ExceptionRate
            })
            .OrderBy(d => d.ParcelsPerMinute)
            .ToList();

        // 找到成功率高于阈值的最大产能
        var safePoints = dataPoints
            .Where(d => d.SuccessRate >= _thresholds.MinSuccessRate && 
                       d.AvgLatency <= _thresholds.MaxAcceptableLatencyMs &&
                       d.ExceptionRate <= _thresholds.MaxExceptionRate)
            .ToList();

        double safeMin = 0;
        double safeMax = 0;
        
        if (safePoints.Any())
        {
            safeMin = safePoints.Min(p => p.ParcelsPerMinute);
            safeMax = safePoints.Max(p => p.ParcelsPerMinute);
        }

        // 找到成功率开始下降的临界点作为危险阈值
        var dangerousThreshold = dataPoints
            .FirstOrDefault(d => d.SuccessRate < _thresholds.MinSuccessRate || 
                                d.ExceptionRate > _thresholds.MaxExceptionRate)
            ?.ParcelsPerMinute ?? (safeMax > 0 ? safeMax * 1.2 : 0);

        // 计算置信度：数据点越多，置信度越高
        double confidence = Math.Min(1.0, dataPoints.Count / 10.0);

        return new CapacityEstimationResult
        {
            SafeMinParcelsPerMinute = safeMin,
            SafeMaxParcelsPerMinute = safeMax,
            DangerousThresholdParcelsPerMinute = dangerousThreshold,
            Confidence = confidence,
            DataPointCount = dataPoints.Count
        };
    }
}

/// <summary>
/// 产能估算阈值配置。
/// </summary>
public class CapacityEstimationThresholds
{
    /// <summary>
    /// 最低可接受成功率（默认：0.95）。
    /// </summary>
    public double MinSuccessRate { get; set; } = 0.95;

    /// <summary>
    /// 最大可接受平均延迟（毫秒，默认：3000）。
    /// </summary>
    public double MaxAcceptableLatencyMs { get; set; } = 3000;

    /// <summary>
    /// 最大可接受异常率（默认：0.05）。
    /// </summary>
    public double MaxExceptionRate { get; set; } = 0.05;
}
