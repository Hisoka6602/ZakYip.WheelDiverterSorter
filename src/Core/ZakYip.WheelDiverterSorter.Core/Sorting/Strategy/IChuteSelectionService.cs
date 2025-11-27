namespace ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;

/// <summary>
/// 格口选择服务接口
/// </summary>
/// <remarks>
/// 组合服务，内部根据当前分拣模式自动选择具体策略。
/// Host / Worker / Orchestrator 只依赖此接口，不依赖具体策略实现类。
/// </remarks>
public interface IChuteSelectionService
{
    /// <summary>
    /// 选择目标格口
    /// </summary>
    /// <param name="context">分拣上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>格口选择结果</returns>
    /// <remarks>
    /// 此方法会根据 context.SortingMode 自动路由到对应的策略实现：
    /// <list type="bullet">
    ///   <item>Formal: FormalChuteSelectionStrategy</item>
    ///   <item>FixedChute: FixedChuteSelectionStrategy</item>
    ///   <item>RoundRobin: RoundRobinChuteSelectionStrategy</item>
    /// </list>
    /// </remarks>
    Task<ChuteSelectionResult> SelectChuteAsync(SortingContext context, CancellationToken cancellationToken);
}
