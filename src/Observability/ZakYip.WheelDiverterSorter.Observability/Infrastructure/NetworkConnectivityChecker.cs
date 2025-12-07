using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ZakYip.WheelDiverterSorter.Core.Hardware.Connectivity;

namespace ZakYip.WheelDiverterSorter.Observability.Infrastructure;

/// <summary>
/// 网络连通性检查器实现
/// </summary>
/// <remarks>
/// 使用System.Net.NetworkInformation.Ping和TcpClient进行连通性检查。
/// 支持ICMP Ping和TCP端口探测两种检查方式。
/// </remarks>
public sealed class NetworkConnectivityChecker : INetworkConnectivityChecker
{
    private readonly ILogger<NetworkConnectivityChecker> _logger;

    public NetworkConnectivityChecker(ILogger<NetworkConnectivityChecker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ConnectivityCheckResult> PingAsync(
        string hostOrIp, 
        int timeoutMs = 3000, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostOrIp))
        {
            return ConnectivityCheckResult.Failure(
                "主机名或IP地址不能为空", 
                "INVALID_HOST");
        }

        try
        {
            using var ping = new Ping();
            var stopwatch = Stopwatch.StartNew();
            
            var reply = await ping.SendPingAsync(hostOrIp, timeoutMs);
            stopwatch.Stop();

            if (reply.Status == IPStatus.Success)
            {
                _logger.LogDebug(
                    "Ping成功: {Host}, 响应时间={RoundtripTime}ms", 
                    hostOrIp, reply.RoundtripTime);
                
                return ConnectivityCheckResult.Success(reply.RoundtripTime);
            }
            else
            {
                _logger.LogWarning(
                    "Ping失败: {Host}, 状态={Status}", 
                    hostOrIp, reply.Status);
                
                return ConnectivityCheckResult.Failure(
                    $"Ping失败: {reply.Status}", 
                    $"PING_{reply.Status}");
            }
        }
        catch (PingException ex)
        {
            _logger.LogWarning(ex, "Ping异常: {Host}", hostOrIp);
            return ConnectivityCheckResult.Failure(
                $"Ping异常: {ex.Message}", 
                "PING_EXCEPTION");
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Ping操作被取消: {Host}", hostOrIp);
            return ConnectivityCheckResult.Failure(
                "操作已取消", 
                "CANCELLED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ping发生未预期异常: {Host}", hostOrIp);
            return ConnectivityCheckResult.Failure(
                $"Ping发生异常: {ex.Message}", 
                "UNEXPECTED_ERROR");
        }
    }

    /// <inheritdoc/>
    public async Task<ConnectivityCheckResult> CheckTcpPortAsync(
        string hostOrIp, 
        int port, 
        int timeoutMs = 3000, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostOrIp))
        {
            return ConnectivityCheckResult.Failure(
                "主机名或IP地址不能为空", 
                "INVALID_HOST");
        }

        if (port <= 0 || port > 65535)
        {
            return ConnectivityCheckResult.Failure(
                $"端口号无效: {port}，有效范围为1-65535", 
                "INVALID_PORT");
        }

        TcpClient? tcpClient = null;
        CancellationTokenSource? timeoutCts = null;
        try
        {
            tcpClient = new TcpClient();
            var stopwatch = Stopwatch.StartNew();

            // 创建组合取消令牌以支持超时和外部取消
            timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            // 使用取消令牌进行连接，确保超时时能够取消连接操作
            var connectTask = tcpClient.ConnectAsync(hostOrIp, port, timeoutCts.Token);
            
            await connectTask;
            
            stopwatch.Stop();

            if (tcpClient.Connected)
            {
                _logger.LogDebug(
                    "TCP端口连接成功: {Host}:{Port}, 耗时={ElapsedMs}ms", 
                    hostOrIp, port, stopwatch.ElapsedMilliseconds);
                
                return ConnectivityCheckResult.Success(stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "TCP端口连接失败: {Host}:{Port}", 
                    hostOrIp, port);
                
                return ConnectivityCheckResult.Failure(
                    $"TCP端口连接失败: {hostOrIp}:{port}", 
                    "TCP_CONNECT_FAILED");
            }
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            // 超时取消（不是外部取消令牌触发的）
            _logger.LogWarning(
                "TCP端口连接超时: {Host}:{Port}, 超时={TimeoutMs}ms", 
                hostOrIp, port, timeoutMs);
            
            return ConnectivityCheckResult.Failure(
                $"连接超时（{timeoutMs}ms）", 
                "TCP_TIMEOUT");
        }
        catch (OperationCanceledException)
        {
            // 外部取消令牌触发的取消
            _logger.LogDebug("TCP连接操作被取消: {Host}:{Port}", hostOrIp, port);
            return ConnectivityCheckResult.Failure(
                "操作已取消", 
                "CANCELLED");
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, 
                "TCP端口连接失败: {Host}:{Port}, SocketErrorCode={ErrorCode}", 
                hostOrIp, port, ex.SocketErrorCode);
            
            return ConnectivityCheckResult.Failure(
                $"TCP连接失败: {ex.SocketErrorCode} - {ex.Message}", 
                $"TCP_{ex.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "TCP端口检查发生未预期异常: {Host}:{Port}", 
                hostOrIp, port);
            
            return ConnectivityCheckResult.Failure(
                $"TCP检查发生异常: {ex.Message}", 
                "UNEXPECTED_ERROR");
        }
        finally
        {
            timeoutCts?.Dispose();
            tcpClient?.Dispose();
        }
    }
}
