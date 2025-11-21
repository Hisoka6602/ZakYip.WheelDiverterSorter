using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.System;

/// <summary>
/// 告警严重程度 / Alert Severity Level
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// 信息级告警 - 用于通知性消息
    /// Info - Informational messages
    /// </summary>
    [Description("信息")]
    Info = 0,

    /// <summary>
    /// 警告级告警 - 需要关注但不紧急
    /// Warning - Requires attention but not urgent
    /// </summary>
    [Description("警告")]
    Warning = 1,

    /// <summary>
    /// 严重级告警 - 需要立即处理
    /// Critical - Requires immediate attention
    /// </summary>
    [Description("严重")]
    Critical = 2
}
