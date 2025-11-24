namespace ZakYip.WheelDiverterSorter.Host.Models.Communication;

/// <summary>
/// 状态验证错误响应 - State Validation Error Response
/// </summary>
/// <remarks>
/// 用于当系统状态不满足操作要求时的错误响应
/// </remarks>
public sealed record StateValidationErrorResponse
{
    /// <summary>
    /// 错误消息 - Error message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 当前系统状态 - Current system state
    /// </summary>
    public required string CurrentState { get; init; }

    /// <summary>
    /// 要求的系统状态 - Required system state
    /// </summary>
    public required string RequiredState { get; init; }

    /// <summary>
    /// 提示信息 - Hint message
    /// </summary>
    public string? Hint { get; init; }
}
