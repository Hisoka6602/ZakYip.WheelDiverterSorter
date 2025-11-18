namespace ZakYip.WheelDiverterSorter.Core.Tracing;

/// <summary>
/// 包裹分拣审计事件参数
/// </summary>
/// <remarks>
/// 记录包裹在分拣过程中的关键生命周期事件。
/// 该记录用于审计追踪和故障排查，不用于实时查询。
/// </remarks>
public readonly record struct ParcelTraceEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public long ItemId { get; init; }

    /// <summary>
    /// 包裹条码（可选）
    /// </summary>
    public string? BarCode { get; init; }

    /// <summary>
    /// 事件发生时间（UTC）
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// 事件阶段
    /// </summary>
    /// <remarks>
    /// 典型值：
    /// - Created: 包裹在入口光电创建
    /// - UpstreamAssigned: 上游系统返回目标格口
    /// - RoutePlanned: 路径规划完成
    /// - NodeArrived: 到达关键节点
    /// - EjectCommandIssued: 发出吐件指令
    /// - Diverted: 正常落格
    /// - ExceptionDiverted: 异常口落格
    /// - OverloadDecision: 超载策略决策
    /// </remarks>
    public string Stage { get; init; }

    /// <summary>
    /// 事件来源
    /// </summary>
    /// <remarks>
    /// 典型值：
    /// - Ingress: 入口传感器
    /// - Upstream: 上游系统
    /// - Execution: 执行层路径规划/执行
    /// - OverloadPolicy: 超载策略
    /// - Simulation: 仿真环境
    /// </remarks>
    public string Source { get; init; }

    /// <summary>
    /// 事件详细信息（可选）
    /// </summary>
    /// <remarks>
    /// 短文本或 JSON 格式，记录本阶段的关键参数。
    /// 例如：入口编号、目标格口、路径节点数、异常原因等。
    /// </remarks>
    public string? Details { get; init; }
}
