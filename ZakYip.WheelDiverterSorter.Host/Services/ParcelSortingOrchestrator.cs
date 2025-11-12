using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Ingress.Services;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 包裹分拣编排服务
/// </summary>
/// <remarks>
/// 负责协调整个分拣流程：
/// 1. 监听传感器检测到的包裹
/// 2. 向RuleEngine请求格口号
/// 3. 生成摆轮路径
/// 4. 执行分拣动作
/// </remarks>
public class ParcelSortingOrchestrator : IDisposable
{
    private readonly IParcelDetectionService _parcelDetectionService;
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly ILogger<ParcelSortingOrchestrator> _logger;
    private bool _isConnected;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ParcelSortingOrchestrator(
        IParcelDetectionService parcelDetectionService,
        IRuleEngineClient ruleEngineClient,
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        ILogger<ParcelSortingOrchestrator> logger)
    {
        _parcelDetectionService = parcelDetectionService ?? throw new ArgumentNullException(nameof(parcelDetectionService));
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 订阅包裹检测事件
        _parcelDetectionService.ParcelDetected += OnParcelDetected;
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
    /// 处理包裹检测事件
    /// </summary>
    private async void OnParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        var parcelId = e.ParcelId;
        _logger.LogInformation("检测到包裹 {ParcelId}，开始处理分拣流程", parcelId);

        try
        {
            // 步骤1: 向RuleEngine请求格口号
            var response = await _ruleEngineClient.RequestChuteAssignmentAsync(parcelId);

            if (!response.IsSuccess)
            {
                _logger.LogError(
                    "包裹 {ParcelId} 获取格口号失败: {ErrorMessage}，将发送到异常格口",
                    parcelId,
                    response.ErrorMessage);

                // 使用异常格口
                response = response with { ChuteNumber = "CHUTE_EXCEPTION" };
            }

            var targetChuteId = response.ChuteNumber;
            _logger.LogInformation("包裹 {ParcelId} 分配到格口 {ChuteNumber}", parcelId, targetChuteId);

            // 步骤2: 生成摆轮路径
            var path = _pathGenerator.GeneratePath(targetChuteId);

            if (path == null)
            {
                _logger.LogWarning(
                    "包裹 {ParcelId} 无法生成到格口 {TargetChuteId} 的路径，将发送到异常格口",
                    parcelId,
                    targetChuteId);

                // 生成到异常格口的路径
                targetChuteId = "CHUTE_EXCEPTION";
                path = _pathGenerator.GeneratePath(targetChuteId);

                if (path == null)
                {
                    _logger.LogError("包裹 {ParcelId} 连异常格口路径都无法生成，分拣失败", parcelId);
                    return;
                }
            }

            // 步骤3: 执行摆轮路径
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
            _logger.LogError(ex, "处理包裹 {ParcelId} 时发生异常", parcelId);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 取消订阅事件
        _parcelDetectionService.ParcelDetected -= OnParcelDetected;

        // 断开连接
        StopAsync().GetAwaiter().GetResult();
    }
}
