using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 更新落格回调配置请求
/// </summary>
public record UpdateChuteDropoffCallbackConfigRequest
{
    /// <summary>
    /// 触发模式
    /// </summary>
    /// <example>OnSensorTrigger</example>
    [Required(ErrorMessage = "TriggerMode is required")]
    public required string TriggerMode { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; init; } = true;
}
