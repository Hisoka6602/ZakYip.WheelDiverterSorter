namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 安全执行服务接口 - 提供统一的异常捕获和日志记录机制
/// Safe execution service interface - provides unified exception catching and logging
/// </summary>
public interface ISafeExecutionService
{
    /// <summary>
    /// 安全执行异步操作
    /// Safely execute an async operation
    /// </summary>
    /// <param name="action">要执行的异步操作 - The async operation to execute</param>
    /// <param name="operationName">操作名称（用于日志记录） - Operation name for logging</param>
    /// <param name="cancellationToken">取消令牌 - Cancellation token</param>
    /// <returns>如果操作成功返回 true，否则返回 false - Returns true if operation succeeded, false otherwise</returns>
    Task<bool> ExecuteAsync(
        Func<Task> action,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 安全执行带返回值的异步操作
    /// Safely execute an async operation with return value
    /// </summary>
    /// <typeparam name="T">返回值类型 - Return value type</typeparam>
    /// <param name="action">要执行的异步操作 - The async operation to execute</param>
    /// <param name="operationName">操作名称（用于日志记录） - Operation name for logging</param>
    /// <param name="defaultValue">失败时的默认返回值 - Default value to return on failure</param>
    /// <param name="cancellationToken">取消令牌 - Cancellation token</param>
    /// <returns>操作返回值或默认值 - Operation result or default value</returns>
    Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        string operationName,
        T defaultValue = default!,
        CancellationToken cancellationToken = default);
}
