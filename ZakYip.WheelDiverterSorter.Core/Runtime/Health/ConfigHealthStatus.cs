namespace ZakYip.WheelDiverterSorter.Core.Runtime.Health;

/// <summary>
/// 配置健康状态
/// </summary>
public record ConfigHealthStatus
{
    /// <summary>
    /// 配置是否有效
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// 错误消息（中文，如果配置无效）
    /// </summary>
    public string? ErrorMessage { get; init; }
}
