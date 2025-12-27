namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 检测开关配置响应模型
/// </summary>
/// <remarks>
/// 统一管理两个检测开关的配置状态：干扰检测、超时检测
/// </remarks>
public record DetectionSwitchesDto
{
    /// <summary>
    /// 干扰检测开关（提前触发检测）
    /// </summary>
    /// <remarks>
    /// <para>当启用时，系统会在摆轮前传感器触发时检查 EarliestDequeueTime，防止包裹提前到达导致的错位问题。</para>
    /// <para>检测逻辑：</para>
    /// <list type="bullet">
    ///   <item>传感器触发时窥视队列头部任务</item>
    ///   <item>若当前时间 &lt; EarliestDequeueTime，判定为提前触发</item>
    ///   <item>提前触发时不出队、不执行摆轮动作，仅记录告警</item>
    /// </list>
    /// <para>默认值为 true（启用），防止包裹错位问题</para>
    /// </remarks>
    /// <example>true</example>
    public bool EnableInterferenceDetection { get; init; }

    /// <summary>
    /// 超时检测开关（包裹传输超时检测）
    /// </summary>
    /// <remarks>
    /// <para>当启用时，如果包裹在超时阈值后仍未到达下一个节点，判定为超时并路由到异常格口。</para>
    /// <para>当禁用时，超时后继续等待，不进行超时处理。</para>
    /// <para>超时阈值 = 理论传输时间 + 时间容差（来自输送线段配置）</para>
    /// <para>默认值为 false（禁用超时检测）</para>
    /// </remarks>
    /// <example>false</example>
    public bool EnableTimeoutDetection { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    /// <example>2025-12-25T15:00:00Z</example>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 检测开关配置更新请求模型
/// </summary>
/// <remarks>
/// 用于更新检测开关配置，所有字段均为可选，仅更新提供的字段
/// </remarks>
public record UpdateDetectionSwitchesRequest
{
    /// <summary>
    /// 干扰检测开关（提前触发检测）
    /// </summary>
    /// <remarks>
    /// <para>可选参数，如不提供则保持当前值不变</para>
    /// <para>当启用时，系统会在摆轮前传感器触发时检查 EarliestDequeueTime，防止包裹提前到达导致的错位问题。</para>
    /// <para>默认值为 true（启用），防止包裹错位问题</para>
    /// </remarks>
    /// <example>true</example>
    public bool? EnableInterferenceDetection { get; init; }

    /// <summary>
    /// 超时检测开关（包裹传输超时检测）
    /// </summary>
    /// <remarks>
    /// <para>可选参数，如不提供则保持当前值不变</para>
    /// <para>当启用时，如果包裹在超时阈值后仍未到达下一个节点，判定为超时并路由到异常格口。</para>
    /// <para>当禁用时，超时后继续等待，不进行超时处理。</para>
    /// <para>默认值为 false（禁用超时检测）</para>
    /// </remarks>
    /// <example>false</example>
    public bool? EnableTimeoutDetection { get; init; }
}
