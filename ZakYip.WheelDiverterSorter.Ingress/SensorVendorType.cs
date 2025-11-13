namespace ZakYip.WheelDiverterSorter.Ingress;

/// <summary>
/// 传感器厂商类型
/// </summary>
public enum SensorVendorType
{
    /// <summary>
    /// 模拟传感器（用于测试）
    /// </summary>
    Mock = 0,

    /// <summary>
    /// 雷赛（Leadshine）传感器
    /// </summary>
    Leadshine = 1,

    /// <summary>
    /// 西门子（Siemens）传感器
    /// </summary>
    Siemens = 2,

    /// <summary>
    /// 三菱（Mitsubishi）传感器
    /// </summary>
    Mitsubishi = 3,

    /// <summary>
    /// 欧姆龙（Omron）传感器
    /// </summary>
    Omron = 4
}
