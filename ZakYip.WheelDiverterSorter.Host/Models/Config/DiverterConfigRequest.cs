using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 摆轮配置请求模型
/// </summary>
public class DiverterConfigRequest {
    /// <summary>
    /// 摆轮标识或设备ID
    /// </summary>
    /// <example>DIV-001</example>
    [Required(ErrorMessage = "摆轮ID不能为空")]
    public string DiverterId { get; set; }

    /// <summary>
    /// 目标摆轮转向方向（直行、左转、右转）
    /// </summary>
    /// <example>Left</example>
    [Required(ErrorMessage = "目标方向不能为空")]
    public DiverterDirection TargetDirection { get; set; }

    /// <summary>
    /// 段的顺序号，从1开始
    /// </summary>
    /// <example>1</example>
    [Required(ErrorMessage = "顺序号不能为空")]
    [Range(1, int.MaxValue, ErrorMessage = "顺序号必须大于0")]
    public int SequenceNumber { get; set; }
}