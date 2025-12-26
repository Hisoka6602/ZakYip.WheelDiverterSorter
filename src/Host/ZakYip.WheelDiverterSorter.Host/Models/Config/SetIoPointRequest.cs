using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 设置单个 IO 点的请求模型
/// </summary>
public sealed record class SetIoPointRequest
{
    /// <summary>
    /// 目标电平状态
    /// </summary>
    /// <remarks>
    /// - ActiveHigh：高电平有效（输出1）
    /// - ActiveLow：低电平有效（输出0）
    /// </remarks>
    [Required(ErrorMessage = "Level 不能为空")]
    public required TriggerLevel Level { get; init; }

    /// <summary>
    /// 延迟执行时间（秒），默认为 0 表示立即执行
    /// </summary>
    /// <example>0</example>
    [Range(0, 3600, ErrorMessage = "延迟时间必须在 0-3600 秒之间")]
    public int DelaySeconds { get; init; } = 0;
}
