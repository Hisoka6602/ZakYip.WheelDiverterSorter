using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 包裹生命周期上下文
/// </summary>
/// <remarks>
/// 包含包裹生命周期事件的完整上下文信息
/// </remarks>
public sealed record class ParcelLifecycleContext
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 条码（可选）
    /// </summary>
    public string? Barcode { get; init; }

    /// <summary>
    /// 入口时间
    /// </summary>
    public DateTimeOffset? EntryTime { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public long? TargetChuteId { get; init; }

    /// <summary>
    /// 实际格口ID
    /// </summary>
    public long? ActualChuteId { get; init; }

    /// <summary>
    /// 事件时间
    /// </summary>
    public DateTimeOffset EventTime { get; init; }

    /// <summary>
    /// 系统状态快照（可选）
    /// </summary>
    public string? SystemState { get; init; }

    /// <summary>
    /// 附加属性（用于扩展信息）
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; init; }
}
