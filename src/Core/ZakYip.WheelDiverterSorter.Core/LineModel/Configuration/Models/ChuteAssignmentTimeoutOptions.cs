namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 格口分配超时配置选项
/// </summary>
/// <remarks>
/// 用于配置格口分配等待超时的安全系数
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

        return (true, null);
    }
}
