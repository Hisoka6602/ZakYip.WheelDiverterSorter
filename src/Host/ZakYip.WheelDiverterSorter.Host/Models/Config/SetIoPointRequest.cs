using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums;

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
}
