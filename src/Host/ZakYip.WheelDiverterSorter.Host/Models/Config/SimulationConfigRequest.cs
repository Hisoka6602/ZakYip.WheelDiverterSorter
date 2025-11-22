using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Simulation;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 仿真配置请求模型
/// </summary>
public class SimulationConfigRequest
{
    /// <summary>
    /// 要仿真的包裹数量
    /// </summary>
    [Required(ErrorMessage = "包裹数量不能为空")]
    [Range(1, 100000, ErrorMessage = "包裹数量必须在 1-100000 之间")]
    public int ParcelCount { get; set; } = 1000;

    /// <summary>
    /// 线速（毫米/秒）
    /// </summary>
    [Required(ErrorMessage = "线速不能为空")]
    [Range(100, 10000, ErrorMessage = "线速必须在 100-10000 mm/s 之间")]
    public decimal LineSpeedMmps { get; set; } = 1000m;

    /// <summary>
    /// 包裹到达间隔（毫秒）
    /// </summary>
    [Required(ErrorMessage = "包裹间隔不能为空")]
    [Range(10, 60000, ErrorMessage = "包裹间隔必须在 10-60000 毫秒之间")]
    public int ParcelIntervalMs { get; set; } = 300;

    /// <summary>
    /// 分拣模式
    /// </summary>
    /// <remarks>
    /// 可选值：Formal（正式模式）、FixedChute（固定格口）、RoundRobin（轮询）
    /// </remarks>
    [Required(ErrorMessage = "分拣模式不能为空")]
    public string SortingMode { get; set; } = "RoundRobin";

    /// <summary>
    /// 固定格口ID列表（仅在FixedChute模式下使用）
    /// </summary>
    public List<long>? FixedChuteIds { get; set; }

    /// <summary>
    /// 异常格口ID
    /// </summary>
    [Required(ErrorMessage = "异常格口ID不能为空")]
    [Range(1, 999, ErrorMessage = "异常格口ID必须在 1-999 之间")]
    public long ExceptionChuteId { get; set; } = 21;

    /// <summary>
    /// 是否启用随机摩擦模拟
    /// </summary>
    public bool IsEnableRandomFriction { get; set; } = true;

    /// <summary>
    /// 是否启用随机掉包模拟
    /// </summary>
    public bool IsEnableRandomDropout { get; set; } = false;

    /// <summary>
    /// 摩擦模型最小因子
    /// </summary>
    [Range(0.1, 2.0, ErrorMessage = "摩擦因子必须在 0.1-2.0 之间")]
    public decimal FrictionMinFactor { get; set; } = 0.95m;

    /// <summary>
    /// 摩擦模型最大因子
    /// </summary>
    [Range(0.1, 2.0, ErrorMessage = "摩擦因子必须在 0.1-2.0 之间")]
    public decimal FrictionMaxFactor { get; set; } = 1.05m;

    /// <summary>
    /// 摩擦模型是否确定性
    /// </summary>
    public bool FrictionIsDeterministic { get; set; } = true;

    /// <summary>
    /// 摩擦模型随机种子
    /// </summary>
    public int? FrictionSeed { get; set; } = 42;

    /// <summary>
    /// 掉包概率（每段）
    /// </summary>
    [Range(0, 1, ErrorMessage = "掉包概率必须在 0-1 之间")]
    public decimal DropoutProbability { get; set; } = 0.0m;

    /// <summary>
    /// 掉包模型随机种子
    /// </summary>
    public int? DropoutSeed { get; set; } = 42;

    /// <summary>
    /// 最小空间安全间隔（头距，单位：mm）
    /// </summary>
    [Range(0, 10000, ErrorMessage = "最小空间安全间隔必须在 0-10000mm 之间")]
    public decimal? MinSafeHeadwayMm { get; set; } = 300m;

    /// <summary>
    /// 最小时间安全间隔（单位：毫秒）
    /// </summary>
    [Range(0, 60000, ErrorMessage = "最小时间安全间隔必须在 0-60000ms 之间")]
    public int? MinSafeHeadwayTimeMs { get; set; } = 300;

    /// <summary>
    /// 高密度包裹策略
    /// </summary>
    public DenseParcelStrategy DenseParcelStrategy { get; set; } = DenseParcelStrategy.RouteToException;

    /// <summary>
    /// 是否启用摆轮前传感器故障
    /// </summary>
    public bool IsPreDiverterSensorFault { get; set; } = false;

    /// <summary>
    /// 是否启用传感器抖动
    /// </summary>
    public bool IsEnableSensorJitter { get; set; } = false;

    /// <summary>
    /// 抖动触发次数
    /// </summary>
    [Range(1, 100, ErrorMessage = "抖动触发次数必须在 1-100 之间")]
    public int JitterTriggerCount { get; set; } = 3;

    /// <summary>
    /// 抖动间隔（毫秒）
    /// </summary>
    [Range(1, 10000, ErrorMessage = "抖动间隔必须在 1-10000ms 之间")]
    public int JitterIntervalMs { get; set; } = 50;

    /// <summary>
    /// 抖动概率
    /// </summary>
    [Range(0, 1, ErrorMessage = "抖动概率必须在 0-1 之间")]
    public decimal JitterProbability { get; set; } = 0.0m;

    /// <summary>
    /// 是否启用详细日志
    /// </summary>
    public bool IsEnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// 是否启用仿真模式
    /// </summary>
    /// <remarks>
    /// 当启用仿真模式时，面板按钮将触发仿真场景而非真机运行
    /// </remarks>
    public bool IsSimulationEnabled { get; set; } = false;
}
