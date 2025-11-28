namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 面板 IO 配置选项。
/// 定义面板按钮和信号塔的启用状态及驱动模式。
/// </summary>
public sealed record class PanelIoOptions
{
    /// <summary>
    /// 是否启用面板 IO 功能。
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// 是否使用仿真模式。
    /// true: 使用仿真驱动（SimulatedPanelInputReader / SimulatedSignalTowerOutput）
    /// false: 使用真实硬件驱动
    /// </summary>
    public bool UseSimulation { get; init; } = true;

    /// <summary>
    /// 面板按钮轮询间隔（毫秒）。
    /// </summary>
    public int PollingIntervalMs { get; init; } = 100;

    /// <summary>
    /// 按钮防抖时间（毫秒）。
    /// 在此时间内的重复触发将被忽略。
    /// </summary>
    public int DebounceMs { get; init; } = 50;
}
