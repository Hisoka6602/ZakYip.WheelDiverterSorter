using System.Threading;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 通信统计服务 - Communication Statistics Service
/// </summary>
/// <remarks>
/// 跟踪通信消息的发送和接收次数
/// </remarks>
public class CommunicationStatsService
{
    private long _messagesSent;
    private long _messagesReceived;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastDisconnectedAt;
    private DateTimeOffset? _firstConnectedAt;

    /// <summary>
    /// 发送消息计数 - Messages sent count
    /// </summary>
    public long MessagesSent => Interlocked.Read(ref _messagesSent);

    /// <summary>
    /// 接收消息计数 - Messages received count
    /// </summary>
    public long MessagesReceived => Interlocked.Read(ref _messagesReceived);

    /// <summary>
    /// 最后连接时间 - Last connection time
    /// </summary>
    public DateTimeOffset? LastConnectedAt => _lastConnectedAt;

    /// <summary>
    /// 最后断开时间 - Last disconnection time
    /// </summary>
    public DateTimeOffset? LastDisconnectedAt => _lastDisconnectedAt;

    /// <summary>
    /// 首次连接时间 - First connection time
    /// </summary>
    public DateTimeOffset? FirstConnectedAt => _firstConnectedAt;

    /// <summary>
    /// 连接时长（秒） - Connection duration in seconds
    /// </summary>
    public long? ConnectionDurationSeconds
    {
        get
        {
            if (_firstConnectedAt == null || _lastDisconnectedAt != null)
                return null;

            return (long)(DateTimeOffset.UtcNow - _firstConnectedAt.Value).TotalSeconds;
        }
    }

    /// <summary>
    /// 增加发送消息计数 - Increment sent messages count
    /// </summary>
    public void IncrementSent()
    {
        Interlocked.Increment(ref _messagesSent);
    }

    /// <summary>
    /// 增加接收消息计数 - Increment received messages count
    /// </summary>
    public void IncrementReceived()
    {
        Interlocked.Increment(ref _messagesReceived);
    }

    /// <summary>
    /// 记录连接 - Record connection
    /// </summary>
    public void RecordConnected()
    {
        var now = DateTimeOffset.UtcNow;
        _lastConnectedAt = now;
        if (_firstConnectedAt == null)
        {
            _firstConnectedAt = now;
        }
    }

    /// <summary>
    /// 记录断开连接 - Record disconnection
    /// </summary>
    public void RecordDisconnected()
    {
        _lastDisconnectedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 重置统计 - Reset statistics
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _messagesSent, 0);
        Interlocked.Exchange(ref _messagesReceived, 0);
        _lastConnectedAt = null;
        _lastDisconnectedAt = null;
        _firstConnectedAt = null;
    }
}
