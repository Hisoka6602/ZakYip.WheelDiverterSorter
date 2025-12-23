using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 服务器重启事件参数
/// Server restarted event arguments
/// </summary>
/// <remarks>
/// 当 UpstreamServerBackgroundService 执行热重启并创建新的服务器实例时触发此事件。
/// This event is triggered when UpstreamServerBackgroundService performs hot restart and creates a new server instance.
/// 
/// 订阅者（如 SortingOrchestrator）可以通过此事件获取新的服务器实例，并重新订阅其事件。
/// Subscribers (like SortingOrchestrator) can get the new server instance through this event and re-subscribe to its events.
/// </remarks>
public class ServerRestartedEventArgs : EventArgs
{
    /// <summary>
    /// 新的服务器实例
    /// New server instance
    /// </summary>
    public IRuleEngineServer? NewServer { get; init; }

    /// <summary>
    /// 服务器重启的时间
    /// Time when the server was restarted
    /// </summary>
    public DateTimeOffset RestartedAt { get; init; }

    /// <summary>
    /// 重启原因（如：配置更新）
    /// Reason for restart (e.g., configuration update)
    /// </summary>
    public string? Reason { get; init; }
}
