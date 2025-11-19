namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 包裹生命周期日志记录器接口
/// </summary>
/// <remarks>
/// 负责记录包裹从创建到完成的完整生命周期
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
}
