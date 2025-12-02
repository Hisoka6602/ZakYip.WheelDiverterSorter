namespace ZakYip.WheelDiverterSorter.Communication.Configuration;

/// <summary>
/// TCP协议配置选项
/// </summary>
public class TcpOptions
{
    /// <summary>
    /// TCP接收缓冲区大小（字节）
    /// </summary>
    /// <remarks>
    /// 默认8KB。根据消息大小调整：
    /// - 小消息（<1KB）：4096
    /// - 中等消息（1-4KB）：8192
    /// - 大消息（>4KB）：16384或更大
    /// </remarks>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// TCP发送缓冲区大小（字节）
    /// </summary>
    /// <remarks>
    /// 默认8KB
    /// </remarks>
    public int SendBufferSize { get; set; } = 8192;

    /// <summary>
    /// 是否启用Nagle算法
    /// </summary>
    /// <remarks>
    /// 默认false（禁用Nagle）以降低延迟
    /// 启用Nagle可以提高网络利用率，但增加延迟
    /// </remarks>
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// 是否启用TCP KeepAlive
    /// </summary>
    /// <remarks>
    /// 默认true。启用后可防止TCP连接空闲超时断线
    /// </remarks>
    public bool EnableKeepAlive { get; set; } = true;

    /// <summary>
    /// TCP KeepAlive时间间隔（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。在连接空闲多久后开始发送KeepAlive探测包
    /// 仅在 EnableKeepAlive = true 时有效
    /// </remarks>
    public int KeepAliveTime { get; set; } = 60;

    /// <summary>
    /// TCP KeepAlive探测间隔（秒）
    /// </summary>
    /// <remarks>
    /// 默认10秒。每次KeepAlive探测包之间的间隔
    /// 仅在 EnableKeepAlive = true 时有效
    /// </remarks>
    public int KeepAliveInterval { get; set; } = 10;

    /// <summary>
    /// TCP KeepAlive重试次数
    /// </summary>
    /// <remarks>
    /// 默认3次。连续多少次探测失败后判定连接断开
    /// 仅在 EnableKeepAlive = true 时有效
    /// 注意：Windows系统可能不完全支持此参数
    /// </remarks>
    public int KeepAliveRetryCount { get; set; } = 3;
}
