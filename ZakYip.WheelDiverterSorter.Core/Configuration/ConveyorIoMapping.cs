using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 单个中段皮带段的 IO 映射配置。
/// 定义了一个皮带段的 IO 点位映射关系。
/// </summary>
public sealed record class ConveyorIoMapping
{
    /// <summary>
    /// 皮带段唯一键（例如："Middle1", "Middle2", "ReturnLoop"）
    /// </summary>
    [Required(ErrorMessage = "皮带段键不能为空")]
    public required string SegmentKey { get; init; }

    /// <summary>
    /// 皮带段显示名称（例如："中段皮带1", "中段皮带2"）
    /// </summary>
    [Required(ErrorMessage = "皮带段显示名称不能为空")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// 启动输出点位编号（必需）
    /// </summary>
    [Required(ErrorMessage = "启动输出点位不能为空")]
    [Range(0, 1023, ErrorMessage = "启动输出点位必须在 0-1023 之间")]
    public required int StartOutputChannel { get; init; }

    /// <summary>
    /// 停止输出点位编号（可选，如无单独停止点位则为 null）
    /// </summary>
    [Range(0, 1023, ErrorMessage = "停止输出点位必须在 0-1023 之间")]
    public int? StopOutputChannel { get; init; }

    /// <summary>
    /// 故障输入点位编号（可选，用于检测皮带故障）
    /// </summary>
    [Range(0, 1023, ErrorMessage = "故障输入点位必须在 0-1023 之间")]
    public int? FaultInputChannel { get; init; }

    /// <summary>
    /// 运行反馈输入点位编号（可选，用于确认皮带已启动）
    /// </summary>
    [Range(0, 1023, ErrorMessage = "运行反馈输入点位必须在 0-1023 之间")]
    public int? RunningInputChannel { get; init; }

    /// <summary>
    /// 段位优先级（用于联动启停顺序，数值越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// 启动超时时间（毫秒），默认 5000ms
    /// </summary>
    [Range(100, 60000, ErrorMessage = "启动超时时间必须在 100-60000 毫秒之间")]
    public int StartTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// 停止超时时间（毫秒），默认 5000ms
    /// </summary>
    [Range(100, 60000, ErrorMessage = "停止超时时间必须在 100-60000 毫秒之间")]
    public int StopTimeoutMs { get; init; } = 5000;
}
