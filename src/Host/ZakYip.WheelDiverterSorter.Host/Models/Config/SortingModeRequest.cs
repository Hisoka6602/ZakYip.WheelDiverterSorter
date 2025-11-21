using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 分拣模式配置请求模型
/// </summary>
public class SortingModeRequest
{
    /// <summary>
    /// 分拣模式
    /// </summary>
    /// <example>Formal</example>
    [Required(ErrorMessage = "分拣模式不能为空")]
    public SortingMode SortingMode { get; set; }

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// </summary>
    /// <example>1</example>
    public int? FixedChuteId { get; set; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// </summary>
    /// <example>[1, 2, 3, 4, 5, 6]</example>
    public List<int>? AvailableChuteIds { get; set; }
}
