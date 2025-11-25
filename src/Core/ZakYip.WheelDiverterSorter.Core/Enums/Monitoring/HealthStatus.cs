using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

/// <summary>
/// 健康状态枚举
/// Health status for system components
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// 健康 - 组件运行正常
    /// Healthy - Component is operating normally
    /// </summary>
    [Description("健康")]
    Healthy = 0,

    /// <summary>
    /// 不健康 - 组件存在问题
    /// Unhealthy - Component has issues
    /// </summary>
    [Description("不健康")]
    Unhealthy = 1,

    /// <summary>
    /// 未知 - 无法确定组件状态
    /// Unknown - Component status cannot be determined
    /// </summary>
    [Description("未知")]
    Unknown = 2
}
