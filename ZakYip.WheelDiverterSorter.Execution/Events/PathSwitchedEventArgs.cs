using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Execution.Events;

/// <summary>
/// 路径切换事件参数
/// </summary>
public readonly record struct PathSwitchedEventArgs
{
    /// <summary>
    /// 包裹标识
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 原始路径
    /// </summary>
    public required SwitchingPath OriginalPath { get; init; }

    /// <summary>
    /// 备用路径
    /// </summary>
    public required SwitchingPath BackupPath { get; init; }

    /// <summary>
    /// 切换原因
    /// </summary>
    public required string SwitchReason { get; init; }

    /// <summary>
    /// 切换时间
    /// </summary>
    public required DateTimeOffset SwitchTime { get; init; }
}
