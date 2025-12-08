using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮驱动器实现
/// </summary>
/// <remarks>
/// 通过TCP协议与数递鸟摆轮设备通信，实现摆轮控制功能。
/// - 支持运行/停止控制
/// - 支持左摆/右摆/回中方向控制
/// - 支持双向通信，接收设备状态上报实现协议心跳检测
/// - 自动处理连接失败和通信异常
/// - 所有异常不向外冒泡，通过日志记录和返回值反馈
/// </remarks>
public sealed class ShuDiNiaoWheelDiverterDriver : IWheelDiverterDriver, IHeartbeatCapable, IDisposable
{
    /// <summary>
    /// 数递鸟摆轮指令写入最小间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 数递鸟摆轮设备要求指令写入间隔不得小于90ms，否则设备可能无法正确处理指令。
    /// </remarks>
    private const int MinCommandIntervalMs = 90;
    
    /// <summary>
    /// 数递鸟心跳超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 根据 README.md，设备每 1 秒发送状态上报，超过 5 秒未收到判定为离线。
    /// </remarks>
    private const int HeartbeatTimeoutMs = 5000;
    
    /// <summary>
    /// 接收任务停止等待超时时间（秒）
    /// </summary>
    private static readonly TimeSpan ReceiveTaskStopTimeout = TimeSpan.FromSeconds(2);
    
    private readonly ILogger<ShuDiNiaoWheelDiverterDriver> _logger;
    private readonly ShuDiNiaoDeviceEntry _config;
    private readonly ISystemClock _clock;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;
    private string _currentStatus = "未连接";
    private long _lastCommandTicks = 0;
    private string? _lastCommandSent = null;
    
    // 心跳相关字段
    private DateTimeOffset _lastStatusReportTime = DateTimeOffset.MinValue;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;

    /// <inheritdoc/>
    public string DiverterId => _config.DiverterId.ToString();

    /// <summary>
    /// 获取连接信息（IP:端口）
    /// </summary>
    public string ConnectionInfo => $"{_config.Host}:{_config.Port}";

    /// <summary>
    /// 获取最后发送的命令（十六进制格式）
    /// </summary>
    public string? LastCommandSent => _lastCommandSent;

