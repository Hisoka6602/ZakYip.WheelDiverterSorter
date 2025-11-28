using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Events;

/// <summary>
/// 包裹异常落格事件载荷，当包裹因各种原因被发送到异常格口时触发
/// </summary>
public readonly record struct ParcelDivertedToExceptionEventArgs
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 落格时间（UTC）
    /// </summary>
    public required DateTimeOffset DivertedAt { get; init; }

    /// <summary>
    /// 异常格口ID
    /// </summary>
    public required long ExceptionChuteId { get; init; }

    /// <summary>
    /// 异常原因
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// 原始目标格口ID（如果有）
    /// </summary>
    public long? OriginalTargetChuteId { get; init; }

    /// <summary>
    /// 异常类型
    /// </summary>
    public ExceptionType ExceptionType { get; init; }

    /// <summary>
    /// 分拣总耗时（毫秒）
    /// </summary>
    public double TotalTimeMs { get; init; }
}
