namespace ZakYip.WheelDiverterSorter.Core.LineModel.Routing;

/// <summary>
/// 路由计算结果 - LineModel 路由计算内部使用
/// Route computation result - for internal LineModel routing operations
/// </summary>
/// <remarks>
/// <para>
/// 此类型仅用于路由计算/改口逻辑的内部流程，不携带错误码。
/// 对于需要错误码支持的公共 API，请使用 <see cref="Core.Results.OperationResult"/>。
/// </para>
/// <para>
/// PR-S5: 按规范重命名自 OperationResult，避免与公共结果模型命名冲突。
/// </para>
/// </remarks>
internal sealed class RouteComputationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }

    private RouteComputationResult(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static RouteComputationResult Success() => new(true);
    public static RouteComputationResult Failure(string errorMessage) => new(false, errorMessage);
}
