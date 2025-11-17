using ZakYip.WheelDiverterSorter.Simulation.Configuration;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 仿真配置响应模型
/// </summary>
public class SimulationConfigResponse
{
    /// <summary>
    /// 要仿真的包裹数量
    /// </summary>
    public int ParcelCount { get; set; }

    /// <summary>
    /// 线速（毫米/秒）
    /// </summary>
    public decimal LineSpeedMmps { get; set; }

    /// <summary>
    /// 包裹到达间隔（毫秒）
    /// </summary>
    public int ParcelIntervalMs { get; set; }

    /// <summary>
    /// 分拣模式
    /// </summary>
    public string SortingMode { get; set; } = string.Empty;

    /// <summary>
    /// 固定格口ID列表（仅在FixedChute模式下使用）
    /// </summary>
    public List<long>? FixedChuteIds { get; set; }

    /// <summary>
    /// 异常格口ID
    /// </summary>
    public long ExceptionChuteId { get; set; }

    /// <summary>
    /// 是否启用随机摩擦模拟
    /// </summary>
    public bool IsEnableRandomFriction { get; set; }

    /// <summary>
    /// 是否启用随机掉包模拟
    /// </summary>
    public bool IsEnableRandomDropout { get; set; }

    /// <summary>
    /// 摩擦模型最小因子
    /// </summary>
    public decimal FrictionMinFactor { get; set; }

    /// <summary>
    /// 摩擦模型最大因子
    /// </summary>
    public decimal FrictionMaxFactor { get; set; }

    /// <summary>
    /// 摩擦模型是否确定性
    /// </summary>
    public bool FrictionIsDeterministic { get; set; }

    /// <summary>
    /// 摩擦模型随机种子
    /// </summary>
    public int? FrictionSeed { get; set; }

    /// <summary>
    /// 掉包概率（每段）
    /// </summary>
    public decimal DropoutProbability { get; set; }

    /// <summary>
    /// 掉包模型随机种子
    /// </summary>
    public int? DropoutSeed { get; set; }

    /// <summary>
    /// 最小空间安全间隔（头距，单位：mm）
    /// </summary>
    public decimal? MinSafeHeadwayMm { get; set; }

    /// <summary>
    /// 最小时间安全间隔（单位：毫秒）
    /// </summary>
    public int? MinSafeHeadwayTimeMs { get; set; }

    /// <summary>
    /// 高密度包裹策略
    /// </summary>
    public DenseParcelStrategy DenseParcelStrategy { get; set; }

    /// <summary>
    /// 是否启用摆轮前传感器故障
    /// </summary>
    public bool IsPreDiverterSensorFault { get; set; }

    /// <summary>
    /// 是否启用传感器抖动
    /// </summary>
    public bool IsEnableSensorJitter { get; set; }

    /// <summary>
    /// 抖动触发次数
    /// </summary>
    public int JitterTriggerCount { get; set; }

    /// <summary>
    /// 抖动间隔（毫秒）
    /// </summary>
    public int JitterIntervalMs { get; set; }

    /// <summary>
    /// 抖动概率
    /// </summary>
    public decimal JitterProbability { get; set; }

    /// <summary>
    /// 是否启用详细日志
    /// </summary>
    public bool IsEnableVerboseLogging { get; set; }
}
