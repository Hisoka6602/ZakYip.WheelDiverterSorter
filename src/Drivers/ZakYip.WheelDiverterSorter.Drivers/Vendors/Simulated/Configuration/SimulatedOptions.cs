namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated.Configuration;

/// <summary>
/// 仿真驱动器配置选项
/// </summary>
/// <remarks>
/// 用于配置仿真驱动器的行为参数，如延迟时间等。
/// </remarks>
public class SimulatedOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Drivers:Simulated";

    /// <summary>
    /// 仿真命令执行延迟（毫秒）
    /// </summary>
    public int CommandDelayMs { get; set; } = 50;

    /// <summary>
    /// 仿真连接建立延迟（毫秒）
    /// </summary>
    public int ConnectionDelayMs { get; set; } = 100;

    /// <summary>
    /// 是否启用随机故障注入
    /// </summary>
    public bool EnableRandomFailures { get; set; } = false;

    /// <summary>
    /// 随机故障概率（0.0-1.0）
    /// </summary>
    public double FailureProbability { get; set; } = 0.01;
}
