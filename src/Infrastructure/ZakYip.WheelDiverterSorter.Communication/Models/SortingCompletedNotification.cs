namespace ZakYip.WheelDiverterSorter.Communication.Models;

/// <summary>
/// 落格完成通知（通信层模型）
/// </summary>
/// <remarks>
/// PR-UPSTREAM02: 新增类型，用于通知上游系统包裹已完成落格。
/// 与 Core 层的 SortingCompletedNotification 对应。
/// </remarks>
public sealed record SortingCompletedNotificationDto
{
    /// <summary>
    /// 消息类型
    /// </summary>
    /// <remarks>
    /// 固定值 "SortingCompleted"，用于上游系统识别消息类型
    /// </remarks>
    public string Type { get; init; } = "SortingCompleted";

    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 实际落格格口ID
    /// </summary>
    public required long ActualChuteId { get; init; }

    /// <summary>
    /// 落格完成时间
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// 是否成功（false 表示进入异常口或失败）
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// 失败原因（如果失败）
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 包裹最终状态
    /// </summary>
    /// <remarks>
    /// 使用 ParcelFinalStatus 枚举区分结果类型：Success/Timeout/Lost/ExecutionError
    /// </remarks>
    public ZakYip.WheelDiverterSorter.Core.Enums.Parcel.ParcelFinalStatus FinalStatus { get; init; } = ZakYip.WheelDiverterSorter.Core.Enums.Parcel.ParcelFinalStatus.Success;
}
