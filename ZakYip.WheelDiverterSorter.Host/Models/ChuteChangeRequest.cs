namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 改口请求DTO
/// </summary>
public class ChuteChangeRequest
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; set; }

    /// <summary>
    /// 请求的新目标格口ID
    /// </summary>
    public required int RequestedChuteId { get; set; }

    /// <summary>
    /// 请求时间（可选，默认为服务器当前时间）
    /// </summary>
    public DateTimeOffset? RequestedAt { get; set; }
}
