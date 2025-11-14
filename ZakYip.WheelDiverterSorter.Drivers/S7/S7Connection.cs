using Microsoft.Extensions.Logging;
using S7.Net;
using System.Net.Sockets;

namespace ZakYip.WheelDiverterSorter.Drivers.S7;

/// <summary>
/// S7 PLC连接管理器，负责连接管理和自动重连
/// </summary>
public class S7Connection : IDisposable
{
    private readonly ILogger<S7Connection> _logger;
    private readonly S7Options _options;
    private Plc? _plc;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// 连接是否已建立
    /// </summary>
    public bool IsConnected => _plc?.IsConnected ?? false;

    /// <summary>
    /// 初始化S7连接管理器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">S7配置选项</param>
    public S7Connection(ILogger<S7Connection> logger, S7Options options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 连接到PLC
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否连接成功</returns>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_plc?.IsConnected == true)
            {
                return true;
            }

            _logger.LogInformation("正在连接到S7 PLC: {IpAddress}, Rack: {Rack}, Slot: {Slot}, CPU: {CpuType}",
                _options.IpAddress, _options.Rack, _options.Slot, _options.CpuType);

            var cpuType = ConvertCpuType(_options.CpuType);
            _plc = new Plc(cpuType, _options.IpAddress, _options.Rack, _options.Slot);

            // S7.Net doesn't support async connect, so we use Task.Run
            await Task.Run(() => _plc.Open(), cancellationToken);

            if (_plc.IsConnected)
            {
                _logger.LogInformation("成功连接到S7 PLC: {IpAddress}", _options.IpAddress);
                return true;
            }

            _logger.LogWarning("无法连接到S7 PLC: {IpAddress}", _options.IpAddress);
            return false;
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "连接S7 PLC时发生网络错误: {IpAddress}", _options.IpAddress);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接S7 PLC时发生错误: {IpAddress}", _options.IpAddress);
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// 确保连接已建立，如果未连接则尝试重连
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否连接成功</returns>
    public async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return true;
        }

        for (int attempt = 1; attempt <= _options.MaxReconnectAttempts; attempt++)
        {
            _logger.LogInformation("尝试重连到S7 PLC (第 {Attempt}/{MaxAttempts} 次): {IpAddress}",
                attempt, _options.MaxReconnectAttempts, _options.IpAddress);

            if (await ConnectAsync(cancellationToken))
            {
                return true;
            }

            if (attempt < _options.MaxReconnectAttempts)
            {
                await Task.Delay(_options.ReconnectDelay, cancellationToken);
            }
        }

        _logger.LogError("无法重连到S7 PLC，已达到最大重连次数: {IpAddress}", _options.IpAddress);
        return false;
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        _connectionLock.Wait();
        try
        {
            if (_plc?.IsConnected == true)
            {
                _plc.Close();
                _logger.LogInformation("已断开与S7 PLC的连接: {IpAddress}", _options.IpAddress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开S7 PLC连接时发生错误: {IpAddress}", _options.IpAddress);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// 获取PLC实例
    /// </summary>
    /// <returns>PLC实例，如果未连接则返回null</returns>
    public Plc? GetPlc()
    {
        return _plc;
    }

    /// <summary>
    /// 转换CPU类型
    /// </summary>
    private static CpuType ConvertCpuType(S7CpuType cpuType)
    {
        return cpuType switch
        {
            S7CpuType.S71200 => CpuType.S71200,
            S7CpuType.S71500 => CpuType.S71500,
            S7CpuType.S7300 => CpuType.S7300,
            S7CpuType.S7400 => CpuType.S7400,
            _ => CpuType.S71200
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

        Disconnect();
        _connectionLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
