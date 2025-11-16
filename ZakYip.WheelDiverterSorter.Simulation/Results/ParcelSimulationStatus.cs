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
    SortedToWrongChute
}
