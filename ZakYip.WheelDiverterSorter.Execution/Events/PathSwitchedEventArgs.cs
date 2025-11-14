using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Execution.Events;

/// <summary>
/// 备用路径已计算事件参数
/// </summary>
/// <remarks>
/// 注意：此事件表示系统已计算出备用路径，但该路径尚未被执行。
/// 事件名称"PathSwitched"具有历史原因，实际上应该理解为"BackupPathCalculated"。
/// 订阅者如果需要自动故障转移，必须显式执行BackupPath。
/// </remarks>
public class PathSwitchedEventArgs : EventArgs
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
