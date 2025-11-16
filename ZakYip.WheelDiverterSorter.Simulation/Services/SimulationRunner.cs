using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 仿真运行器
/// </summary>
/// <remarks>
/// 负责协调整个仿真流程：生成虚拟包裹、触发检测事件、执行分拣、收集结果
/// </remarks>
public class SimulationRunner
{
    private readonly SimulationOptions _options;
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly SimulationReportPrinter _reportPrinter;
    private readonly ILogger<SimulationRunner> _logger;
    
    private readonly Dictionary<long, TaskCompletionSource<int>> _pendingAssignments = new();
    private readonly Dictionary<long, int> _parcelResults = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public SimulationRunner(
        IOptions<SimulationOptions> options,
        IRuleEngineClient ruleEngineClient,
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        SimulationReportPrinter reportPrinter,
        ILogger<SimulationRunner> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _reportPrinter = reportPrinter ?? throw new ArgumentNullException(nameof(reportPrinter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 订阅格口分配事件
        _ruleEngineClient.ChuteAssignmentReceived += OnChuteAssignmentReceived;
    }

    /// <summary>
    /// 运行仿真
    /// </summary>
    public async Task<SimulationStatistics> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始仿真...");
        _reportPrinter.PrintConfigurationSummary(_options);

        var startTime = DateTimeOffset.UtcNow;

        // 连接到RuleEngine
        var connected = await _ruleEngineClient.ConnectAsync(cancellationToken);
        if (!connected)
        {
            _logger.LogError("无法连接到RuleEngine，仿真终止");
            throw new InvalidOperationException("无法连接到RuleEngine");
        }

        _logger.LogInformation("已连接到RuleEngine（模拟）");

        // 生成并处理虚拟包裹
        var successCount = 0;
        var failureCount = 0;

        for (int i = 0; i < _options.ParcelCount; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("仿真被取消");
                break;
            }

            var parcelId = GenerateParcelId(i);
            
            if (_options.IsEnableVerboseLogging)
            {
                _logger.LogInformation("处理包裹 {Index}/{Total}，包裹ID: {ParcelId}", 
                    i + 1, _options.ParcelCount, parcelId);
            }

            try
            {
                // 模拟包裹到达并处理分拣
                var chuteId = await ProcessParcelAsync(parcelId, cancellationToken);
                
                lock (_lockObject)
                {
                    _parcelResults[parcelId] = chuteId;
                }

                successCount++;

                if (_options.IsEnableVerboseLogging)
                {
                    _logger.LogInformation("包裹 {ParcelId} 成功分拣到格口 {ChuteId}", parcelId, chuteId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理包裹 {ParcelId} 时发生错误", parcelId);
                
                lock (_lockObject)
                {
                    _parcelResults[parcelId] = (int)_options.ExceptionChuteId;
                }
                
                failureCount++;
            }

            // 等待下一个包裹到达
            if (i < _options.ParcelCount - 1)
            {
                await Task.Delay(_options.ParcelInterval, cancellationToken);
            }
        }

        var endTime = DateTimeOffset.UtcNow;
        var totalDuration = endTime - startTime;

        // 统计结果
        var statistics = GenerateStatistics(successCount, failureCount, totalDuration);
        
        // 打印报告
        _reportPrinter.PrintStatisticsReport(statistics);

        // 断开连接
        await _ruleEngineClient.DisconnectAsync();

        _logger.LogInformation("仿真完成");

        return statistics;
    }

    /// <summary>
    /// 生成包裹ID
    /// </summary>
    private long GenerateParcelId(int index)
    {
        // 使用当前时间戳加上索引作为包裹ID
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return baseTimestamp + index;
    }

    /// <summary>
    /// 处理单个包裹的分拣
    /// </summary>
    private async Task<int> ProcessParcelAsync(long parcelId, CancellationToken cancellationToken)
    {
        // 创建等待格口分配的任务
        var tcs = new TaskCompletionSource<int>();
        
        lock (_lockObject)
        {
            _pendingAssignments[parcelId] = tcs;
        }

        // 通知RuleEngine包裹到达
        var notified = await _ruleEngineClient.NotifyParcelDetectedAsync(parcelId, cancellationToken);
        
        if (!notified)
        {
            lock (_lockObject)
            {
                _pendingAssignments.Remove(parcelId);
            }
            throw new InvalidOperationException($"无法通知RuleEngine包裹 {parcelId} 到达");
        }

        // 等待格口分配（带超时）
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        try
        {
            var chuteId = await tcs.Task.WaitAsync(linkedCts.Token);
            
            // 生成并执行摆轮路径
            var path = _pathGenerator.GeneratePath(chuteId);
            
            if (path == null)
            {
                _logger.LogWarning("无法为包裹 {ParcelId} 生成到格口 {ChuteId} 的路径", parcelId, chuteId);
                return (int)_options.ExceptionChuteId;
            }
            
            // 可选：注入随机故障
            if (_options.IsEnableRandomFaultInjection)
            {
                var random = new Random();
                if (random.NextDouble() < _options.FaultInjectionProbability)
                {
                    _logger.LogWarning("注入故障：包裹 {ParcelId} 路径执行将失败", parcelId);
                    return (int)_options.ExceptionChuteId;
                }
            }
            
            var result = await _pathExecutor.ExecuteAsync(path, cancellationToken);
            
            return result.ActualChuteId;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("等待格口分配超时：包裹 {ParcelId}", parcelId);
            throw;
        }
        finally
        {
            lock (_lockObject)
            {
                _pendingAssignments.Remove(parcelId);
            }
        }
    }

    /// <summary>
    /// 处理格口分配事件
    /// </summary>
    private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentNotificationEventArgs e)
    {
        lock (_lockObject)
        {
            if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
            {
                tcs.TrySetResult(e.ChuteId);
                
                if (_options.IsEnableVerboseLogging)
                {
                    _logger.LogDebug("收到格口分配：包裹 {ParcelId} -> 格口 {ChuteId}", 
                        e.ParcelId, e.ChuteId);
                }
            }
        }
    }

    /// <summary>
    /// 生成统计数据
    /// </summary>
    private SimulationStatistics GenerateStatistics(int successCount, int failureCount, TimeSpan totalDuration)
    {
        var statistics = new SimulationStatistics
        {
            TotalParcels = _options.ParcelCount,
            SuccessfulSorts = successCount,
            FailedSorts = failureCount,
            TotalDuration = totalDuration
        };

        // 统计每个格口的分拣数量
        lock (_lockObject)
        {
            foreach (var (parcelId, chuteId) in _parcelResults)
            {
                if (!statistics.ChuteStatistics.ContainsKey(chuteId))
                {
                    statistics.ChuteStatistics[chuteId] = 0;
                }
                statistics.ChuteStatistics[chuteId]++;
            }
        }

        return statistics;
    }
}
