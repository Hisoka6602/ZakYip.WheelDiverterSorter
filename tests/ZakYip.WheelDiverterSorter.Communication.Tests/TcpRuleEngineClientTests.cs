using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Communication.Tests;

/// <summary>
/// TCP客户端测试：连接、断开、重连、超时
/// </summary>
public class TcpRuleEngineClientTests : IDisposable
{
    private readonly Mock<ILogger<TcpRuleEngineClient>> _loggerMock;
    private TcpListener? _testServer;
    private readonly int _testPort;
    private CancellationTokenSource? _serverCts;

    public TcpRuleEngineClientTests()
    {
        _loggerMock = new Mock<ILogger<TcpRuleEngineClient>>();
        // Use a dynamic port for testing
        _testPort = GetAvailablePort();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            TcpServer = "localhost:9999"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TcpRuleEngineClient(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TcpRuleEngineClient(_loggerMock.Object, null!));
    }

    [Fact]
    public void Constructor_WithEmptyTcpServer_ThrowsArgumentException()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            TcpServer = ""
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TcpRuleEngineClient(_loggerMock.Object, options));
    }

    [Fact]
    public async Task ConnectAsync_WithValidServer_ReturnsTrue()
    {
        // Arrange
        StartTestServer();
        var options = new UpstreamConnectionOptions
        {
            TcpServer = $"localhost:{_testPort}"
        };
        using var client = new TcpRuleEngineClient(_loggerMock.Object, options);

        // Act
        var result = // Connection is automatic - await client.PingAsync();

        // Assert
        Assert.True(result);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidServer_ReturnsFalse()
    {
        // Arrange
        var options = new UpstreamConnectionOptions
        {
            TcpServer = "localhost:19999",
            TimeoutMs = 1000
        };

        // Act
        var result = // Connection is automatic - await client.PingAsync();

        // Assert
        Assert.False(result);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_ReturnsTrue()
    {
        // Arrange
        StartTestServer();
        // Connection is automatic - await client.PingAsync();

        // Act
        var result = // Connection is automatic - await client.PingAsync();

        // Assert
        Assert.True(result);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_DisconnectsSuccessfully()
    {
        // Arrange
        StartTestServer();
        // Connection is automatic - await client.PingAsync();

        // Act
        client.Dispose();

        // Assert
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_CompletesSuccessfully()
    {
        // Arrange
        // (client is not connected)

        // Act & Assert (should not throw)
        client.Dispose();
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task NotifyParcelDetectedAsync_TriggersEventWithResponse()
    {
        // Arrange
        StartTestServerWithResponse();
        var options = new UpstreamConnectionOptions
        {
            TcpServer = $"localhost:{_testPort}",
            TimeoutMs = 5000
        };
        var parcelId = 123456789L;
        ChuteAssignmentEventArgs? receivedNotification = null;

        client.ChuteAssigned += (sender, args) =>
        {
            receivedNotification = args;
        };

        // Act
        var result = await client.SendAsync(new ParcelDetectedMessage { ParcelId = parcelId);

        // Assert
        Assert.True(result);
        Assert.NotNull(receivedNotification);
        Assert.Equal(parcelId, receivedNotification.ParcelId);
    }

    private void StartTestServer()
    {
        _serverCts = new CancellationTokenSource();
        _testServer = new TcpListener(IPAddress.Loopback, _testPort);
        _testServer.Start();

        Task.Run(async () =>
        {
            try
            {
                while (!_serverCts.Token.IsCancellationRequested)
                {
                    if (_testServer.Pending())
                    {
                        var tcpClient = await _testServer.AcceptTcpClientAsync(_serverCts.Token);
                        tcpClient.Close();
                    }
                    await Task.Delay(10, _serverCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }, _serverCts.Token);
    }

    private void StartTestServerWithResponse()
    {
        _serverCts = new CancellationTokenSource();
        _testServer = new TcpListener(IPAddress.Loopback, _testPort);
        _testServer.Start();

        Task.Run(async () =>
        {
            try
            {
                while (!_serverCts.Token.IsCancellationRequested)
                {
                    if (_testServer.Pending())
                    {
                        var tcpClient = await _testServer.AcceptTcpClientAsync(_serverCts.Token);
                        var stream = tcpClient.GetStream();

                        // Read request
                        var buffer = new byte[1024];
                        var bytesRead = await stream.ReadAsync(buffer, _serverCts.Token);
                        var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        var request = JsonSerializer.Deserialize<ChuteAssignmentRequest>(requestJson);

                        // Send response
                        var response = new ChuteAssignmentResponse
                        {
                            ParcelId = request?.ParcelId ?? 0,
                            ChuteId = 1,
                            IsSuccess = true,
                            ResponseTime = DateTimeOffset.Now
                        };
                        var responseJson = JsonSerializer.Serialize(response) + "\n";
                        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                        await stream.WriteAsync(responseBytes, _serverCts.Token);
                        await stream.FlushAsync(_serverCts.Token);

                        tcpClient.Close();
                    }
                    await Task.Delay(10, _serverCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }, _serverCts.Token);
    }

    private void StartTestServerWithDelay(TimeSpan delay)
    {
        _serverCts = new CancellationTokenSource();
        _testServer = new TcpListener(IPAddress.Loopback, _testPort);
        _testServer.Start();

        Task.Run(async () =>
        {
            try
            {
                while (!_serverCts.Token.IsCancellationRequested)
                {
                    if (_testServer.Pending())
                    {
                        var tcpClient = await _testServer.AcceptTcpClientAsync(_serverCts.Token);
                        var stream = tcpClient.GetStream();
                        
                        // Read the request but don't respond (simulate delay/timeout)
                        var buffer = new byte[1024];
                        try
                        {
                            await stream.ReadAsync(buffer, _serverCts.Token);
                            // Wait for the delay before closing (client should timeout before this)
                            await Task.Delay(delay, _serverCts.Token);
                        }
                        catch (Exception)
                        {
                            // Ignore read errors
                        }
                        finally
                        {
                            tcpClient.Close();
                        }
                    }
                    await Task.Delay(10, _serverCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }, _serverCts.Token);
    }

    private void StartTestServerWithConditionalResponse(Func<bool> shouldRespond)
    {
        _serverCts = new CancellationTokenSource();
        _testServer = new TcpListener(IPAddress.Loopback, _testPort);
        _testServer.Start();

        Task.Run(async () =>
        {
            try
            {
                while (!_serverCts.Token.IsCancellationRequested)
                {
                    if (_testServer.Pending())
                    {
                        var tcpClient = await _testServer.AcceptTcpClientAsync(_serverCts.Token);
                        var stream = tcpClient.GetStream();

                        // Read request
                        var buffer = new byte[1024];
                        await stream.ReadAsync(buffer, _serverCts.Token);

                        if (shouldRespond())
                        {
                            var requestJson = Encoding.UTF8.GetString(buffer).Trim('\0', '\n');
                            var request = JsonSerializer.Deserialize<ChuteAssignmentRequest>(requestJson);

                            // Send response
                            var response = new ChuteAssignmentResponse
                            {
                                ParcelId = request?.ParcelId ?? 0,
                                ChuteId = 1,
                                IsSuccess = true,
                                ResponseTime = DateTimeOffset.Now
                            };
                            var responseJson = JsonSerializer.Serialize(response) + "\n";
                            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                            await stream.WriteAsync(responseBytes, _serverCts.Token);
                            await stream.FlushAsync(_serverCts.Token);
                        }

                        tcpClient.Close();
                    }
                    await Task.Delay(10, _serverCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }, _serverCts.Token);
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _serverCts?.Cancel();
        _testServer?.Stop();
        _serverCts?.Dispose();
    }
}