    /// <summary>
    /// 初始化数递鸟摆轮驱动器
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="clock">系统时钟</param>
    public ShuDiNiaoWheelDiverterDriver(
        ShuDiNiaoDeviceEntry config,
        ILogger<ShuDiNiaoWheelDiverterDriver> logger,
        ISystemClock clock)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        _logger.LogInformation(
            "已初始化数递鸟摆轮驱动器 {DiverterId}，主机={Host}，端口={Port}，设备地址=0x{DeviceAddress:X2}",
            DiverterId, _config.Host, _config.Port, _config.DeviceAddress);
    }

    /// <inheritdoc/>
    public async Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行左转", DiverterId);
            
            var result = await SendCommandAsync(ShuDiNiaoControlCommand.TurnLeft, cancellationToken);
            
            if (result)
            {
                _currentStatus = "左转";
                _logger.LogInformation("摆轮 {DiverterId} 左转命令发送成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 左转命令发送失败", DiverterId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 左转异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TurnRightAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行右转", DiverterId);
            
            var result = await SendCommandAsync(ShuDiNiaoControlCommand.TurnRight, cancellationToken);
            
            if (result)
            {
                _currentStatus = "右转";
                _logger.LogInformation("摆轮 {DiverterId} 右转命令发送成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 右转命令发送失败", DiverterId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 右转异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> PassThroughAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行直通（回中）", DiverterId);
            
            var result = await SendCommandAsync(ShuDiNiaoControlCommand.ReturnCenter, cancellationToken);
            
            if (result)
            {
                _currentStatus = "直通";
                _logger.LogInformation("摆轮 {DiverterId} 直通命令发送成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 直通命令发送失败", DiverterId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 直通异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行停止", DiverterId);
            
            var result = await SendCommandAsync(ShuDiNiaoControlCommand.Stop, cancellationToken);
            
            if (result)
            {
                _currentStatus = "已停止";
                _logger.LogInformation("摆轮 {DiverterId} 停止命令发送成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 停止命令发送失败", DiverterId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 停止异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行运行", DiverterId);
            
            var result = await SendCommandAsync(ShuDiNiaoControlCommand.Run, cancellationToken);
            
            if (result)
            {
                _currentStatus = "运行中";
                _logger.LogInformation("摆轮 {DiverterId} 运行命令发送成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 运行命令发送失败", DiverterId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 运行异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<string> GetStatusAsync()
    {
        return Task.FromResult(_currentStatus);
    }

    /// <inheritdoc/>
    public Task<bool> CheckHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        // 实现数递鸟协议心跳检测
        // 根据 README.md，数递鸟设备每 1 秒主动发送状态上报（信息一，消息类型 0x51）
        // 检查距离最后接收状态上报时间是否超过 5 秒
        
        var isConnected = _tcpClient?.Connected == true && _stream != null;
        
        if (!isConnected)
        {
            _logger.LogDebug("摆轮 {DiverterId} 心跳检查: TCP连接断开", DiverterId);
            return Task.FromResult(false);
        }
        
        // 检查心跳超时
        var now = _clock.LocalNow;
#pragma warning disable MA0132 // Both now and _lastStatusReportTime are DateTimeOffset
        var timeSinceLastReport = now - _lastStatusReportTime;
#pragma warning restore MA0132
        
        // 如果从未收到状态上报，则使用TCP连接状态（可能是设备刚连接）
        if (_lastStatusReportTime == DateTimeOffset.MinValue)
        {
            _logger.LogDebug("摆轮 {DiverterId} 心跳检查: 尚未收到状态上报，TCP连接正常", DiverterId);
            return Task.FromResult(true);
        }
        
        // 检查是否超时
        if (timeSinceLastReport.TotalMilliseconds > HeartbeatTimeoutMs)
        {
            _logger.LogWarning(
                "摆轮 {DiverterId} 心跳超时: 距离最后状态上报已 {Elapsed:F1} 秒（超时阈值 {Timeout} 秒）",
                DiverterId,
                timeSinceLastReport.TotalSeconds,
                HeartbeatTimeoutMs / 1000.0);
            return Task.FromResult(false);
        }
        
        _logger.LogDebug(
            "摆轮 {DiverterId} 心跳正常: 距离最后状态上报 {Elapsed:F1} 秒",
            DiverterId,
            timeSinceLastReport.TotalSeconds);
        
        return Task.FromResult(true);
    }

    /// <summary>
    /// 发送控制命令到设备
    /// </summary>
    private async Task<bool> SendCommandAsync(
        ShuDiNiaoControlCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // 检查命令发送间隔（数递鸟摆轮要求最小90ms间隔）
            var now = Environment.TickCount64;
            var elapsedMs = now - _lastCommandTicks;
            
            if (_lastCommandTicks != 0 && elapsedMs < MinCommandIntervalMs)
            {
                _logger.LogWarning(
                    "[摆轮通信-限流] 摆轮 {DiverterId} 指令间隔过短，拒绝发送 | 命令={Command} | 距上次指令={ElapsedMs}ms | 最小间隔={MinIntervalMs}ms",
                    DiverterId,
                    command,
                    elapsedMs,
                    MinCommandIntervalMs);
                return false;
            }
            
            // 确保连接建立
            if (!await EnsureConnectedAsync(cancellationToken))
            {
                _logger.LogWarning(
                    "[摆轮通信-发送] 摆轮 {DiverterId} 未连接，无法发送命令 | 命令={Command}",
                    DiverterId,
                    command);
                return false;
            }

            // 构造命令帧
            var frame = ShuDiNiaoProtocol.BuildCommandFrame(_config.DeviceAddress, command);
            
            // 保存最后发送的命令（用于诊断）
            _lastCommandSent = ShuDiNiaoProtocol.FormatBytes(frame);
            
            // 记录发送的完整命令帧内容
            _logger.LogInformation(
                "[摆轮通信-发送] 摆轮 {DiverterId} 发送命令 | 命令={Command} | 设备地址=0x{DeviceAddress:X2} | 命令帧={Frame}",
                DiverterId,
                command,
                _config.DeviceAddress,
                _lastCommandSent);

            // 发送命令
            if (_stream != null)
            {
                await _stream.WriteAsync(frame, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
                
                // 更新最后命令时间
                _lastCommandTicks = now;
                
                _logger.LogInformation(
                    "[摆轮通信-发送完成] 摆轮 {DiverterId} 命令发送成功 | 命令={Command} | 字节数={ByteCount}",
                    DiverterId,
                    command,
                    frame.Length);
                return true;
            }

            return false;
        }
        catch (SocketException ex)
        {
            _logger.LogError(
                ex,
                "[摆轮通信-发送] 摆轮 {DiverterId} 发送命令失败（网络异常） | 命令={Command}",
                DiverterId,
                command);
            await CloseConnectionAsync();
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogError(
                ex,
                "[摆轮通信-发送] 摆轮 {DiverterId} 发送命令失败（IO异常） | 命令={Command}",
                DiverterId,
                command);
            await CloseConnectionAsync();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[摆轮通信-发送] 摆轮 {DiverterId} 发送命令失败（未知异常） | 命令={Command}",
                DiverterId,
                command);
            return false;
        }
    }

    /// <summary>
    /// 确保TCP连接已建立
    /// </summary>
    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_tcpClient?.Connected == true && _stream != null)
        {
            return true;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查
            if (_tcpClient?.Connected == true && _stream != null)
            {
                return true;
            }

            // 关闭旧连接
            await CloseConnectionAsync();

            // 建立新连接
            _logger.LogInformation("摆轮 {DiverterId} 正在连接到 {Host}:{Port}...",
                DiverterId, _config.Host, _config.Port);

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_config.Host, _config.Port, cancellationToken);
            _stream = _tcpClient.GetStream();
            
            _currentStatus = "已连接";
            _logger.LogInformation("摆轮 {DiverterId} 连接成功", DiverterId);
            
            // 启动接收任务以监听设备状态上报
            StartReceiveTask();
            
            return true;
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 连接失败（网络异常）：{Message}",
                DiverterId, ex.Message);
            _currentStatus = "连接失败";
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 连接失败（未知异常）：{Message}",
                DiverterId, ex.Message);
            _currentStatus = "连接失败";
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// 关闭TCP连接
    /// </summary>
    private async Task CloseConnectionAsync()
    {
        try
        {
            // 停止接收任务
            StopReceiveTask();
            
            if (_stream != null)
            {
                await _stream.DisposeAsync();
                _stream = null;
            }

            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;

            _currentStatus = "未连接";
            _lastStatusReportTime = DateTimeOffset.MinValue; // 重置心跳时间
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "摆轮 {DiverterId} 关闭连接时出现异常", DiverterId);
        }
    }

    /// <summary>
    /// 启动接收任务
    /// </summary>
    private void StartReceiveTask()
    {
        // 停止旧任务
        StopReceiveTask();
        
        // 启动新任务
        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
        
        _logger.LogDebug("摆轮 {DiverterId} 已启动接收任务", DiverterId);
    }
    
    /// <summary>
    /// 停止接收任务
    /// </summary>
    private void StopReceiveTask()
    {
        if (_receiveCts != null)
        {
            _receiveCts.Cancel();
            _receiveCts.Dispose();
            _receiveCts = null;
        }
        
        if (_receiveTask != null)
        {
            try
            {
                // 等待任务完成，但不阻塞太久
                _receiveTask.Wait(ReceiveTaskStopTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "摆轮 {DiverterId} 停止接收任务时出现异常", DiverterId);
            }
            finally
            {
                _receiveTask = null;
            }
        }
        
        _logger.LogDebug("摆轮 {DiverterId} 已停止接收任务", DiverterId);
    }
    
    /// <summary>
    /// 接收循环，持续监听设备状态上报
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("摆轮 {DiverterId} 接收循环已启动", DiverterId);
        
        var buffer = new byte[ShuDiNiaoProtocol.FrameLength];
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // 先获取stream的局部引用，避免竞态条件（TOCTOU问题）
                var localStream = _stream;
                if (localStream == null)
                {
                    _logger.LogDebug("摆轮 {DiverterId} 接收循环：流已关闭", DiverterId);
                    break;
                }
                
                try
                {
                    // 读取一个完整的协议帧（7字节）
                    var bytesRead = 0;
                    while (bytesRead < ShuDiNiaoProtocol.FrameLength && !cancellationToken.IsCancellationRequested)
                    {
                        var read = await localStream.ReadAsync(
                            buffer.AsMemory(bytesRead, ShuDiNiaoProtocol.FrameLength - bytesRead),
                            cancellationToken);
                        
                        if (read == 0)
                        {
                            // 连接已关闭
                            _logger.LogWarning("摆轮 {DiverterId} 连接已关闭（读取返回0字节）", DiverterId);
                            await CloseConnectionAsync();
                            return;
                        }
                        
                        bytesRead += read;
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    // 解析接收到的数据
                    ProcessReceivedFrame(buffer);
                }
                catch (OperationCanceledException)
                {
                    // 取消操作，正常退出
                    break;
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "摆轮 {DiverterId} 接收数据时IO异常", DiverterId);
                    await CloseConnectionAsync();
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "摆轮 {DiverterId} 接收循环异常", DiverterId);
                    // 继续循环，不退出
                }
            }
        }
        finally
        {
            _logger.LogInformation("摆轮 {DiverterId} 接收循环已退出", DiverterId);
        }
    }
    
    /// <summary>
    /// 处理接收到的协议帧
    /// </summary>
    private void ProcessReceivedFrame(byte[] frame)
    {
        try
        {
            // 尝试解析为设备状态上报（信息一）
            if (ShuDiNiaoProtocol.TryParseDeviceStatus(frame, out var deviceAddress, out var deviceState))
            {
                // 检查设备地址是否匹配
                if (deviceAddress == _config.DeviceAddress)
                {
                    // 更新心跳时间
#pragma warning disable MA0132 // Both sides are DateTimeOffset
                    _lastStatusReportTime = _clock.LocalNow;
#pragma warning restore MA0132
                    
                    _logger.LogDebug(
                        "[摆轮通信-接收] 摆轮 {DiverterId} 收到状态上报 | 设备地址=0x{DeviceAddress:X2} | 状态={State} | 帧={Frame}",
                        DiverterId,
                        deviceAddress,
                        deviceState,
                        ShuDiNiaoProtocol.FormatBytes(frame));
                    
                    // 更新当前状态
                    _currentStatus = deviceState.ToString();
                }
                else
                {
                    _logger.LogWarning(
                        "[摆轮通信-接收] 摆轮 {DiverterId} 收到其他设备的状态上报 | 设备地址=0x{DeviceAddress:X2}（期望=0x{Expected:X2}）",
                        DiverterId,
                        deviceAddress,
                        _config.DeviceAddress);
                }
            }
            // 可以在此处添加对其他消息类型的解析（如应答与完成，信息三）
            else
            {
                _logger.LogDebug(
                    "[摆轮通信-接收] 摆轮 {DiverterId} 收到未识别的帧 | 帧={Frame}",
                    DiverterId,
                    ShuDiNiaoProtocol.FormatBytes(frame));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[摆轮通信-接收] 摆轮 {DiverterId} 处理接收帧时异常 | 帧={Frame}",
                DiverterId,
                ShuDiNiaoProtocol.FormatBytes(frame));
        }
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
            CloseConnectionAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            _connectionLock.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "摆轮 {DiverterId} 释放资源时出现异常", DiverterId);
        }
    }
}
