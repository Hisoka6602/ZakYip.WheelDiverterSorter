namespace ZakYip.WheelDiverterSorter.Core.LineModel.Routing;

/// <summary>
/// 操作结果辅助类 - Core 项目内部简化操作结果
/// Operation result helper class - simplified operation result within Core project
/// </summary>
/// <remarks>
/// 简化的操作结果类型，用于 LineModel 内部操作。
/// 对于需要更复杂结果信息的场景（如上游通信），请使用其他专用 Result 类型。
/// Simplified operation result type for internal LineModel operations.
/// For scenarios requiring more complex result information (e.g., upstream communication), 
/// use other dedicated Result types.
/// </remarks>
public sealed class OperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }

    private OperationResult(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static OperationResult Success() => new(true);
    public static OperationResult Failure(string errorMessage) => new(false, errorMessage);
}
