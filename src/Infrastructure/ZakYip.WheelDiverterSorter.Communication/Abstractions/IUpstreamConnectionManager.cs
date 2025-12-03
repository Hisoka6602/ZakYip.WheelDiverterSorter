using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// 上游连接管理器接口
/// Upstream connection manager interface
/// </summary>
/// <remarks>
/// 负责管理与上游 RuleEngine 的连接状态、重连循环和退避计时
/// Responsible for managing connection state, reconnection loops, and backoff timing with upstream RuleEngine
/// </remarks>
public interface IUpstreamConnectionManager
{
    /// <summary>
    /// 获取当前连接状态
    /// Get current connection state
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 开始连接管理（启动重连循环）
    /// Start connection management (start reconnection loop)
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止连接管理（停止重连循环）
    /// Stop connection management (stop reconnection loop)
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 热更新连接参数
    /// Hot update connection options
    /// </summary>
    /// <param name="options">新的连接选项 / New connection options</param>
    /// <remarks>
    /// 立即切换到新的参数，用新参数继续执行同样的无限重试逻辑
    /// Immediately switch to new parameters and continue infinite retry logic with new parameters
    /// PR-CONFIG-HOTRELOAD02: 使用 Core 层的 UpstreamConnectionOptions 代替 RuleEngineConnectionOptions
    /// </remarks>
    Task UpdateConnectionOptionsAsync(UpstreamConnectionOptions options);

    /// <summary>
    /// 连接状态变化事件
    /// Connection state changed event
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
}

/// <summary>
/// 连接状态变化事件参数
/// Connection state changed event args
/// PR-PERF-EVENTS01: 转换为 sealed record class 以优化性能
/// </summary>
public sealed record class ConnectionStateChangedEventArgs
{
    /// <summary>
    /// 是否已连接
    /// Is connected
    /// </summary>
    public required bool IsConnected { get; init; }

    /// <summary>
    /// 状态变化时间
    /// State changed time
    /// </summary>
    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// 错误消息（如果有）
    /// Error message (if any)
    /// </summary>
    public string? ErrorMessage { get; init; }
}
