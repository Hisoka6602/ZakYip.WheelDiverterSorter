using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 包裹时间轴收集器
/// </summary>
/// <remarks>
/// 在仿真模式下收集和聚合包裹生命周期事件，生成时间轴快照
/// </remarks>
public class ParcelTimelineCollector : IParcelLifecycleLogger
{
    private readonly ConcurrentDictionary<long, ParcelTimelineSnapshot> _snapshots = new();

    /// <summary>
    /// 记录包裹创建事件
    /// </summary>
    public void LogCreated(ParcelLifecycleContext context)
    {
        var snapshot = _snapshots.GetOrAdd(context.ParcelId, _ => new ParcelTimelineSnapshot
        {
            ParcelId = context.ParcelId
        });

        snapshot.TargetChuteId = context.TargetChuteId;
        snapshot.CreatedTime = context.EventTime;
        snapshot.Events.Add(new TimelineEvent
        {
            EventType = "Created",
            EventTime = context.EventTime,
            Description = $"Created with target chute {context.TargetChuteId}"
        });
    }

    /// <summary>
    /// 记录包裹通过传感器事件
    /// </summary>
    public void LogSensorPassed(ParcelLifecycleContext context, string sensorName)
    {
        var snapshot = _snapshots.GetOrAdd(context.ParcelId, _ => new ParcelTimelineSnapshot
        {
            ParcelId = context.ParcelId
        });

        snapshot.Events.Add(new TimelineEvent
        {
            EventType = "SensorPassed",
            EventTime = context.EventTime,
            Description = $"Passed sensor: {sensorName}"
        });
    }

    /// <summary>
    /// 记录格口分配事件
    /// </summary>
    public void LogChuteAssigned(ParcelLifecycleContext context, long chuteId)
    {
        var snapshot = _snapshots.GetOrAdd(context.ParcelId, _ => new ParcelTimelineSnapshot
        {
            ParcelId = context.ParcelId
        });

        snapshot.Events.Add(new TimelineEvent
        {
            EventType = "ChuteAssigned",
            EventTime = context.EventTime,
            Description = $"Chute assigned: {chuteId}"
        });
    }

    /// <summary>
    /// 记录包裹完成事件
    /// </summary>
    public void LogCompleted(ParcelLifecycleContext context, ParcelFinalStatus status)
    {
        var snapshot = _snapshots.GetOrAdd(context.ParcelId, _ => new ParcelTimelineSnapshot
        {
            ParcelId = context.ParcelId
        });

        snapshot.FinalStatus = status;
        snapshot.ActualChuteId = context.ActualChuteId;
        snapshot.CompletedTime = context.EventTime;
        snapshot.Events.Add(new TimelineEvent
        {
            EventType = "Completed",
            EventTime = context.EventTime,
            Description = $"Completed with status: {status}, actual chute: {context.ActualChuteId}"
        });
    }

    /// <summary>
    /// 记录异常事件
    /// </summary>
    public void LogException(ParcelLifecycleContext context, string reason)
    {
        var snapshot = _snapshots.GetOrAdd(context.ParcelId, _ => new ParcelTimelineSnapshot
        {
            ParcelId = context.ParcelId
        });

        snapshot.FailureReason = reason;
        snapshot.Events.Add(new TimelineEvent
        {
            EventType = "Exception",
            EventTime = context.EventTime,
            Description = $"Exception: {reason}"
        });
    }

    /// <summary>
    /// 获取所有包裹的时间轴快照
    /// </summary>
    public IReadOnlyCollection<ParcelTimelineSnapshot> GetSnapshots()
    {
        return _snapshots.Values.ToList();
    }

    /// <summary>
    /// 清除所有快照（释放内存）
    /// </summary>
    public void Clear()
    {
        _snapshots.Clear();
    }
}
