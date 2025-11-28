using System.Diagnostics.Metrics;

namespace ZakYip.WheelDiverterSorter.Host.Services.Application;

/// <summary>
/// 分拣系统性能指标服务
/// </summary>
public class SorterMetrics
{
    private readonly Meter _meter;
    
    // 计数器
    private readonly Counter<long> _sortingRequestsCounter;
    private readonly Counter<long> _sortingSuccessCounter;
    private readonly Counter<long> _sortingFailureCounter;
    private readonly Counter<long> _pathGenerationCounter;
    private readonly Counter<long> _pathExecutionCounter;
    
    // 直方图（用于测量时间）
    private readonly Histogram<double> _sortingDurationHistogram;
    private readonly Histogram<double> _pathGenerationDurationHistogram;
    private readonly Histogram<double> _pathExecutionDurationHistogram;
    
    // 可观察计数器
    private readonly ObservableGauge<int> _activeRequestsGauge;
    private int _activeRequests;

    public SorterMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("ZakYip.WheelDiverterSorter");

        // 初始化计数器
        _sortingRequestsCounter = _meter.CreateCounter<long>(
            "sorter.requests.total",
            description: "分拣请求总数");

        _sortingSuccessCounter = _meter.CreateCounter<long>(
            "sorter.requests.success",
            description: "分拣成功次数");

        _sortingFailureCounter = _meter.CreateCounter<long>(
            "sorter.requests.failure",
            description: "分拣失败次数");

        _pathGenerationCounter = _meter.CreateCounter<long>(
            "sorter.path_generation.total",
            description: "路径生成总次数");

        _pathExecutionCounter = _meter.CreateCounter<long>(
            "sorter.path_execution.total",
            description: "路径执行总次数");

        // 初始化直方图
        _sortingDurationHistogram = _meter.CreateHistogram<double>(
            "sorter.requests.duration",
            unit: "ms",
            description: "分拣请求处理时长（毫秒）");

        _pathGenerationDurationHistogram = _meter.CreateHistogram<double>(
            "sorter.path_generation.duration",
            unit: "ms",
            description: "路径生成时长（毫秒）");

        _pathExecutionDurationHistogram = _meter.CreateHistogram<double>(
            "sorter.path_execution.duration",
            unit: "ms",
            description: "路径执行时长（毫秒）");

        // 初始化可观察计数器
        _activeRequestsGauge = _meter.CreateObservableGauge<int>(
            "sorter.requests.active",
            () => _activeRequests,
            description: "当前活跃的分拣请求数");
    }

    /// <summary>
    /// 记录分拣请求
    /// </summary>
    public void RecordSortingRequest()
    {
        _sortingRequestsCounter.Add(1);
        Interlocked.Increment(ref _activeRequests);
    }

    /// <summary>
    /// 记录分拣成功
    /// </summary>
    public void RecordSortingSuccess(double durationMs)
    {
        _sortingSuccessCounter.Add(1);
        _sortingDurationHistogram.Record(durationMs);
        Interlocked.Decrement(ref _activeRequests);
    }

    /// <summary>
    /// 记录分拣失败
    /// </summary>
    public void RecordSortingFailure(double durationMs)
    {
        _sortingFailureCounter.Add(1);
        _sortingDurationHistogram.Record(durationMs);
        Interlocked.Decrement(ref _activeRequests);
    }

    /// <summary>
    /// 记录路径生成
    /// </summary>
    public void RecordPathGeneration(double durationMs, bool success)
    {
        _pathGenerationCounter.Add(1);
        _pathGenerationDurationHistogram.Record(durationMs);
    }

    /// <summary>
    /// 记录路径执行
    /// </summary>
    public void RecordPathExecution(double durationMs, bool success)
    {
        _pathExecutionCounter.Add(1);
        _pathExecutionDurationHistogram.Record(durationMs);
    }
}
