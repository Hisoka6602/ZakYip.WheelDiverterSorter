using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口分配超时配置请求模型
/// </summary>
public record ChuteAssignmentTimeoutRequest
{
    /// <summary>
    /// 安全系数（范围：0.1 ~ 1.0，默认：0.9）
    /// </summary>
    /// <remarks>
    /// <para>实际等待时间 = 理论物理极限时间 × SafetyFactor</para>
    /// <para>安全系数确保在包裹到达第一个摆轮决策点之前获得格口分配结果</para>
    /// <para>不允许配置超过1.0的安全系数，因为这会超过物理极限</para>
    /// </remarks>
    [Required(ErrorMessage = "安全系数不能为空")]
    [Range(0.1, 1.0, ErrorMessage = "安全系数必须在0.1到1.0之间")]
    public required decimal SafetyFactor { get; init; }

    /// <summary>
    /// 降级策略的默认超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 当无法计算理论物理极限时间时使用的保守默认值
    /// </remarks>
    [Range(1, 60, ErrorMessage = "降级超时时间必须在1到60秒之间")]
    public decimal FallbackTimeoutSeconds { get; init; } = 5m;
}
