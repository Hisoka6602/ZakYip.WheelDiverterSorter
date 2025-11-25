using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口分配超时配置响应模型
/// Chute assignment timeout configuration response model
/// </summary>
/// <remarks>
/// 包含超时配置参数和计算出的有效超时时间
/// </remarks>
[SwaggerSchema(Description = "格口分配超时配置响应，包含配置参数和计算的有效超时时间")]
public record ChuteAssignmentTimeoutResponse
{
    /// <summary>
    /// 安全系数（范围：0.1 ~ 1.0）
    /// Safety factor (range: 0.1 ~ 1.0)
    /// </summary>
    /// <example>0.9</example>
    [SwaggerSchema(Description = "安全系数，实际等待时间 = 理论物理极限时间 × SafetyFactor")]
    public required decimal SafetyFactor { get; init; }

    /// <summary>
    /// 降级策略的默认超时时间（秒）
    /// Default timeout for fallback strategy (seconds)
    /// </summary>
    /// <example>5</example>
    [SwaggerSchema(Description = "当无法计算理论时间时使用的保守默认超时值")]
    public required decimal FallbackTimeoutSeconds { get; init; }

    /// <summary>
    /// 当前线体的理论物理极限时间（秒）
    /// Theoretical physical limit time for current line (seconds)
    /// </summary>
    /// <remarks>
    /// 从入口到第一个摆轮的理论到达时间，基于线体拓扑计算
    /// 如果无法计算则为null
    /// </remarks>
    /// <example>4.5</example>
    [SwaggerSchema(Description = "从入口到第一个摆轮的理论到达时间，null表示无法计算")]
    public decimal? TheoreticalLimitSeconds { get; init; }

    /// <summary>
    /// 当前有效超时时间（秒）
    /// Current effective timeout (seconds)
    /// </summary>
    /// <remarks>
    /// 实际等待时间 = TheoreticalLimitSeconds × SafetyFactor
    /// 或者 FallbackTimeoutSeconds（如果无法计算理论时间）
    /// </remarks>
    /// <example>4.05</example>
    [SwaggerSchema(Description = "实际生效的超时时间")]
    public required decimal EffectiveTimeoutSeconds { get; init; }
}
