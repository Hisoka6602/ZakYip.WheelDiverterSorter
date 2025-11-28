using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Runtime;

/// <summary>
/// 运行时性能指标收集器
/// Runtime performance metrics collector
/// </summary>
/// <remarks>
/// Collects CPU, memory, and GC metrics periodically for PR-41 performance baseline monitoring
/// </remarks>
public class RuntimePerformanceCollector : BackgroundService
{
    private readonly PrometheusMetrics _metrics;
    private readonly ILogger<RuntimePerformanceCollector> _logger;
    private readonly ISystemClock _clock;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly TimeSpan _collectionInterval;
    private int _lastGen0Count;
    private int _lastGen1Count;
    private int _lastGen2Count;

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="metrics">Prometheus指标服务</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="clock">系统时钟</param>
    /// <param name="safeExecutor">安全执行服务</param>
    /// <param name="collectionIntervalSeconds">收集间隔（秒），默认10秒</param>
    public RuntimePerformanceCollector(
        PrometheusMetrics metrics, 
        ILogger<RuntimePerformanceCollector> logger,
        ISystemClock clock,
        ISafeExecutionService safeExecutor,
        int collectionIntervalSeconds = 10)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _collectionInterval = TimeSpan.FromSeconds(collectionIntervalSeconds);
        
        // Initialize GC counts
        _lastGen0Count = GC.CollectionCount(0);
        _lastGen1Count = GC.CollectionCount(1);
        _lastGen2Count = GC.CollectionCount(2);
        
        _lastLogTime = _clock.LocalNow;
    }

    private DateTime _lastLogTime = DateTime.MinValue;

    /// <summary>
    /// 执行性能指标收集
    /// Execute performance metrics collection
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("RuntimePerformanceCollector started with collection interval: {Interval}", _collectionInterval);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                    await Task.Delay(_collectionInterval, stoppingToken);
                    CollectMetrics();
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting runtime performance metrics");
                }
            }

            _logger.LogInformation("RuntimePerformanceCollector stopped");
            },
            operationName: "RuntimePerformanceCollectorLoop",
            cancellationToken: stoppingToken
        );
    }

    /// <summary>
    /// 收集性能指标
    /// Collect performance metrics
    /// </summary>
    private void CollectMetrics()
    {
        try
        {
            // Collect process metrics
            using var process = Process.GetCurrentProcess();

            // CPU usage (approximation based on process time)
            // Note: For accurate CPU usage, consider using a more sophisticated approach
            // This is a simple implementation for baseline monitoring
            var cpuUsage = GetCpuUsage(process);
            if (cpuUsage.HasValue)
            {
                _metrics.SetCpuUsage(cpuUsage.Value);
            }

            // Memory metrics
            var workingSet = process.WorkingSet64;
            var privateMemory = process.PrivateMemorySize64;
            
            _metrics.SetWorkingSet(workingSet);
            _metrics.SetMemoryUsage(privateMemory);

            // Managed heap
            var managedHeap = GC.GetTotalMemory(forceFullCollection: false);
            _metrics.SetManagedHeap(managedHeap);

            // GC collection counts (incremental)
            var currentGen0 = GC.CollectionCount(0);
            var currentGen1 = GC.CollectionCount(1);
            var currentGen2 = GC.CollectionCount(2);

            // Record only new collections since last check
            var gen0Delta = currentGen0 - _lastGen0Count;
            var gen1Delta = currentGen1 - _lastGen1Count;
            var gen2Delta = currentGen2 - _lastGen2Count;

            for (int i = 0; i < gen0Delta; i++)
            {
                _metrics.RecordGcCollection("gen0");
            }
            for (int i = 0; i < gen1Delta; i++)
            {
                _metrics.RecordGcCollection("gen1");
            }
            for (int i = 0; i < gen2Delta; i++)
            {
                _metrics.RecordGcCollection("gen2");
            }

            // Update last counts
            _lastGen0Count = currentGen0;
            _lastGen1Count = currentGen1;
            _lastGen2Count = currentGen2;

            // Log summary periodically (every minute) - using local time for business logging
            if ((_clock.LocalNow - _lastLogTime).TotalSeconds >= 60)
            {
                _logger.LogDebug(
                    "Performance: CPU={CpuUsage:F2}%, Memory={MemoryMB:F2}MB, WorkingSet={WorkingSetMB:F2}MB, " +
                    "ManagedHeap={HeapMB:F2}MB, GC(0/1/2)={Gen0}/{Gen1}/{Gen2}",
                    cpuUsage ?? 0,
                    privateMemory / 1024.0 / 1024.0,
                    workingSet / 1024.0 / 1024.0,
                    managedHeap / 1024.0 / 1024.0,
                    currentGen0,
                    currentGen1,
                    currentGen2);
                
                _lastLogTime = _clock.LocalNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect performance metrics");
        }
    }

    private DateTime _lastCpuCheck = DateTime.MinValue;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;

    /// <summary>
    /// 获取CPU使用率（百分比）
    /// Get CPU usage percentage
    /// </summary>
    /// <remarks>
    /// This is an approximation. For production use, consider using
    /// more accurate CPU monitoring solutions.
    /// </remarks>
    private double? GetCpuUsage(Process process)
    {
        try
        {
            var currentTime = _clock.LocalNow;
            var currentTotalProcessorTime = process.TotalProcessorTime;

            if (_lastCpuCheck == DateTime.MinValue)
            {
                // First check, initialize baseline
                _lastCpuCheck = currentTime;
                _lastTotalProcessorTime = currentTotalProcessorTime;
                return null;
            }

            var timeDelta = (currentTime - _lastCpuCheck).TotalMilliseconds;
            var cpuDelta = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;

            if (timeDelta > 0)
            {
                var cpuUsagePercent = (cpuDelta / timeDelta) * 100.0 / Environment.ProcessorCount;
                
                // Update for next calculation
                _lastCpuCheck = currentTime;
                _lastTotalProcessorTime = currentTotalProcessorTime;

                return cpuUsagePercent;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
