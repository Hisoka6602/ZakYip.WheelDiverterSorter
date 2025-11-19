namespace ZakYip.WheelDiverterSorter.Core.Sorting.Events;

/// <summary>
/// 吐件指令发出事件载荷，当系统向摆轮发出实际吐件指令时触发
/// </summary>
public readonly record struct EjectIssuedEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 指令发出时间（UTC）
    /// </summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// 摆轮节点ID
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 吐件方向（Left/Right）
    /// </summary>
    public required string Direction { get; init; }

    /// <summary>
    /// 指令序号
    /// </summary>
    public int CommandSequence { get; init; }
}
