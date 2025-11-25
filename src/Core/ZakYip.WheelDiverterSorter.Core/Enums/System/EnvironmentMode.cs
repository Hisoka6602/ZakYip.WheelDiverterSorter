using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.System;

/// <summary>
/// 运行环境模式枚举
/// Environment mode for the system
/// </summary>
public enum EnvironmentMode
{
    /// <summary>
    /// 正式环境 - 使用真实硬件驱动
    /// Production - Using real hardware drivers
    /// </summary>
    [Description("正式环境")]
    Production = 0,

    /// <summary>
    /// 仿真环境 - 使用模拟驱动
    /// Simulation - Using simulated drivers
    /// </summary>
    [Description("仿真环境")]
    Simulation = 1
}
