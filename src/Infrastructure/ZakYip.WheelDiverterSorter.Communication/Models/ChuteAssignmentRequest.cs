namespace ZakYip.WheelDiverterSorter.Communication.Models;

/// <summary>
/// 包裹格口分配请求
/// </summary>
public record ChuteAssignmentRequest
{
    /// <summary>
    /// 包裹ID (毫秒时间戳)
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTimeOffset RequestTime { get; init; } = DateTimeOffset.UtcNow;
}
