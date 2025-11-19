using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;

/// <summary>
/// 降级模式枚举
/// Degradation mode for the sorting system
/// </summary>
public enum DegradationMode
{
    /// <summary>
    /// 正常模式：所有节点正常运行
    /// Normal: All nodes operating normally
    /// </summary>
    [Description("正常")]
    None = 0,

    /// <summary>
    /// 节点降级：部分节点不可用，系统部分功能降级运行
    /// Node Degraded: Some nodes unavailable, system operating with reduced capacity
    /// </summary>
    [Description("节点降级")]
    NodeDegraded = 1,

    /// <summary>
    /// 线体降级：多个关键节点不可用，整体线体功能受限
    /// Line Degraded: Multiple critical nodes unavailable, line functionality limited
    /// </summary>
    [Description("线体降级")]
    LineDegraded = 2
}
