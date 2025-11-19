namespace ZakYip.WheelDiverterSorter.Core.Sorting.Overload;

/// <summary>
/// 超载处置策略：当理论上无法在剩余路程内完成分拣时，决定如何处理包裹。
/// </summary>
public interface IOverloadHandlingPolicy
{
    /// <summary>
    /// 评估当前包裹是否属于超载情形，并返回处置决策。
    /// </summary>
    /// <param name="context">超载上下文。</param>
    /// <returns>处置决策。</returns>
    OverloadDecision Evaluate(in OverloadContext context);
}
