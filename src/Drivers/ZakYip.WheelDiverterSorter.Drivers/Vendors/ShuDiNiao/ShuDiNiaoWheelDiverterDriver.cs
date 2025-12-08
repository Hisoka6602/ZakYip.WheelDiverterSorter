using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮驱动器实现
/// </summary>
/// <remarks>
/// 通过TCP协议与数递鸟摆轮设备通信，实现摆轮控制功能。
/// - 支持运行/停止控制
/// - 支持左摆/右摆/回中方向控制
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
    
    private readonly ILogger<ShuDiNiaoWheelDiverterDriver> _logger;
    private readonly ShuDiNiaoDeviceEntry _config;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;
    private string _currentStatus = "未连接";
    private long _lastCommandTicks = 0;
    private string? _lastCommandSent = null;

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
    public ShuDiNiaoWheelDiverterDriver(
        ShuDiNiaoDeviceEntry config,
        ILogger<ShuDiNiaoWheelDiverterDriver> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        // TODO: 实现数递鸟协议心跳检测
        // 根据 README.md，数递鸟设备每 1 秒主动发送状态上报（信息一，消息类型 0x51）
        // 应该：
        // 1. 启动后台线程监听并解析设备状态上报帧
        // 2. 记录最后接收到状态上报的时间戳
        // 3. 在此方法中检查距离最后接收时间是否超过 5 秒
        // 
        // 当前临时实现：检查 TCP 连接状态
        // 注意：TCP连接状态检查无法检测到设备假死（连接未断但设备不响应）的情况
        var isConnected = _tcpClient?.Connected == true && _stream != null;
        
        if (isConnected)
        {
            _logger.LogDebug("摆轮 {DiverterId} 心跳检查: TCP连接正常", DiverterId);
        }
        else
        {
            _logger.LogDebug("摆轮 {DiverterId} 心跳检查: TCP连接断开", DiverterId);
        }
        
        return Task.FromResult(isConnected);
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
            if (_stream != null)
            {
                await _stream.DisposeAsync();
                _stream = null;
            }

            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;

            _currentStatus = "未连接";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "摆轮 {DiverterId} 关闭连接时出现异常", DiverterId);
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
