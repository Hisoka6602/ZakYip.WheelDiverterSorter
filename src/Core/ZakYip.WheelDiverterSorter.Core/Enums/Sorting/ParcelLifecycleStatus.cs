using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

/// <summary>
/// 包裹生命周期状态枚举
/// </summary>
/// <remarks>
/// 定义包裹从检测到完成落格/丢失的完整生命周期状态。
/// 
/// <para><b>状态流转</b>：</para>
/// <code>
/// Detected → Assigned → Routing → Sorted (成功)
///     ↓          ↓         ↓
///  TimedOut  TimedOut  TimedOut (超时)
///     ↓          ↓         ↓
///    Lost      Lost      Lost (丢失)
/// </code>
/// 
/// <para><b>超时与丢失的区别</b>：</para>
/// <list type="bullet">
///   <item>超时：在特定阶段的时间窗口内未能完成对应步骤</item>
///   <item>丢失：超出系统允许的最大存活时间，仍未完成落格且无法确定位置</item>
/// </list>
/// </remarks>
public enum ParcelLifecycleStatus
{
    /// <summary>
    /// 已检测 - 入口传感器检测到包裹，已创建跟踪记录
    /// </summary>
    [Description("已检测")]
    Detected = 0,

    /// <summary>
    /// 已分配格口 - 已收到上游格口分配或本地策略确定目标格口
    /// </summary>
    [Description("已分配格口")]
    Assigned = 1,

    /// <summary>
    /// 路径执行中 - 正在执行摆轮切换路径
    /// </summary>
    [Description("路径执行中")]
    Routing = 2,

    /// <summary>
    /// 已完成落格 - 包裹已成功落入目标格口
    /// </summary>
    [Description("已完成落格")]
    Sorted = 3,

    /// <summary>
    /// 已超时 - 在特定阶段超过允许的时间窗口
    /// </summary>
    /// <remarks>
    /// 包括：
    /// <list type="bullet">
    ///   <item>分配超时：检测后超过 DetectionToAssignmentTimeout 未收到格口分配</item>
    ///   <item>落格超时：分配后超过 AssignmentToSortingTimeout 未完成落格</item>
    /// </list>
    /// </remarks>
    [Description("已超时")]
    TimedOut = 4,

    /// <summary>
    /// 已丢失 - 超过系统最大允许存活时间，仍未完成落格且无法确定位置
    /// </summary>
    /// <remarks>
    /// 当包裹超过 MaxLifetimeBeforeLost 时间后仍未完成落格，
    /// 且无法通过任何传感器或编排状态确定其位置时，判定为丢失。
    /// </remarks>
    [Description("已丢失")]
    Lost = 5
}
