namespace ZakYip.WheelDiverterSorter.Simulation.Configuration;

/// <summary>
/// 掉包模型配置选项
/// </summary>
/// <remarks>
/// 定义包裹在传送带各段上掉落的概率和允许掉落的段。
/// 掉包后，包裹不再产生后续传感器事件，并在结果中标记为 Dropped 状态。
/// </remarks>
public record class DropoutModelOptions
{
    /// <summary>
    /// 每段的掉包概率（0.0-1.0）
    /// </summary>
    /// <remarks>
    /// 例如 0.05 表示每段有 5% 的概率掉包
    /// </remarks>
    public decimal DropoutProbabilityPerSegment { get; init; } = 0.0m;

    /// <summary>
    /// 允许掉包的传感器段列表（为空表示所有段都允许）
    /// </summary>
    /// <remarks>
    /// 例如 ["D1-D2", "D2-D3"] 表示只能在这两段之间掉包
    /// 段的命名格式：入口传感器用 "Entry"，摆轮传感器用 "D1", "D2", "D3" 等
    /// </remarks>
    public IReadOnlyList<string>? AllowedSegments { get; init; }

    /// <summary>
    /// 随机数种子（用于确定性测试）
    /// </summary>
    public int? Seed { get; init; }
}
