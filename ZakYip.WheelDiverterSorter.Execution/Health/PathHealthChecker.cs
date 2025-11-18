using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Execution.Health;

/// <summary>
/// 路径健康检查器 - 验证路径中的所有节点是否健康
/// Path health checker - validates that all nodes in a path are healthy
/// </summary>
public class PathHealthChecker
{
    private readonly INodeHealthRegistry _nodeHealthRegistry;

    public PathHealthChecker(INodeHealthRegistry nodeHealthRegistry)
    {
        _nodeHealthRegistry = nodeHealthRegistry ?? throw new ArgumentNullException(nameof(nodeHealthRegistry));
    }

    /// <summary>
    /// 检查路径是否经过任何不健康的节点
    /// Check if path goes through any unhealthy nodes
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>路径健康验证结果</returns>
    public PathHealthResult ValidatePath(SwitchingPath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var unhealthyNodes = new List<int>();

        foreach (var segment in path.Segments)
        {
            // Map diverter ID to node ID (in this simple case, they're the same)
            // In a more complex system, you'd have a mapping service
            var nodeId = segment.DiverterId;

            if (!_nodeHealthRegistry.IsNodeHealthy(nodeId))
            {
                unhealthyNodes.Add(nodeId);
            }
        }

        if (unhealthyNodes.Count > 0)
        {
            return PathHealthResult.Unhealthy(unhealthyNodes);
        }

        return PathHealthResult.Healthy();
    }
}

/// <summary>
/// 路径健康验证结果
/// Path health validation result
/// </summary>
public class PathHealthResult
{
    /// <summary>
    /// 路径是否健康（所有节点都健康）
    /// Whether the path is healthy (all nodes healthy)
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// 不健康的节点ID列表
    /// List of unhealthy node IDs
    /// </summary>
    public IReadOnlyList<int> UnhealthyNodeIds { get; init; }

    /// <summary>
    /// 创建健康结果
    /// </summary>
    public static PathHealthResult Healthy()
    {
        return new PathHealthResult
        {
            IsHealthy = true,
            UnhealthyNodeIds = Array.Empty<int>()
        };
    }

    /// <summary>
    /// 创建不健康结果
    /// </summary>
    public static PathHealthResult Unhealthy(List<int> unhealthyNodeIds)
    {
        return new PathHealthResult
        {
            IsHealthy = false,
            UnhealthyNodeIds = unhealthyNodeIds.AsReadOnly()
        };
    }

    private PathHealthResult()
    {
        UnhealthyNodeIds = Array.Empty<int>();
    }
}
