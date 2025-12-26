using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.System;

/// <summary>
/// 运行模式枚举
/// Runtime mode enumeration for the system
/// </summary>
/// <remarks>
/// 用于区分生产和性能测试等不同运行场景，
/// 每种模式对应不同的驱动器和服务实现组合。
/// Used to differentiate between production and performance testing scenarios.
/// Each mode corresponds to different driver and service implementation combinations.
/// </remarks>
public enum RuntimeMode
{
    /// <summary>
    /// 生产模式 - 使用真实硬件驱动，连接实际上游系统
    /// Production mode - Using real hardware drivers, connecting to actual upstream systems
    /// </summary>
    [Description("生产模式")]
    Production = 0,

    /// <summary>
    /// 性能测试模式 - 跳过实际 IO，专注于路径/算法性能测试
    /// Performance test mode - Skip actual IO, focus on path/algorithm performance testing
    /// </summary>
    [Description("性能测试模式")]
    PerformanceTest = 2
}
