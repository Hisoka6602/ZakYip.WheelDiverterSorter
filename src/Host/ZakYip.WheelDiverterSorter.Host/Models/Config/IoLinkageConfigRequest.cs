using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// IO 联动配置请求模型
/// </summary>
public sealed record class IoLinkageConfigRequest
{
    /// <summary>
    /// 是否启用 IO 联动功能
    /// </summary>
    [Required(ErrorMessage = "Enabled 不能为空")]
    public required bool Enabled { get; init; }

    /// <summary>
    /// 运行中状态时联动的 IO 点列表
    /// </summary>
    [Required(ErrorMessage = "RunningStateIos 不能为空")]
    public required List<IoLinkagePointRequest> RunningStateIos { get; init; }

    /// <summary>
    /// 停止/复位状态时联动的 IO 点列表
    /// </summary>
    [Required(ErrorMessage = "StoppedStateIos 不能为空")]
    public required List<IoLinkagePointRequest> StoppedStateIos { get; init; }
}
