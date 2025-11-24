namespace ZakYip.WheelDiverterSorter.Host.Models.Communication;

/// <summary>
/// 测试包裹响应模型 - Test Parcel Response Model
/// </summary>
public sealed record TestParcelResponse
{
    /// <summary>
    /// 测试是否成功 - Whether the test succeeded
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// 包裹ID - Parcel ID
    /// </summary>
    public required string ParcelId { get; init; }

    /// <summary>
    /// 消息 - Message
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 错误详情 - Error details (if failed)
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// 发送时间 - Sent time
    /// </summary>
    public DateTimeOffset SentAt { get; init; }

    /// <summary>
    /// 响应时间（毫秒） - Response time in milliseconds
    /// </summary>
    public long? ResponseTimeMs { get; init; }
}
