namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 调试接口的响应模型
/// </summary>
public class DebugSortResponse
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    public required string TargetChuteId { get; init; }

    /// <summary>
    /// 执行是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 实际落格的格口标识
    /// </summary>
    public required string ActualChuteId { get; init; }

    /// <summary>
    /// 执行结果消息（中文）
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 失败原因（如果失败）
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 生成的路径段数量
    /// </summary>
    public int PathSegmentCount { get; init; }
}
