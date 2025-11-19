namespace ZakYip.WheelDiverterSorter.Communication.Configuration;

/// <summary>
/// SignalR协议配置选项
/// </summary>
public class SignalROptions
{
    /// <summary>
    /// 握手超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认15秒
    /// </remarks>
    public int HandshakeTimeout { get; set; } = 15;

    /// <summary>
    /// 保持连接超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认30秒。服务端发送心跳包的间隔
    /// </remarks>
    public int KeepAliveInterval { get; set; } = 30;

    /// <summary>
    /// 服务端超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。未收到服务端心跳的最大等待时间
    /// </remarks>
    public int ServerTimeout { get; set; } = 60;

    /// <summary>
    /// 重连间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 默认null（使用默认策略）。可设置固定重连间隔，如 [0, 2000, 5000, 10000]
    /// </remarks>
    public int[]? ReconnectIntervals { get; set; }

    /// <summary>
    /// 是否跳过协商（直接使用WebSocket）
    /// </summary>
    /// <remarks>
    /// 默认false。设为true可减少连接延迟，但需要服务端支持
    /// </remarks>
    public bool SkipNegotiation { get; set; } = false;
}
