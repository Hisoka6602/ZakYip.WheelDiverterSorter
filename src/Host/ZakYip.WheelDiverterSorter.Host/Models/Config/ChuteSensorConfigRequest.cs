using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口传感器配置请求模型
/// </summary>
[SwaggerSchema(Description = "格口前触发传感器的配置信息")]
public record ChuteSensorConfigRequest
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    /// <remarks>
    /// 关联到传感器配置中的传感器标识符
    /// </remarks>
    /// <example>1</example>
    [Required(ErrorMessage = "传感器ID不能为空")]
    [SwaggerSchema(Description = "传感器的唯一编号，关联到传感器配置表")]
    public required long SensorId { get; init; }

    /// <summary>
    /// 传感器类型 (Photoelectric/Laser)
    /// </summary>
    /// <example>Photoelectric</example>
    [SwaggerSchema(Description = "传感器类型，可选值：Photoelectric（光电）、Laser（激光）")]
    public string SensorType { get; init; } = "Photoelectric";

    /// <summary>
    /// IO输入位（仅硬件传感器需要）
    /// </summary>
    /// <remarks>
    /// 硬件传感器的输入端口位号，例如雷赛控制卡的输入位
    /// </remarks>
    /// <example>5</example>
    [SwaggerSchema(Description = "硬件传感器在控制卡上的输入位编号，模拟传感器可为空")]
    public int? InputBit { get; init; }

    /// <summary>
    /// 是否启用此传感器
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "是否启用此传感器，禁用后传感器不会触发检测")]
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 去抖动时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 防止误触发的去抖动延迟时间，默认100ms
    /// </remarks>
    /// <example>100</example>
    [SwaggerSchema(Description = "防止误触发的去抖动延迟时间，单位：毫秒")]
    public int DebounceTimeMs { get; init; } = 100;
}
