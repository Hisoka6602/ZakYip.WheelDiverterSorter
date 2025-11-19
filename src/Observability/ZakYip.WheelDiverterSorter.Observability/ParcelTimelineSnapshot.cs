namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 包裹时间轴快照
/// </summary>
/// <remarks>
/// 用于仿真报告生成，包含包裹的完整生命周期信息
/// </remarks>
public class ParcelTimelineSnapshot
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public long ParcelId { get; set; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public int? TargetChuteId { get; set; }

    /// <summary>
    /// 实际格口ID
    /// </summary>
    public int? ActualChuteId { get; set; }

    /// <summary>
    /// 最终状态
    /// </summary>
    public ParcelFinalStatus FinalStatus { get; set; }

    /// <summary>
    /// 失败原因（如果有）
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset? CreatedTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTimeOffset? CompletedTime { get; set; }

    /// <summary>
    /// 关键时间点列表（传感器通过、分拣动作等）
    /// </summary>
    public List<TimelineEvent> Events { get; set; } = new();

    /// <summary>
    /// 是否为高密度包裹
    /// </summary>
    public bool IsDenseParcel { get; set; }

    /// <summary>
    /// 与前一包裹的时间间隔
    /// </summary>
    public TimeSpan? HeadwayTime { get; set; }

    /// <summary>
    /// 与前一包裹的空间间隔（mm）
    /// </summary>
    public decimal? HeadwayMm { get; set; }
}

/// <summary>
/// 时间轴事件
/// </summary>
public class TimelineEvent
{
    /// <summary>
    /// 事件类型（Created, SensorPassed, ChuteAssigned, Completed, Exception）
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 事件时间
    /// </summary>
    public DateTimeOffset EventTime { get; set; }

    /// <summary>
    /// 事件描述
    /// </summary>
    public string? Description { get; set; }
}
