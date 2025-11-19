namespace ZakYip.WheelDiverterSorter.Core.Sorting.Overload;

/// <summary>
/// 策略实例工厂，根据 StrategyProfile 创建 Overload / 路由策略实例。
/// Strategy instance factory for creating Overload/routing strategy instances from StrategyProfile.
/// </summary>
public interface IStrategyFactory
{
    /// <summary>
    /// 根据 Profile 创建 Overload 策略实例
    /// Create Overload policy instance from profile
    /// </summary>
    /// <param name="profile">策略配置 Profile / Strategy configuration profile</param>
    /// <returns>IOverloadHandlingPolicy 实例 / IOverloadHandlingPolicy instance</returns>
    IOverloadHandlingPolicy CreateOverloadPolicy(StrategyProfile profile);

    // 若路由策略也需要 Profile 化，可以在此扩展：
    // If routing strategy also needs to be profiled, extend here:
    // IRoutePlanningPolicy CreateRoutePolicy(StrategyProfile profile);
}
