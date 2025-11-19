namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 摆轮配置DTO
/// </summary>
public class LeadshineDiverterConfigDto
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public int DiverterId { get; set; }

    /// <summary>
    /// 摆轮名称
    /// </summary>
    public required string DiverterName { get; set; }

    /// <summary>
    /// 连接输送线的长度（mm）
    /// </summary>
    public double ConnectedConveyorLengthMm { get; set; }

    /// <summary>
    /// 连接输送线的速度（mm/s）
    /// </summary>
    public double ConnectedConveyorSpeedMmPerSec { get; set; }

    /// <summary>
    /// 摆轮速度（mm/s）
    /// </summary>
    public double DiverterSpeedMmPerSec { get; set; }

    /// <summary>
    /// 输出起始位
    /// </summary>
    public required int OutputStartBit { get; set; }

    /// <summary>
    /// 反馈输入位（可选）
    /// </summary>
    public int? FeedbackInputBit { get; set; }
}
