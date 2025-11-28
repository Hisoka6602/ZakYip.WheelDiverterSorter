using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 安全执行服务实现 - 提供统一的异常捕获和日志记录机制
/// Safe execution service implementation - provides unified exception catching and logging
/// </summary>
/// <remarks>
/// 此服务确保未处理的异常不会导致 Host 崩溃，
/// 所有异常都会被捕获并记录详细的日志信息（包括操作名称、本地时间、异常类型和消息）。
/// This service ensures unhandled exceptions won't crash the Host,
/// all exceptions are caught and logged with detailed information (operation name, local time, exception type and message).
/// </remarks>
public sealed class SafeExecutionService : ISafeExecutionService
{
    private readonly ILogger<SafeExecutionService> _logger;
    private readonly ISystemClock _systemClock;

    public SafeExecutionService(
        ILogger<SafeExecutionService> logger,
        ISystemClock systemClock)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <inheritdoc/>
    public async Task<bool> ExecuteAsync(
        Func<Task> action,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("Operation name cannot be null or whitespace.", nameof(operationName));
        }

        try
        {
            await action().ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation, log as information
            _logger.LogInformation(
                "[{LocalTime}] Operation '{OperationName}' was cancelled.",
                _systemClock.LocalNow,
                operationName);
            return false;
        }
        catch (Exception ex)
        {
            // Unexpected exception, log as error but do not rethrow
            _logger.LogError(
                ex,
                "[{LocalTime}] Operation '{OperationName}' failed with exception type '{ExceptionType}': {Message}",
                _systemClock.LocalNow,
                operationName,
                ex.GetType().Name,
                ex.Message);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        string operationName,
        T defaultValue = default!,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("Operation name cannot be null or whitespace.", nameof(operationName));
        }

        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation, log as information
            _logger.LogInformation(
                "[{LocalTime}] Operation '{OperationName}' was cancelled.",
                _systemClock.LocalNow,
                operationName);
            return defaultValue;
        }
        catch (Exception ex)
        {
            // Unexpected exception, log as error but do not rethrow
            _logger.LogError(
                ex,
                "[{LocalTime}] Operation '{OperationName}' failed with exception type '{ExceptionType}': {Message}",
                _systemClock.LocalNow,
                operationName,
                ex.GetType().Name,
                ex.Message);
            return defaultValue;
        }
    }
}
