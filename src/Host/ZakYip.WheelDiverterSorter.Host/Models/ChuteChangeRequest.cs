using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 改口请求DTO
/// </summary>
[SwaggerSchema(Description = "包裹改口请求，用于在分拣过程中动态修改包裹的目标格口")]
public record ChuteChangeRequest
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    /// <example>1001</example>
    [SwaggerSchema(Description = "需要改口的包裹的唯一标识符")]
    public required long ParcelId { get; init; }

    /// <summary>
    /// 请求的新目标格口ID
    /// </summary>
    /// <example>5</example>
    [SwaggerSchema(Description = "包裹需要改为的新目标格口编号")]
    public required long RequestedChuteId { get; init; }

    /// <summary>
    /// 请求时间（可选，默认为服务器当前时间）
    /// </summary>
    /// <example>2025-11-17T10:30:00Z</example>
    [SwaggerSchema(Description = "改口请求的时间戳，默认为服务器当前时间")]
    public DateTimeOffset? RequestedAt { get; init; }
}
