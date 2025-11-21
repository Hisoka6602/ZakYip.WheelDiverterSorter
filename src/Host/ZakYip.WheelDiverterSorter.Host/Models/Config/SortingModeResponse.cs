using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 分拣模式配置响应模型
/// </summary>
public class SortingModeResponse
{
    /// <summary>
    /// 分拣模式
    /// </summary>
    public SortingMode SortingMode { get; set; }

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// </summary>
    public int? FixedChuteId { get; set; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// </summary>
    public List<int> AvailableChuteIds { get; set; } = new();
}
