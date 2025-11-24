using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

/// <summary>
/// 分拣路由计划生命周期状态。
/// </summary>
public enum RoutePlanStatus
{
    /// <summary>计划已创建，尚未锁定执行。</summary>
    [Description("已创建")]
    Created,

    /// <summary>计划已锁定并正在执行（可酌情允许改口）。</summary>
    [Description("执行中")]
    Executing,

    /// <summary>已完成分拣（包裹已确认落入某格口）。</summary>
    [Description("已完成")]
    Completed,

    /// <summary>已进入异常路径（已分配到异常口，或正在往异常口路由）。</summary>
    [Description("异常路由")]
    ExceptionRouted,

    /// <summary>计划失败或被废弃，不再接受改口。</summary>
    [Description("失败")]
    Failed
}
