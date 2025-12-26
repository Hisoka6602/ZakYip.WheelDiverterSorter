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
    Production = 0
}
