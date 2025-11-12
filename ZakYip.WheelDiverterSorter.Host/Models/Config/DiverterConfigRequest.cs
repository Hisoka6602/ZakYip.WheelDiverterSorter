using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 摆轮配置请求模型
/// </summary>
public class DiverterConfigRequest
{
    /// <summary>
    /// 摆轮标识或设备ID
    /// </summary>
    public required string DiverterId { get; set; }

    /// <summary>
    /// 目标摆轮角度（0, 30, 45, 90）
    /// </summary>
    public required DiverterAngle TargetAngle { get; set; }

    /// <summary>
    /// 段的顺序号，从1开始
    /// </summary>
    public required int SequenceNumber { get; set; }
}
