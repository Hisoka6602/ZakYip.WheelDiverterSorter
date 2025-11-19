namespace ZakYip.WheelDiverterSorter.Core.Sorting.Overload;

/// <summary>
/// 超载决策：策略对包裹的处置决定。
/// </summary>
public readonly record struct OverloadDecision
{
    /// <summary>
    /// 是否直接送异常口。
    /// </summary>
    public bool ShouldForceException { get; init; }

    /// <summary>
    /// 是否打"超载包裹"标记。
    /// </summary>
    public bool ShouldMarkAsOverflow { get; init; }

    /// <summary>
    /// 是否优先考虑回流（如果拓扑支持）。
    /// </summary>
    public bool ShouldPreferRecirculation { get; init; }

    /// <summary>
    /// 决策原因（用于日志和监控）。
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 超载原因枚举（PR-14：用于更结构化的原因记录）
    /// Overload reason enum for structured tracking
    /// </summary>
    public OverloadReason ReasonCode { get; init; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public OverloadDecision()
    {
        ShouldForceException = false;
        ShouldMarkAsOverflow = false;
        ShouldPreferRecirculation = false;
        Reason = null;
        ReasonCode = OverloadReason.None;
    }

    /// <summary>
    /// 创建一个"继续正常分拣"的决策。
    /// </summary>
    public static OverloadDecision ContinueNormal() => new()
    {
        ShouldForceException = false,
        ShouldMarkAsOverflow = false,
        ShouldPreferRecirculation = false,
        Reason = "正常",
        ReasonCode = OverloadReason.None
    };

    /// <summary>
    /// 创建一个"强制异常口"的决策。
    /// </summary>
    public static OverloadDecision ForceException(string reason, OverloadReason reasonCode = OverloadReason.Other) => new()
    {
        ShouldForceException = true,
        ShouldMarkAsOverflow = true,
        ShouldPreferRecirculation = false,
        Reason = reason,
        ReasonCode = reasonCode
    };

    /// <summary>
    /// 创建一个"仅打标记"的决策。
    /// </summary>
    public static OverloadDecision MarkOnly(string reason, OverloadReason reasonCode = OverloadReason.Other) => new()
    {
        ShouldForceException = false,
        ShouldMarkAsOverflow = true,
        ShouldPreferRecirculation = false,
        Reason = reason,
        ReasonCode = reasonCode
    };
}
