using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 扩展方法：安全调用事件处理器 - 防止单个订阅者异常影响其他订阅者
/// Extension methods for safely invoking event handlers - prevents one subscriber's exception from affecting others
/// </summary>
/// <remarks>
/// 设为 internal 以限制可见性 - 当前未被使用，但保留以备未来需要
/// Set to internal to limit visibility - currently unused but kept for future use
/// </remarks>
internal static class EventHandlerExtensions
{
    /// <summary>
    /// 安全调用事件 - 捕获并记录每个订阅者的异常，但不阻止其他订阅者执行
    /// Safely invoke event - catch and log each subscriber's exception without blocking others
    /// </summary>
    /// <typeparam name="TEventArgs">事件参数类型 / Event args type</typeparam>
    /// <param name="eventHandler">事件处理器 / Event handler</param>
    /// <param name="sender">事件发送者 / Event sender</param>
    /// <param name="args">事件参数 / Event arguments</param>
    /// <param name="logger">日志记录器（可选） / Logger (optional)</param>
    /// <param name="eventName">事件名称（用于日志） / Event name (for logging)</param>
    /// <remarks>
    /// 此方法遍历所有订阅者并逐个调用，捕获每个订阅者的异常而不影响其他订阅者。
    /// This method iterates through all subscribers and invokes them individually, 
    /// catching each subscriber's exception without affecting others.
    /// 
    /// 使用场景：
    /// - 事件发布者想要确保所有订阅者都能收到事件，即使某些订阅者抛出异常
    /// - 需要记录哪个订阅者抛出了异常，便于调试
    /// 
    /// Use cases:
    /// - Event publisher wants to ensure all subscribers receive the event, even if some throw exceptions
    /// - Need to log which subscriber threw an exception for debugging
    /// </remarks>
    public static void SafeInvoke<TEventArgs>(
        this EventHandler<TEventArgs>? eventHandler,
        object? sender,
        TEventArgs args,
        ILogger? logger = null,
        string? eventName = null)
        where TEventArgs : EventArgs
    {
        if (eventHandler == null)
        {
            return;
        }

        var invocationList = eventHandler.GetInvocationList();
        var eventNameDisplay = eventName ?? typeof(TEventArgs).Name;

        foreach (var handler in invocationList)
        {
            try
            {
                ((EventHandler<TEventArgs>)handler).Invoke(sender, args);
            }
            catch (Exception ex)
            {
                // Log the exception but continue invoking other subscribers
                logger?.LogError(
                    ex,
                    "订阅者处理事件 '{EventName}' 时发生异常 / Subscriber threw exception while handling event: Target={Target}, Method={Method}",
                    eventNameDisplay,
                    handler.Target?.GetType().Name ?? "Unknown",
                    handler.Method.Name);
            }
        }
    }

    /// <summary>
    /// 安全调用简单事件（无参数）
    /// Safely invoke simple event (no arguments)
    /// </summary>
    /// <param name="eventHandler">事件处理器 / Event handler</param>
    /// <param name="sender">事件发送者 / Event sender</param>
    /// <param name="logger">日志记录器（可选） / Logger (optional)</param>
    /// <param name="eventName">事件名称（用于日志） / Event name (for logging)</param>
    public static void SafeInvoke(
        this EventHandler? eventHandler,
        object? sender,
        ILogger? logger = null,
        string? eventName = null)
    {
        if (eventHandler == null)
        {
            return;
        }

        var eventNameDisplay = eventName ?? "EventHandler";
        
        // Iterate through all delegates
        foreach (var del in eventHandler.GetInvocationList())
        {
            var originalDelegate = (EventHandler)del;
            try
            {
                originalDelegate.Invoke(sender, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    ex,
                    "订阅者处理事件 '{EventName}' 时发生异常 / Subscriber threw exception while handling event: Target={Target}, Method={Method}",
                    eventNameDisplay,
                    del.Target?.GetType().Name ?? "Unknown",
                    del.Method.Name);
            }
        }
    }
}
