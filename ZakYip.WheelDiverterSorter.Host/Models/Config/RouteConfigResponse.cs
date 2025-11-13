namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口路由配置响应模型
/// </summary>
public class RouteConfigResponse
{
    /// <summary>
    /// 配置ID
    /// </summary>
    /// <example>1</example>
    public int Id { get; set; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>CHUTE-01</example>
    public required string ChuteId { get; set; }

    /// <summary>
    /// 格口名称（可选）- Chute Name (Optional)
    /// </summary>
    /// <example>A区01号口</example>
    public string? ChuteName { get; set; }

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    public required List<DiverterConfigRequest> DiverterConfigurations { get; set; }

    /// <summary>
    /// 皮带速度（米/秒）- Belt Speed (m/s)
    /// </summary>
    /// <example>1.0</example>
    public double BeltSpeedMeterPerSecond { get; set; }

    /// <summary>
    /// 皮带长度（米）- Belt Length (m)
    /// </summary>
    /// <example>10.0</example>
    public double BeltLengthMeter { get; set; }

    /// <summary>
    /// 容差时间（毫秒）- Tolerance Time (ms)
    /// </summary>
    /// <example>2000</example>
    public int ToleranceTimeMs { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    /// <example>2025-11-12T16:30:00Z</example>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    /// <example>2025-11-12T16:30:00Z</example>
    public DateTime UpdatedAt { get; set; }
}
