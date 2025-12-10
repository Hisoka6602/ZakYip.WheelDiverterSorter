using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Execution.Health;

/// <summary>
/// 节点健康监控服务
/// Background service to monitor node health and update metrics
/// </summary>
public class NodeHealthMonitorService : BackgroundService
{
    private readonly INodeHealthRegistry _nodeHealthRegistry;
    private readonly PrometheusMetrics? _metrics;
    private readonly ILogger<NodeHealthMonitorService> _logger;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(10);
    
    // 用于跟踪上一次的健康状态，避免重复日志
    private int _lastUnhealthyNodesCount = -1;
    private DegradationMode _lastDegradationMode = (DegradationMode)(-1);

    public NodeHealthMonitorService(
        INodeHealthRegistry nodeHealthRegistry,
        ILogger<NodeHealthMonitorService> logger,
        ISafeExecutionService safeExecutor,
        PrometheusMetrics? metrics = null)
    {
        _nodeHealthRegistry = nodeHealthRegistry ?? throw new ArgumentNullException(nameof(nodeHealthRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _metrics = metrics;

        // Subscribe to node health changes
        _nodeHealthRegistry.NodeHealthChanged += OnNodeHealthChanged;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("节点健康监控服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            await _safeExecutor.ExecuteAsync(
                async () =>
                {
                    UpdateMetrics();
                    await Task.Delay(_updateInterval, stoppingToken);
                },
                "NodeHealthMonitoring",
                stoppingToken);
        }

        _logger.LogInformation("节点健康监控服务已停止");
    }

    private void OnNodeHealthChanged(object? sender, NodeHealthChangedEventArgs e)
    {
        // 只在健康状态实际发生变化时才输出日志
        bool statusChanged = e.PreviousStatus == null || e.PreviousStatus.Value.IsHealthy != e.NewStatus.IsHealthy;
        
        if (statusChanged)
        {
            if (!e.NewStatus.IsHealthy)
            {
                _logger.LogWarning(
                    "节点健康状态变更: NodeId={NodeId}, IsHealthy={IsHealthy}",
                    e.NodeId, e.NewStatus.IsHealthy);
            }
            else
            {
                _logger.LogInformation(
                    "节点健康状态恢复: NodeId={NodeId}, IsHealthy={IsHealthy}",
                    e.NodeId, e.NewStatus.IsHealthy);
            }
        }

        // Immediately update metrics when health changes
        UpdateMetrics();
    }

    private void UpdateMetrics()
    {
        if (_metrics == null)
        {
            return;
        }

        try
        {
            var unhealthyNodes = _nodeHealthRegistry.GetUnhealthyNodes();
            var degradationMode = _nodeHealthRegistry.GetDegradationMode();

            // Update Prometheus metrics
            _metrics.SetDegradedNodesTotal(unhealthyNodes.Count);
            _metrics.SetDegradedMode((int)degradationMode);

            // 只有当状态发生变化时才记录日志
            if (unhealthyNodes.Count != _lastUnhealthyNodesCount || degradationMode != _lastDegradationMode)
            {
                _logger.LogDebug(
                    "更新降级指标: 不健康节点数={Count}, 降级模式={Mode}",
                    unhealthyNodes.Count, degradationMode);
                
                _lastUnhealthyNodesCount = unhealthyNodes.Count;
                _lastDegradationMode = degradationMode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新节点健康指标失败");
        }
    }

    public override void Dispose()
    {
        _nodeHealthRegistry.NodeHealthChanged -= OnNodeHealthChanged;
        base.Dispose();
    }
}
