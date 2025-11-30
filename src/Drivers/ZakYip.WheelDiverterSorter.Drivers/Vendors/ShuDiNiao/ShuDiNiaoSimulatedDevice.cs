using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮仿真设备（伪服务器）
/// </summary>
/// <remarks>
/// 用于测试和仿真环境，模拟数递鸟摆轮设备端行为：
/// - 监听TCP端口
/// - 接收并解析控制命令
/// - 返回应答和完成消息
/// - 周期性上报设备状态（可选）
/// </remarks>
public sealed class ShuDiNiaoSimulatedDevice : IDisposable
{
    private readonly ILogger<ShuDiNiaoSimulatedDevice> _logger;
    private readonly ISystemClock _clock;
    private readonly string _host;
    private readonly int _port;
    private readonly byte _deviceAddress;
    private readonly int _actionDelayMs;
    
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _disposed;
    private ShuDiNiaoDeviceState _currentState = ShuDiNiaoDeviceState.Standby;

    /// <summary>
    /// 初始化仿真设备
    /// </summary>
    /// <param name="host">监听地址</param>
    /// <param name="port">监听端口</param>
    /// <param name="deviceAddress">设备地址</param>
    /// <param name="actionDelayMs">模拟动作执行延时（毫秒）</param>
    /// <param name="clock">系统时钟</param>
    /// <param name="logger">日志记录器</param>
    public ShuDiNiaoSimulatedDevice(
        string host,
        int port,
        byte deviceAddress,
        int actionDelayMs,
        ISystemClock clock,
        ILogger<ShuDiNiaoSimulatedDevice> logger)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _port = port;
        _deviceAddress = deviceAddress;
        _actionDelayMs = actionDelayMs;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 启动仿真设备
    /// </summary>
    public void Start()
    {
        if (_listener != null)
        {
            _logger.LogWarning("仿真设备已启动，无需重复启动");
            return;
        }

        try
        {
            var ipAddress = IPAddress.Parse(_host);
            _listener = new TcpListener(ipAddress, _port);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenAsync(_cts.Token), _cts.Token);

            _logger.LogInformation(
                "数递鸟仿真设备已启动，监听 {Host}:{Port}，设备地址=0x{DeviceAddress:X2}",
                _host, _port, _deviceAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仿真设备启动失败");
            throw;
        }
    }

    /// <summary>
    /// 停止仿真设备
    /// </summary>
    public async Task StopAsync()
    {
        if (_listener == null)
        {
            return;
        }

        try
        {
            _cts?.Cancel();
            
            if (_listenerTask != null)
            {
                await _listenerTask;
            }

            _listener.Stop();
            _listener = null;

            _logger.LogInformation("数递鸟仿真设备已停止");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "仿真设备停止时出现异常");
        }
    }

    /// <summary>
    /// 监听客户端连接
    /// </summary>
    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation("仿真设备接受客户端连接: {RemoteEndPoint}",
                    client.Client.RemoteEndPoint);

                // 为每个客户端启动处理任务
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "仿真设备接受连接时出现异常");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 处理客户端通信
    /// </summary>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[7]; // 固定7字节帧

                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        // 读取命令帧
                        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                        
                        if (bytesRead == 0)
                        {
                            _logger.LogInformation("客户端断开连接");
                            break;
                        }

                        if (bytesRead != 7)
                        {
                            _logger.LogWarning("接收到非法帧长度: {Length}", bytesRead);
                            continue;
                        }

                        // 解析并处理命令
                        await ProcessCommandAsync(buffer, stream, cancellationToken);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "客户端通信中断");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理客户端时出现异常");
        }
    }

    /// <summary>
    /// 处理接收到的命令
    /// </summary>
    private async Task ProcessCommandAsync(
        byte[] frame,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("仿真设备接收到帧: {Frame}",
                ShuDiNiaoProtocol.FormatBytes(frame));

            // 校验固定字节
            if (frame[0] != 0x51 || frame[1] != 0x52 || frame[2] != 0x57 || frame[6] != 0xFE)
            {
                _logger.LogWarning("接收到非法帧格式");
                return;
            }

            // 检查设备地址
            if (frame[3] != _deviceAddress)
            {
                _logger.LogDebug("设备地址不匹配: 期望=0x{Expected:X2}, 实际=0x{Actual:X2}",
                    _deviceAddress, frame[3]);
                return;
            }

            // 检查消息类型（必须是控制命令）
            if (frame[4] != 0x52)
            {
                _logger.LogWarning("接收到非控制命令消息类型: 0x{MessageType:X2}", frame[4]);
                return;
            }

            var commandByte = frame[5];
            
            // 发送应答
            await SendResponseAsync(stream, commandByte, isAck: true, cancellationToken);

            // 模拟动作执行延时
            if (_actionDelayMs > 0)
            {
                await Task.Delay(_actionDelayMs, cancellationToken);
            }

            // 发送完成
            await SendResponseAsync(stream, commandByte, isAck: false, cancellationToken);

            // 更新设备状态
            UpdateDeviceState(commandByte);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理命令时出现异常");
        }
    }

    /// <summary>
    /// 发送应答或完成消息
    /// </summary>
    private async Task SendResponseAsync(
        NetworkStream stream,
        byte commandByte,
        bool isAck,
        CancellationToken cancellationToken)
    {
        try
        {
            // 根据命令码计算应答码
            byte responseByte = commandByte switch
            {
                0x51 => (byte)(isAck ? 0x51 : 0x51), // 运行：应答0x51，完成无需发送
                0x52 => (byte)(isAck ? 0x52 : 0x52), // 停止：应答0x52，完成无需发送
                0x53 => (byte)(isAck ? 0x53 : 0x56), // 左摆：应答0x53，完成0x56
                0x54 => (byte)(isAck ? 0x54 : 0x57), // 回中：应答0x54，完成0x57
                0x55 => (byte)(isAck ? 0x55 : 0x58), // 右摆：应答0x55，完成0x58
                _ => throw new InvalidOperationException($"未知命令码: 0x{commandByte:X2}")
            };

            // 对于运行和停止命令，完成消息和应答消息相同，不重复发送
            if (!isAck && (commandByte == 0x51 || commandByte == 0x52))
            {
                return;
            }

            var response = new byte[]
            {
                0x51,                                               // 起始字节1
                0x52,                                               // 起始字节2
                0x57,                                               // 长度字节
                _deviceAddress,                                     // 设备地址
                (byte)ShuDiNiaoMessageType.ResponseAndCompletion, // 消息类型：应答与完成
                responseByte,                                       // 应答/完成码
                0xFE                                                // 结束字符
            };

            await stream.WriteAsync(response, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            _logger.LogDebug("仿真设备发送{Type}: {Frame}",
                isAck ? "应答" : "完成", ShuDiNiaoProtocol.FormatBytes(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送响应失败");
        }
    }

    /// <summary>
    /// 更新设备状态
    /// </summary>
    private void UpdateDeviceState(byte commandByte)
    {
        _currentState = commandByte switch
        {
            0x51 => ShuDiNiaoDeviceState.Running,  // 运行
            0x52 => ShuDiNiaoDeviceState.Standby,  // 停止 -> 待机
            _ => ShuDiNiaoDeviceState.Running      // 左摆/回中/右摆都算运行
        };
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
            _cts?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "仿真设备释放资源时出现异常");
        }
    }
}
