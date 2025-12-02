using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

/// <summary>
/// 分拣结果枚举
/// </summary>
/// <remarks>
/// 用于 SortingCompletedNotification 向上游报告包裹的最终分拣结果。
/// 
/// <para><b>使用场景</b>：</para>
/// <list type="bullet">
///   <item>Success - 正常落格，包裹到达目标格口</item>
///   <item>Timeout - 超时落格，包裹因超时被路由到异常格口</item>
///   <item>Lost - 包裹丢失，超出最大存活时间仍未落格</item>
///   <item>Failed - 执行失败，路径执行过程中发生错误</item>
/// </list>
/// </remarks>
public enum SortingOutcome
{
    /// <summary>
    /// 成功 - 包裹正常落入目标格口
    /// </summary>
    [Description("成功")]
    Success = 0,

    /// <summary>
    /// 超时 - 包裹因超时被路由到异常格口
    /// </summary>
    /// <remarks>
    /// 包括分配超时和落格超时两种情况。
    /// </remarks>
    [Description("超时")]
    Timeout = 1,

    /// <summary>
    /// 丢失 - 包裹超出最大存活时间仍未落格
    /// </summary>
    /// <remarks>
    /// 无法确定包裹位置，视为永久丢失。
    /// </remarks>
    [Description("丢失")]
    Lost = 2,

    /// <summary>
    /// 失败 - 路径执行过程中发生错误
    /// </summary>
    /// <remarks>
    /// 包括摆轮驱动失败、传感器故障等情况。
    /// </remarks>
    [Description("失败")]
    Failed = 3
}
