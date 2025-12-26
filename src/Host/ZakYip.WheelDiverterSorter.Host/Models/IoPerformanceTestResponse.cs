using System.Text.Json.Serialization;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// IO性能测试响应
/// </summary>
public record IoPerformanceTestResponse
{
    /// <summary>
    /// 测试的位编号
    /// </summary>
    [JsonPropertyName("bitNumber")]
    public required int BitNumber { get; init; }

    /// <summary>
    /// 实际执行的循环次数
    /// </summary>
    [JsonPropertyName("iterationCount")]
    public required int IterationCount { get; init; }

    /// <summary>
    /// 是否使用异步读取
    /// </summary>
    [JsonPropertyName("isAsync")]
    public required bool IsAsync { get; init; }

    /// <summary>
    /// 总耗时（毫秒）
    /// </summary>
    [JsonPropertyName("totalDurationMs")]
    public required double TotalDurationMs { get; init; }

    /// <summary>
    /// 平均每次读取耗时（毫秒）
    /// </summary>
    [JsonPropertyName("averageDurationMs")]
    public required double AverageDurationMs { get; init; }

    /// <summary>
    /// 最小读取耗时（毫秒）
    /// </summary>
    [JsonPropertyName("minDurationMs")]
    public required double MinDurationMs { get; init; }

    /// <summary>
    /// 最大读取耗时（毫秒）
    /// </summary>
    [JsonPropertyName("maxDurationMs")]
    public required double MaxDurationMs { get; init; }

    /// <summary>
    /// 每次读取的详细耗时记录（毫秒）
    /// </summary>
    /// <remarks>
    /// 仅当循环次数 ≤ 1000 时返回详细记录，超过 1000 次时此字段为 null
    /// </remarks>
    [JsonPropertyName("detailedTimings")]
    public double[]? DetailedTimings { get; init; }

    /// <summary>
    /// 成功读取次数
    /// </summary>
    [JsonPropertyName("successCount")]
    public required int SuccessCount { get; init; }

    /// <summary>
    /// 失败读取次数
    /// </summary>
    [JsonPropertyName("failedCount")]
    public required int FailedCount { get; init; }

    /// <summary>
    /// 测试开始时间
    /// </summary>
    [JsonPropertyName("startTime")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 测试结束时间
    /// </summary>
    [JsonPropertyName("endTime")]
    public required DateTimeOffset EndTime { get; init; }
}
