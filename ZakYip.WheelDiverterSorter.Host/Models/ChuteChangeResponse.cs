namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 改口响应DTO
/// </summary>
public class ChuteChangeResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; set; }

    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; set; }

    /// <summary>
    /// 原目标格口ID
    /// </summary>
    public int? OriginalChuteId { get; set; }

    /// <summary>
    /// 请求的新格口ID
    /// </summary>
    public required int RequestedChuteId { get; set; }

    /// <summary>
    /// 实际生效的格口ID
    /// </summary>
    public int? EffectiveChuteId { get; set; }

    /// <summary>
    /// 决策结果（Accepted, IgnoredAlreadyCompleted, IgnoredExceptionRouted, RejectedInvalidState, RejectedTooLate）
    /// </summary>
    public string? Outcome { get; set; }

    /// <summary>
    /// 结果消息或原因
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 处理时间
    /// </summary>
    public DateTimeOffset ProcessedAt { get; set; }
}
