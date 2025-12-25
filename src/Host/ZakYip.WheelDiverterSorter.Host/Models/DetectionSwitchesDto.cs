namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 检测开关配置响应模型
/// </summary>
/// <remarks>
/// 统一管理三个检测开关的配置状态：干扰检测、超时检测、包裹丢失检测
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
    /// <para>默认值为 false（禁用），确保向后兼容性</para>
    /// </remarks>
    /// <example>false</example>
    public bool EnableInterferenceDetection { get; init; }

    /// <summary>
    /// 超时检测开关（包裹传输超时检测）
    /// </summary>
    /// <remarks>
    /// <para>当启用时，如果包裹在超时阈值后仍未到达下一个节点，判定为超时并路由到异常格口。</para>
    /// <para>当禁用时，超时后继续等待，不进行超时处理。</para>
    /// <para>超时阈值 = 理论传输时间 + 时间容差（来自输送线段配置）</para>
    /// </remarks>
    /// <example>true</example>
    public bool EnableTimeoutDetection { get; init; }

    /// <summary>
    /// 包裹丢失检测开关
    /// </summary>
    /// <remarks>
    /// <para><b>控制范围</b>：</para>
    /// <list type="bullet">
    /// <item><description>后台主动巡检服务（ParcelLossMonitoringService）：巡检是否执行</description></item>
    /// <item><description>队列任务的丢失判定字段（LostDetectionDeadline）：是否设置和使用</description></item>
    /// </list>
    /// <para><b>启用（true）</b>：</para>
    /// <list type="bullet">
    /// <item><description>后台服务定期巡检队列，检测超时丢失的包裹</description></item>
    /// <item><description>包裹超过阈值未到达时，触发丢失处理（清除任务、记录日志、触发事件）</description></item>
    /// </list>
    /// <para><b>禁用（false）</b>：</para>
    /// <list type="bullet">
    /// <item><description>后台服务跳过所有巡检逻辑，不执行任何检测</description></item>
    /// <item><description>包裹不会因为超时/丢失而被自动移除，只能通过正常到达或手动清空队列</description></item>
    /// </list>
    /// <para><b>默认值</b>：true（启用检测，确保系统能自动清理丢失包裹）</para>
    /// </remarks>
    /// <example>true</example>
    public bool EnableParcelLossDetection { get; init; }

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
    /// </remarks>
    /// <example>false</example>
    public bool? EnableInterferenceDetection { get; init; }

    /// <summary>
    /// 超时检测开关（包裹传输超时检测）
    /// </summary>
    /// <remarks>
    /// <para>可选参数，如不提供则保持当前值不变</para>
    /// <para>当启用时，如果包裹在超时阈值后仍未到达下一个节点，判定为超时并路由到异常格口。</para>
    /// <para>当禁用时，超时后继续等待，不进行超时处理。</para>
    /// </remarks>
    /// <example>true</example>
    public bool? EnableTimeoutDetection { get; init; }

    /// <summary>
    /// 包裹丢失检测开关
    /// </summary>
    /// <remarks>
    /// <para>可选参数，如不提供则保持当前值不变</para>
    /// <para>控制整个包裹丢失检测功能的开关</para>
    /// <para>true: 启用丢失检测和超时检测</para>
    /// <para>false: 关闭所有检测，包裹不会因超时或丢失而被移除</para>
    /// </remarks>
    /// <example>true</example>
    public bool? EnableParcelLossDetection { get; init; }
}
