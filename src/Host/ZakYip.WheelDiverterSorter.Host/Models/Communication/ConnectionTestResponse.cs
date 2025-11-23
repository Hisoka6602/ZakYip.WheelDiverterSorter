namespace ZakYip.WheelDiverterSorter.Host.Models.Communication;

/// <summary>
/// 连接测试响应模型 - Connection Test Response Model
/// </summary>
public record ConnectionTestResponse
{
    /// <summary>
    /// 测试是否成功 - Whether the test succeeded
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// 响应时间（毫秒） - Response time in milliseconds
    /// </summary>
    public long? ResponseTimeMs { get; init; }

    /// <summary>
    /// 消息 - Message
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 错误详情 - Error details
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// 测试时间 - Test time
    /// </summary>
    public required DateTimeOffset TestedAt { get; init; }
}
