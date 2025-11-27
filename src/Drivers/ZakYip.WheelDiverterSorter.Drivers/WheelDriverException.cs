using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 摆轮驱动器异常基类
/// Base exception class for wheel diverter driver errors
/// </summary>
/// <remarks>
/// <para>
/// 此异常用于封装驱动层的硬件错误，供上层（Execution/Host）统一捕获并转换为 OperationResult。
/// </para>
/// <para>
/// 设计原则：
/// - 驱动层抛出此异常表示硬件操作失败
/// - 包含标准化的错误码，便于上层统一处理
/// - 上层在错误边界处捕获并转换为 OperationResult
/// </para>
/// </remarks>
public class WheelDriverException : Exception
{
    /// <summary>
    /// 错误码
    /// </summary>
    /// <remarks>
    /// 使用 <see cref="ErrorCodes"/> 中定义的标准错误码
    /// </remarks>
    public string ErrorCode { get; }

    /// <summary>
    /// 摆轮ID
    /// </summary>
    public string? DiverterId { get; }

    /// <summary>
    /// 创建摆轮驱动器异常
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="message">错误消息</param>
    /// <param name="diverterId">摆轮ID（可选）</param>
    public WheelDriverException(string errorCode, string message, string? diverterId = null)
        : base(message)
    {
        ErrorCode = errorCode;
        DiverterId = diverterId;
    }

    /// <summary>
    /// 创建摆轮驱动器异常（带内部异常）
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    /// <param name="diverterId">摆轮ID（可选）</param>
    public WheelDriverException(string errorCode, string message, Exception innerException, string? diverterId = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        DiverterId = diverterId;
    }

    /// <summary>
    /// 创建摆轮未找到异常
    /// </summary>
    public static WheelDriverException NotFound(string diverterId)
        => new(ErrorCodes.WheelNotFound, $"找不到摆轮控制器: {diverterId}", diverterId);

    /// <summary>
    /// 创建摆轮命令超时异常
    /// </summary>
    public static WheelDriverException CommandTimeout(string diverterId, int timeoutMs)
        => new(ErrorCodes.WheelCommandTimeout, $"摆轮 {diverterId} 命令执行超时（{timeoutMs}ms）", diverterId);

    /// <summary>
    /// 创建摆轮命令失败异常
    /// </summary>
    public static WheelDriverException CommandFailed(string diverterId, string reason)
        => new(ErrorCodes.WheelCommandFailed, $"摆轮 {diverterId} 命令执行失败: {reason}", diverterId);

    /// <summary>
    /// 创建摆轮通信错误异常
    /// </summary>
    public static WheelDriverException CommunicationError(string diverterId, Exception innerException)
        => new(ErrorCodes.WheelCommunicationError, $"摆轮 {diverterId} 通信错误: {innerException.Message}", innerException, diverterId);

    /// <summary>
    /// 创建摆轮方向无效异常
    /// </summary>
    public static WheelDriverException InvalidDirection(string diverterId, string direction)
        => new(ErrorCodes.WheelInvalidDirection, $"摆轮 {diverterId} 方向无效: {direction}", diverterId);

    /// <summary>
    /// 转换为 OperationResult
    /// </summary>
    public OperationResult ToOperationResult()
        => OperationResult.Failure(ErrorCode, Message);

    /// <summary>
    /// 转换为 OperationResult&lt;T&gt;
    /// </summary>
    public OperationResult<T> ToOperationResult<T>()
        => OperationResult<T>.Failure(ErrorCode, Message);
}
