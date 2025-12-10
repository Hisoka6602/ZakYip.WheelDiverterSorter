using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 提供带日志去重功能的 ILogger 扩展方法
/// Provides ILogger extension methods with log deduplication functionality
/// </summary>
/// <remarks>
/// 这些扩展方法使用 ILogDeduplicator 防止相同日志在短时间内重复输出。
/// These extension methods use ILogDeduplicator to prevent identical logs from being output repeatedly in a short time period.
/// 
/// **使用场景**：
/// - 传感器状态变化日志（频繁触发）
/// - 节点健康状态变更日志（可能重复通知）
/// - 设备连接状态日志（重复断开/重连）
/// 
/// **不适用场景**：
/// - 包裹生命周期事件（每个包裹唯一，不应去重）
/// - 审计日志（必须记录每次操作）
/// - 错误日志中包含唯一标识的情况
/// </remarks>
public static class DeduplicatedLoggerExtensions
{
    /// <summary>
    /// 记录去重的 Information 级别日志
    /// Log deduplicated Information level message
    /// </summary>
    /// <param name="logger">日志记录器 - Logger instance</param>
    /// <param name="deduplicator">日志去重器 - Log deduplicator instance</param>
    /// <param name="message">日志消息 - Log message</param>
    /// <param name="args">格式化参数 - Format arguments</param>
    public static void LogInformationDeduplicated(
        this ILogger logger,
        ILogDeduplicator deduplicator,
        string message,
        params object?[] args)
    {
        if (logger == null || deduplicator == null)
        {
            return;
        }

        var formattedMessage = string.Format(message, args);
        
        if (deduplicator.ShouldLog(LogLevel.Information, formattedMessage))
        {
            logger.LogInformation(message, args);
            deduplicator.RecordLog(LogLevel.Information, formattedMessage);
        }
    }

    /// <summary>
    /// 记录去重的 Warning 级别日志
    /// Log deduplicated Warning level message
    /// </summary>
    /// <param name="logger">日志记录器 - Logger instance</param>
    /// <param name="deduplicator">日志去重器 - Log deduplicator instance</param>
    /// <param name="message">日志消息 - Log message</param>
    /// <param name="args">格式化参数 - Format arguments</param>
    public static void LogWarningDeduplicated(
        this ILogger logger,
        ILogDeduplicator deduplicator,
        string message,
        params object?[] args)
    {
        if (logger == null || deduplicator == null)
        {
            return;
        }

        var formattedMessage = string.Format(message, args);
        
        if (deduplicator.ShouldLog(LogLevel.Warning, formattedMessage))
        {
            logger.LogWarning(message, args);
            deduplicator.RecordLog(LogLevel.Warning, formattedMessage);
        }
    }

    /// <summary>
    /// 记录去重的 Debug 级别日志
    /// Log deduplicated Debug level message
    /// </summary>
    /// <param name="logger">日志记录器 - Logger instance</param>
    /// <param name="deduplicator">日志去重器 - Log deduplicator instance</param>
    /// <param name="message">日志消息 - Log message</param>
    /// <param name="args">格式化参数 - Format arguments</param>
    public static void LogDebugDeduplicated(
        this ILogger logger,
        ILogDeduplicator deduplicator,
        string message,
        params object?[] args)
    {
        if (logger == null || deduplicator == null)
        {
            return;
        }

        var formattedMessage = string.Format(message, args);
        
        if (deduplicator.ShouldLog(LogLevel.Debug, formattedMessage))
        {
            logger.LogDebug(message, args);
            deduplicator.RecordLog(LogLevel.Debug, formattedMessage);
        }
    }

    /// <summary>
    /// 记录去重的 Error 级别日志（带异常）
    /// Log deduplicated Error level message with exception
    /// </summary>
    /// <param name="logger">日志记录器 - Logger instance</param>
    /// <param name="deduplicator">日志去重器 - Log deduplicator instance</param>
    /// <param name="exception">异常对象 - Exception object</param>
    /// <param name="message">日志消息 - Log message</param>
    /// <param name="args">格式化参数 - Format arguments</param>
    public static void LogErrorDeduplicated(
        this ILogger logger,
        ILogDeduplicator deduplicator,
        Exception exception,
        string message,
        params object?[] args)
    {
        if (logger == null || deduplicator == null)
        {
            return;
        }

        var formattedMessage = string.Format(message, args);
        var exceptionType = exception?.GetType().Name;
        
        if (deduplicator.ShouldLog(LogLevel.Error, formattedMessage, exceptionType))
        {
            logger.LogError(exception, message, args);
            deduplicator.RecordLog(LogLevel.Error, formattedMessage, exceptionType);
        }
    }
}
