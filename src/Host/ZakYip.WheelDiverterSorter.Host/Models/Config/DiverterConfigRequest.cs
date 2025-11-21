using ZakYip.WheelDiverterSorter.Core.LineModel;
using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 摆轮配置请求模型
/// </summary>
[SwaggerSchema(Description = "单个摆轮的配置信息，包括摆轮ID、目标方向和段参数")]
public class DiverterConfigRequest {

    /// <summary>
    /// 摆轮标识或设备ID
    /// </summary>
    /// <example>1</example>
    [Required(ErrorMessage = "摆轮ID不能为空")]
    [SwaggerSchema(Description = "摆轮设备的唯一编号")]
    public int DiverterId { get; set; }

    /// <summary>
    /// 目标摆轮转向方向（直行、左转、右转）
    /// </summary>
    /// <example>Left</example>
    [Required(ErrorMessage = "目标方向不能为空")]
    [SwaggerSchema(Description = "摆轮的转向方向，可选值：Straight（直行）、Left（左转）、Right（右转）")]
    public DiverterDirection TargetDirection { get; set; }

    /// <summary>
    /// 段的顺序号，从1开始
    /// </summary>
    /// <example>1</example>
    [Required(ErrorMessage = "顺序号不能为空")]
    [Range(1, int.MaxValue, ErrorMessage = "顺序号必须大于0")]
    [SwaggerSchema(Description = "摆轮在路径中的执行顺序号，从1开始连续编号")]
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 到达本摆轮的皮带段长度（毫米）- Segment Belt Length (mm)
    /// </summary>
    /// <example>5000.0</example>
    [Range(100, 1000000, ErrorMessage = "段长度必须在100到1000000毫米之间")]
    [SwaggerSchema(Description = "从入口传感器到本摆轮的皮带段长度，单位：毫米")]
    public double SegmentLengthMm { get; set; } = 5000.0;

    /// <summary>
    /// 本段皮带速度（毫米/秒）- Segment Belt Speed (mm/s)
    /// </summary>
    /// <example>1000.0</example>
    [Range(100, 10000, ErrorMessage = "段速度必须在100到10000毫米/秒之间")]
    [SwaggerSchema(Description = "本段皮带的运行速度，单位：毫米/秒")]
    public double SegmentSpeedMmPerSecond { get; set; } = 1000.0;

    /// <summary>
    /// 本段容差时间（毫秒）- Segment Tolerance Time (ms)
    /// </summary>
    /// <example>2000</example>
    [Range(0, 60000, ErrorMessage = "容差时间必须在0到60000毫秒之间")]
    [SwaggerSchema(Description = "本段允许的时间误差范围，用于判断包裹到达是否正常，单位：毫秒")]
    public int SegmentToleranceTimeMs { get; set; } = 2000;
}