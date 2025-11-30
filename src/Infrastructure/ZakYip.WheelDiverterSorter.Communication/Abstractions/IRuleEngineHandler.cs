using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// RuleEngine回调处理器接口
/// </summary>
/// <remarks>
/// 定义用于处理RuleEngine推送消息的标准回调接口。
/// 所有协议实现都应该使用此接口来处理接收到的消息。
/// </remarks>
public interface IRuleEngineHandler
{
    /// <summary>
    /// 处理格口分配通知
    /// </summary>
    /// <param name="notification">格口分配通知事件参数</param>
    /// <returns>异步任务</returns>
    Task HandleChuteAssignmentAsync(ChuteAssignmentNotificationEventArgs notification);

    /// <summary>
    /// 处理连接状态变化
    /// </summary>
    /// <param name="isConnected">是否已连接</param>
    /// <param name="reason">状态变化原因</param>
    /// <returns>异步任务</returns>
    Task HandleConnectionStateChangedAsync(bool isConnected, string? reason = null);

    /// <summary>
    /// 处理错误
    /// </summary>
    /// <param name="error">错误信息</param>
    /// <param name="exception">异常对象（可选）</param>
    /// <returns>异步任务</returns>
    Task HandleErrorAsync(string error, Exception? exception = null);

    /// <summary>
    /// 处理心跳响应
    /// </summary>
    /// <param name="timestamp">心跳时间戳</param>
    /// <returns>异步任务</returns>
    Task HandleHeartbeatAsync(DateTime timestamp);
}
