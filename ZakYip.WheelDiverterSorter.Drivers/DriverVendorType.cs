namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 驱动器厂商类型
/// </summary>
public enum DriverVendorType
{
    /// <summary>
    /// 模拟驱动器（用于测试）
    /// </summary>
    Mock = 0,

    /// <summary>
    /// 雷赛（Leadshine）控制器
    /// </summary>
    Leadshine = 1,

    /// <summary>
    /// 西门子（Siemens）PLC
    /// </summary>
    Siemens = 2,

    /// <summary>
    /// 三菱（Mitsubishi）PLC
    /// </summary>
    Mitsubishi = 3,

    /// <summary>
    /// 欧姆龙（Omron）PLC
    /// </summary>
    Omron = 4
}
