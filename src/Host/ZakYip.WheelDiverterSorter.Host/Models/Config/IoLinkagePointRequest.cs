using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// IO 联动点配置请求模型
/// </summary>
public sealed record class IoLinkagePointRequest
{
    /// <summary>
    /// IO 端口编号（0-1023）
    /// </summary>
    [Required(ErrorMessage = "IO 端口编号不能为空")]
    [Range(0, 1023, ErrorMessage = "IO 端口编号必须在 0-1023 之间")]
    public required int BitNumber { get; init; }

    /// <summary>
    /// 目标电平状态
    /// </summary>
    [Required(ErrorMessage = "IO 电平状态不能为空")]
    public required TriggerLevel Level { get; init; }

    /// <summary>
    /// 延迟执行时间（毫秒），默认为 0 表示立即执行
    /// </summary>
    /// <example>0</example>
    [Range(0, 3600000, ErrorMessage = "延迟时间必须在 0-3600000 毫秒之间")]
    public int DelayMilliseconds { get; init; } = 0;
}
