namespace ZakYip.Sorting.Core.Overload;

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
    /// 创建一个"继续正常分拣"的决策。
    /// </summary>
    public static OverloadDecision ContinueNormal() => new()
    {
        ShouldForceException = false,
        ShouldMarkAsOverflow = false,
        ShouldPreferRecirculation = false,
        Reason = "正常"
    };

    /// <summary>
    /// 创建一个"强制异常口"的决策。
    /// </summary>
    public static OverloadDecision ForceException(string reason) => new()
    {
        ShouldForceException = true,
        ShouldMarkAsOverflow = true,
        ShouldPreferRecirculation = false,
        Reason = reason
    };

    /// <summary>
    /// 创建一个"仅打标记"的决策。
    /// </summary>
    public static OverloadDecision MarkOnly(string reason) => new()
    {
        ShouldForceException = false,
        ShouldMarkAsOverflow = true,
        ShouldPreferRecirculation = false,
        Reason = reason
    };
}
