using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// IO性能测试请求
/// </summary>
public record IoPerformanceTestRequest
{
    /// <summary>
    /// 读取的位编号
    /// </summary>
    /// <example>0</example>
    [Required]
    [Range(0, 1023, ErrorMessage = "位编号必须在 0-1023 范围内")]
    public required int BitNumber { get; init; }

    /// <summary>
    /// 循环次数
    /// </summary>
    /// <remarks>
    /// 最大支持 100,000 次
    /// </remarks>
    /// <example>1000</example>
    [Required]
    [Range(1, 100000, ErrorMessage = "循环次数必须在 1-100,000 范围内")]
    public required int IterationCount { get; init; }

    /// <summary>
    /// 是否使用异步读取
    /// </summary>
    /// <remarks>
    /// - true: 使用异步方式 (ReadAsync)
    /// - false: 使用同步阻塞方式（默认）
    /// </remarks>
    /// <example>false</example>
    public bool IsAsync { get; init; } = false;
}
