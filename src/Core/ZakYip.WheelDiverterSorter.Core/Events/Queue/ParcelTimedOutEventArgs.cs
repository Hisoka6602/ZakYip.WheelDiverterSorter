namespace ZakYip.WheelDiverterSorter.Core.Events.Queue;

/// <summary>
/// 包裹超时事件参数
/// </summary>
/// <remarks>
/// 当包裹在待执行队列中等待超时时触发此事件。
/// 用于拓扑驱动分拣流程中的超时包裹处理。
/// </remarks>
public sealed class ParcelTimedOutEventArgs : EventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 绑定的摆轮节点ID
    /// </summary>
    public required string WheelNodeId { get; init; }

    /// <summary>
    /// 已等待时间（毫秒）
    /// </summary>
    public required double ElapsedMs { get; init; }
}
