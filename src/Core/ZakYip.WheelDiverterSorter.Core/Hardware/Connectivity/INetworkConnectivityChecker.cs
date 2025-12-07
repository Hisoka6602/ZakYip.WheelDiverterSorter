using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Core.Hardware.Connectivity;

/// <summary>
/// 网络连通性检查器接口
/// </summary>
/// <remarks>
/// 提供网络连通性检查功能，用于在连接硬件设备前验证网络可达性。
/// 支持ICMP Ping、TCP端口探测等多种检查方式。
/// </remarks>
public interface INetworkConnectivityChecker
{
    /// <summary>
    /// 检查主机是否可达（通过ICMP Ping）
    /// </summary>
    /// <param name="hostOrIp">主机名或IP地址</param>
    /// <param name="timeoutMs">超时时间（毫秒），默认3000ms</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查结果，包含是否可达和响应时间</returns>
    Task<ConnectivityCheckResult> PingAsync(
        string hostOrIp, 
        int timeoutMs = 3000, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查TCP端口是否可连接
    /// </summary>
    /// <param name="hostOrIp">主机名或IP地址</param>
    /// <param name="port">TCP端口号</param>
    /// <param name="timeoutMs">超时时间（毫秒），默认3000ms</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查结果，包含是否可连接和连接耗时</returns>
    Task<ConnectivityCheckResult> CheckTcpPortAsync(
        string hostOrIp, 
        int port, 
        int timeoutMs = 3000, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 连通性检查结果
/// </summary>
public sealed record ConnectivityCheckResult
{
    /// <summary>是否可达/可连接</summary>
    public required bool IsReachable { get; init; }

    /// <summary>响应时间（毫秒），null表示检查失败</summary>
    public long? ResponseTimeMs { get; init; }

    /// <summary>错误消息（如果检查失败）</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>错误代码（如果检查失败）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ConnectivityCheckResult Success(long responseTimeMs) => new()
    {
        IsReachable = true,
        ResponseTimeMs = responseTimeMs,
        ErrorMessage = null,
        ErrorCode = null
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ConnectivityCheckResult Failure(string errorMessage, string errorCode) => new()
    {
        IsReachable = false,
        ResponseTimeMs = null,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}
