namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口分配超时配置响应模型
/// </summary>
public record ChuteAssignmentTimeoutResponse
{
    /// <summary>
    /// 安全系数（范围：0.1 ~ 1.0）
    /// </summary>
    public required decimal SafetyFactor { get; init; }

    /// <summary>
    /// 降级策略的默认超时时间（秒）
    /// </summary>
    public required decimal FallbackTimeoutSeconds { get; init; }

    /// <summary>
    /// 当前线体的理论物理极限时间（秒）
    /// </summary>
    /// <remarks>
    /// 从入口到第一个摆轮的理论到达时间，基于线体拓扑计算
    /// 如果无法计算则为null
    /// </remarks>
    public decimal? TheoreticalLimitSeconds { get; init; }

    /// <summary>
    /// 当前有效超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 实际等待时间 = TheoreticalLimitSeconds × SafetyFactor
    /// 或者 FallbackTimeoutSeconds（如果无法计算理论时间）
    /// </remarks>
    public required decimal EffectiveTimeoutSeconds { get; init; }
}
