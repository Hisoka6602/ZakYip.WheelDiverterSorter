using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Routing;

/// <summary>
/// 路径重规划服务接口
/// Path rerouting service interface
/// </summary>
/// <remarks>
/// 当路径段失败时，尝试从后续节点重新生成路径到目标格口。
/// When a path segment fails, attempts to generate a new path from subsequent nodes to the target chute.
/// </remarks>
public interface IPathReroutingService
{
    /// <summary>
    /// 尝试为失败的包裹重新规划路径
    /// Attempts to reroute a failed parcel
    /// </summary>
    /// <param name="parcelId">包裹ID / Parcel ID</param>
    /// <param name="currentPath">当前路径 / Current path</param>
    /// <param name="failedNodeId">失败的节点ID / Failed node ID</param>
    /// <param name="failureReason">失败原因 / Failure reason</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>重规划结果 / Rerouting result</returns>
    /// <remarks>
    /// <para><strong>重规划策略：</strong></para>
    /// <list type="number">
    /// <item>确定失败节点在拓扑中的位置</item>
    /// <item>查找后续节点中能到达目标格口的节点</item>
    /// <item>如果找到可用节点，生成从该节点开始的新路径</item>
    /// <item>如果没有可用节点，返回失败，包裹将进入异常格口</item>
    /// </list>
    /// <para><strong>硬约束：</strong>宁可异常，不得错分。
    /// 如果无法安全重规划，必须返回失败。</para>
    /// </remarks>
    Task<ReroutingResult> TryRerouteAsync(
        long parcelId,
        SwitchingPath currentPath,
        int failedNodeId,
        PathFailureReason failureReason,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 路径重规划结果
/// Path rerouting result
/// </summary>
public record class ReroutingResult
{
    /// <summary>
    /// 是否成功重规划
    /// Whether rerouting was successful
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 包裹ID
    /// Parcel ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 原始目标格口ID
    /// Original target chute ID
    /// </summary>
    public required int OriginalTargetChuteId { get; init; }

    /// <summary>
    /// 新路径（如果成功）
    /// New path (if successful)
    /// </summary>
    public SwitchingPath? NewPath { get; init; }

    /// <summary>
    /// 失败原因（如果失败）
    /// Failure reason (if failed)
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 失败的节点ID
    /// Failed node ID
    /// </summary>
    public int? FailedNodeId { get; init; }

    /// <summary>
    /// 重规划时间
    /// Rerouting timestamp
    /// </summary>
    public DateTimeOffset ReroutedAt { get; init; }

    /// <summary>
    /// 创建成功的重规划结果
    /// Create a successful rerouting result
    /// </summary>
    public static ReroutingResult Success(
        long parcelId,
        int originalTargetChuteId,
        SwitchingPath newPath)
    {
        return new ReroutingResult
        {
            IsSuccess = true,
            ParcelId = parcelId,
            OriginalTargetChuteId = originalTargetChuteId,
            NewPath = newPath,
            ReroutedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// 创建失败的重规划结果
    /// Create a failed rerouting result
    /// </summary>
    public static ReroutingResult Failure(
        long parcelId,
        int originalTargetChuteId,
        int failedNodeId,
        string reason)
    {
        return new ReroutingResult
        {
            IsSuccess = false,
            ParcelId = parcelId,
            OriginalTargetChuteId = originalTargetChuteId,
            FailedNodeId = failedNodeId,
            FailureReason = reason,
            ReroutedAt = DateTimeOffset.UtcNow
        };
    }
}
