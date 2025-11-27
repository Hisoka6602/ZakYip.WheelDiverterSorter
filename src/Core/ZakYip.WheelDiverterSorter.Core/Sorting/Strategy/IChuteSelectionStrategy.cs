namespace ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;

/// <summary>
/// 格口选择策略接口
/// </summary>
/// <remarks>
/// 定义统一的格口选择抽象，所有分拣模式（固定格口、轮询、正式）都实现此接口。
/// 策略实现负责：
/// <list type="bullet">
///   <item>根据分拣模式选择目标格口</item>
///   <item>处理异常情况并返回异常格口</item>
///   <item>统一异常格口兜底逻辑</item>
/// </list>
/// </remarks>
public interface IChuteSelectionStrategy
{
    /// <summary>
    /// 选择目标格口
    /// </summary>
    /// <param name="context">分拣上下文，包含分拣模式、条码、可用格口列表、异常格口 ID 等信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>格口选择结果，包含目标格口 / 是否走异常格口 / 错误信息</returns>
    Task<ChuteSelectionResult> SelectChuteAsync(SortingContext context, CancellationToken cancellationToken);
}
