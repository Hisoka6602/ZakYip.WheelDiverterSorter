namespace ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;

/// <summary>
/// 格口选择结果
/// </summary>
/// <remarks>
/// 统一表达格口选择的结果：目标格口 / 是否走异常格口 / 错误信息。
/// 调用方只需要处理这个结果，不再自己决定走异常格口。
/// </remarks>
public record ChuteSelectionResult
{
    /// <summary>
    /// 是否成功选择格口
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    /// <remarks>
    /// 当 IsSuccess 为 true 时，此值为实际目标格口；
    /// 当 IsException 为 true 时，此值为异常格口ID。
    /// </remarks>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 是否路由到异常格口
    /// </summary>
    public bool IsException { get; init; }

    /// <summary>
    /// 异常原因（当 IsException 为 true 时）
    /// </summary>
    public string? ExceptionReason { get; init; }

    /// <summary>
    /// 错误信息（当 IsSuccess 为 false 时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ChuteSelectionResult Success(long targetChuteId)
        => new()
        {
            IsSuccess = true,
            TargetChuteId = targetChuteId,
            IsException = false
        };

    /// <summary>
    /// 创建异常格口结果
    /// </summary>
    public static ChuteSelectionResult Exception(long exceptionChuteId, string reason)
        => new()
        {
            IsSuccess = true,
            TargetChuteId = exceptionChuteId,
            IsException = true,
            ExceptionReason = reason
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ChuteSelectionResult Failure(string errorMessage)
        => new()
        {
            IsSuccess = false,
            TargetChuteId = 0,
            IsException = false,
            ErrorMessage = errorMessage
        };
}
