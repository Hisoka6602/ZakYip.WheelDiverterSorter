namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 表示格口路由配置，包含从入口到目标格口的摆轮配置列表
/// </summary>
public class ChuteRouteConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 目标格口标识（数字ID，与上游系统对应）
    /// </summary>
    public required long ChuteId { get; set; }

    /// <summary>
    /// 格口名称（可选）- Chute Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "A区01号口"、"CHUTE-01"
    /// </remarks>
    public string? ChuteName { get; set; }

    /// <summary>
    /// 摆轮配置列表，按顺序执行
    /// </summary>
    public required List<DiverterConfigurationEntry> DiverterConfigurations { get; set; }

    /// <summary>
    /// 皮带速度（毫米/秒）- Belt Speed (mm/s)
    /// </summary>
    /// <remarks>
    /// 用于计算包裹到达格口的预期时间
    /// </remarks>
    public double BeltSpeedMmPerSecond { get; set; } = 1000.0;

    /// <summary>
    /// 皮带长度（毫米）- Belt Length (mm)
    /// </summary>
    /// <remarks>
    /// 从上一个检测点（入口或上一个格口）到此格口的距离
    /// </remarks>
    public double BeltLengthMm { get; set; } = 10000.0;

    /// <summary>
    /// 容差时间（毫秒）- Tolerance Time (ms)
    /// </summary>
    /// <remarks>
    /// 允许的时间误差范围，用于判断包裹是否超时到达或丢失
    /// 包裹实际到达时间 = 理论时间 ± 容差时间
    /// </remarks>
    public int ToleranceTimeMs { get; set; } = 2000;

    /// <summary>
    /// 格口前触发传感器IO配置 - Chute Trigger Sensor IO Configuration
    /// </summary>
    /// <remarks>
    /// 每个格口前面都有一个触发传感器，用于检测包裹到达
    /// </remarks>
    public ChuteSensorConfig? SensorConfig { get; set; }

    /// <summary>
    /// 配置创建时间（本地时间）
    /// </summary>
    /// <remarks>
    /// 由仓储在创建时通过 ISystemClock.LocalNow 设置
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 配置最后更新时间（本地时间）
    /// </summary>
    /// <remarks>
    /// 由仓储在更新时通过 ISystemClock.LocalNow 设置
    /// </remarks>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 是否启用此配置
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
