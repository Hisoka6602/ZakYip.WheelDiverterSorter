using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;

namespace ZakYip.WheelDiverterSorter.Execution.Infrastructure;

/// <summary>
/// 系统状态与 IO 联动协调服务。
/// 负责在系统状态变更时自动触发相应的 IO 联动操作。
/// </summary>
public class SystemStateIoLinkageService
{
    private readonly ISystemRunStateService _stateService;
    private readonly IIoLinkageCoordinator _linkageCoordinator;
    private readonly IIoLinkageExecutor _linkageExecutor;
    private readonly IWheelDiverterDriverManager? _wheelDiverterDriverManager;
    private readonly IoLinkageOptions _linkageOptions;
    private readonly ILogger<SystemStateIoLinkageService> _logger;

    public SystemStateIoLinkageService(
        ISystemRunStateService stateService,
        IIoLinkageCoordinator linkageCoordinator,
        IIoLinkageExecutor linkageExecutor,
        IOptions<SystemConfiguration> systemConfig,
        ILogger<SystemStateIoLinkageService> logger,
        IWheelDiverterDriverManager? wheelDiverterDriverManager = null)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        _linkageCoordinator = linkageCoordinator ?? throw new ArgumentNullException(nameof(linkageCoordinator));
        _linkageExecutor = linkageExecutor ?? throw new ArgumentNullException(nameof(linkageExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wheelDiverterDriverManager = wheelDiverterDriverManager;

        if (systemConfig?.Value == null)
        {
            throw new ArgumentNullException(nameof(systemConfig));
        }

        _linkageOptions = systemConfig.Value.IoLinkage ?? new IoLinkageOptions();
    }

    /// <summary>
    /// 处理启动按钮事件（包含状态切换和 IO 联动）
    /// </summary>
    public async Task<OperationResult> HandleStartAsync(CancellationToken cancellationToken = default)
    {
        // 1. 尝试状态切换
        var stateResult = _stateService.TryHandleStart();
        if (!stateResult.IsSuccess)
        {
            // 状态切换失败，不执行 IO 联动
            return stateResult;
        }

        // 2. 状态切换成功，执行 IO 联动
        var currentState = _stateService.Current;
        var linkagePoints = _linkageCoordinator.DetermineIoLinkagePoints(currentState, _linkageOptions);

        if (linkagePoints.Count > 0)
        {
            _logger.LogInformation("系统状态切换为 {State}，准备执行 {Count} 个 IO 联动点", currentState, linkagePoints.Count);
            var ioResult = await _linkageExecutor.ExecuteAsync(linkagePoints, cancellationToken);

            if (!ioResult.IsSuccess)
            {
                _logger.LogError("IO 联动执行失败: {ErrorMessage}", ioResult.ErrorMessage);
                // IO 执行失败时，仍然返回成功（状态已切换），但记录错误
            }
        }

        // 3. 启动所有已连接的摆轮
        await RunAllWheelDivertersAsync(cancellationToken);

        return OperationResult.Success();
    }

    /// <summary>
    /// 处理停止按钮事件（包含状态切换和 IO 联动）
    /// </summary>
    public async Task<OperationResult> HandleStopAsync(CancellationToken cancellationToken = default)
    {
        // 1. 尝试状态切换
        var stateResult = _stateService.TryHandleStop();
        if (!stateResult.IsSuccess)
        {
            // 状态切换失败，不执行 IO 联动
            return stateResult;
        }

        // 2. 停止所有摆轮
        await StopAllWheelDivertersAsync(cancellationToken);

        // 3. 执行 IO 联动
        var currentState = _stateService.Current;
        var linkagePoints = _linkageCoordinator.DetermineIoLinkagePoints(currentState, _linkageOptions);

        if (linkagePoints.Count > 0)
        {
            _logger.LogInformation("系统状态切换为 {State}，准备执行 {Count} 个 IO 联动点", currentState, linkagePoints.Count);
            var ioResult = await _linkageExecutor.ExecuteAsync(linkagePoints, cancellationToken);

            if (!ioResult.IsSuccess)
            {
                _logger.LogError("IO 联动执行失败: {ErrorMessage}", ioResult.ErrorMessage);
            }
        }

        return OperationResult.Success();
    }

    /// <summary>
    /// 处理急停按钮事件（包含状态切换和 IO 联动）
    /// </summary>
    public async Task<OperationResult> HandleEmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        // 1. 尝试状态切换
        var stateResult = _stateService.TryHandleEmergencyStop();
        if (!stateResult.IsSuccess)
        {
            // 状态切换失败，不执行 IO 联动
            return stateResult;
        }

        // 2. 停止所有摆轮（急停）
        _logger.LogWarning("急停触发！");
        await StopAllWheelDivertersAsync(cancellationToken);

        // 3. 状态切换成功，执行停止联动 IO（急停时使用停止联动 IO）
        var currentState = _stateService.Current;
        
        // 急停时直接使用 StoppedStateIos
        IReadOnlyList<IoLinkagePoint> linkagePoints = _linkageOptions.Enabled 
            ? _linkageOptions.StoppedStateIos.AsReadOnly()
            : Array.Empty<IoLinkagePoint>();

        if (linkagePoints.Count > 0)
        {
            _logger.LogWarning("急停触发！准备执行 {Count} 个停止联动 IO", linkagePoints.Count);
            var ioResult = await _linkageExecutor.ExecuteAsync(linkagePoints, cancellationToken);

            if (!ioResult.IsSuccess)
            {
                _logger.LogError("急停 IO 联动执行失败: {ErrorMessage}", ioResult.ErrorMessage);
            }
        }

        return OperationResult.Success();
    }

