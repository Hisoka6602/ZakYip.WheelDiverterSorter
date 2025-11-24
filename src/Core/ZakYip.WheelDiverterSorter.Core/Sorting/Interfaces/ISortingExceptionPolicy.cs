using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Interfaces;

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

    /// <summary>
    /// 处理上游异常，决定如何响应
    /// </summary>
    /// <param name="request">原始分拣请求</param>
    /// <param name="exception">上游抛出的异常</param>
    /// <param name="attemptCount">已尝试次数</param>
    /// <returns>分拣响应，可能是异常格口或重试标志</returns>
    SortingResponse HandleUpstreamException(
        SortingRequest request,
        Exception exception,
        int attemptCount);
}
