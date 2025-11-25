using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Host.Health;

/// <summary>
/// 健康状态提供器实现 / Health Status Provider Implementation
/// 从 SystemStateManager、AlertHistoryService 等服务收集并聚合健康状态
/// Collects and aggregates health status from SystemStateManager, AlertHistoryService and other services
/// </summary>
public class HostHealthStatusProvider : IHealthStatusProvider
{
    private readonly ISystemStateManager _stateManager;
    private readonly ISystemConfigurationRepository? _systemConfigRepository;
    private readonly DiagnosticsOptions? _diagnosticsOptions;
    private readonly AlertHistoryService? _alertHistoryService;
    private readonly PrometheusMetrics? _prometheusMetrics;
    private readonly ILogger<HostHealthStatusProvider> _logger;

    public HostHealthStatusProvider(
        ISystemStateManager stateManager,
        ILogger<HostHealthStatusProvider> logger,
        ISystemConfigurationRepository? systemConfigRepository = null,
        IOptions<DiagnosticsOptions>? diagnosticsOptions = null,
        AlertHistoryService? alertHistoryService = null,
        PrometheusMetrics? prometheusMetrics = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemConfigRepository = systemConfigRepository;
        _diagnosticsOptions = diagnosticsOptions?.Value;
        _alertHistoryService = alertHistoryService;
        _prometheusMetrics = prometheusMetrics;
    }

    /// <inheritdoc />
    public Task<LineHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var currentState = _stateManager.CurrentState;
            var lastReport = _stateManager.LastSelfTestReport;

            // 计算线体可用性
            var isLineAvailable = (currentState == SystemState.Ready || currentState == SystemState.Running) &&
                                  (lastReport?.IsSuccess ?? true);

            // 提取降级信息
            var degradationMode = lastReport?.DegradationMode ?? DegradationMode.None;
            var degradedNodes = lastReport?.NodeStatuses?
                .Where(n => !n.IsHealthy)
                .ToList();

            // 获取最近Critical告警数量
            var recentCriticalAlertCount = _alertHistoryService?.GetRecentCriticalAlerts(10).Count ?? 0;

            // 计算异常口比例 (如果有相关数据)
            double? exceptionChuteRatio = null;
            // TODO: 可从metrics或其他服务获取异常口数据
            // exceptionChuteRatio = CalculateExceptionChuteRatio();

            var snapshot = new LineHealthSnapshot
            {
                SystemState = currentState,
                IsSelfTestSuccess = lastReport?.IsSuccess ?? false,
                LastSelfTestAt = lastReport?.PerformedAt,
                Drivers = lastReport?.Drivers?.ToList(),
                Upstreams = lastReport?.Upstreams?.ToList(),
                Config = lastReport?.Config,
                DegradationMode = degradationMode,
                DegradedNodesCount = degradedNodes?.Count ?? 0,
                DegradedNodes = degradedNodes,
                DiagnosticsLevel = _diagnosticsOptions?.Level ?? DiagnosticsLevel.Basic,
                ConfigVersion = _systemConfigRepository?.Get()?.ConfigName,
                RecentCriticalAlertCount = recentCriticalAlertCount,
                CurrentCongestionLevel = GetCongestionLevelFromState(currentState),
                IsLineAvailable = isLineAvailable,
                ExceptionChuteRatio = exceptionChuteRatio
            };

            // PR-34: 更新 Prometheus 健康检查指标
            UpdateHealthMetrics(snapshot);

            return Task.FromResult(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health snapshot");
            
            // 返回最小健康快照
            return Task.FromResult(new LineHealthSnapshot
            {
                SystemState = SystemState.Faulted,
                IsSelfTestSuccess = false,
                LastSelfTestAt = null,
                RecentCriticalAlertCount = 0,
                IsLineAvailable = false
            });
        }
    }

    private static CongestionLevel? GetCongestionLevelFromState(SystemState state)
    {
        return state switch
        {
            SystemState.Faulted => CongestionLevel.Severe,
            SystemState.EmergencyStop => CongestionLevel.Severe,
            _ => CongestionLevel.Normal
        };
    }

    /// <summary>
    /// 更新 Prometheus 健康检查指标
    /// Update Prometheus health check metrics
    /// </summary>
    private void UpdateHealthMetrics(LineHealthSnapshot snapshot)
    {
        if (_prometheusMetrics == null)
        {
            return;
        }

        try
        {
            // 更新整体健康检查状态
            var isReady = snapshot.IsLineAvailable && snapshot.IsSelfTestSuccess;
            var ruleEngineHealthy = snapshot.Upstreams?.All(u => u.IsHealthy) ?? true;
            var driversHealthy = snapshot.Drivers?.All(d => d.IsHealthy) ?? true;
            isReady = isReady && ruleEngineHealthy && driversHealthy;

            _prometheusMetrics.SetHealthCheckStatus("live", true); // 能执行到这里说明进程存活
            _prometheusMetrics.SetHealthCheckStatus("startup", _stateManager.CurrentState != SystemState.Booting);
            _prometheusMetrics.SetHealthCheckStatus("ready", isReady);

            // 更新 RuleEngine 连接健康状态
            if (snapshot.Upstreams != null)
            {
                foreach (var upstream in snapshot.Upstreams)
                {
                    _prometheusMetrics.SetUpstreamHealthStatus(upstream.EndpointName, upstream.IsHealthy);
                    // 如果是 RuleEngine 连接，也更新专用指标
                    if (upstream.EndpointName.StartsWith("RuleEngine", StringComparison.OrdinalIgnoreCase))
                    {
                        var connectionType = upstream.EndpointName.Replace("RuleEngine-", "");
                        _prometheusMetrics.SetRuleEngineConnectionHealth(connectionType, upstream.IsHealthy);
                    }
                }
            }

            // 更新驱动器健康状态
            if (snapshot.Drivers != null)
            {
                foreach (var driver in snapshot.Drivers)
                {
                    _prometheusMetrics.SetDriverHealthStatus(driver.DriverName, driver.IsHealthy);
                }
            }

            // TODO PR-34: 更新 TTL 调度器健康状态
            // 当前暂时设置为健康，待实现 TTL 调度器健康检查
            _prometheusMetrics.SetTtlSchedulerHealth(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新 Prometheus 健康检查指标失败");
        }
    }
}
