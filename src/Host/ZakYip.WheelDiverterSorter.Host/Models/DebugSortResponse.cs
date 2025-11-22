using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 调试接口的响应模型
/// </summary>
[SwaggerSchema(Description = "调试分拣接口的响应结果，包含分拣执行的详细信息")]
public class DebugSortResponse
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    /// <example>PKG001</example>
    [SwaggerSchema(Description = "包裹的唯一标识符")]
    public required string ParcelId { get; init; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "请求的目标格口编号")]
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 执行是否成功
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "分拣路径是否执行成功")]
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 实际落格的格口标识
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "包裹实际落入的格口编号")]
    public required long ActualChuteId { get; init; }

    /// <summary>
    /// 执行结果消息（中文）
    /// </summary>
    /// <example>分拣成功：包裹 PKG001 已送达格口 CHUTE-01</example>
    [SwaggerSchema(Description = "分拣执行结果的详细描述")]
    public required string Message { get; init; }

    /// <summary>
    /// 失败原因（如果失败）
    /// </summary>
    /// <example>null</example>
    [SwaggerSchema(Description = "如果分拣失败，此字段包含失败的具体原因")]
    public string? FailureReason { get; init; }

    /// <summary>
    /// 生成的路径段数量
    /// </summary>
    /// <example>3</example>
    [SwaggerSchema(Description = "生成的摆轮路径段数量，即需要操作的摆轮数量")]
    public int PathSegmentCount { get; init; }
}
