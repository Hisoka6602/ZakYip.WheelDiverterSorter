using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 启动时执行系统自检的后台服务
/// </summary>
public class BootHostedService : IHostedService
{
    private readonly ISystemStateManager _stateManager;
    private readonly PrometheusMetrics? _metrics;
    private readonly ILogger<BootHostedService> _logger;
    private readonly ISafeExecutionService _safeExecutor;

    public BootHostedService(
        ISystemStateManager stateManager,
        ISafeExecutionService safeExecutor,
        ILogger<BootHostedService> logger,
        PrometheusMetrics? metrics = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
    }

    /// <summary>
    /// 启动时执行自检（非阻塞）
    /// </summary>
    /// <remarks>
    /// PR-FIX-1053: 使用即发即忘模式（fire-and-forget）避免阻塞应用启动。
    /// 自检在后台运行，不影响 Windows Service 启动响应时间。
    /// 使用 ISafeExecutionService 包裹后台任务，确保异常不会导致程序崩溃。
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 在后台执行自检，不等待完成（非阻塞）
        // 使用 SafeExecutionService 确保后台任务的异常被正确处理
        _ = _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("========== 系统启动自检（后台） ==========");

                // 执行系统自检
                var report = await _stateManager.BootAsync(cancellationToken);

                if (report.IsSuccess)
                {
                    _logger.LogInformation("✅ 自检通过，系统状态: Ready");
                    _logger.LogInformation("驱动器自检: {DriverCount} 个驱动器，全部健康", report.Drivers.Count);
                    _logger.LogInformation("上游系统检查: {UpstreamCount} 个上游端点检查完成", report.Upstreams.Count);
                    _logger.LogInformation("配置验证: {ConfigStatus}", report.Config.IsValid ? "通过" : "失败");

                    // 更新Prometheus指标
                    _metrics?.RecordSelfTestSuccess();
                    _metrics?.SetSystemState((int)_stateManager.CurrentState);
                }
                else
                {
                    _logger.LogError("❌ 自检失败，系统状态: Faulted");
                    _logger.LogError("请检查以下组件:");

                    foreach (var driver in report.Drivers.Where(d => !d.IsHealthy))
                    {
                        _logger.LogError("  - 驱动器 [{DriverName}]: {ErrorMessage}", 
                            driver.DriverName, driver.ErrorMessage);
                    }

                    foreach (var upstream in report.Upstreams.Where(u => !u.IsHealthy))
                    {
                        _logger.LogError("  - 上游系统 [{EndpointName}]: {ErrorMessage}", 
                            upstream.EndpointName, upstream.ErrorMessage);
                    }

                    if (!report.Config.IsValid)
                    {
                        _logger.LogError("  - 配置: {ErrorMessage}", report.Config.ErrorMessage);
                    }

                    // 更新Prometheus指标
                    _metrics?.RecordSelfTestFailure();
                    _metrics?.SetSystemState((int)_stateManager.CurrentState);
                }

                _logger.LogInformation("========================================");
            },
            operationName: "SystemBootSelfCheck",
            cancellationToken: cancellationToken
        );

        _logger.LogInformation("BootHostedService 已启动（自检在后台进行）");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务（无操作）
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Boot service停止");
        return Task.CompletedTask;
    }
}
