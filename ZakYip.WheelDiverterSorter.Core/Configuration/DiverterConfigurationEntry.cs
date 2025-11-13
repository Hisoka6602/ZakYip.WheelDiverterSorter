namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 表示单个摆轮的配置条目，用于LiteDB存储
/// </summary>
public class DiverterConfigurationEntry
{
    /// <summary>
    /// 摆轮标识或设备ID
    /// </summary>
    public required string DiverterId { get; set; }

    /// <summary>
    /// 目标摆轮转向方向
    /// </summary>
    public required DiverterDirection TargetDirection { get; set; }

    /// <summary>
    /// 段的顺序号，从1开始
    /// </summary>
    public required int SequenceNumber { get; set; }
}
