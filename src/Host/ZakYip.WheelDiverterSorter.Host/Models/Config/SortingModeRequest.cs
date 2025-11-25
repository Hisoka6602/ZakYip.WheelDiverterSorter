using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 分拣模式配置请求模型
/// Sorting mode configuration request model
/// </summary>
/// <remarks>
/// 用于配置系统的分拣模式
/// </remarks>
[SwaggerSchema(Description = "分拣模式配置请求，支持正式模式、固定格口和循环格口三种模式")]
public record SortingModeRequest
{
    /// <summary>
    /// 分拣模式
    /// Sorting mode
    /// </summary>
    /// <example>Formal</example>
    [Required(ErrorMessage = "分拣模式不能为空")]
    [SwaggerSchema(Description = "系统运行模式：Formal（正式）、FixedChute（固定格口）、RoundRobin（循环格口）")]
    public required SortingMode SortingMode { get; init; }

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// Fixed chute ID (only used in FixedChute mode)
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "固定格口模式下的目标格口编号，仅在SortingMode为FixedChute时必填")]
    public long? FixedChuteId { get; init; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// Available chute IDs (only used in RoundRobin mode)
    /// </summary>
    /// <example>[1, 2, 3, 4, 5, 6]</example>
    [SwaggerSchema(Description = "循环格口模式下的可用格口编号列表，仅在SortingMode为RoundRobin时必填")]
    public List<long>? AvailableChuteIds { get; init; }
}
