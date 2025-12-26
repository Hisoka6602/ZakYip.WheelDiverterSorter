using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 单个 IO 联动点配置。
/// 表示一个 IO 端口号和应设置的电平状态。
/// </summary>
public sealed record class IoLinkagePoint
{
    /// <summary>
    /// IO 端口编号（0-1023）。
    /// </summary>
    [Required(ErrorMessage = "IO 端口编号不能为空")]
    [Range(0, 1023, ErrorMessage = "IO 端口编号必须在 0-1023 之间")]
    public required int BitNumber { get; init; }

    /// <summary>
    /// 目标电平状态（ActiveHigh=高电平，ActiveLow=低电平）。
    /// </summary>
    [Required(ErrorMessage = "IO 电平状态不能为空")]
    public required TriggerLevel Level { get; init; }

    /// <summary>
    /// 延迟执行时间（毫秒），默认为 0 表示立即执行。
    /// </summary>
    /// <remarks>
    /// 如果配置了延迟时间，IO点将在延迟指定毫秒后才生效。
    /// 延迟期间如果系统状态发生变化（如急停/停止），则取消执行。
    /// 优先级：急停 > 停止 > 运行
    /// </remarks>
    [Range(0, 3600000, ErrorMessage = "延迟时间必须在 0-3600000 毫秒之间")]
    public int DelayMilliseconds { get; init; } = 0;
}
