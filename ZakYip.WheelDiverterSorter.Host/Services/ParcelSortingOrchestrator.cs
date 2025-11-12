using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Ingress.Services;
using Microsoft.Extensions.Options;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 包裹分拣编排服务
/// </summary>
/// <remarks>
/// 负责协调整个分拣流程：
/// 1. 监听传感器检测到的包裹
/// 2. 通知RuleEngine包裹到达
/// 3. 等待RuleEngine推送格口分配（带超时）
/// 4. 生成摆轮路径
/// 5. 执行分拣动作
/// </remarks>
public class ParcelSortingOrchestrator : IDisposable
{
    private readonly IParcelDetectionService _parcelDetectionService;
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly ILogger<ParcelSortingOrchestrator> _logger;
    private readonly RuleEngineConnectionOptions _options;
    private readonly Dictionary<string, TaskCompletionSource<string>> _pendingAssignments;
    private readonly object _lockObject = new object();
    private bool _isConnected;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ParcelSortingOrchestrator(
        IParcelDetectionService parcelDetectionService,
        IRuleEngineClient ruleEngineClient,
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        IOptions<RuleEngineConnectionOptions> options,
        ILogger<ParcelSortingOrchestrator> logger)
    {
        _parcelDetectionService = parcelDetectionService ?? throw new ArgumentNullException(nameof(parcelDetectionService));
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _pendingAssignments = new Dictionary<string, TaskCompletionSource<string>>();

        // 订阅包裹检测事件
        _parcelDetectionService.ParcelDetected += OnParcelDetected;
        
        // 订阅格口分配事件
        _ruleEngineClient.ChuteAssignmentReceived += OnChuteAssignmentReceived;
    }

    /// <summary>
    /// 启动编排服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在启动包裹分拣编排服务...");

        // 连接到RuleEngine
        _isConnected = await _ruleEngineClient.ConnectAsync(cancellationToken);

        if (_isConnected)
        {
            _logger.LogInformation("成功连接到RuleEngine，包裹分拣编排服务已启动");
        }
        else
        {
            _logger.LogWarning("无法连接到RuleEngine，将在包裹检测时尝试重新连接");
        }
    }

    /// <summary>
    /// 停止编排服务
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("正在停止包裹分拣编排服务...");

        // 断开与RuleEngine的连接
        await _ruleEngineClient.DisconnectAsync();
        _isConnected = false;

        _logger.LogInformation("包裹分拣编排服务已停止");
    }

    /// <summary>
    /// 处理格口分配通知
    /// </summary>
    private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentNotificationEventArgs e)
    {
        lock (_lockObject)
        {
            if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
            {
                _logger.LogDebug("收到包裹 {ParcelId} 的格口分配: {ChuteNumber}", e.ParcelId, e.ChuteNumber);
                tcs.TrySetResult(e.ChuteNumber);
                _pendingAssignments.Remove(e.ParcelId);
            }
        }
    }

    /// <summary>
    /// 处理包裹检测事件
    /// </summary>
    private async void OnParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        var parcelId = e.ParcelId;
        _logger.LogInformation("检测到包裹 {ParcelId}，开始处理分拣流程", parcelId);

        try
        {
            // 步骤1: 通知RuleEngine包裹到达
            var notificationSent = await _ruleEngineClient.NotifyParcelDetectedAsync(parcelId);
            
            if (!notificationSent)
            {
                _logger.LogError(
                    "包裹 {ParcelId} 无法发送检测通知到RuleEngine，将发送到异常格口",
                    parcelId);
                await ProcessSortingAsync(parcelId, WellKnownChuteIds.Exception);
                return;
            }

            // 步骤2: 等待RuleEngine推送格口分配（带超时）
            string? targetChuteId = null;
            var tcs = new TaskCompletionSource<string>();
            
            lock (_lockObject)
            {
                _pendingAssignments[parcelId] = tcs;
            }

            try
            {
                // 等待格口分配或超时
                using var cts = new CancellationTokenSource(_options.ChuteAssignmentTimeoutMs);
                targetChuteId = await tcs.Task.WaitAsync(cts.Token);
                
                _logger.LogInformation("包裹 {ParcelId} 分配到格口 {ChuteNumber}", parcelId, targetChuteId);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "包裹 {ParcelId} 等待格口分配超时（{TimeoutMs}ms），将发送到异常格口",
                    parcelId,
                    _options.ChuteAssignmentTimeoutMs);
                targetChuteId = WellKnownChuteIds.Exception;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "包裹 {ParcelId} 等待格口分配超时（{TimeoutMs}ms），将发送到异常格口",
                    parcelId,
                    _options.ChuteAssignmentTimeoutMs);
                targetChuteId = WellKnownChuteIds.Exception;
            }
            finally
            {
                // 清理待处理的分配
                lock (_lockObject)
                {
                    _pendingAssignments.Remove(parcelId);
                }
            }

            // 步骤3: 执行分拣
            await ProcessSortingAsync(parcelId, targetChuteId!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理包裹 {ParcelId} 时发生异常", parcelId);
        }
    }

    /// <summary>
    /// 执行分拣流程
    /// </summary>
    private async Task ProcessSortingAsync(string parcelId, string targetChuteId)
    {
        try
        {
            // 生成摆轮路径
            var path = _pathGenerator.GeneratePath(targetChuteId);

            if (path == null)
            {
                _logger.LogWarning(
                    "包裹 {ParcelId} 无法生成到格口 {TargetChuteId} 的路径，将发送到异常格口",
                    parcelId,
                    targetChuteId);

                // 生成到异常格口的路径
                targetChuteId = WellKnownChuteIds.Exception;
                path = _pathGenerator.GeneratePath(targetChuteId);

                if (path == null)
                {
                    _logger.LogError("包裹 {ParcelId} 连异常格口路径都无法生成，分拣失败", parcelId);
                    return;
                }
            }

            // 执行摆轮路径
            var executionResult = await _pathExecutor.ExecuteAsync(path);

            if (executionResult.IsSuccess)
            {
                _logger.LogInformation(
                    "包裹 {ParcelId} 成功分拣到格口 {ActualChuteId}",
                    parcelId,
                    executionResult.ActualChuteId);
            }
            else
            {
                _logger.LogError(
                    "包裹 {ParcelId} 分拣失败: {FailureReason}，实际到达格口: {ActualChuteId}",
                    parcelId,
                    executionResult.FailureReason,
                    executionResult.ActualChuteId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行包裹 {ParcelId} 分拣时发生异常", parcelId);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 取消订阅事件
        _parcelDetectionService.ParcelDetected -= OnParcelDetected;
        _ruleEngineClient.ChuteAssignmentReceived -= OnChuteAssignmentReceived;

        // 断开连接
        StopAsync().GetAwaiter().GetResult();
    }
}
