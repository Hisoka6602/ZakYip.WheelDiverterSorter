namespace ZakYip.WheelDiverterSorter.Communication;

/// <summary>
/// 包裹格口分配请求
/// </summary>
public record ChuteAssignmentRequest
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTimeOffset RequestTime { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 包裹格口分配响应
/// </summary>
public record ChuteAssignmentResponse
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口号
    /// </summary>
    public required string ChuteNumber { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// 错误消息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 响应时间
    /// </summary>
    public DateTimeOffset ResponseTime { get; init; } = DateTimeOffset.UtcNow;
}