    /// <summary>
    /// 处理急停复位事件（包含状态切换，不执行 IO 联动）
    /// </summary>
    public OperationResult HandleEmergencyReset()
    {
        // 急停复位只切换状态，不执行 IO 联动
        return _stateService.TryHandleEmergencyReset();
    }

    /// <summary>
    /// 获取当前系统运行状态
    /// </summary>
    public SystemOperatingState GetCurrentState()
    {
        return _stateService.Current;
    }

    /// <summary>
    /// 验证当前状态是否允许创建包裹
    /// </summary>
    public OperationResult ValidateParcelCreation()
    {
        return _stateService.ValidateParcelCreation();
    }

    /// <summary>
    /// 启动所有已连接的摆轮
    /// </summary>
    private async Task RunAllWheelDivertersAsync(CancellationToken cancellationToken)
    {
        if (_wheelDiverterDriverManager == null)
        {
            return;
        }

        var activeDrivers = _wheelDiverterDriverManager.GetActiveDrivers();
        if (activeDrivers.Count == 0)
        {
            return;
        }

        _logger.LogInformation("系统启动，准备启动 {Count} 个摆轮", activeDrivers.Count);
        await ExecuteWheelDiverterOperationAsync(activeDrivers, 
            (driver, ct) => driver.RunAsync(ct), 
            "启动", cancellationToken);
    }

    /// <summary>
    /// 停止所有摆轮
    /// </summary>
    private async Task StopAllWheelDivertersAsync(CancellationToken cancellationToken)
    {
        if (_wheelDiverterDriverManager == null)
        {
            return;
        }

        var activeDrivers = _wheelDiverterDriverManager.GetActiveDrivers();
        if (activeDrivers.Count == 0)
        {
            return;
        }

        _logger.LogInformation("准备停止 {Count} 个摆轮", activeDrivers.Count);
        await ExecuteWheelDiverterOperationAsync(activeDrivers, 
            (driver, ct) => driver.StopAsync(ct), 
            "停止", cancellationToken);
    }

    /// <summary>
    /// 执行摆轮操作（避免代码重复）
    /// </summary>
    private async Task ExecuteWheelDiverterOperationAsync(
        IReadOnlyDictionary<string, IWheelDiverterDriver> drivers,
        Func<IWheelDiverterDriver, CancellationToken, Task<bool>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var failedDriverIds = new List<string>();

        foreach (var kvp in drivers)
        {
            try
            {
                var success = await operation(kvp.Value, cancellationToken);
                if (success)
                {
                    successCount++;
                    _logger.LogDebug("摆轮 {DiverterId} {Operation}成功", kvp.Key, operationName);
                }
                else
                {
                    failedDriverIds.Add(kvp.Key);
                    _logger.LogWarning("摆轮 {DiverterId} {Operation}失败", kvp.Key, operationName);
                }
            }
            catch (Exception ex)
            {
                failedDriverIds.Add(kvp.Key);
                _logger.LogError(ex, "摆轮 {DiverterId} {Operation}异常", kvp.Key, operationName);
            }
        }

        if (failedDriverIds.Count > 0)
        {
            _logger.LogWarning(
                "部分摆轮{Operation}失败: 成功={SuccessCount}/{TotalCount}, 失败摆轮={FailedIds}",
                operationName, successCount, drivers.Count, string.Join(", ", failedDriverIds));
        }
        else
        {
            _logger.LogInformation("所有摆轮{Operation}成功: {Count} 个", operationName, successCount);
        }
    }
}
