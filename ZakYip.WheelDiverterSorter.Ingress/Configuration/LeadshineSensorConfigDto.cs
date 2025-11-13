using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 雷赛传感器配置DTO
/// </summary>
public class LeadshineSensorConfigDto {

    /// <summary>
    /// 传感器ID
    /// </summary>
    public string SensorId { get; set; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public SensorType Type { get; set; }

    /// <summary>
    /// 输入位索引
    /// </summary>
    public int InputBit { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}