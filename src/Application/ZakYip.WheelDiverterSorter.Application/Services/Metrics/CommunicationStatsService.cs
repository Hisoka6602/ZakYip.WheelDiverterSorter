using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.Application.Services.Metrics;

/// <summary>
/// 通信统计服务接口 - Communication Statistics Service Interface
/// </summary>
/// <remarks>
/// 定义跟踪通信消息发送和接收次数的接口
/// 实现 IMessageStatsCallback 以便 Communication 层可以直接使用，无需适配器
/// </remarks>
public interface ICommunicationStatsService : IMessageStatsCallback
{
    /// <summary>
    /// 发送消息计数 - Messages sent count
    /// </summary>
    long MessagesSent { get; }

    /// <summary>
    /// 接收消息计数 - Messages received count
    /// </summary>
    long MessagesReceived { get; }

    /// <summary>
    /// 最后连接时间 - Last connection time
    /// </summary>
    DateTimeOffset? LastConnectedAt { get; }

    /// <summary>
    /// 最后断开时间 - Last disconnection time
    /// </summary>
    DateTimeOffset? LastDisconnectedAt { get; }

    /// <summary>
    /// 首次连接时间 - First connection time
    /// </summary>
    DateTimeOffset? FirstConnectedAt { get; }

    /// <summary>
    /// 连接时长（秒） - Connection duration in seconds
    /// </summary>
    long? ConnectionDurationSeconds { get; }

    // IncrementSent() 和 IncrementReceived() 继承自 IMessageStatsCallback

    /// <summary>
    /// 记录连接 - Record connection
    /// </summary>
    void RecordConnected();

    /// <summary>
    /// 记录断开连接 - Record disconnection
    /// </summary>
    void RecordDisconnected();

    /// <summary>
    /// 重置统计 - Reset statistics
    /// </summary>
    void Reset();
}

/// <summary>
/// 通信统计服务 - Communication Statistics Service
/// </summary>
/// <remarks>
/// 跟踪通信消息的发送和接收次数
/// </remarks>
public class CommunicationStatsService : ICommunicationStatsService
{
    private readonly ISystemClock _clock;
    private long _messagesSent;
    private long _messagesReceived;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastDisconnectedAt;
    private DateTimeOffset? _firstConnectedAt;

    public CommunicationStatsService(ISystemClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

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

            return (long)(new DateTimeOffset(_clock.LocalNow) - _firstConnectedAt.Value).TotalSeconds;
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
        var now = new DateTimeOffset(_clock.LocalNow);
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
        _lastDisconnectedAt = new DateTimeOffset(_clock.LocalNow);
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
