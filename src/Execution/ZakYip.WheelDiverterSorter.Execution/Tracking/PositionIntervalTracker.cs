using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Tracking;

/// <summary>
/// Position 间隔追踪器实现
/// </summary>
/// <remarks>
/// 基于方案A+（中位数自适应超时检测）的实现：
/// - 使用滑动窗口记录每个 position 的最近 N 次触发间隔
/// - 计算中位数（抗异常值）
/// - 动态阈值 = 中位数 * 可配置系数
/// - 仅使用内存缓存，无数据库依赖
/// - 内存开销：每个 position 约 80 字节
/// </remarks>
public sealed class PositionIntervalTracker : IPositionIntervalTracker
{
    private readonly ISystemClock _clock;
    private readonly ILogger<PositionIntervalTracker> _logger;
    private readonly PositionIntervalTrackerOptions _options;
    
    // 每个 position 的间隔历史记录
    private readonly ConcurrentDictionary<int, CircularBuffer<double>> _intervalHistory;
    
    // 每个包裹在各个position的时间戳
    // Key: parcelId, Value: Dictionary<positionIndex, arrivedAt>
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<int, DateTime>> _parcelPositionTimes;
    
    // 每个 position 的最后更新时间
    private readonly ConcurrentDictionary<int, DateTime> _lastUpdatedTimes;

