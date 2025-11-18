using System.ComponentModel;

namespace ZakYip.Sorting.Core.Models;

/// <summary>
/// 拥堵级别枚举
/// Congestion level for the sorting system
/// </summary>
public enum CongestionLevel
{
    /// <summary>
    /// 正常 - 系统运行正常，无拥堵
    /// Normal - System is running normally without congestion
    /// </summary>
    [Description("正常")]
    Normal = 0,

    /// <summary>
    /// 警告 - 系统出现轻微拥堵迹象
    /// Warning - System shows signs of mild congestion
    /// </summary>
    [Description("警告")]
    Warning = 1,

    /// <summary>
    /// 严重 - 系统严重拥堵
    /// Severe - System is severely congested
    /// </summary>
    [Description("严重")]
    Severe = 2
}
