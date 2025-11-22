using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 改口响应DTO
/// </summary>
[SwaggerSchema(Description = "包裹改口响应，包含改口请求的处理结果和详细信息")]
public class ChuteChangeResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "改口请求是否成功")]
    public required bool IsSuccess { get; set; }

    /// <summary>
    /// 包裹ID
    /// </summary>
    /// <example>1001</example>
    [SwaggerSchema(Description = "改口的包裹的唯一标识符")]
    public required long ParcelId { get; set; }

    /// <summary>
    /// 原目标格口ID
    /// </summary>
    /// <example>3</example>
    [SwaggerSchema(Description = "包裹原计划的目标格口编号")]
    public long? OriginalChuteId { get; set; }

    /// <summary>
    /// 请求的新格口ID
    /// </summary>
    /// <example>5</example>
    [SwaggerSchema(Description = "改口请求指定的新目标格口编号")]
    public required long RequestedChuteId { get; set; }

    /// <summary>
    /// 实际生效的格口ID
    /// </summary>
    /// <example>5</example>
    [SwaggerSchema(Description = "改口实际生效的格口编号，可能与请求的格口不同")]
    public long? EffectiveChuteId { get; set; }

    /// <summary>
    /// 决策结果（Accepted, IgnoredAlreadyCompleted, IgnoredExceptionRouted, RejectedInvalidState, RejectedTooLate）
    /// </summary>
    /// <example>Accepted</example>
    [SwaggerSchema(Description = "改口请求的决策结果类型")]
    public string? Outcome { get; set; }

    /// <summary>
    /// 结果消息或原因
    /// </summary>
    /// <example>改口成功，包裹路径已重新规划</example>
    [SwaggerSchema(Description = "改口处理结果的详细说明或失败原因")]
    public string? Message { get; set; }

    /// <summary>
    /// 处理时间
    /// </summary>
    /// <example>2025-11-17T10:30:01Z</example>
    [SwaggerSchema(Description = "改口请求处理完成的时间")]
    public DateTimeOffset ProcessedAt { get; set; }
}
