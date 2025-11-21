using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 面板按钮状态。
/// 表示单个按钮的当前状态，包括是否按下及最后变更时间。
/// </summary>
public readonly record struct PanelButtonState
{
    /// <summary>按钮类型</summary>
    public required PanelButtonType ButtonType { get; init; }

    /// <summary>是否按下</summary>
    public required bool IsPressed { get; init; }

    /// <summary>最后状态变更时间（UTC）</summary>
    public required DateTimeOffset LastChangedAt { get; init; }

    /// <summary>按下时长（毫秒）。如果未按下则为 0。</summary>
    public int PressedDurationMs { get; init; }
}
