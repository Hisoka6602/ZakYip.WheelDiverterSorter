using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Tests;

/// <summary>
/// TCP客户端契约测试实现
/// PR-U1: 使用 IUpstreamRoutingClient 替代 IRuleEngineClient
/// </summary>
public class TcpRuleEngineClientContractTests : RuleEngineClientContractTestsBase
{
    private TcpMockServer? _mockServer;
    private readonly int _testPort;

    public TcpRuleEngineClientContractTests()
    {
        _testPort = GetAvailablePort();
    }

    protected override IUpstreamRoutingClient CreateClient()
    {
        var options = new UpstreamConnectionOptions
        {
            TcpServer = $"localhost:{_testPort}",
            TimeoutMs = 5000,
            RetryCount = 2,
            RetryDelayMs = 500
        };

        var loggerMock = new Mock<ILogger<TcpRuleEngineClient>>();
        var clockMock = Mock.Of<ISystemClock>();

        return new TcpRuleEngineClient(loggerMock.Object, options, clockMock);
    }

    protected override async Task StartMockServerAsync()
    {
        _mockServer = new TcpMockServer(_testPort);
        await _mockServer.StartAsync();
    }

    protected override async Task StopMockServerAsync()
    {
        if (_mockServer != null)
        {
            await _mockServer.StopAsync();
            _mockServer = null;
        }
    }

    protected override async Task ConfigureMockServerBehaviorAsync(MockServerBehavior behavior)
    {
        if (_mockServer != null)
        {
            _mockServer.Behavior = behavior;
        }
        await Task.CompletedTask;
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public override void Dispose()
    {
        base.Dispose();
        _mockServer?.Dispose();
    }
}

/// <summary>
/// TCP模拟服务器，用于测试
/// </summary>
internal class TcpMockServer : IDisposable
{
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly List<TcpClient> _clients = new();
    public MockServerBehavior Behavior { get; set; } = MockServerBehavior.PushNormally;

    public TcpMockServer(int port)
    {
        _port = port;
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _cts = new CancellationTokenSource();

        // 启动接受连接的循环
        _ = Task.Run(() => AcceptClientsAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        
        // 关闭所有客户端连接
        foreach (var client in _clients.ToList())
        {
            client.Close();
            client.Dispose();
        }
        _clients.Clear();

        _listener?.Stop();
        
        await Task.CompletedTask;
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                _clients.Add(client);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Ignore errors during shutdown
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested && client.Connected)
        {
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break;

                var receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(receivedText);
                
                // 处理所有完整的消息（以换行符分隔）
                var messages = messageBuffer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // 如果最后一个字符不是换行符，保留最后一条不完整的消息
                if (!receivedText.EndsWith('\n') && messages.Length > 0)
                {
                    messageBuffer.Clear();
                    messageBuffer.Append(messages[^1]);
                    messages = messages[..^1];
                }
                else
                {
                    messageBuffer.Clear();
                }

                // 处理每条完整的消息
                foreach (var message in messages)
                {
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    // 解析请求
                    ParcelDetectionNotification? notification = null;
                    try
                    {
                        notification = JsonSerializer.Deserialize<ParcelDetectionNotification>(message.Trim());
                    }
                    catch
                    {
                        // 如果解析失败，忽略
                        continue;
                    }

                    if (notification != null)
                    {
                        await HandleNotificationAsync(stream, notification, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // 忽略错误，继续处理
                break;
            }
        }
    }

    private async Task HandleNotificationAsync(
        NetworkStream stream, 
        ParcelDetectionNotification notification,
        CancellationToken cancellationToken)
    {
        switch (Behavior)
        {
            case MockServerBehavior.PushNormally:
                // 立即推送格口分配
                var response = new ChuteAssignmentEventArgs
                {
                    ParcelId = notification.ParcelId,
                    ChuteId = 1,
                    AssignedAt = DateTimeOffset.Now
                };
                await SendResponseAsync(stream, response, cancellationToken);
                break;

            case MockServerBehavior.NeverPush:
                // 不推送，模拟超时
                break;

            case MockServerBehavior.DelayedPush:
                // 延迟后推送
                await Task.Delay(3000, cancellationToken);
                var delayedResponse = new ChuteAssignmentEventArgs
                {
                    ParcelId = notification.ParcelId,
                    ChuteId = 2,
                    AssignedAt = DateTimeOffset.Now
                };
                await SendResponseAsync(stream, delayedResponse, cancellationToken);
                break;

            case MockServerBehavior.RandomDisconnect:
                // 随机断开连接
                if (Random.Shared.Next(2) == 0)
                {
                    throw new IOException("Simulated disconnect");
                }
                break;
        }
    }

    private async Task SendResponseAsync<T>(
        NetworkStream stream, 
        T response,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        _listener?.Stop();
    }
}
