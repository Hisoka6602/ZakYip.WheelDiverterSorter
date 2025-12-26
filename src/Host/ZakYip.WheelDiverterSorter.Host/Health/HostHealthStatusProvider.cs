using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Diagnostics;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
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
    private readonly ILogger<HostHealthStatusProvider> _logger;

    public HostHealthStatusProvider(
        ISystemStateManager stateManager,
        ILogger<HostHealthStatusProvider> logger,
        ISystemConfigurationRepository? systemConfigRepository = null,
        IOptions<DiagnosticsOptions>? diagnosticsOptions = null,
        AlertHistoryService? alertHistoryService = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemConfigRepository = systemConfigRepository;
        _diagnosticsOptions = diagnosticsOptions?.Value;
        _alertHistoryService = alertHistoryService;
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
            // TD-043: 可从metrics或其他服务获取异常口数据
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

            // PR-34: Metrics removed (Prometheus removed)
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
    /// Update health check metrics (removed)
    /// </summary>
    private void UpdateHealthMetrics(LineHealthSnapshot snapshot)
    {
        // Metrics removed (Prometheus removed)
        // This method is now a no-op
    }
}
