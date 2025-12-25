using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 输送线段配置请求
/// </summary>
/// <remarks>
/// 定义摆轮前输送线段的物理参数，用于计算包裹传输时间、超时检测和丢失判定
/// </remarks>
public record ConveyorSegmentRequest
{
    /// <summary>
    /// 线段唯一标识符
    /// </summary>
    /// <example>1</example>
    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "线段ID必须大于0")]
    public required long SegmentId { get; init; }

    /// <summary>
    /// 线段显示名称
    /// </summary>
    /// <example>入口到摆轮D1</example>
    [StringLength(200, ErrorMessage = "线段名称长度不能超过200个字符")]
    public string? SegmentName { get; init; }

    /// <summary>
    /// 线段长度（毫米）
    /// </summary>
    /// <remarks>
    /// 从上一个节点到当前节点的物理距离
    /// </remarks>
    /// <example>5000</example>
    [Required]
    [Range(1, 1000000, ErrorMessage = "线段长度必须在1-1000000毫米之间")]
    public required double LengthMm { get; init; }

    /// <summary>
    /// 线速（毫米/秒）
    /// </summary>
    /// <remarks>
    /// 输送带的运行速度，用于计算包裹传输时间
    /// </remarks>
    /// <example>1000</example>
    [Required]
    [Range(0.1, 10000, ErrorMessage = "线速必须在0.1-10000毫米/秒之间")]
    public required decimal SpeedMmps { get; init; }

    /// <summary>
    /// 时间容差（毫秒）
    /// </summary>
    /// <remarks>
    /// <para>允许的时间偏差，用于判定超时阈值。</para>
    /// <para>超时阈值 = 理论传输时间 + 时间容差</para>
    /// </remarks>
    /// <example>500</example>
    [Required]
    [Range(0, 60000, ErrorMessage = "时间容差必须在0-60000毫秒之间")]
    public required long TimeToleranceMs { get; init; }

    /// <summary>
    /// 备注信息
    /// </summary>
    /// <example>入口到第一个摆轮的输送段</example>
    [StringLength(500, ErrorMessage = "备注长度不能超过500个字符")]
    public string? Remarks { get; init; }
}

/// <summary>
/// 输送线段配置响应
/// </summary>
public record ConveyorSegmentResponse
{
    /// <summary>
    /// 线段唯一标识符
    /// </summary>
    /// <example>1</example>
    public required long SegmentId { get; init; }

    /// <summary>
    /// 线段显示名称
    /// </summary>
    /// <example>入口到摆轮D1</example>
    public string? SegmentName { get; init; }

    /// <summary>
    /// 线段长度（毫米）
    /// </summary>
    /// <example>5000</example>
    public required double LengthMm { get; init; }

    /// <summary>
    /// 线速（毫米/秒）
    /// </summary>
    /// <example>1000</example>
    public required decimal SpeedMmps { get; init; }

    /// <summary>
    /// 时间容差（毫秒）
    /// </summary>
    /// <example>500</example>
    public required long TimeToleranceMs { get; init; }

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }

    /// <summary>
    /// 理论传输时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 根据线段长度和线速自动计算：传输时间 = (长度 / 速度) × 1000
    /// </remarks>
    /// <example>5000</example>
    public double CalculatedTransitTimeMs { get; init; }

    /// <summary>
    /// 超时阈值（毫秒）
    /// </summary>
    /// <remarks>
    /// 超时阈值 = 理论传输时间 + 时间容差
    /// </remarks>
    /// <example>5500</example>
    public double CalculatedTimeoutThresholdMs { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 输送线段批量创建请求
/// </summary>
public record ConveyorSegmentBatchRequest
{
    /// <summary>
    /// 线段配置列表
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "至少需要一个线段配置")]
    public required List<ConveyorSegmentRequest> Segments { get; init; }
}

/// <summary>
/// 输送线段批量操作响应
/// </summary>
public record ConveyorSegmentBatchResponse
{
    /// <summary>
    /// 成功处理的数量
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// 失败处理的数量
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// 错误消息列表
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool IsFullSuccess => FailureCount == 0;
}
