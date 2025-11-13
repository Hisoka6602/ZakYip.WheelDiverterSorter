using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 表示单个摆轮的配置条目，用于LiteDB存储
/// </summary>
public class DiverterConfigurationEntry
{
    /// <summary>
    /// 摆轮标识（数字ID，与硬件设备对应）
    /// </summary>
    public required int DiverterId { get; set; }

    /// <summary>
    /// 摆轮名称（可选）- Diverter Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "DIV-001"、"1号摆轮"
    /// </remarks>
    public string? DiverterName { get; set; }

    /// <summary>
    /// 目标摆轮转向方向
    /// </summary>
    /// <remarks>
    /// 可选值:
    /// - Straight (0): 直行通过
    /// - Left (1): 转向左侧格口
    /// - Right (2): 转向右侧格口
    /// </remarks>
    public required DiverterDirection TargetDirection { get; set; }

    /// <summary>
    /// 段的顺序号，从1开始
    /// </summary>
    public required int SequenceNumber { get; set; }
}
