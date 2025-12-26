using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Execution.SelfTest;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Application.Services.Health;

/// <summary>
/// 基于 SystemSelfTestCoordinator 的运行前健康检查适配器
/// Pre-run health check adapter based on SystemSelfTestCoordinator
/// </summary>
/// <remarks>
/// 此适配器将 SystemSelfTestCoordinator 的详细自检报告转换为
/// PreRunHealthCheckResult 格式，以保持API向后兼容
/// </remarks>
public class PreRunHealthCheckAdapter : IPreRunHealthCheckService
{
    private readonly ISelfTestCoordinator _selfTestCoordinator;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<PreRunHealthCheckAdapter> _logger;

    public PreRunHealthCheckAdapter(
        ISelfTestCoordinator selfTestCoordinator,
        ISafeExecutionService safeExecutor,
        ILogger<PreRunHealthCheckAdapter> logger)
    {
        _selfTestCoordinator = selfTestCoordinator ?? throw new ArgumentNullException(nameof(selfTestCoordinator));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PreRunHealthCheckResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("开始运行前健康检查（使用 SystemSelfTestCoordinator）...");

                // 执行完整的系统自检
                var selfTestReport = await _selfTestCoordinator.RunAsync(cancellationToken);

                // 将自检报告转换为 PreRunHealthCheckResult
                var checks = new List<HealthCheckItem>();

                // 1. 配置检查
                checks.Add(new HealthCheckItem
                {
                    Name = "ConfigurationValid",
                    Status = selfTestReport.Config.IsValid ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                    Message = selfTestReport.Config.IsValid
                        ? "系统配置验证成功（包括异常口、面板IO、拓扑、上游配置）"
                        : selfTestReport.Config.ErrorMessage ?? "系统配置验证失败"
                });

                // 2. 驱动器检查 - 聚合所有驱动器的状态
                var driverChecks = selfTestReport.Drivers
                    .Select(d => new HealthCheckItem
                    {
                        Name = $"Driver_{d.DriverName.Replace(" ", "_").Replace("（", "").Replace("）", "")}",
                        Status = d.IsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                        Message = d.ErrorMessage ?? (d.IsHealthy ? $"{d.DriverName} 已就绪" : $"{d.DriverName} 不健康")
                    })
                    .ToList();

                checks.AddRange(driverChecks);

                // 3. 上游连接检查 - 但不作为必需项
                var upstreamChecks = selfTestReport.Upstreams
                    .Select(u => new HealthCheckItem
                    {
                        Name = $"Upstream_{u.EndpointName.Replace("-", "_")}",
                        Status = u.IsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,  // 上游连接失败标记为 Unhealthy
                        Message = u.ErrorMessage ?? (u.IsHealthy ? $"{u.EndpointName} 已连接" : $"{u.EndpointName} 未连接（非必需）")
                    })
                    .ToList();

                checks.AddRange(upstreamChecks);

                // 计算整体状态 - 只考虑配置和驱动器，上游连接是可选的
                var overallStatus = selfTestReport.Config.IsValid && 
                                  selfTestReport.Drivers.All(d => d.IsHealthy)
                    ? HealthStatus.Healthy
                    : HealthStatus.Unhealthy;

                _logger.LogInformation("运行前健康检查完成，整体状态: {OverallStatus}", overallStatus);

                return new PreRunHealthCheckResult
                {
                    OverallStatus = overallStatus,
                    Checks = checks
                };
            },
            operationName: "PreRunHealthCheck",
            defaultValue: new PreRunHealthCheckResult
            {
                OverallStatus = HealthStatus.Unhealthy,
                Checks = new List<HealthCheckItem>
                {
                    new HealthCheckItem
                    {
                        Name = "PreRunCheck",
                        Status = HealthStatus.Unhealthy,
                        Message = "运行前健康检查执行时发生异常"
                    }
                }
            },
            cancellationToken: cancellationToken
        );
    }
}
