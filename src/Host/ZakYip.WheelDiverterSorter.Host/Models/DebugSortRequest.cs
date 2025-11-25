using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 调试接口的请求模型
/// </summary>
[SwaggerSchema(Description = "调试分拣接口的请求参数，用于测试包裹分拣功能")]
public record DebugSortRequest
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    /// <example>PKG20251117001</example>
    [Required(ErrorMessage = "包裹ID不能为空")]
    [SwaggerSchema(Description = "包裹的唯一标识符，用于追踪包裹")]
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>1</example>
    [Required(ErrorMessage = "目标格口ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "目标格口ID必须大于0")]
    [SwaggerSchema(Description = "目标格口的编号，必须是已配置的有效格口")]
    public required long TargetChuteId { get; init; }
}
