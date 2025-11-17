namespace ZakYip.WheelDiverterSorter.Simulation.Results;

/// <summary>
/// 包裹仿真状态枚举
/// </summary>
public enum ParcelSimulationStatus
{
    /// <summary>
    /// 成功分拣到目标格口
    /// </summary>
    /// <remarks>
    /// 条件：FinalChuteId == TargetChuteId，且传感器链路完备，在 TTL 内完成
    /// </remarks>
    SortedToTargetChute,

    /// <summary>
    /// 超时（未在 TTL 内到达目标格口）
    /// </summary>
    Timeout,

    /// <summary>
    /// 包裹在中途掉落
    /// </summary>
    Dropped,

    /// <summary>
    /// 路径执行错误
    /// </summary>
    ExecutionError,

    /// <summary>
    /// 规则引擎超时（未能及时返回格口分配）
    /// </summary>
    RuleEngineTimeout,

    /// <summary>
    /// 分拣到错误格口（此状态在仿真中不应出现）
    /// </summary>
    /// <remarks>
    /// 在仿真中，此状态应该始终为 0。如果出现，说明系统存在严重问题。
    /// </remarks>
    SortedToWrongChute,

    /// <summary>
    /// 传感器故障导致无法检测包裹
    /// </summary>
    /// <remarks>
    /// 当预期包裹应通过某传感器但在 TTL 内未检测到时标记为此状态
    /// </remarks>
    SensorFault,

    /// <summary>
    /// 未经入口传感器创建的包裹（来源不明）
    /// </summary>
    /// <remarks>
    /// 包裹未通过入口传感器创建，而是从中段传感器等其他路径产生
    /// </remarks>
    UnknownSource,

    /// <summary>
    /// 间隔过近导致无法安全分拣
    /// </summary>
    /// <remarks>
    /// 包裹与前一包裹的时间或空间间隔小于安全阈值，无法在摆轮位置安全分拣，路由到异常口
    /// </remarks>
    TooCloseToSort
}
