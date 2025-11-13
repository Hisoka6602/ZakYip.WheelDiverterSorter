namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 调试接口的响应模型
/// </summary>
public class DebugSortResponse
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    /// <example>PKG001</example>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>1</example>
    public required int TargetChuteId { get; init; }

    /// <summary>
    /// 执行是否成功
    /// </summary>
    /// <example>true</example>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 实际落格的格口标识
    /// </summary>
    /// <example>1</example>
    public required int ActualChuteId { get; init; }

    /// <summary>
    /// 执行结果消息（中文）
    /// </summary>
    /// <example>分拣成功：包裹 PKG001 已送达格口 CHUTE-01</example>
    public required string Message { get; init; }

    /// <summary>
    /// 失败原因（如果失败）
    /// </summary>
    /// <example>null</example>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 生成的路径段数量
    /// </summary>
    /// <example>3</example>
    public int PathSegmentCount { get; init; }
}
