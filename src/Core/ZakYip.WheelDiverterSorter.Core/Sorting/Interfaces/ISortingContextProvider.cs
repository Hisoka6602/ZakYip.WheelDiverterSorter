using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;

/// <summary>
/// 分拣上下文提供者接口
/// </summary>
/// <remarks>
/// 提供当前线体模式、异常格口 ID 等信息
/// </remarks>
public interface ISortingContextProvider
{
    /// <summary>
    /// 获取当前分拣模式
    /// </summary>
    /// <returns>当前分拣模式</returns>
    SortingMode GetSortingMode();

    /// <summary>
    /// 获取异常格口ID
    /// </summary>
    /// <returns>异常格口ID</returns>
    long GetExceptionChuteId();

    /// <summary>
    /// 获取固定格口ID（仅在 FixedChute 模式下有效）
    /// </summary>
    /// <returns>固定格口ID，如果不在 FixedChute 模式下则返回 null</returns>
    long? GetFixedChuteId();

    /// <summary>
    /// 获取可用格口ID列表（仅在 RoundRobin 模式下有效）
    /// </summary>
    /// <returns>可用格口ID列表</returns>
    IReadOnlyList<long> GetAvailableChuteIds();

    /// <summary>
    /// 获取异常路由策略
    /// </summary>
    /// <returns>异常路由策略</returns>
    ExceptionRoutingPolicy GetExceptionRoutingPolicy();
}
