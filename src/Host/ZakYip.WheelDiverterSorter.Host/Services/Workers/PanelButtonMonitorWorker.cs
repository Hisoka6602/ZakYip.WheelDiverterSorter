using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Host.StateMachine;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 面板按钮监控后台服务
/// </summary>
/// <remarks>
/// 定期轮询面板按钮状态，当检测到按钮按下时触发相应的IO联动动作。
/// 轮询间隔可通过面板配置中的 PollingIntervalMs 参数配置（范围：10~5000ms）。
/// </remarks>
public sealed class PanelButtonMonitorWorker : BackgroundService
{
    private readonly ILogger<PanelButtonMonitorWorker> _logger;
    private readonly IPanelInputReader _panelInputReader;
    private readonly IIoLinkageConfigService _ioLinkageConfigService;
    private readonly ISystemStateManager _stateManager;
    private readonly IPanelConfigurationRepository _panelConfigRepository;
    private readonly ISafeExecutionService _safeExecutor;
    
    /// <summary>
    /// 默认按钮轮询间隔（毫秒）
    /// </summary>
    private const int DefaultPollingIntervalMs = 100;
    
    /// <summary>
    /// 上次按钮状态缓存，用于检测状态变化
    /// </summary>
    private readonly Dictionary<PanelButtonType, bool> _lastButtonStates = new();

    public PanelButtonMonitorWorker(
        ILogger<PanelButtonMonitorWorker> logger,
        IPanelInputReader panelInputReader,
        IIoLinkageConfigService ioLinkageConfigService,
        ISystemStateManager stateManager,
        IPanelConfigurationRepository panelConfigRepository,
        ISafeExecutionService safeExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _panelInputReader = panelInputReader ?? throw new ArgumentNullException(nameof(panelInputReader));
        _ioLinkageConfigService = ioLinkageConfigService ?? throw new ArgumentNullException(nameof(ioLinkageConfigService));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("面板按钮监控服务已启动");

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // 从配置中读取轮询间隔
                        var panelConfig = _panelConfigRepository.Get();
                        int pollingInterval = panelConfig?.PollingIntervalMs ?? DefaultPollingIntervalMs;
                        
                        // 确保轮询间隔在有效范围内（10~5000ms）
                        if (pollingInterval < 10 || pollingInterval > 5000)
                        {
                            _logger.LogWarning(
                                "面板轮询间隔 {PollingInterval}ms 超出有效范围（10~5000ms），使用默认值 {DefaultInterval}ms",
                                pollingInterval,
                                DefaultPollingIntervalMs);
                            pollingInterval = DefaultPollingIntervalMs;
                        }

                        // 读取所有按钮状态
                        var buttonStates = await _panelInputReader.ReadAllButtonStatesAsync(stoppingToken);

                        // 检查每个按钮的状态变化
                        foreach (var (buttonType, buttonState) in buttonStates)
                        {
                            // 获取上次状态
                            bool wasPressed = _lastButtonStates.GetValueOrDefault(buttonType, false);
                            bool isPressed = buttonState.IsPressed;

                            // 检测按钮从未按下到按下的状态变化（上升沿）
                            if (isPressed && !wasPressed)
                            {
                                _logger.LogInformation(
                                    "检测到面板按钮按下：{ButtonType}",
                                    buttonType);

                                // 触发IO联动
                                await TriggerIoLinkageAsync(buttonType, stoppingToken);
                            }

                            // 更新状态缓存
                            _lastButtonStates[buttonType] = isPressed;
                        }

                        // 使用配置的轮询间隔等待下一次轮询
                        await Task.Delay(pollingInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "面板按钮监控异常");
                        
                        // 发生异常后稍作延迟再继续
                        await Task.Delay(1000, stoppingToken);
                    }
                }

                _logger.LogInformation("面板按钮监控服务已停止");
            },
            "PanelButtonMonitorLoop",
            stoppingToken);
    }

    /// <summary>
    /// 触发IO联动
    /// </summary>
    private async Task TriggerIoLinkageAsync(PanelButtonType buttonType, CancellationToken cancellationToken)
    {
        try
        {
            // 获取当前系统状态
            var currentState = _stateManager.CurrentState;
            
            _logger.LogInformation(
                "触发按钮 {ButtonType} 的IO联动，当前系统状态：{SystemState}",
                buttonType,
                currentState);

            // 根据当前系统状态触发IO联动
            var result = await _ioLinkageConfigService.TriggerIoLinkageAsync(currentState);
            
            if (result.Success)
            {
                _logger.LogInformation(
                    "按钮 {ButtonType} 的IO联动触发成功，触发了 {Count} 个IO点",
                    buttonType,
                    result.TriggeredIoPoints.Count);
            }
            else
            {
                _logger.LogWarning(
                    "按钮 {ButtonType} 的IO联动触发失败：{ErrorMessage}",
                    buttonType,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "触发按钮 {ButtonType} 的IO联动异常",
                buttonType);
        }
    }
}
