namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 格口分配超时配置选项
/// </summary>
/// <remarks>
/// 用于配置格口分配等待超时的安全系数。
/// PR-NOSHADOW-ALL: 落格超时和最大存活时间由程序根据输送线长度和速度计算，不需要配置。
/// </remarks>
public class ChuteAssignmentTimeoutOptions
{
    /// <summary>
    /// 安全系数（范围：0.1 ~ 1.0，默认：0.9）
    /// </summary>
    /// <remarks>
    /// <para>实际等待时间 = 理论物理极限时间 × SafetyFactor</para>
    /// <para>安全系数确保在包裹到达第一个摆轮决策点之前获得格口分配结果</para>
    /// <para>不允许配置超过1.0的安全系数，因为这会超过物理极限</para>
    /// </remarks>
    public decimal SafetyFactor { get; set; } = 0.9m;

    /// <summary>
    /// 降级策略的默认超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 当无法计算理论物理极限时间时使用的保守默认值
    /// </remarks>
    public decimal FallbackTimeoutSeconds { get; set; } = 5m;

    /// <summary>
    /// 丢失判定的安全系数（范围：1.0 ~ 3.0，默认：1.5）
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 包裹最大存活时间 = 理论通过时间 × LostDetectionSafetyFactor。
    /// 超过此时间仍未完成落格，且无法确定位置，则判定为"包裹丢失"。
    /// 系数大于1是为了容忍输送线速度波动和传感器延迟。
    /// </remarks>
    public decimal LostDetectionSafetyFactor { get; set; } = 1.5m;

    /// <summary>
    /// 验证配置参数的有效性
    /// </summary>
    /// <returns>验证结果和错误消息</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (SafetyFactor < 0.1m || SafetyFactor > 1.0m)
        {
            return (false, "安全系数必须在0.1到1.0之间");
        }

        if (FallbackTimeoutSeconds < 1m || FallbackTimeoutSeconds > 60m)
        {
            return (false, "降级超时时间必须在1到60秒之间");
        }

        if (LostDetectionSafetyFactor < 1.0m || LostDetectionSafetyFactor > 3.0m)
        {
            return (false, "丢失判定安全系数必须在1.0到3.0之间");
        }

        return (true, null);
    }
}
