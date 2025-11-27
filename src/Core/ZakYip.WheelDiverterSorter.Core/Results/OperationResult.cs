namespace ZakYip.WheelDiverterSorter.Core.Results;

/// <summary>
/// 统一的操作结果类型（不携带数据）
/// Unified operation result type (without data payload)
/// </summary>
/// <remarks>
/// <para>用于表示操作成功/失败的结果，包含错误码和错误消息。</para>
/// <para>
/// 设计原则：
/// - 替代 bool + string message 的返回模式
/// - 替代 null 表示失败的模式
/// - 提供统一的错误分类和错误码
/// - 支持 switch (result.ErrorCode) 的错误处理模式
/// </para>
/// </remarks>
public sealed record OperationResult
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 错误码（当 IsSuccess = false 时）
    /// </summary>
    /// <remarks>
    /// 使用 <see cref="ErrorCodes"/> 中定义的标准错误码
    /// </remarks>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 错误消息（当 IsSuccess = false 时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 私有构造函数，使用静态工厂方法创建实例
    /// </summary>
    private OperationResult() { }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OperationResult Success() => new()
    {
        IsSuccess = true
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorCode">错误码，建议使用 <see cref="ErrorCodes"/> 中定义的常量</param>
    /// <param name="errorMessage">错误消息</param>
    public static OperationResult Failure(string errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    /// <summary>
    /// 创建失败结果（仅消息，无错误码）
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <remarks>
    /// 仅用于简单场景，建议优先使用带错误码的重载
    /// </remarks>
    public static OperationResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = ErrorCodes.Unknown,
        ErrorMessage = errorMessage
    };

    /// <summary>
    /// 从异常创建失败结果
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="errorCode">错误码，默认为 Unknown</param>
    public static OperationResult FromException(Exception exception, string? errorCode = null) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode ?? ErrorCodes.Unknown,
        ErrorMessage = exception.Message
    };

    /// <summary>
    /// 隐式转换为 bool
    /// </summary>
    public static implicit operator bool(OperationResult result) => result.IsSuccess;
}

/// <summary>
/// 统一的操作结果类型（携带数据）
/// Unified operation result type (with data payload)
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
/// <remarks>
/// <para>用于表示需要返回数据的操作结果，包含错误码、错误消息和可选的数据负载。</para>
/// <para>
/// 设计原则：
/// - 替代 Tuple&lt;bool, T?, string&gt; 的返回模式
/// - 替代 null 表示失败、非 null 表示成功的模式
/// - 明确区分成功但数据为 null 和失败的情况
/// </para>
/// </remarks>
public sealed record OperationResult<T>
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 结果数据（当 IsSuccess = true 时）
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// 错误码（当 IsSuccess = false 时）
    /// </summary>
    /// <remarks>
    /// 使用 <see cref="ErrorCodes"/> 中定义的标准错误码
    /// </remarks>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 错误消息（当 IsSuccess = false 时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 私有构造函数，使用静态工厂方法创建实例
    /// </summary>
    private OperationResult() { }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="data">结果数据</param>
    public static OperationResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorCode">错误码，建议使用 <see cref="ErrorCodes"/> 中定义的常量</param>
    /// <param name="errorMessage">错误消息</param>
    public static OperationResult<T> Failure(string errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    /// <summary>
    /// 创建失败结果（仅消息，无错误码）
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <remarks>
    /// 仅用于简单场景，建议优先使用带错误码的重载
    /// </remarks>
    public static OperationResult<T> Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = ErrorCodes.Unknown,
        ErrorMessage = errorMessage
    };

    /// <summary>
    /// 从异常创建失败结果
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="errorCode">错误码，默认为 Unknown</param>
    public static OperationResult<T> FromException(Exception exception, string? errorCode = null) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode ?? ErrorCodes.Unknown,
        ErrorMessage = exception.Message
    };

    /// <summary>
    /// 隐式转换为 bool
    /// </summary>
    public static implicit operator bool(OperationResult<T> result) => result.IsSuccess;

    /// <summary>
    /// 转换为不携带数据的 OperationResult
    /// </summary>
    public OperationResult ToResult() => IsSuccess
        ? OperationResult.Success()
        : OperationResult.Failure(ErrorCode ?? ErrorCodes.Unknown, ErrorMessage ?? string.Empty);

    /// <summary>
    /// 映射数据类型
    /// </summary>
    /// <typeparam name="TNew">新的数据类型</typeparam>
    /// <param name="mapper">映射函数</param>
    /// <returns>映射后的结果</returns>
    public OperationResult<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (!IsSuccess || Data is null)
        {
            return OperationResult<TNew>.Failure(ErrorCode ?? ErrorCodes.Unknown, ErrorMessage ?? string.Empty);
        }

        return OperationResult<TNew>.Success(mapper(Data));
    }
}
