namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 雷赛传感器配置DTO
/// </summary>
public class LeadshineSensorConfigDto
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorType Type { get; set; }

    /// <summary>
    /// 输入位索引
    /// </summary>
    public required int InputBit { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
