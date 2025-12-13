namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 落格回调配置DTO
/// </summary>
public record ChuteDropoffCallbackConfigDto
{
    /// <summary>
    /// 触发模式
    /// </summary>
    /// <example>OnSensorTrigger</example>
    public required string TriggerMode { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
