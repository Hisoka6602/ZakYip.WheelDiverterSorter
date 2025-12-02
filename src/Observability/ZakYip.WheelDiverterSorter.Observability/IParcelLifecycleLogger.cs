using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 包裹生命周期日志记录器接口
/// </summary>
/// <remarks>
/// 负责记录包裹从创建到完成的完整生命周期。
/// PR-NOSHADOW-ALL: 扩展接口，添加超时和丢失事件的日志记录方法。
/// </remarks>
public interface IParcelLifecycleLogger
{
    /// <summary>
    /// 记录包裹创建事件
    /// </summary>
    /// <param name="context">包裹生命周期上下文</param>
    void LogCreated(ParcelLifecycleContext context);

    /// <summary>
    /// 记录包裹通过传感器事件
    /// </summary>
    /// <param name="context">包裹生命周期上下文</param>
    /// <param name="sensorName">传感器名称</param>
    void LogSensorPassed(ParcelLifecycleContext context, string sensorName);

    /// <summary>
    /// 记录格口分配事件
    /// </summary>
    /// <param name="context">包裹生命周期上下文</param>
    /// <param name="chuteId">分配的格口ID</param>
    void LogChuteAssigned(ParcelLifecycleContext context, long chuteId);

    /// <summary>
    /// 记录包裹完成事件
    /// </summary>
    /// <param name="context">包裹生命周期上下文</param>
    /// <param name="status">最终状态</param>
    void LogCompleted(ParcelLifecycleContext context, ParcelFinalStatus status);

    /// <summary>
    /// 记录异常事件
    /// </summary>
    /// <param name="context">包裹生命周期上下文</param>
    /// <param name="reason">异常原因</param>
    void LogException(ParcelLifecycleContext context, string reason);

    /// <summary>
    /// 记录包裹超时事件
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 当包裹在规定时间内未完成某阶段（分配超时或落格超时）时调用。
    /// <list type="bullet">
    ///   <item>分配超时：检测后超过 DetectionToAssignmentTimeoutSeconds 未收到格口分配</item>
    ///   <item>落格超时：分配后超过 AssignmentToSortingTimeoutSeconds 未完成落格</item>
    /// </list>
    /// </remarks>
    /// <param name="context">包裹生命周期上下文</param>
    /// <param name="timeoutType">超时类型（"AssignmentTimeout" 或 "SortingTimeout"）</param>
    /// <param name="elapsedSeconds">已经过时间（秒）</param>
    void LogTimeout(ParcelLifecycleContext context, string timeoutType, double elapsedSeconds);

    /// <summary>
    /// 记录包裹丢失事件
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 当包裹超过最大存活时间仍未完成落格，且无法确定位置时调用。
    /// 此时包裹已超出系统控制范围，仅记录和通知上游。
    /// </remarks>
    /// <param name="context">包裹生命周期上下文</param>
    /// <param name="lifetimeSeconds">包裹在系统中的总存活时间（秒）</param>
    void LogLost(ParcelLifecycleContext context, double lifetimeSeconds);
}
