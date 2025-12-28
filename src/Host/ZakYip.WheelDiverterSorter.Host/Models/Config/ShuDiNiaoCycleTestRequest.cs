using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 数递鸟摆轮循环测试请求模型
/// </summary>
public record class ShuDiNiaoCycleTestRequest
{
    /// <summary>
    /// 摆轮ID数组
    /// </summary>
    /// <example>[1, 2, 3]</example>
    [Required]
    [MinLength(1, ErrorMessage = "至少需要一个摆轮ID")]
    public required long[] DiverterIds { get; init; }

    /// <summary>
    /// 循环方向数组
    /// </summary>
    /// <remarks>
    /// 长度必须与摆轮ID数组相同，每个摆轮对应一个方向。
    /// 支持的方向：Left（左摆）、Right（右摆）、Straight（回中）
    /// </remarks>
    /// <example>["Left", "Right", "Straight"]</example>
    [Required]
    [MinLength(1, ErrorMessage = "至少需要一个方向")]
    public required DiverterDirection[] Directions { get; init; }

    /// <summary>
    /// 循环次数
    /// </summary>
    /// <remarks>
    /// 每个摆轮将执行指定方向动作的次数。
    /// 最大值为 int.MaxValue (2,147,483,647)
    /// </remarks>
    /// <example>10</example>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "循环次数必须在 1 到 2147483647 之间")]
    public required int CycleCount { get; init; }

    /// <summary>
    /// 每次指令间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 数递鸟摆轮要求指令间隔不得小于90ms。
    /// 建议值：90-1000ms
    /// </remarks>
    /// <example>100</example>
    [Required]
    [Range(90, 60000, ErrorMessage = "指令间隔必须在 90ms 到 60000ms 之间")]
    public required int IntervalMs { get; init; }
}

/// <summary>
/// 数递鸟摆轮循环测试响应模型
/// </summary>
public record class ShuDiNiaoCycleTestResponse
{
    /// <summary>
    /// 是否成功启动测试
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 消息
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 测试ID（用于停止测试）
    /// </summary>
    public string? TestId { get; init; }

    /// <summary>
    /// 已启动的测试任务详情
    /// </summary>
    public List<DiverterTestTask>? Tasks { get; init; }
}

/// <summary>
/// 摆轮测试任务详情
/// </summary>
public record class DiverterTestTask
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public required long DiverterId { get; init; }

    /// <summary>
    /// 测试方向
    /// </summary>
    public required DiverterDirection Direction { get; init; }

    /// <summary>
    /// 循环次数
    /// </summary>
    public required int CycleCount { get; init; }

    /// <summary>
    /// 指令间隔（毫秒）
    /// </summary>
    public required int IntervalMs { get; init; }

    /// <summary>
    /// 任务状态
    /// </summary>
    public string? Status { get; init; }
}

/// <summary>
/// 停止循环测试请求模型
/// </summary>
public record class StopCycleTestRequest
{
    /// <summary>
    /// 测试ID（可选，如果不提供则停止所有测试）
    /// </summary>
    public string? TestId { get; init; }
}

/// <summary>
/// 停止循环测试响应模型
/// </summary>
public record class StopCycleTestResponse
{
    /// <summary>
    /// 是否成功停止
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 消息
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 已停止的测试数量
    /// </summary>
    public int StoppedCount { get; init; }
}
