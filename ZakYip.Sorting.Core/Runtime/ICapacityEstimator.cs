namespace ZakYip.Sorting.Core.Runtime;

/// <summary>
/// 产能估算器：根据历史数据估算安全产能区间。
/// </summary>
public interface ICapacityEstimator
{
    /// <summary>
    /// 估算当前安全放包能力（每分钟包裹数，或推荐间隔），仅用于监控展示。
    /// </summary>
    /// <param name="history">历史测试数据。</param>
    /// <returns>产能估算结果。</returns>
    CapacityEstimationResult Estimate(in CapacityHistory history);
}
