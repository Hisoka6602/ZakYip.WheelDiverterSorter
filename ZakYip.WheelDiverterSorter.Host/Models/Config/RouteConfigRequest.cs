using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口路由配置请求模型
/// </summary>
public class RouteConfigRequest
{
    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>1</example>
    [Required(ErrorMessage = "格口ID不能为空")]
    public required int ChuteId { get; set; }

    /// <summary>
    /// 格口名称（可选）- Chute Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "A区01号口"
    /// </remarks>
    /// <example>A区01号口</example>
    public string? ChuteName { get; set; }

    /// <summary>
    /// 摆轮配置列表，按顺序执行
    /// </summary>
    [Required(ErrorMessage = "摆轮配置列表不能为空")]
    [MinLength(1, ErrorMessage = "至少需要一个摆轮配置")]
    public required List<DiverterConfigRequest> DiverterConfigurations { get; set; }

    /// <summary>
    /// 皮带速度（米/秒）- Belt Speed (m/s)
    /// </summary>
    /// <remarks>
    /// 用于计算包裹到达摆轮的预期时间。默认值：1.0 m/s
    /// </remarks>
    /// <example>1.0</example>
    public double BeltSpeedMeterPerSecond { get; set; } = 1.0;

    /// <summary>
    /// 皮带长度（米）- Belt Length (m)
    /// </summary>
    /// <remarks>
    /// 从入口传感器到格口的总长度。默认值：10.0 m
    /// </remarks>
    /// <example>10.0</example>
    public double BeltLengthMeter { get; set; } = 10.0;

    /// <summary>
    /// 容差时间（毫秒）- Tolerance Time (ms)
    /// </summary>
    /// <remarks>
    /// 允许的时间误差范围，用于判断包裹是否超时到达或丢失。
    /// 包裹实际到达时间 = 理论时间 ± 容差时间。默认值：2000 ms
    /// </remarks>
    /// <example>2000</example>
    public int ToleranceTimeMs { get; set; } = 2000;

    /// <summary>
    /// 格口前触发传感器IO配置
    /// </summary>
    /// <remarks>
    /// 每个格口前面都有一个触发传感器，用于检测包裹到达
    /// </remarks>
    public ChuteSensorConfigRequest? SensorConfig { get; set; }

    /// <summary>
    /// 是否启用此配置
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; set; } = true;
}
