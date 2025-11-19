using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Observability;

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
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(10);

    public NodeHealthMonitorService(
        INodeHealthRegistry nodeHealthRegistry,
        ILogger<NodeHealthMonitorService> logger,
        PrometheusMetrics? metrics = null)
    {
        _nodeHealthRegistry = nodeHealthRegistry ?? throw new ArgumentNullException(nameof(nodeHealthRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;

        // Subscribe to node health changes
        _nodeHealthRegistry.NodeHealthChanged += OnNodeHealthChanged;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("节点健康监控服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                UpdateMetrics();
                await Task.Delay(_updateInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "节点健康监控更新失败");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("节点健康监控服务已停止");
    }

    private void OnNodeHealthChanged(object? sender, NodeHealthChangedEventArgs e)
    {
        _logger.LogInformation(
            "节点健康状态变更: NodeId={NodeId}, IsHealthy={IsHealthy}",
            e.NodeId, e.NewStatus.IsHealthy);

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

            _logger.LogDebug(
                "更新降级指标: 不健康节点数={Count}, 降级模式={Mode}",
                unhealthyNodes.Count, degradationMode);
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
