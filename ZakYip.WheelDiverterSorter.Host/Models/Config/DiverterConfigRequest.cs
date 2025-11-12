using System.ComponentModel.DataAnnotations;
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
    /// <example>DIV-001</example>
    [Required(ErrorMessage = "摆轮ID不能为空")]
    public required string DiverterId { get; set; }

    /// <summary>
    /// 目标摆轮角度（0, 30, 45, 90）
    /// </summary>
    /// <example>45</example>
    [Required(ErrorMessage = "目标角度不能为空")]
    public required DiverterAngle TargetAngle { get; set; }

    /// <summary>
    /// 段的顺序号，从1开始
    /// </summary>
    /// <example>1</example>
    [Required(ErrorMessage = "顺序号不能为空")]
    [Range(1, int.MaxValue, ErrorMessage = "顺序号必须大于0")]
    public required int SequenceNumber { get; set; }
}
