namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 信号塔配置选项。
/// 定义三色灯和蜂鸣器的默认行为参数。
/// </summary>
public sealed record class SignalTowerOptions
{
    /// <summary>
    /// 是否启用信号塔功能。
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// 默认闪烁间隔（毫秒）。
    /// </summary>
    public int DefaultBlinkIntervalMs { get; init; } = 500;

    /// <summary>
    /// 蜂鸣器最大持续时长（毫秒）。
    /// 超过此时长后自动关闭，防止持续响铃。0 表示不限制。
    /// </summary>
    public int BuzzerMaxDurationMs { get; init; } = 10000;

    /// <summary>
    /// 是否在系统启动时测试所有通道（亮灯测试）。
    /// </summary>
    public bool TestAllChannelsOnStartup { get; init; } = true;

    /// <summary>
    /// 启动测试时每个通道的点亮时长（毫秒）。
    /// </summary>
    public int StartupTestDurationMs { get; init; } = 500;
}
