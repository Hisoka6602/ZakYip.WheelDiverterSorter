using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;

/// <summary>
/// 节点健康注册表接口
/// Interface for publishing and querying node health status
/// </summary>
public interface INodeHealthRegistry
{
    /// <summary>
    /// 注册或更新节点健康状态
    /// Register or update node health status
    /// </summary>
    /// <param name="status">节点健康状态</param>
    void UpdateNodeHealth(NodeHealthStatus status);

    /// <summary>
    /// 获取指定节点的健康状态
    /// Get health status for specific node
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <returns>节点健康状态，如果节点不存在则返回null</returns>
    NodeHealthStatus? GetNodeHealth(long nodeId);

    /// <summary>
    /// 获取所有节点的健康状态
    /// Get health status for all nodes
    /// </summary>
    /// <returns>所有节点的健康状态列表</returns>
    IReadOnlyList<NodeHealthStatus> GetAllNodeHealth();

    /// <summary>
    /// 获取所有不健康的节点
    /// Get all unhealthy nodes
    /// </summary>
    /// <returns>不健康节点列表</returns>
    IReadOnlyList<NodeHealthStatus> GetUnhealthyNodes();

    /// <summary>
    /// 检查指定节点是否健康
    /// Check if specific node is healthy
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <returns>节点是否健康，如果节点不存在则返回true（假设未注册的节点是健康的）</returns>
    bool IsNodeHealthy(long nodeId);

    /// <summary>
    /// 获取当前降级模式
    /// Get current degradation mode
    /// </summary>
    /// <returns>当前降级模式</returns>
    DegradationMode GetDegradationMode();

    /// <summary>
    /// 节点健康状态变更事件
    /// Event fired when node health status changes
    /// </summary>
    event EventHandler<NodeHealthChangedEventArgs>? NodeHealthChanged;
}

/// <summary>
/// 节点健康状态变更事件参数
/// Event args for node health status changes
/// </summary>
public class NodeHealthChangedEventArgs : EventArgs
{
    /// <summary>
    /// 节点ID
    /// </summary>
    public required long NodeId { get; init; }

    /// <summary>
    /// 新的健康状态
    /// </summary>
    public required NodeHealthStatus NewStatus { get; init; }

    /// <summary>
    /// 之前的健康状态（如果存在）
    /// </summary>
    public NodeHealthStatus? PreviousStatus { get; init; }
}
