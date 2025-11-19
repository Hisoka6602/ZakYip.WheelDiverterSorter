namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛摆轮配置
/// </summary>
public class LeadshineDiverterConfig
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public int DiverterId { get; init; }

    /// <summary>
    /// 摆轮名称
    /// </summary>
    public required string DiverterName { get; init; }

    /// <summary>
    /// 连接输送线的长度（mm）
    /// </summary>
    public double ConnectedConveyorLengthMm { get; init; }

    /// <summary>
    /// 连接输送线的速度（mm/s）
    /// </summary>
    public double ConnectedConveyorSpeedMmPerSec { get; init; }

    /// <summary>
    /// 摆轮速度（mm/s）
    /// </summary>
    public double DiverterSpeedMmPerSec { get; init; }

    /// <summary>
    /// 输出起始位（用于控制摆轮角度）
    /// </summary>
    public required int OutputStartBit { get; init; }

    /// <summary>
    /// 可选的反馈输入位（用于读取摆轮状态）
    /// </summary>
    public int? FeedbackInputBit { get; init; }
}
