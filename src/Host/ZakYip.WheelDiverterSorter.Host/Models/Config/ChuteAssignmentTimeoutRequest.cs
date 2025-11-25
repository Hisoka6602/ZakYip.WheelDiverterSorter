using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口分配超时配置请求模型
/// Chute assignment timeout configuration request model
/// </summary>
/// <remarks>
/// 用于配置等待上游格口分配指令的超时参数
/// </remarks>
[SwaggerSchema(Description = "格口分配超时配置请求，用于配置等待上游指令的超时参数")]
public record ChuteAssignmentTimeoutRequest
{
    /// <summary>
    /// 安全系数（范围：0.1 ~ 1.0，默认：0.9）
    /// Safety factor (range: 0.1 ~ 1.0, default: 0.9)
    /// </summary>
    /// <remarks>
    /// <para>实际等待时间 = 理论物理极限时间 × SafetyFactor</para>
    /// <para>安全系数确保在包裹到达第一个摆轮决策点之前获得格口分配结果</para>
    /// <para>不允许配置超过1.0的安全系数，因为这会超过物理极限</para>
    /// </remarks>
    /// <example>0.9</example>
    [Required(ErrorMessage = "安全系数不能为空")]
    [Range(0.1, 1.0, ErrorMessage = "安全系数必须在0.1到1.0之间")]
    [SwaggerSchema(Description = "安全系数，实际等待时间 = 理论时间 × 安全系数，必须在0.1-1.0之间")]
    public required decimal SafetyFactor { get; init; }

    /// <summary>
    /// 降级策略的默认超时时间（秒）
    /// Default timeout for fallback strategy (seconds)
    /// </summary>
    /// <remarks>
    /// 当无法计算理论物理极限时间时使用的保守默认值
    /// </remarks>
    /// <example>5</example>
    [Range(1, 60, ErrorMessage = "降级超时时间必须在1到60秒之间")]
    [SwaggerSchema(Description = "降级超时时间（秒），当无法计算理论时间时使用")]
    public decimal FallbackTimeoutSeconds { get; init; } = 5m;
}
