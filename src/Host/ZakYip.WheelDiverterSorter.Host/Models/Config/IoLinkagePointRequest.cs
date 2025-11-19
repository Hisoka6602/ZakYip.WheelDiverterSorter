using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

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
}
