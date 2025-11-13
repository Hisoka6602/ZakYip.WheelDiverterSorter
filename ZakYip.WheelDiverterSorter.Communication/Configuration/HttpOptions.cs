namespace ZakYip.WheelDiverterSorter.Communication.Configuration;

/// <summary>
/// HTTP协议配置选项
/// </summary>
public class HttpOptions
{
    /// <summary>
    /// HTTP连接池最大连接数
    /// </summary>
    /// <remarks>
    /// 默认10。根据并发需求调整
    /// </remarks>
    public int MaxConnectionsPerServer { get; set; } = 10;

    /// <summary>
    /// 连接空闲超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认60秒。连接空闲超过此时间会被回收
    /// </remarks>
    public int PooledConnectionIdleTimeout { get; set; } = 60;

    /// <summary>
    /// 连接生存时间（秒）
    /// </summary>
    /// <remarks>
    /// 默认0（无限制）。强制回收长期连接以应对DNS变化
    /// </remarks>
    public int PooledConnectionLifetime { get; set; } = 0;

    /// <summary>
    /// 是否使用HTTP/2协议
    /// </summary>
    /// <remarks>
    /// 默认false。仅当服务端支持时启用
    /// </remarks>
    public bool UseHttp2 { get; set; } = false;
}
