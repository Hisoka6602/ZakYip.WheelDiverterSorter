using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
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
    private readonly ILogger<NodeHealthMonitorService> _logger;
    private readonly ILogDeduplicator _logDeduplicator;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(10);

    public NodeHealthMonitorService(
        INodeHealthRegistry nodeHealthRegistry,
        ILogger<NodeHealthMonitorService> logger,
        ILogDeduplicator logDeduplicator,
        ISafeExecutionService safeExecutor)
    {
        _nodeHealthRegistry = nodeHealthRegistry ?? throw new ArgumentNullException(nameof(nodeHealthRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logDeduplicator = logDeduplicator ?? throw new ArgumentNullException(nameof(logDeduplicator));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));

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
                // 使用去重日志记录，防止相同健康警告在短时间内重复输出
                _logger.LogWarningDeduplicated(
                    _logDeduplicator,
                    "节点健康状态变更: NodeId={NodeId}, IsHealthy={IsHealthy}",
                    e.NodeId, e.NewStatus.IsHealthy);
            }
            else
            {
                // 使用去重日志记录健康恢复
                _logger.LogInformationDeduplicated(
                    _logDeduplicator,
                    "节点健康状态恢复: NodeId={NodeId}, IsHealthy={IsHealthy}",
                    e.NodeId, e.NewStatus.IsHealthy);
            }
        }

        // Immediately update metrics when health changes
        UpdateMetrics();
    }

    private void UpdateMetrics()
    {
        // Metrics removed for performance optimization
        // This method is now a no-op
    }

    public override void Dispose()
    {
        _nodeHealthRegistry.NodeHealthChanged -= OnNodeHealthChanged;
        base.Dispose();
    }
}
