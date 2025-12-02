namespace ZakYip.WheelDiverterSorter.Core.Sorting.Tracking;

/// <summary>
/// 包裹超时配置选项
/// </summary>
/// <remarks>
/// 配置包裹生命周期各阶段的超时时间限制。
/// 
/// <para><b>超时类型说明</b>：</para>
/// <list type="bullet">
///   <item>分配超时：从检测到收到格口分配的最大等待时间</item>
///   <item>落格超时：从分配格口到落格确认的最大等待时间</item>
///   <item>最大存活时间：包裹在系统中的最大存活时长，超过视为丢失</item>
/// </list>
/// 
/// <para><b>配置示例</b>：</para>
/// <code>
/// {
///   "ParcelTimeout": {
///     "DetectionToAssignmentTimeoutSeconds": 5,
///     "AssignmentToSortingTimeoutSeconds": 30,
///     "MaxLifetimeBeforeLostSeconds": 120
///   }
/// }
/// </code>
/// </remarks>
public sealed record class ParcelTimeoutOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ParcelTimeout";

    /// <summary>
    /// 分配超时：从检测到收到格口分配的最大允许时间
    /// </summary>
    /// <remarks>
    /// 当包裹检测后超过此时间仍未收到格口分配时，判定为"分配超时"。
    /// 超时后系统会尝试路由到异常格口，并通知上游 Outcome=Timeout。
    /// </remarks>
    public required TimeSpan DetectionToAssignmentTimeout { get; init; }

    /// <summary>
    /// 落格超时：从分配格口到落格确认的最大允许时间
    /// </summary>
    /// <remarks>
    /// 当包裹已分配格口后超过此时间仍未完成落格时，判定为"落格超时"。
    /// 超时后系统会尝试路由到异常格口（如果还能控制），并通知上游 Outcome=Timeout。
    /// </remarks>
    public required TimeSpan AssignmentToSortingTimeout { get; init; }

    /// <summary>
    /// 最大存活时间：包裹在系统中存活的最大时长，超过视为丢失
    /// </summary>
    /// <remarks>
    /// 当包裹从首次检测起超过此时间后仍未获得落格确认，
    /// 也无法通过传感器/编排状态确定位置时，判定为"包裹丢失"。
    /// </remarks>
    public required TimeSpan MaxLifetimeBeforeLost { get; init; }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    /// <returns>默认配置实例</returns>
    public static ParcelTimeoutOptions GetDefault() => new()
    {
        DetectionToAssignmentTimeout = TimeSpan.FromSeconds(5),
        AssignmentToSortingTimeout = TimeSpan.FromSeconds(30),
        MaxLifetimeBeforeLost = TimeSpan.FromSeconds(120)
    };

    /// <summary>
    /// 验证配置参数的有效性
    /// </summary>
    /// <returns>验证结果和错误消息</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (DetectionToAssignmentTimeout <= TimeSpan.Zero)
        {
            return (false, "分配超时时间必须大于零");
        }

        if (AssignmentToSortingTimeout <= TimeSpan.Zero)
        {
            return (false, "落格超时时间必须大于零");
        }

        if (MaxLifetimeBeforeLost <= TimeSpan.Zero)
        {
            return (false, "最大存活时间必须大于零");
        }

        if (MaxLifetimeBeforeLost <= DetectionToAssignmentTimeout + AssignmentToSortingTimeout)
        {
            return (false, "最大存活时间必须大于分配超时时间与落格超时时间之和");
        }

        return (true, null);
    }
}
