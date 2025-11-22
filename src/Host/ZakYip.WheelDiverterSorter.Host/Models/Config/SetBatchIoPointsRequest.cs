using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 批量设置 IO 点的请求模型
/// </summary>
public sealed record class SetBatchIoPointsRequest
{
    /// <summary>
    /// IO 点配置列表
    /// </summary>
    [Required(ErrorMessage = "IoPoints 不能为空")]
    public required List<IoLinkagePointRequest> IoPoints { get; init; }
}
