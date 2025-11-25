using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Modi;

/// <summary>
/// 莫迪摆轮驱动器实现
/// </summary>
/// <remarks>
/// 通过TCP协议与莫迪摆轮设备通信，实现摆轮控制功能。
/// - 支持运行/停止控制
/// - 支持左摆/右摆/回中方向控制
/// - 自动处理连接失败和通信异常
/// - 所有异常不向外冒泡，通过日志记录和返回值反馈
/// </remarks>
public sealed class ModiWheelDiverterDriver : IWheelDiverterDriver, IDisposable
{
    private readonly ILogger<ModiWheelDiverterDriver> _logger;
    private readonly ModiDeviceEntry _config;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;
    private string _currentStatus = "未连接";

    /// <inheritdoc/>
    public string DiverterId => _config.DiverterId;

    /// <summary>
    /// 初始化莫迪摆轮驱动器
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="logger">日志记录器</param>
    public ModiWheelDiverterDriver(
        ModiDeviceEntry config,
        ILogger<ModiWheelDiverterDriver> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation(
            "已初始化莫迪摆轮驱动器 {DiverterId}，主机={Host}，端口={Port}，设备编号={DeviceId}",
            DiverterId, _config.Host, _config.Port, _config.DeviceId);
    }

    /// <inheritdoc/>
    public async Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行左转", DiverterId);
            
            var result = await SendCommandAsync(ModiControlCommand.TurnLeft, cancellationToken);
            
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
            
            var result = await SendCommandAsync(ModiControlCommand.TurnRight, cancellationToken);
            
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
            
            var result = await SendCommandAsync(ModiControlCommand.ReturnCenter, cancellationToken);
            
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
            
            var result = await SendCommandAsync(ModiControlCommand.Stop, cancellationToken);
            
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
    public Task<string> GetStatusAsync()
    {
        return Task.FromResult(_currentStatus);
    }

    /// <summary>
    /// 发送控制命令到设备
    /// </summary>
    private async Task<bool> SendCommandAsync(
        ModiControlCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // 确保连接建立
            if (!await EnsureConnectedAsync(cancellationToken))
            {
                _logger.LogWarning("摆轮 {DiverterId} 未连接，无法发送命令 {Command}",
                    DiverterId, command);
                return false;
            }

            // 构造命令帧
            var frame = ModiProtocol.BuildCommandFrame(_config.DeviceId, command);
            
            _logger.LogDebug("摆轮 {DiverterId} 发送命令: {Frame}",
                DiverterId, ModiProtocol.FormatBytes(frame));

            // 发送命令
            if (_stream != null)
            {
                await _stream.WriteAsync(frame, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
                return true;
            }

            return false;
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 发送命令失败（网络异常）", DiverterId);
            await CloseConnectionAsync();
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 发送命令失败（IO异常）", DiverterId);
            await CloseConnectionAsync();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 发送命令失败（未知异常）", DiverterId);
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
