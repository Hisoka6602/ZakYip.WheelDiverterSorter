using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 更新包裹丢失检测配置请求
/// </summary>
public record UpdateParcelLossDetectionConfigRequest
{
    /// <summary>
    /// 丢失检测系数
    /// </summary>
    /// <remarks>
    /// 丢失阈值 = 中位数间隔 * 丢失检测系数
    /// 默认值：1.5
    /// 推荐范围：1.5-2.5
    /// </remarks>
    /// <example>1.5</example>
    [Range(1.0, 5.0, ErrorMessage = "丢失检测系数必须在1.0-5.0之间")]
    public required double LostDetectionMultiplier { get; init; }
    
    /// <summary>
    /// 超时检测系数（可选）
    /// </summary>
    /// <remarks>
    /// 超时阈值 = 中位数间隔 * 超时检测系数
    /// 默认值：3.0
    /// 推荐范围：2.5-3.5
    /// </remarks>
    /// <example>3.0</example>
    [Range(1.5, 10.0, ErrorMessage = "超时检测系数必须在1.5-10.0之间")]
    public double? TimeoutMultiplier { get; init; }
}
