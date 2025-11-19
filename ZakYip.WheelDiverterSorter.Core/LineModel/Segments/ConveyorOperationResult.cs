namespace ZakYip.WheelDiverterSorter.Core.LineModel.Segments;

/// <summary>
/// 中段皮带操作结果。
/// 表示启动、停止等操作的执行结果。
/// </summary>
public record class ConveyorOperationResult
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 失败原因说明（当 IsSuccess = false 时）
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 涉及的皮带段标识
    /// </summary>
    public ConveyorSegmentId? SegmentId { get; init; }

    /// <summary>
    /// 操作时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ConveyorOperationResult Success(ConveyorSegmentId? segmentId = null) =>
        new() { IsSuccess = true, SegmentId = segmentId };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ConveyorOperationResult Failure(string reason, ConveyorSegmentId? segmentId = null) =>
        new() { IsSuccess = false, FailureReason = reason, SegmentId = segmentId };
}
