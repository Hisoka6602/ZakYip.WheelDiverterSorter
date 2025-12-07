using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Core.Events.Device;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮TCP服务器实现
/// </summary>
/// <remarks>
/// 在服务端模式下，本系统作为TCP服务器，等待摆轮设备主动连接。
/// - 接收设备状态上报（信息一）
/// - 接收设备应答确认（信息三）
/// - 发送控制命令（信息二）
/// - 支持多设备并发连接
/// </remarks>
public sealed class ShuDiNiaoWheelServer : IDisposable
{
    private readonly ILogger<ShuDiNiaoWheelServer> _logger;
    private readonly ISystemClock _systemClock;
    private readonly string _listenAddress;
    private readonly int _listenPort;
    private readonly ConcurrentDictionary<byte, ConnectedDevice> _connectedDevices = new();
    
    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private Task? _acceptTask;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// 设备状态上报事件
    /// </summary>
    public event EventHandler<DeviceStatusEventArgs>? DeviceStatusReceived;

    /// <summary>
    /// 设备连接事件
    /// </summary>
    public event EventHandler<DeviceConnectionEventArgs>? DeviceConnected;

    /// <summary>
    /// 设备断开事件
    /// </summary>
    public event EventHandler<DeviceConnectionEventArgs>? DeviceDisconnected;

