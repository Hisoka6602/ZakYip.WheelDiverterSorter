using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Execution.Health;

/// <summary>
/// 节点健康注册表实现
/// Default implementation of node health registry
/// </summary>
public class NodeHealthRegistry : INodeHealthRegistry
{
    private readonly ConcurrentDictionary<int, NodeHealthStatus> _nodeHealthStatuses = new();
    private readonly ILogger<NodeHealthRegistry>? _logger;

    /// <summary>
    /// 节点健康状态变更事件
    /// </summary>
    public event EventHandler<NodeHealthChangedEventArgs>? NodeHealthChanged;

    /// <summary>
    /// 构造函数
    /// </summary>
    public NodeHealthRegistry(ILogger<NodeHealthRegistry>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void UpdateNodeHealth(NodeHealthStatus status)
    {
        var previousStatus = _nodeHealthStatuses.TryGetValue(status.NodeId, out var prev) ? prev : (NodeHealthStatus?)null;
        
        _nodeHealthStatuses[status.NodeId] = status;

        // Log health status change
        if (previousStatus?.IsHealthy != status.IsHealthy)
        {
            if (status.IsHealthy)
            {
                _logger?.LogInformation(
                    "节点 {NodeId} ({NodeType}) 恢复健康",
                    status.NodeId, status.NodeType ?? "Unknown");
            }
            else
            {
                _logger?.LogWarning(
                    "节点 {NodeId} ({NodeType}) 变为不健康: {ErrorCode} - {ErrorMessage}",
                    status.NodeId, status.NodeType ?? "Unknown", status.ErrorCode, status.ErrorMessage);
            }
        }

        // Fire event
        NodeHealthChanged?.Invoke(this, new NodeHealthChangedEventArgs
        {
            NodeId = status.NodeId,
            NewStatus = status,
            PreviousStatus = previousStatus
        });
    }

    /// <inheritdoc/>
    public NodeHealthStatus? GetNodeHealth(int nodeId)
    {
        return _nodeHealthStatuses.TryGetValue(nodeId, out var status) ? status : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<NodeHealthStatus> GetAllNodeHealth()
    {
        return _nodeHealthStatuses.Values.ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<NodeHealthStatus> GetUnhealthyNodes()
    {
        return _nodeHealthStatuses.Values.Where(s => !s.IsHealthy).ToList();
    }

    /// <inheritdoc/>
    public bool IsNodeHealthy(int nodeId)
    {
        // If node is not registered, assume it's healthy
        if (!_nodeHealthStatuses.TryGetValue(nodeId, out var status))
        {
            return true;
        }

        return status.IsHealthy;
    }

    /// <inheritdoc/>
    public DegradationMode GetDegradationMode()
    {
        var unhealthyNodes = GetUnhealthyNodes();
        
        if (unhealthyNodes.Count == 0)
        {
            return DegradationMode.None;
        }
        
        // If more than 30% of nodes are unhealthy, consider it line degradation
        var totalNodes = _nodeHealthStatuses.Count;
        if (totalNodes > 0 && unhealthyNodes.Count >= totalNodes * 0.3)
        {
            return DegradationMode.LineDegraded;
        }

        // Otherwise, it's node degradation
        return DegradationMode.NodeDegraded;
    }
}
