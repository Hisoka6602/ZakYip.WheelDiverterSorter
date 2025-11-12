using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 调试接口的请求模型
/// </summary>
public class DebugSortRequest
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    /// <example>PKG001</example>
    [Required(ErrorMessage = "包裹ID不能为空")]
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>CHUTE-01</example>
    [Required(ErrorMessage = "目标格口ID不能为空")]
    public required string TargetChuteId { get; init; }
}
