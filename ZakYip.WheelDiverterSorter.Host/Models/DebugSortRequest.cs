namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 调试接口的请求模型
/// </summary>
public class DebugSortRequest
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    public required string TargetChuteId { get; init; }
}
