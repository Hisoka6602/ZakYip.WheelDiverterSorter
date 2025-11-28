using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Overload;

/// <summary>
/// 默认超载处置策略实现。
/// </summary>
public class DefaultOverloadHandlingPolicy : IOverloadHandlingPolicy
{
    private readonly OverloadPolicyConfiguration _config;

    public DefaultOverloadHandlingPolicy(OverloadPolicyConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public OverloadDecision Evaluate(in OverloadContext context)
    {
        // 如果策略未启用，直接返回"继续正常"
        if (!_config.Enabled)
        {
            return OverloadDecision.ContinueNormal();
        }

        // 检查是否严重拥堵且配置为强制异常
        if (context.CurrentCongestionLevel == CongestionLevel.Severe && _config.ForceExceptionOnSevere)
        {
            return OverloadDecision.ForceException("严重拥堵，系统超载");
        }

        // 检查在途包裹数是否超过阈值
        if (_config.MaxInFlightParcels.HasValue && context.InFlightParcels >= _config.MaxInFlightParcels.Value)
        {
            return _config.ForceExceptionOnOverCapacity
                ? OverloadDecision.ForceException($"在途包裹数超载：{context.InFlightParcels} >= {_config.MaxInFlightParcels.Value}")
                : OverloadDecision.MarkOnly($"在途包裹数偏高：{context.InFlightParcels}");
        }

        // 检查剩余TTL是否不足
        if (context.RemainingTtlMs < _config.MinRequiredTtlMs)
        {
            return _config.ForceExceptionOnTimeout
                ? OverloadDecision.ForceException($"剩余TTL不足：{context.RemainingTtlMs}ms < {_config.MinRequiredTtlMs}ms")
                : OverloadDecision.MarkOnly("剩余TTL偏低");
        }

        // 检查到达窗口是否过小
        if (context.EstimatedArrivalWindowMs < _config.MinArrivalWindowMs)
        {
            return _config.ForceExceptionOnWindowMiss
                ? OverloadDecision.ForceException($"到达窗口不足：{context.EstimatedArrivalWindowMs}ms < {_config.MinArrivalWindowMs}ms")
                : OverloadDecision.MarkOnly("到达窗口紧张");
        }

        // 所有检查通过，继续正常分拣
        return OverloadDecision.ContinueNormal();
    }
}

/// <summary>
/// 超载策略配置。
/// </summary>
public class OverloadPolicyConfiguration
{
    /// <summary>
    /// 是否启用超载检测和处置（默认：true）。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 严重拥堵时是否直接路由到异常口（默认：true）。
    /// </summary>
    public bool ForceExceptionOnSevere { get; set; } = true;

    /// <summary>
    /// 超过在途包裹容量时是否强制异常（默认：false，仅打标记）。
    /// </summary>
    public bool ForceExceptionOnOverCapacity { get; set; } = false;

    /// <summary>
    /// TTL不足时是否强制异常（默认：true）。
    /// </summary>
    public bool ForceExceptionOnTimeout { get; set; } = true;

    /// <summary>
    /// 到达窗口不足时是否强制异常（默认：false）。
    /// </summary>
    public bool ForceExceptionOnWindowMiss { get; set; } = false;

    /// <summary>
    /// 最大允许在途包裹数（null表示不限制，默认：null）。
    /// </summary>
    public int? MaxInFlightParcels { get; set; }

    /// <summary>
    /// 最小所需剩余TTL（毫秒，默认：500）。
    /// </summary>
    public double MinRequiredTtlMs { get; set; } = 500;

    /// <summary>
    /// 最小到达窗口（毫秒，默认：200）。
    /// </summary>
    public double MinArrivalWindowMs { get; set; } = 200;
}
