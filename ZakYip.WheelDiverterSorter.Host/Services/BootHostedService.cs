using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 启动时执行系统自检的后台服务
/// </summary>
public class BootHostedService : IHostedService
{
    private readonly ISystemStateManager _stateManager;
    private readonly PrometheusMetrics? _metrics;
    private readonly ILogger<BootHostedService> _logger;

    public BootHostedService(
        ISystemStateManager stateManager,
        ILogger<BootHostedService> logger,
        PrometheusMetrics? metrics = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
    }

    /// <summary>
    /// 启动时执行自检
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("========== 系统启动自检 ==========");

        try
        {
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "系统自检过程中发生异常");
            _metrics?.RecordSelfTestFailure();
            _metrics?.SetSystemState((int)SystemState.Faulted);
        }
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
