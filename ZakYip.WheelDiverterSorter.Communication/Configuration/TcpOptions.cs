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
    /// TCP KeepAlive时间间隔（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。0表示禁用KeepAlive
    /// </remarks>
    public int KeepAliveInterval { get; set; } = 60;
}
