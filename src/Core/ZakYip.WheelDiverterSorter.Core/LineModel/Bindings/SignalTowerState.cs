using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 信号塔单个通道的状态。
/// 表示三色灯或蜂鸣器的亮灯/闪烁状态。
/// </summary>
public readonly record struct SignalTowerState
{
    /// <summary>信号通道类型</summary>
    public required SignalTowerChannel Channel { get; init; }

    /// <summary>是否点亮或激活</summary>
    public required bool IsActive { get; init; }

    /// <summary>是否闪烁</summary>
    public bool IsBlinking { get; init; }

    /// <summary>闪烁间隔（毫秒）。仅在 IsBlinking 为 true 时有效。</summary>
    public int BlinkIntervalMs { get; init; }

    /// <summary>持续时长（毫秒）。0 表示持续保持，直到状态改变。</summary>
    public int DurationMs { get; init; }

    /// <summary>创建一个简单的点亮状态</summary>
    public static SignalTowerState CreateOn(SignalTowerChannel channel) =>
        new()
        {
            Channel = channel,
            IsActive = true,
            IsBlinking = false,
            BlinkIntervalMs = 0,
            DurationMs = 0
        };

    /// <summary>创建一个简单的熄灭状态</summary>
    public static SignalTowerState CreateOff(SignalTowerChannel channel) =>
        new()
        {
            Channel = channel,
            IsActive = false,
            IsBlinking = false,
            BlinkIntervalMs = 0,
            DurationMs = 0
        };

    /// <summary>创建一个闪烁状态</summary>
    public static SignalTowerState CreateBlinking(SignalTowerChannel channel, int intervalMs = 500) =>
        new()
        {
            Channel = channel,
            IsActive = true,
            IsBlinking = true,
            BlinkIntervalMs = intervalMs,
            DurationMs = 0
        };
}
