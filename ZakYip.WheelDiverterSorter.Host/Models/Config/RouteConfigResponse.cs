using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口路由配置响应模型
/// </summary>
[SwaggerSchema(Description = "格口路由配置的完整信息，包括配置ID和时间戳")]
public class RouteConfigResponse
{
    /// <summary>
    /// 配置ID
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "配置记录的唯一标识符")]
    public int Id { get; set; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "目标格口的唯一编号")]
    public required int ChuteId { get; set; }

    /// <summary>
    /// 格口名称（可选）- Chute Name (Optional)
    /// </summary>
    /// <example>A区01号口</example>
    [SwaggerSchema(Description = "格口的友好名称")]
    public string? ChuteName { get; set; }

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    [SwaggerSchema(Description = "从入口到该格口需要经过的摆轮配置序列")]
    public required List<DiverterConfigRequest> DiverterConfigurations { get; set; }

    /// <summary>
    /// 皮带速度（毫米/秒）- Belt Speed (mm/s)
    /// </summary>
    /// <example>1000.0</example>
    [SwaggerSchema(Description = "主皮带的运行速度，单位：毫米/秒")]
    public double BeltSpeedMmPerSecond { get; set; }

    /// <summary>
    /// 皮带长度（毫米）- Belt Length (mm)
    /// </summary>
    /// <example>10000.0</example>
    [SwaggerSchema(Description = "从入口传感器到该格口的总皮带长度，单位：毫米")]
    public double BeltLengthMm { get; set; }

    /// <summary>
    /// 容差时间（毫秒）- Tolerance Time (ms)
    /// </summary>
    /// <example>2000</example>
    [SwaggerSchema(Description = "允许的时间误差范围，单位：毫秒")]
    public int ToleranceTimeMs { get; set; }

    /// <summary>
    /// 格口前触发传感器IO配置
    /// </summary>
    [SwaggerSchema(Description = "格口前的触发传感器配置")]
    public ChuteSensorConfigRequest? SensorConfig { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "是否启用此格口配置")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    /// <example>2025-11-17T08:00:00Z</example>
    [SwaggerSchema(Description = "配置的创建时间")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    /// <example>2025-11-17T10:30:00Z</example>
    [SwaggerSchema(Description = "配置的最后更新时间")]
    public DateTime UpdatedAt { get; set; }
}
