using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口路由配置请求模型
/// </summary>
[SwaggerSchema(Description = "格口的完整路由配置，包括摆轮序列、皮带参数和传感器配置")]
public class RouteConfigRequest
{
    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>1</example>
    [Required(ErrorMessage = "格口ID不能为空")]
    [SwaggerSchema(Description = "目标格口的唯一编号")]
    public required long ChuteId { get; set; }

    /// <summary>
    /// 格口名称（可选）- Chute Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "A区01号口"
    /// </remarks>
    /// <example>A区01号口</example>
    [SwaggerSchema(Description = "格口的友好名称，便于识别和管理")]
    public string? ChuteName { get; set; }

    /// <summary>
    /// 摆轮配置列表，按顺序执行
    /// </summary>
    [Required(ErrorMessage = "摆轮配置列表不能为空")]
    [MinLength(1, ErrorMessage = "至少需要一个摆轮配置")]
    [SwaggerSchema(Description = "从入口到该格口需要经过的摆轮配置序列")]
    public required List<DiverterConfigRequest> DiverterConfigurations { get; set; }

    /// <summary>
    /// 皮带速度（毫米/秒）- Belt Speed (mm/s)
    /// </summary>
    /// <remarks>
    /// 用于计算包裹到达摆轮的预期时间。默认值：1000.0 mm/s
    /// </remarks>
    /// <example>1000.0</example>
    [SwaggerSchema(Description = "主皮带的运行速度，用于计算包裹到达时间，单位：毫米/秒")]
    public double BeltSpeedMmPerSecond { get; set; } = 1000.0;

    /// <summary>
    /// 皮带长度（毫米）- Belt Length (mm)
    /// </summary>
    /// <remarks>
    /// 从入口传感器到格口的总长度。默认值：10000.0 mm
    /// </remarks>
    /// <example>10000.0</example>
    [SwaggerSchema(Description = "从入口传感器到该格口的总皮带长度，单位：毫米")]
    public double BeltLengthMm { get; set; } = 10000.0;

    /// <summary>
    /// 容差时间（毫秒）- Tolerance Time (ms)
    /// </summary>
    /// <remarks>
    /// 允许的时间误差范围，用于判断包裹是否超时到达或丢失。
    /// 包裹实际到达时间 = 理论时间 ± 容差时间。默认值：2000 ms
    /// </remarks>
    /// <example>2000</example>
    [SwaggerSchema(Description = "允许的时间误差范围，用于判断包裹到达是否正常，单位：毫秒")]
    public int ToleranceTimeMs { get; set; } = 2000;

    /// <summary>
    /// 格口前触发传感器IO配置
    /// </summary>
    /// <remarks>
    /// 每个格口前面都有一个触发传感器，用于检测包裹到达
    /// </remarks>
    [SwaggerSchema(Description = "格口前的触发传感器配置，用于检测包裹到达")]
    public ChuteSensorConfigRequest? SensorConfig { get; set; }

    /// <summary>
    /// 是否启用此配置
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "是否启用此格口配置，禁用后该格口不可用")]
    public bool IsEnabled { get; set; } = true;
}
