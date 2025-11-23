namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 摆轮配置DTO
/// </summary>
public record LeadshineDiverterConfigDto
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public required int DiverterId { get; init; }

    /// <summary>
    /// 摆轮名称
    /// </summary>
    public required string DiverterName { get; init; }

    /// <summary>
    /// 连接输送线的长度（mm）
    /// </summary>
    public required double ConnectedConveyorLengthMm { get; init; }

    /// <summary>
    /// 连接输送线的速度（mm/s）
    /// </summary>
    public required double ConnectedConveyorSpeedMmPerSec { get; init; }

    /// <summary>
    /// 摆轮速度（mm/s）
    /// </summary>
    public required double DiverterSpeedMmPerSec { get; init; }

    /// <summary>
    /// 输出起始位
    /// </summary>
    public required int OutputStartBit { get; init; }

    /// <summary>
    /// 反馈输入位（可选）
    /// </summary>
    public int? FeedbackInputBit { get; init; }
}
