namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 表示摆轮路径执行的结果
/// </summary>
public record class PathExecutionResult
{
    /// <summary>
    /// 执行是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 实际落格的格口标识
    /// 当执行失败时，此值为异常格口（exception chute）
    /// </summary>
    public required string ActualChuteId { get; init; }

    /// <summary>
    /// 失败原因说明
    /// 当 IsSuccess = false 时，此字段描述失败的具体原因
    /// </summary>
    public string? FailureReason { get; init; }
}
