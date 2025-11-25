using System;

namespace ZakYip.WheelDiverterSorter.E2ETests.Simulation;

/// <summary>
/// PR-42: 包裹仿真追踪记录
/// 记录仿真过程中包裹的关键时间点，用于验证 Parcel-First 时间顺序不变式
/// </summary>
public sealed record ParcelSimulationTrace
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required Guid ParcelId { get; init; }

    /// <summary>
    /// 包裹在本地由感应 IO 创建的时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 本地向上游发送请求（携带 ParcelId）的时间
    /// </summary>
    public DateTime UpstreamRequestedAt { get; init; }

    /// <summary>
    /// 上游返回路由（包含 ParcelId + ChuteId）的时间
    /// </summary>
    public DateTime UpstreamRepliedAt { get; init; }

    /// <summary>
    /// 本地把路由绑定到该包裹的时间
    /// </summary>
    public DateTime RouteBoundAt { get; init; }

    /// <summary>
    /// 落格确认传感器触发 / 本地确认落格的时间
    /// </summary>
    public DateTime DropConfirmedAt { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public int? TargetChuteId { get; init; }

    /// <summary>
    /// 实际落格格口ID
    /// </summary>
    public long? ActualChuteId { get; init; }

    /// <summary>
    /// 是否成功分拣到目标格口
    /// </summary>
    public bool IsSuccessful => TargetChuteId.HasValue 
        && ActualChuteId.HasValue 
        && TargetChuteId == ActualChuteId;

    /// <summary>
    /// 验证时间顺序不变式
    /// </summary>
    /// <returns>如果时间顺序正确返回 true，否则返回 false</returns>
    public bool ValidateTimeSequence()
    {
        // 严格时间顺序：t(Created) < t(UpstreamRequested) < t(UpstreamReplied) < t(RouteBound) < t(DropConfirmed)
        if (CreatedAt == default || UpstreamRequestedAt == default)
            return false;

        if (CreatedAt >= UpstreamRequestedAt)
            return false;

        if (UpstreamRepliedAt != default && UpstreamRequestedAt >= UpstreamRepliedAt)
            return false;

        if (RouteBoundAt != default)
        {
            if (UpstreamRepliedAt != default && UpstreamRepliedAt > RouteBoundAt)
                return false;

            if (CreatedAt >= RouteBoundAt)
                return false;
        }

        if (DropConfirmedAt != default && RouteBoundAt != default && RouteBoundAt >= DropConfirmedAt)
            return false;

        return true;
    }

    /// <summary>
    /// 获取时间顺序的诊断信息
    /// </summary>
    public string GetTimeSequenceDiagnostics()
    {
        return $"ParcelId={ParcelId}, " +
               $"Created={CreatedAt:HH:mm:ss.fff}, " +
               $"Requested={UpstreamRequestedAt:HH:mm:ss.fff}, " +
               $"Replied={UpstreamRepliedAt:HH:mm:ss.fff}, " +
               $"Bound={RouteBoundAt:HH:mm:ss.fff}, " +
               $"Dropped={DropConfirmedAt:HH:mm:ss.fff}, " +
               $"Valid={ValidateTimeSequence()}";
    }
}
