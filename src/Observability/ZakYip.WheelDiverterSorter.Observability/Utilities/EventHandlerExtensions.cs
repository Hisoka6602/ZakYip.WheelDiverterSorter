using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

/// <summary>
/// 扩展方法：安全调用事件处理器 - 防止单个订阅者异常影响其他订阅者
/// Extension methods for safely invoking event handlers - prevents one subscriber's exception from affecting others
/// </summary>
/// <remarks>
/// PR-SAFE-EVENTS: 确保所有事件订阅者的异常不会影响其他订阅者和发布者
/// PR-SAFE-EVENTS: Ensures exceptions from event subscribers don't affect other subscribers or the publisher
/// 
/// PR-async-events: 使用直接调用而非Task.Run，避免高频事件耗尽线程池
/// PR-async-events: Uses direct invocation instead of Task.Run to avoid thread pool exhaustion in high-frequency events
/// </remarks>
public static class EventHandlerExtensions
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
    /// <para><b>性能优化（PR-async-events）</b>：</para>
    /// 订阅者直接同步调用，但每个订阅者的异常被隔离。
    /// 事件订阅者应该使用 async void 模式自行异步处理，避免阻塞发布者。
    /// 
    /// Subscribers are invoked directly and synchronously, but each subscriber's exception is isolated.
    /// Event subscribers should use async void pattern to handle work asynchronously on their own,
    /// avoiding blocking the publisher.
    /// 
    /// 使用场景：
    /// - 事件发布者想要确保所有订阅者都能收到事件，即使某些订阅者抛出异常
    /// - 需要记录哪个订阅者抛出了异常，便于调试
    /// - 高频事件（传感器触发每2ms）需要避免Task.Run开销
    /// 
    /// Use cases:
    /// - Event publisher wants to ensure all subscribers receive the event, even if some throw exceptions
    /// - Need to log which subscriber threw an exception for debugging
    /// - High-frequency events (sensor triggers every 2ms) need to avoid Task.Run overhead
    /// 
    /// PR-SAFE-EVENTS: 移除 EventArgs 约束，支持自定义事件参数类型
    /// PR-SAFE-EVENTS: Removed EventArgs constraint to support custom event argument types
    /// 
    /// PR-async-events: 订阅者使用 async void 自行异步处理，发布者不使用Task.Run
    /// PR-async-events: Subscribers use async void to handle work asynchronously, publisher doesn't use Task.Run
    /// </remarks>
    public static void SafeInvoke<TEventArgs>(
        this EventHandler<TEventArgs>? eventHandler,
        object? sender,
        TEventArgs args,
        ILogger? logger = null,
        string? eventName = null)
    {
        if (eventHandler == null)
        {
            return;
        }

        var invocationList = eventHandler.GetInvocationList();
        var eventNameDisplay = eventName ?? typeof(TEventArgs).Name;

        // 直接同步调用所有订阅者，每个订阅者的异常被隔离
        // 订阅者应使用 async void 模式自行异步处理（如 SortingOrchestrator.OnParcelDetected）
        // Directly invoke all subscribers synchronously, isolating each subscriber's exception
        // Subscribers should use async void pattern to handle work asynchronously (e.g., SortingOrchestrator.OnParcelDetected)
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
    /// <remarks>
    /// PR-async-events: 订阅者使用 async void 自行异步处理，发布者不使用Task.Run
    /// PR-async-events: Subscribers use async void to handle work asynchronously, publisher doesn't use Task.Run
    /// </remarks>
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
        
        // 直接同步调用所有订阅者，每个订阅者的异常被隔离
        // Directly invoke all subscribers synchronously, isolating each subscriber's exception
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
