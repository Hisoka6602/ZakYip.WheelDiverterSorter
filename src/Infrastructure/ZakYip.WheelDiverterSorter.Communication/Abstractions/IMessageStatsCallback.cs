namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// 消息统计回调接口
/// Message statistics callback interface
/// </summary>
/// <remarks>
/// 用于Communication层与Application层解耦的回调接口。
/// Communication层通过此接口通知消息发送/接收事件，
/// Application层实现此接口来更新统计数据。
/// </remarks>
public interface IMessageStatsCallback
{
    /// <summary>
    /// 增加发送消息计数
    /// Increment sent message count
    /// </summary>
    void IncrementSent();

    /// <summary>
    /// 增加接收消息计数
    /// Increment received message count
    /// </summary>
    void IncrementReceived();
}
