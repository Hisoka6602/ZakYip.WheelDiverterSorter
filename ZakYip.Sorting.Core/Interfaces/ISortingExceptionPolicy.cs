using ZakYip.Sorting.Core.Models;

namespace ZakYip.Sorting.Core.Interfaces;

/// <summary>
/// 分拣异常策略接口
/// </summary>
/// <remarks>
/// 根据失败原因决定是否走异常口、重试或丢弃
/// </remarks>
public interface ISortingExceptionPolicy
{
    /// <summary>
    /// 判断是否应该使用异常格口
    /// </summary>
    /// <param name="failureReason">失败原因</param>
    /// <returns>是否使用异常格口</returns>
    bool ShouldUseExceptionChute(string failureReason);

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="failureReason">失败原因</param>
    /// <param name="attemptCount">已尝试次数</param>
    /// <returns>是否应该重试</returns>
    bool ShouldRetry(string failureReason, int attemptCount);

    /// <summary>
    /// 获取异常路由策略
    /// </summary>
    /// <returns>异常路由策略</returns>
    ExceptionRoutingPolicy GetPolicy();
}
