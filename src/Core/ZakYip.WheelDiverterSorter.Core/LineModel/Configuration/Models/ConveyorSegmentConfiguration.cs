using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 输送线段配置
/// </summary>
/// <remarks>
/// <para>定义摆轮前输送线段的物理参数，用于计算包裹传输时间、超时检测和丢失判定。</para>
/// <para>每个输送线段对应拓扑配置中的一个 SegmentId，描述从上一个节点（入口或摆轮）到下一个摆轮的输送带。</para>
/// </remarks>
public record class ConveyorSegmentConfiguration
{
    /// <summary>
    /// 线段唯一标识符（对应拓扑配置中的 SegmentId）
    /// </summary>
    /// <example>1</example>
    public required long SegmentId { get; init; }

    /// <summary>
    /// 线段显示名称
    /// </summary>
    /// <example>入口到摆轮D1</example>
    public string? SegmentName { get; init; }

    /// <summary>
    /// 线段长度（毫米）
    /// </summary>
    /// <remarks>
    /// 从上一个节点到当前节点的物理距离
    /// </remarks>
    /// <example>5000</example>
    public required double LengthMm { get; init; }

    /// <summary>
    /// 线速（毫米/秒）
    /// </summary>
    /// <remarks>
    /// 输送带的运行速度，用于计算包裹传输时间
    /// </remarks>
    /// <example>1000</example>
    public required decimal SpeedMmps { get; init; }

    /// <summary>
    /// 时间容差（毫秒）
    /// </summary>
    /// <remarks>
    /// <para>允许的时间偏差，用于判定超时阈值。</para>
    /// <para>超时阈值 = 理论传输时间 + 时间容差</para>
    /// </remarks>
    /// <example>500</example>
    public required long TimeToleranceMs { get; init; }

    /// <summary>
    /// 是否启用丢失检测
    /// </summary>
    /// <remarks>
    /// <para>当启用时，如果包裹在超时阈值后仍未到达下一个节点，判定为丢失。</para>
    /// <para>当禁用时，超时后继续等待，不判定丢失。</para>
    /// </remarks>
    public bool EnableLossDetection { get; init; } = true;

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 计算理论传输时间（毫秒）
    /// </summary>
    /// <returns>包裹通过此线段所需的理论时间</returns>
    /// <remarks>
    /// 传输时间 = (线段长度 / 线速) × 1000
    /// </remarks>
    public double CalculateTransitTimeMs()
    {
        return (LengthMm / (double)SpeedMmps) * 1000;
    }

    /// <summary>
    /// 计算超时阈值（毫秒）
    /// </summary>
    /// <returns>判定包裹超时的时间阈值</returns>
    /// <remarks>
    /// 超时阈值 = 理论传输时间 + 时间容差
    /// </remarks>
    public double CalculateTimeoutThresholdMs()
    {
        return CalculateTransitTimeMs() + TimeToleranceMs;
    }

    /// <summary>
    /// 验证包裹传输时间是否在正常范围内
    /// </summary>
    /// <param name="actualTimeMs">实际传输时间（毫秒）</param>
    /// <returns>验证结果：(是否正常, 是否超时, 是否丢失)</returns>
    public (bool IsNormal, bool IsTimeout, bool IsLost) ValidateTransitTime(double actualTimeMs)
    {
        var theoreticalTime = CalculateTransitTimeMs();
        var timeoutThreshold = CalculateTimeoutThresholdMs();

        // 正常：实际时间 <= 超时阈值
        var isNormal = actualTimeMs <= timeoutThreshold;

        // 超时：实际时间 > 超时阈值
        var isTimeout = actualTimeMs > timeoutThreshold;

        // 丢失：启用丢失检测 且 超时
        var isLost = EnableLossDetection && isTimeout;

        return (isNormal, isTimeout, isLost);
    }

    /// <summary>
    /// 获取默认线段配置（用于测试和初始化）
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <param name="now">当前时间</param>
    /// <returns>默认配置</returns>
    public static ConveyorSegmentConfiguration GetDefault(long segmentId, DateTime now)
    {
        return new ConveyorSegmentConfiguration
        {
            SegmentId = segmentId,
            SegmentName = $"输送段 {segmentId}",
            LengthMm = 5000,  // 默认 5 米
            SpeedMmps = 1000m,  // 默认 1 m/s
            TimeToleranceMs = 500,  // 默认 500 毫秒容差
            EnableLossDetection = true,
            Remarks = "默认配置",
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
