namespace ZakYip.WheelDiverterSorter.Ingress.Upstream;

/// <summary>
/// 操作结果，封装上游调用的结果
/// </summary>
/// <typeparam name="T">结果数据类型</typeparam>
public record OperationResult<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 结果数据
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 操作延迟（毫秒）
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// 来源（通道名称）
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 是否来自降级/备用方案
    /// </summary>
    public bool IsFallback { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OperationResult<T> Success(T data, double latencyMs, string? source = null)
    {
        return new OperationResult<T>
        {
            IsSuccess = true,
            Data = data,
            LatencyMs = latencyMs,
            Source = source
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static OperationResult<T> Failure(string errorMessage, string? errorCode = null, double latencyMs = 0, string? source = null)
    {
        return new OperationResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            LatencyMs = latencyMs,
            Source = source
        };
    }

    /// <summary>
    /// 创建降级结果
    /// </summary>
    public static OperationResult<T> Fallback(T data, double latencyMs, string? source = null)
    {
        return new OperationResult<T>
        {
            IsSuccess = true,
            Data = data,
            LatencyMs = latencyMs,
            Source = source,
            IsFallback = true
        };
    }
}
