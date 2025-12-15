using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 日志去重服务实现 - 使用滑动时间窗口防止日志刷屏
/// Log deduplication service implementation - uses sliding time window to prevent log flooding
/// </summary>
/// <remarks>
/// 此服务在指定时间窗口内（默认 1 秒）对相同的日志（基于级别、消息、异常类型）只允许记录一次。
/// This service allows the same log (based on level, message, exception type) to be recorded only once within the specified time window (default 1 second).
/// </remarks>
public sealed class LogDeduplicator : ILogDeduplicator
{
    private readonly ConcurrentDictionary<string, DateTime> _logCache = new();
    private readonly TimeSpan _windowDuration;
    private readonly ISystemClock _systemClock;

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="systemClock">系统时钟 - System clock</param>
    /// <param name="windowDurationSeconds">去重时间窗口（秒），默认 1 秒 - Deduplication window in seconds, default 1 second</param>
    public LogDeduplicator(ISystemClock systemClock, int windowDurationSeconds = 1)
    {
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _windowDuration = TimeSpan.FromSeconds(windowDurationSeconds);
    }

    /// <inheritdoc/>
    public bool ShouldLog(LogLevel logLevel, string message, string? exceptionType = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return true; // Always log if message is empty
        }

        var key = GenerateKey(logLevel, message, exceptionType);
        var now = _systemClock.LocalNow;

        // Check if we have seen this log recently
        if (_logCache.TryGetValue(key, out var lastLogTime))
        {
            var elapsed = now - lastLogTime;
            if (elapsed < _windowDuration)
            {
                // Within deduplication window, skip this log
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public void RecordLog(LogLevel logLevel, string message, string? exceptionType = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var key = GenerateKey(logLevel, message, exceptionType);
        var now = _systemClock.LocalNow;
        
        _logCache.AddOrUpdate(key, now, (_, _) => now);

        // Periodically clean up old entries (simple cleanup every 100 entries)
        if (_logCache.Count > 1000)
        {
            CleanupOldEntries();
        }
    }

    private static string GenerateKey(LogLevel logLevel, string message, string? exceptionType)
    {
        // PR-PERF: Optimize - avoid unnecessary string allocations
        if (string.IsNullOrEmpty(exceptionType))
        {
            // No exception type, simpler concatenation
            return $"{logLevel}|{message}";
        }
        
        return $"{logLevel}|{message}|{exceptionType}";
    }

    private void CleanupOldEntries()
    {
        // PR-PERF: Optimize - avoid LINQ allocations, use direct enumeration
        var now = _systemClock.LocalNow;
        var cutoffTime = now - _windowDuration;

        // Remove entries older than the window duration
        // Use List with pre-allocated capacity to avoid resizing
        var keysToRemove = new List<string>(Math.Min(_logCache.Count / 2, 500));
        
        foreach (var kvp in _logCache)
        {
            if (kvp.Value < cutoffTime)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _logCache.TryRemove(key, out _);
        }
    }
}
