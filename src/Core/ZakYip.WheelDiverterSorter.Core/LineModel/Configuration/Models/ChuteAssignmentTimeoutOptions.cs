namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 格口分配超时配置选项
/// </summary>
/// <remarks>
/// 用于配置格口分配等待超时的安全系数，以及包裹生命周期超时/丢失判定参数。
/// PR-NOSHADOW-ALL: 扩展原有配置，添加丢失判定参数，避免新增配置类。
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
    /// 从检测到格口分配的最大允许时间（秒）
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 超过此时间未收到格口分配，判定为"分配超时"。
    /// 默认值 5 秒。
    /// </remarks>
    public decimal DetectionToAssignmentTimeoutSeconds { get; set; } = 5m;

    /// <summary>
    /// 从格口分配到落格确认的最大允许时间（秒）
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 超过此时间未完成落格，判定为"落格超时"。
    /// 默认值 30 秒。
    /// </remarks>
    public decimal AssignmentToSortingTimeoutSeconds { get; set; } = 30m;

    /// <summary>
    /// 包裹在系统中的最大存活时间（秒）
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 从首次检测时间起，若超过此时间仍未完成落格，
    /// 且无法通过任何传感器/编排状态确定位置，则判定为"包裹丢失"。
    /// 默认值 120 秒（2分钟）。
    /// </remarks>
    public decimal MaxLifetimeBeforeLostSeconds { get; set; } = 120m;

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

        if (DetectionToAssignmentTimeoutSeconds < 1m || DetectionToAssignmentTimeoutSeconds > 60m)
        {
            return (false, "检测到分配超时时间必须在1到60秒之间");
        }

        if (AssignmentToSortingTimeoutSeconds < 5m || AssignmentToSortingTimeoutSeconds > 300m)
        {
            return (false, "分配到落格超时时间必须在5到300秒之间");
        }

        if (MaxLifetimeBeforeLostSeconds < 30m || MaxLifetimeBeforeLostSeconds > 600m)
        {
            return (false, "最大存活时间必须在30到600秒之间");
        }

        // PR-NOSHADOW-ALL: 最大存活时间必须大于等于任一超时时间
        if (MaxLifetimeBeforeLostSeconds < DetectionToAssignmentTimeoutSeconds &&
            MaxLifetimeBeforeLostSeconds < AssignmentToSortingTimeoutSeconds)
        {
            return (false, "最大存活时间必须至少大于等于一种超时时间");
        }

        return (true, null);
    }
}
