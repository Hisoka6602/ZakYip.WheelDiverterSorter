using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;

/// <summary>
/// 雷赛传感器配置选项
/// </summary>
public class LeadshineSensorOptions
{
    /// <summary>
    /// 控制器卡号
    /// </summary>
    public ushort CardNo { get; set; } = 0;

    /// <summary>
    /// 传感器配置列表
    /// </summary>
    public List<LeadshineSensorConfigDto> Sensors { get; set; } = new();
}
