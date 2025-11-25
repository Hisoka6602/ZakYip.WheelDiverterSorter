using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 分拣模式配置响应模型
/// Sorting mode configuration response model
/// </summary>
/// <remarks>
/// 返回当前的分拣模式配置
/// </remarks>
[SwaggerSchema(Description = "分拣模式配置响应，包含当前的运行模式和相关格口配置")]
public record SortingModeResponse
{
    /// <summary>
    /// 分拣模式
    /// Sorting mode
    /// </summary>
    /// <example>Formal</example>
    [SwaggerSchema(Description = "当前系统运行模式：Formal、FixedChute、RoundRobin")]
    public required SortingMode SortingMode { get; init; }

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// Fixed chute ID (only used in FixedChute mode)
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "固定格口模式下的目标格口编号")]
    public int? FixedChuteId { get; init; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// Available chute IDs (only used in RoundRobin mode)
    /// </summary>
    /// <example>[1, 2, 3]</example>
    [SwaggerSchema(Description = "循环格口模式下的可用格口编号列表")]
    public List<int> AvailableChuteIds { get; init; } = new();
}