    /// <summary>
    /// 初始化数递鸟摆轮服务器
    /// </summary>
    /// <param name="listenAddress">监听地址（如 "0.0.0.0" 表示监听所有网卡）</param>
    /// <param name="listenPort">监听端口</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="systemClock">系统时钟（可选）</param>
    public ShuDiNiaoWheelServer(
        string listenAddress,
        int listenPort,
        ILogger<ShuDiNiaoWheelServer> logger,
        ISystemClock? systemClock = null)
    {
        _listenAddress = listenAddress ?? throw new ArgumentNullException(nameof(listenAddress));
        _listenPort = listenPort;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? new LocalSystemClock();

        if (_listenPort <= 0 || _listenPort > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(listenPort), "端口必须在 1-65535 范围内");
        }
    }

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 已连接设备数量
    /// </summary>
    public int ConnectedDeviceCount => _connectedDevices.Count;

    /// <summary>
    /// 启动TCP服务器
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("数递鸟摆轮服务器已在运行，无需重复启动");
            return;
        }

        try
        {
            var ipAddress = _listenAddress == "0.0.0.0" 
                ? IPAddress.Any 
                : IPAddress.Parse(_listenAddress);

            _listener = new TcpListener(ipAddress, _listenPort);
            _listener.Start();
            _isRunning = true;

            _serverCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _serverCts.Token, 
                cancellationToken);

            _acceptTask = AcceptClientsAsync(linkedCts.Token);

            _logger.LogInformation(
                "[数递鸟服务端] TCP服务器已启动，监听 {Address}:{Port}",
                _listenAddress,
                _listenPort);
        }
        catch (Exception ex)
        {
            _isRunning = false;
            _logger.LogError(ex, "[数递鸟服务端] 启动TCP服务器失败");
            throw;
        }
    }

    /// <summary>
    /// 停止TCP服务器
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        _logger.LogInformation("[数递鸟服务端] 正在停止TCP服务器...");

        _isRunning = false;
        _serverCts?.Cancel();

        // 断开所有设备连接
        foreach (var device in _connectedDevices.Values)
        {
            await DisconnectDeviceAsync(device.DeviceAddress);
        }
        _connectedDevices.Clear();

        // 停止监听
        _listener?.Stop();

        // 等待Accept任务完成
        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("[数递鸟服务端] Accept任务停止超时");
            }
        }

        _serverCts?.Dispose();
        _serverCts = null;

        _logger.LogInformation("[数递鸟服务端] TCP服务器已停止");
    }

    /// <summary>
    /// 向指定设备发送控制命令
    /// </summary>
    /// <param name="deviceAddress">设备地址</param>
    /// <param name="command">控制命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<bool> SendCommandAsync(
        byte deviceAddress,
        ShuDiNiaoControlCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_connectedDevices.TryGetValue(deviceAddress, out var device))
        {
            _logger.LogWarning(
                "[数递鸟服务端-发送] 设备 0x{DeviceAddress:X2} 未连接",
                deviceAddress);
            return false;
        }

        try
        {
            var frame = ShuDiNiaoProtocol.BuildCommandFrame(deviceAddress, command);
            
            _logger.LogInformation(
                "[数递鸟服务端-发送] 向设备 0x{DeviceAddress:X2} 发送命令 | 命令={Command} | 命令帧={Frame}",
                deviceAddress,
                command,
                ShuDiNiaoProtocol.FormatBytes(frame));

            await device.Stream.WriteAsync(frame, cancellationToken);
            await device.Stream.FlushAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[数递鸟服务端-发送] 向设备 0x{DeviceAddress:X2} 发送命令失败 | 命令={Command}",
                deviceAddress,
                command);

            // 发送失败，断开设备连接
            await DisconnectDeviceAsync(deviceAddress);
            return false;
        }
    }

    /// <summary>
    /// 获取已连接设备列表
    /// </summary>
    public IReadOnlyList<byte> GetConnectedDevices()
    {
        return _connectedDevices.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// 接受客户端连接的循环
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;

                _logger.LogInformation(
                    "[数递鸟服务端] 接收到设备连接 | 地址={RemoteAddress}",
                    remoteEndPoint?.ToString() ?? "Unknown");

                // 为每个连接启动独立的处理任务
                _ = Task.Run(() => HandleDeviceAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 正常停止
                break;
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    _logger.LogError(ex, "[数递鸟服务端] 接受客户端连接时发生异常");
                }
            }
        }

        _logger.LogInformation("[数递鸟服务端] Accept循环已退出");
    }

    /// <summary>
    /// 处理单个设备连接
    /// </summary>
    private async Task HandleDeviceAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        var clientAddress = remoteEndPoint?.ToString() ?? "Unknown";
        byte? deviceAddress = null;

        try
        {
            var stream = client.GetStream();
            var buffer = new byte[7]; // 数递鸟协议固定7字节帧

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                // 读取完整的7字节帧
                var bytesRead = 0;
                while (bytesRead < 7 && !cancellationToken.IsCancellationRequested)
                {
                    var count = await stream.ReadAsync(
                        buffer.AsMemory(bytesRead, 7 - bytesRead), 
                        cancellationToken);

                    if (count == 0)
                    {
                        // 连接已关闭
                        _logger.LogInformation(
                            "[数递鸟服务端] 设备 {ClientAddress} 连接已关闭",
                            clientAddress);
                        return;
                    }

                    bytesRead += count;
                }

                if (bytesRead == 7)
                {
                    deviceAddress = ProcessFrame(buffer, stream, deviceAddress, clientAddress);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[数递鸟服务端] 处理设备 {ClientAddress} 时发生异常",
                clientAddress);
        }
        finally
        {
            if (deviceAddress.HasValue)
            {
                await DisconnectDeviceAsync(deviceAddress.Value);
            }

            client.Close();
            client.Dispose();
        }
    }

    /// <summary>
    /// 处理接收到的协议帧
    /// </summary>
    /// <returns>设备地址（如果已识别）</returns>
    private byte? ProcessFrame(
        byte[] buffer,
        NetworkStream stream,
        byte? currentDeviceAddress,
        string clientAddress)
    {
        byte? deviceAddress = currentDeviceAddress;

        // 尝试解析设备状态上报（信息一）
        if (ShuDiNiaoProtocol.TryParseDeviceStatus(
            buffer, 
            out var devAddr, 
            out var deviceState))
        {
            _logger.LogInformation(
                "[数递鸟服务端-接收] 收到设备状态上报 | 设备地址=0x{DeviceAddress:X2} | 状态={State} | 原始帧={Frame}",
                devAddr,
                deviceState,
                ShuDiNiaoProtocol.FormatBytes(buffer));

            // 如果是首次收到该设备消息，记录连接
            if (!deviceAddress.HasValue)
            {
                deviceAddress = devAddr;
                var device = new ConnectedDevice
                {
                    DeviceAddress = devAddr,
                    Stream = stream,
                    ClientAddress = clientAddress,
                    ConnectedAt = _systemClock.LocalNow
                };

                _connectedDevices.TryAdd(devAddr, device);

                OnDeviceConnected(devAddr, clientAddress);
            }

            // 触发状态事件
            DeviceStatusReceived?.Invoke(this, new DeviceStatusEventArgs
            {
                DeviceAddress = devAddr,
                DeviceState = deviceState,
                ReceivedAt = _systemClock.LocalNow
            });

            return deviceAddress;
        }

        // 尝试解析应答与完成（信息三）
        if (ShuDiNiaoProtocol.TryParseResponse(
            buffer,
            out var respDevAddr,
            out var responseCode))
        {
            _logger.LogInformation(
                "[数递鸟服务端-接收] 收到设备应答 | 设备地址=0x{DeviceAddress:X2} | 应答码={ResponseCode} | 原始帧={Frame}",
                respDevAddr,
                responseCode,
                ShuDiNiaoProtocol.FormatBytes(buffer));

            return deviceAddress;
        }

        // 无法解析的帧
        _logger.LogWarning(
            "[数递鸟服务端-接收] 收到无效帧 | 客户端={ClientAddress} | 原始帧={Frame}",
            clientAddress,
            ShuDiNiaoProtocol.FormatBytes(buffer));

        return deviceAddress;
    }

    /// <summary>
    /// 断开设备连接
    /// </summary>
    private async Task DisconnectDeviceAsync(byte deviceAddress)
    {
        if (_connectedDevices.TryRemove(deviceAddress, out var device))
        {
            try
            {
                device.Stream.Close();
                await device.Stream.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[数递鸟服务端] 断开设备 0x{DeviceAddress:X2} 时发生异常",
                    deviceAddress);
            }

            OnDeviceDisconnected(deviceAddress, device.ClientAddress);
        }
    }

    /// <summary>
    /// 触发设备连接事件
    /// </summary>
    private void OnDeviceConnected(byte deviceAddress, string clientAddress)
    {
        _logger.LogInformation(
            "[数递鸟服务端] 设备已连接 | 设备地址=0x{DeviceAddress:X2} | 客户端={ClientAddress}",
            deviceAddress,
            clientAddress);

        DeviceConnected?.Invoke(this, new DeviceConnectionEventArgs
        {
            DeviceAddress = deviceAddress,
            ClientAddress = clientAddress,
            Timestamp = _systemClock.LocalNow
        });
    }

    /// <summary>
    /// 触发设备断开事件
    /// </summary>
    private void OnDeviceDisconnected(byte deviceAddress, string clientAddress)
    {
        _logger.LogInformation(
            "[数递鸟服务端] 设备已断开 | 设备地址=0x{DeviceAddress:X2} | 客户端={ClientAddress}",
            deviceAddress,
            clientAddress);

        DeviceDisconnected?.Invoke(this, new DeviceConnectionEventArgs
        {
            DeviceAddress = deviceAddress,
            ClientAddress = clientAddress,
            Timestamp = _systemClock.LocalNow
        });
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[数递鸟服务端] 释放资源时发生异常");
        }
    }

    /// <summary>
    /// 已连接设备信息
    /// </summary>
    private sealed class ConnectedDevice
    {
        public required byte DeviceAddress { get; init; }
        public required NetworkStream Stream { get; init; }
        public required string ClientAddress { get; init; }
        public required DateTime ConnectedAt { get; init; }
    }
}
