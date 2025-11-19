using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 日志去重服务接口 - 防止相同日志在短时间内重复记录
/// Log deduplication service interface - prevents duplicate logs within a short time window
/// </summary>
public interface ILogDeduplicator
{
    /// <summary>
    /// 判断是否应该记录日志（去重检查）
    /// Determine if the log should be recorded (deduplication check)
    /// </summary>
    /// <param name="logLevel">日志级别 - Log level</param>
    /// <param name="message">日志消息 - Log message</param>
    /// <param name="exceptionType">异常类型（可选） - Exception type (optional)</param>
    /// <returns>如果应该记录返回 true，否则返回 false - Returns true if should log, false otherwise</returns>
    bool ShouldLog(LogLevel logLevel, string message, string? exceptionType = null);

    /// <summary>
    /// 记录已写入的日志条目（更新去重缓存）
    /// Record logged entry (update deduplication cache)
    /// </summary>
    /// <param name="logLevel">日志级别 - Log level</param>
    /// <param name="message">日志消息 - Log message</param>
    /// <param name="exceptionType">异常类型（可选） - Exception type (optional)</param>
    void RecordLog(LogLevel logLevel, string message, string? exceptionType = null);
}
