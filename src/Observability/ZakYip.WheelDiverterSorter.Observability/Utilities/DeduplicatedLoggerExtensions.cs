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
    /// <param name="message">日志消息 - Log message (structured logging format with named placeholders)</param>
    /// <param name="args">格式化参数 - Format arguments</param>
    /// <remarks>
    /// BUG FIX: 使用消息模板作为去重键，而不是格式化后的消息。
    /// 这样可以支持结构化日志格式（如 "{SensorId}"）而不需要转换为位置格式（如 "{0}"）。
    /// BUG FIX: Use message template as deduplication key instead of formatted message.
    /// This supports structured logging format (like "{SensorId}") without converting to positional format (like "{0}").
    /// </remarks>
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

        // Use message template as deduplication key to avoid string.Format issues
        var deduplicationKey = message;
        
        if (deduplicator.ShouldLog(LogLevel.Information, deduplicationKey))
        {
            logger.LogInformation(message, args);
            deduplicator.RecordLog(LogLevel.Information, deduplicationKey);
        }
    }

    /// <summary>
    /// 记录去重的 Warning 级别日志
    /// Log deduplicated Warning level message
    /// </summary>
    /// <param name="logger">日志记录器 - Logger instance</param>
    /// <param name="deduplicator">日志去重器 - Log deduplicator instance</param>
    /// <param name="message">日志消息 - Log message (structured logging format with named placeholders)</param>
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

        var deduplicationKey = message;
        
        if (deduplicator.ShouldLog(LogLevel.Warning, deduplicationKey))
        {
            logger.LogWarning(message, args);
            deduplicator.RecordLog(LogLevel.Warning, deduplicationKey);
        }
    }

    /// <summary>
    /// 记录去重的 Debug 级别日志
    /// Log deduplicated Debug level message
    /// </summary>
    /// <param name="logger">日志记录器 - Logger instance</param>
    /// <param name="deduplicator">日志去重器 - Log deduplicator instance</param>
    /// <param name="message">日志消息 - Log message (structured logging format with named placeholders)</param>
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

        var deduplicationKey = message;
        
        if (deduplicator.ShouldLog(LogLevel.Debug, deduplicationKey))
        {
            logger.LogDebug(message, args);
            deduplicator.RecordLog(LogLevel.Debug, deduplicationKey);
        }
    }

    /// <summary>
    /// 记录去重的 Error 级别日志（带异常）
    /// Log deduplicated Error level message with exception
    /// </summary>
    /// <param name="logger">日志记录器 - Logger instance</param>
    /// <param name="deduplicator">日志去重器 - Log deduplicator instance</param>
    /// <param name="exception">异常对象 - Exception object</param>
    /// <param name="message">日志消息 - Log message (structured logging format with named placeholders)</param>
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

        var deduplicationKey = message;
        var exceptionType = exception?.GetType().Name;
        
        if (deduplicator.ShouldLog(LogLevel.Error, deduplicationKey, exceptionType))
        {
            logger.LogError(exception, message, args);
            deduplicator.RecordLog(LogLevel.Error, deduplicationKey, exceptionType);
        }
    }
}
