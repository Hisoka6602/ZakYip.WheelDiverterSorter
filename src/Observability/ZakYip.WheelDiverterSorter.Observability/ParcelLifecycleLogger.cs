using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 包裹生命周期日志记录器实现
/// </summary>
/// <remarks>
/// 使用 Microsoft.Extensions.Logging 记录包裹生命周期事件到独立的日志文件
/// </remarks>
public class ParcelLifecycleLogger : IParcelLifecycleLogger
{
    private readonly ILogger<ParcelLifecycleLogger> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public ParcelLifecycleLogger(ILogger<ParcelLifecycleLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 记录包裹创建事件
    /// </summary>
    public void LogCreated(ParcelLifecycleContext context)
    {
        _logger.LogInformation(
            "ParcelLifecycle | Event=Created | ParcelId={ParcelId} | Barcode={Barcode} | EntryTime={EntryTime} | TargetChuteId={TargetChuteId}",
            context.ParcelId,
            context.Barcode ?? "N/A",
            context.EntryTime?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "N/A",
            context.TargetChuteId ?? 0);
    }

    /// <summary>
    /// 记录包裹通过传感器事件
    /// </summary>
    public void LogSensorPassed(ParcelLifecycleContext context, string sensorName)
    {
        _logger.LogInformation(
            "ParcelLifecycle | Event=SensorPassed | ParcelId={ParcelId} | SensorName={SensorName} | EventTime={EventTime}",
            context.ParcelId,
            sensorName,
            context.EventTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }

    /// <summary>
    /// 记录格口分配事件
    /// </summary>
    public void LogChuteAssigned(ParcelLifecycleContext context, long chuteId)
    {
        _logger.LogInformation(
            "ParcelLifecycle | Event=ChuteAssigned | ParcelId={ParcelId} | ChuteId={ChuteId} | EventTime={EventTime}",
            context.ParcelId,
            chuteId,
            context.EventTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }

    /// <summary>
    /// 记录包裹完成事件
    /// </summary>
    public void LogCompleted(ParcelLifecycleContext context, ParcelFinalStatus status)
    {
        _logger.LogInformation(
            "ParcelLifecycle | Event=Completed | ParcelId={ParcelId} | FinalStatus={FinalStatus} | TargetChuteId={TargetChuteId} | ActualChuteId={ActualChuteId} | EventTime={EventTime}",
            context.ParcelId,
            status,
            context.TargetChuteId ?? 0,
            context.ActualChuteId ?? 0,
            context.EventTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }

    /// <summary>
    /// 记录异常事件
    /// </summary>
    public void LogException(ParcelLifecycleContext context, string reason)
    {
        _logger.LogWarning(
            "ParcelLifecycle | Event=Exception | ParcelId={ParcelId} | Reason={Reason} | EventTime={EventTime}",
            context.ParcelId,
            reason,
            context.EventTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }

    /// <summary>
    /// 记录包裹超时事件
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 当包裹在规定时间内未完成某阶段时调用。
    /// </remarks>
    public void LogTimeout(ParcelLifecycleContext context, string timeoutType, double elapsedSeconds)
    {
        _logger.LogWarning(
            "ParcelLifecycle | Event=Timeout | ParcelId={ParcelId} | TimeoutType={TimeoutType} | ElapsedSeconds={ElapsedSeconds:F1} | TargetChuteId={TargetChuteId} | EventTime={EventTime}",
            context.ParcelId,
            timeoutType,
            elapsedSeconds,
            context.TargetChuteId ?? 0,
            context.EventTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }

    /// <summary>
    /// 记录包裹丢失事件
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 当包裹超过最大存活时间仍未完成落格时调用。
    /// </remarks>
    public void LogLost(ParcelLifecycleContext context, double lifetimeSeconds)
    {
        _logger.LogError(
            "ParcelLifecycle | Event=Lost | ParcelId={ParcelId} | LifetimeSeconds={LifetimeSeconds:F1} | TargetChuteId={TargetChuteId} | EntryTime={EntryTime} | EventTime={EventTime}",
            context.ParcelId,
            lifetimeSeconds,
            context.TargetChuteId ?? 0,
            context.EntryTime?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "N/A",
            context.EventTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }
}
