namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 系统时钟抽象接口 - 提供本地时间访问
/// System clock abstraction interface - provides local time access
/// </summary>
/// <remarks>
/// 系统内部所有业务时间使用本地时间；如指标/日志以其它时区展示，会单独注明。
/// All business times within the system use local time; if metrics/logs are displayed in other time zones, it will be noted separately.
/// </remarks>
public interface ISystemClock
{
    /// <summary>
    /// 获取当前本地时间（DateTime）
    /// Get current local time as DateTime
    /// </summary>
    DateTime LocalNow { get; }

    /// <summary>
    /// 获取当前本地时间（DateTimeOffset）
    /// Get current local time as DateTimeOffset
    /// </summary>
    DateTimeOffset LocalNowOffset { get; }

    /// <summary>
    /// 获取当前 UTC 时间（仅用于与外部系统交互时的时间转换）
    /// Get current UTC time (only for time conversion when interacting with external systems)
    /// </summary>
    DateTime UtcNow { get; }
}