    public PositionIntervalTracker(
        ISystemClock clock,
        ILogger<PositionIntervalTracker> logger,
        IOptions<PositionIntervalTrackerOptions> options)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        _intervalHistory = new ConcurrentDictionary<int, CircularBuffer<double>>();
        _parcelPositionTimes = new ConcurrentDictionary<long, ConcurrentDictionary<int, DateTime>>();
        _lastUpdatedTimes = new ConcurrentDictionary<int, DateTime>();
    }

    /// <inheritdoc/>
    public void RecordInterval(int positionIndex, double intervalMs)
    {
        if (intervalMs < 0)
        {
            _logger.LogWarning(
                "忽略无效的间隔值: PositionIndex={PositionIndex}, IntervalMs={IntervalMs}",
                positionIndex, intervalMs);
            return;
        }
        
        var buffer = _intervalHistory.GetOrAdd(
            positionIndex,
            _ => new CircularBuffer<double>(_options.WindowSize));
        
        buffer.Add(intervalMs);
        _lastUpdatedTimes[positionIndex] = _clock.LocalNow;
        
        _logger.LogDebug(
            "记录 Position {PositionIndex} 触发间隔: {IntervalMs}ms",
            positionIndex, intervalMs);
    }

    /// <inheritdoc/>
    public void RecordParcelPosition(long parcelId, int positionIndex, DateTime arrivedAt)
    {
        // 获取或创建该包裹的位置时间记录
        var positionTimes = _parcelPositionTimes.GetOrAdd(
            parcelId,
            _ => new ConcurrentDictionary<int, DateTime>());
        
        // 记录当前位置的到达时间
        positionTimes[positionIndex] = arrivedAt;
        
        // 如果不是起点(position 1)，计算与前一个position的间隔
        if (positionIndex > 1)
        {
            int previousPosition = positionIndex - 1;
            
            // 尝试获取前一个position的时间
            if (positionTimes.TryGetValue(previousPosition, out var previousTime))
            {
                var intervalMs = (arrivedAt - previousTime).TotalMilliseconds;
                
                // 只记录有效的间隔
                if (intervalMs > 0 && intervalMs < _options.MaxReasonableIntervalMs)
                {
                    RecordInterval(positionIndex, intervalMs);
                    
                    _logger.LogDebug(
                        "包裹 {ParcelId} 从 Position {PrevPos} 到 Position {CurrPos} 间隔: {IntervalMs}ms",
                        parcelId, previousPosition, positionIndex, intervalMs);
                }
                else
                {
                    _logger.LogWarning(
                        "包裹 {ParcelId} 从 Position {PrevPos} 到 Position {CurrPos} 间隔异常: {IntervalMs}ms",
                        parcelId, previousPosition, positionIndex, intervalMs);
                }
            }
            else
            {
                _logger.LogDebug(
                    "包裹 {ParcelId} Position {PosIndex}: 未找到前一位置时间，跳过间隔计算",
                    parcelId, positionIndex);
            }
        }
        else
        {
            _logger.LogDebug(
                "包裹 {ParcelId} 到达起点 Position {PosIndex}，不计算间隔",
                parcelId, positionIndex);
        }
        
        // 定期清理过期的包裹记录（保留最近1000个包裹）
        if (_parcelPositionTimes.Count > 1000)
        {
            CleanupOldParcelRecords();
        }
    }

    /// <inheritdoc/>
    public (int PositionIndex, double? MedianIntervalMs, int SampleCount, double? MinIntervalMs, double? MaxIntervalMs, DateTime? LastUpdatedAt)? GetStatistics(int positionIndex)
    {
        if (!_intervalHistory.TryGetValue(positionIndex, out var buffer) || buffer.Count == 0)
        {
            return null;
        }
        
        var intervals = buffer.ToArray();
        var median = CalculateMedian(intervals);
        var min = intervals.Min();
        var max = intervals.Max();
        
        _lastUpdatedTimes.TryGetValue(positionIndex, out var lastUpdated);
        
        return (
            positionIndex,
            median,
            intervals.Length,
            min,
            max,
            lastUpdated == default ? null : lastUpdated
        );
    }

    /// <inheritdoc/>
    public IReadOnlyList<(int PositionIndex, double? MedianIntervalMs, int SampleCount, double? MinIntervalMs, double? MaxIntervalMs, DateTime? LastUpdatedAt)> GetAllStatistics()
    {
        return _intervalHistory.Keys
            .Where(k => k > 1) // 排除 positionIndex = 1 (起点没有间隔数据)
            .OrderBy(k => k)
            .Select(GetStatistics)
            .Where(stat => stat != null)
            .Select(stat => stat!.Value)
            .ToList();
    }

    /// <inheritdoc/>
    public double? GetDynamicThreshold(int positionIndex)
    {
        if (!_intervalHistory.TryGetValue(positionIndex, out var buffer))
        {
            return null;
        }
        
        // 数据不足，返回 null
        if (buffer.Count < _options.MinSamplesForThreshold)
        {
            return null;
        }
        
        var intervals = buffer.ToArray();
        var median = CalculateMedian(intervals);
        
        // 应用系数计算动态阈值
        var threshold = median * _options.TimeoutMultiplier;
        
        // 限制在合理范围内
        return Math.Clamp(threshold, _options.MinThresholdMs, _options.MaxThresholdMs);
    }

    /// <inheritdoc/>
    public void ClearStatistics(int positionIndex)
    {
        _intervalHistory.TryRemove(positionIndex, out _);
        _lastUpdatedTimes.TryRemove(positionIndex, out _);
        
        _logger.LogInformation("已清空 Position {PositionIndex} 的统计数据", positionIndex);
    }

    /// <inheritdoc/>
    public void ClearAllStatistics()
    {
        _intervalHistory.Clear();
        _lastUpdatedTimes.Clear();
        
        _logger.LogInformation("已清空所有 Position 的统计数据");
    }

    /// <summary>
    /// 计算中位数
    /// </summary>
    private static double CalculateMedian(double[] values)
    {
        if (values.Length == 0)
            return 0;
        
        var sorted = values.OrderBy(v => v).ToArray();
        int n = sorted.Length;
        
        return n % 2 == 0
            ? (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0
            : sorted[n / 2];
    }

    /// <summary>
    /// 清理旧的包裹位置记录
    /// </summary>
    /// <remarks>
    /// 当包裹记录超过1000个时，删除最旧的记录以防止内存泄漏
    /// </remarks>
    private void CleanupOldParcelRecords()
    {
        try
        {
            // 只保留最近的包裹记录（按包裹ID降序，假设ID是时间戳）
            var parcelIds = _parcelPositionTimes.Keys.OrderByDescending(id => id).ToList();
            
            // 删除超过限制的旧记录
            var toRemove = parcelIds.Skip(800).ToList(); // 保留800个，给新包裹留空间
            
            foreach (var parcelId in toRemove)
            {
                _parcelPositionTimes.TryRemove(parcelId, out _);
            }
            
            if (toRemove.Count > 0)
            {
                _logger.LogDebug(
                    "清理了 {Count} 个旧包裹的位置记录",
                    toRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理旧包裹记录时发生异常");
        }
    }
}

/// <summary>
/// Position 间隔追踪器配置选项
/// </summary>
public sealed class PositionIntervalTrackerOptions
{
    /// <summary>
    /// 历史窗口大小（保留最近 N 个间隔样本）
    /// </summary>
    /// <remarks>
    /// 默认值：10
    /// 推荐范围：10-20
    /// </remarks>
    public int WindowSize { get; set; } = 10;
    
    /// <summary>
    /// 超时系数（应用于中位数）
    /// </summary>
    /// <remarks>
    /// 动态阈值 = 中位数 * 超时系数
    /// 默认值：3.0
    /// 推荐范围：2.5-3.5
    /// </remarks>
    public double TimeoutMultiplier { get; set; } = 3.0;
    
    /// <summary>
    /// 最小阈值（毫秒）
    /// </summary>
    /// <remarks>
    /// 防止阈值过低导致误判
    /// 默认值：1000ms (1秒)
    /// </remarks>
    public double MinThresholdMs { get; set; } = 1000;
    
    /// <summary>
    /// 最大阈值（毫秒）
    /// </summary>
    /// <remarks>
    /// 防止阈值过高导致检测延迟过长
    /// 默认值：10000ms (10秒)
    /// </remarks>
    public double MaxThresholdMs { get; set; } = 10000;
    
    /// <summary>
    /// 计算动态阈值所需的最小样本数
    /// </summary>
    /// <remarks>
    /// 样本数少于此值时，不计算动态阈值
    /// 默认值：3
    /// </remarks>
    public int MinSamplesForThreshold { get; set; } = 3;
    
    /// <summary>
    /// 最大合理间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 超过此值的间隔被视为异常，不计入统计
    /// 默认值：60000ms (60秒)
    /// </remarks>
    public double MaxReasonableIntervalMs { get; set; } = 60000;
}
