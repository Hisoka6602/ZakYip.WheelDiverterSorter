using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using S7.Net;
using System.Diagnostics;
using System.Net.Sockets;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;

/// <summary>
/// S7 PLC连接管理器，负责连接管理和自动重连
/// </summary>
public class S7Connection : IDisposable
{
    private readonly ILogger<S7Connection> _logger;
    private readonly IOptionsMonitor<S7Options> _optionsMonitor;
    private S7Options _options;
    private Plc? _plc;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;
    private Timer? _healthCheckTimer;
    private readonly S7ConnectionHealth _health = new();
    private readonly S7PerformanceMetrics _metrics = new();

    /// <summary>
    /// 连接是否已建立
    /// </summary>
    public bool IsConnected => _plc?.IsConnected ?? false;

    /// <summary>
    /// 初始化S7连接管理器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="optionsMonitor">S7配置选项监视器（支持热更新）</param>
    public S7Connection(ILogger<S7Connection> logger, IOptionsMonitor<S7Options> optionsMonitor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _options = _optionsMonitor.CurrentValue;

        // 监听配置变更
        _optionsMonitor.OnChange(OnOptionsChanged);

        // 初始化健康检查定时器
        if (_options.EnableHealthCheck)
        {
            _healthCheckTimer = new Timer(
                PerformHealthCheckAsync,
                null,
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds),
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));
        }
    }

    /// <summary>
    /// 配置变更时的回调
    /// </summary>
    private void OnOptionsChanged(S7Options newOptions)
    {
        _logger.LogInformation("检测到S7配置变更，将重新连接PLC");
        _options = newOptions;

        // 断开当前连接
        Disconnect();

        // 尝试使用新配置重新连接
        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置变更后重新连接失败");
            }
        });

        // 更新健康检查定时器
        if (_options.EnableHealthCheck && _healthCheckTimer == null)
        {
            _healthCheckTimer = new Timer(
                PerformHealthCheckAsync,
                null,
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds),
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));
        }
        else if (!_options.EnableHealthCheck && _healthCheckTimer != null)
        {
            _healthCheckTimer.Dispose();
            _healthCheckTimer = null;
        }
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    private async void PerformHealthCheckAsync(object? state)
    {
        try
        {
            if (!IsConnected)
            {
                _health.IsConnected = false;
                _health.ConsecutiveFailures++;
                
                if (_health.ConsecutiveFailures >= _options.FailureThreshold)
                {
                    _logger.LogWarning("连接健康检查失败次数达到阈值，尝试重连");
                    await EnsureConnectedAsync();
                }
                return;
            }

            // 尝试读取一个测试位（DB1.DBX0.0）
            var stopwatch = Stopwatch.StartNew();
            await ReadBitAsync("DB1", 0, 0);
            stopwatch.Stop();

            _health.LastSuccessfulRead = DateTime.UtcNow;
            _health.ConsecutiveFailures = 0;
            _health.IsConnected = true;
            _health.AverageReadTime = stopwatch.Elapsed;

            _logger.LogTrace("健康检查成功，读取时间: {ReadTime}ms", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _health.ConsecutiveFailures++;
            _health.IsConnected = false;
            _logger.LogWarning(ex, "健康检查失败 (连续失败次数: {FailureCount})", _health.ConsecutiveFailures);

            if (_health.ConsecutiveFailures >= _options.FailureThreshold)
            {
                _logger.LogError("连接健康检查失败次数达到阈值，尝试重连");
                await EnsureConnectedAsync();
            }
        }
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
    public virtual async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
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
    public virtual Plc? GetPlc()
    {
        return _plc;
    }

    /// <summary>
    /// 读取单个位
    /// </summary>
    /// <param name="dbNumber">DB块号(如"DB1")</param>
    /// <param name="byteAddress">字节地址</param>
    /// <param name="bitAddress">位地址(0-7)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>位值</returns>
    public async Task<bool> ReadBitAsync(
        string dbNumber,
        int byteAddress,
        int bitAddress,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("PLC未连接");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var address = $"{dbNumber}.DBX{byteAddress}.{bitAddress}";
            var result = await Task.Run(() => _plc!.Read(address), cancellationToken);
            
            stopwatch.Stop();
            
            // 记录性能指标
            if (_options.EnablePerformanceMetrics)
            {
                _metrics.TotalReads++;
                _metrics.TotalReadTime += stopwatch.Elapsed;
                _health.LastSuccessfulRead = DateTime.UtcNow;
            }
            
            return result != null && (bool)result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // 记录失败指标
            if (_options.EnablePerformanceMetrics)
            {
                _metrics.FailedReads++;
            }
            
            _logger.LogError(ex, "读取PLC位失败: {DbNumber}.DBX{Byte}.{Bit}",
                dbNumber, byteAddress, bitAddress);
            throw;
        }
    }

    /// <summary>
    /// 写入单个位
    /// </summary>
    public async Task WriteBitAsync(
        string dbNumber,
        int byteAddress,
        int bitAddress,
        bool value,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("PLC未连接");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var address = $"{dbNumber}.DBX{byteAddress}.{bitAddress}";
            await Task.Run(() => _plc!.Write(address, value), cancellationToken);
            
            stopwatch.Stop();
            
            // 记录性能指标
            if (_options.EnablePerformanceMetrics)
            {
                _metrics.TotalWrites++;
                _metrics.TotalWriteTime += stopwatch.Elapsed;
                _health.LastSuccessfulWrite = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // 记录失败指标
            if (_options.EnablePerformanceMetrics)
            {
                _metrics.FailedWrites++;
            }
            
            _logger.LogError(ex, "写入PLC位失败: {DbNumber}.DBX{Byte}.{Bit} = {Value}",
                dbNumber, byteAddress, bitAddress, value);
            throw;
        }
    }

    /// <summary>
    /// 读取单个字节
    /// </summary>
    public async Task<byte> ReadByteAsync(
        string dbNumber,
        int byteAddress,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("PLC未连接");
        }

        try
        {
            var address = $"{dbNumber}.DBB{byteAddress}";
            var result = await Task.Run(() => _plc!.Read(address), cancellationToken);
            return result != null ? (byte)result : (byte)0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取PLC字节失败: {DbNumber}.DBB{Byte}",
                dbNumber, byteAddress);
            throw;
        }
    }

    /// <summary>
    /// 写入单个字节
    /// </summary>
    public async Task WriteByteAsync(
        string dbNumber,
        int byteAddress,
        byte value,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("PLC未连接");
        }

        try
        {
            var address = $"{dbNumber}.DBB{byteAddress}";
            await Task.Run(() => _plc!.Write(address, value), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入PLC字节失败: {DbNumber}.DBB{Byte} = {Value}",
                dbNumber, byteAddress, value);
            throw;
        }
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
    /// 获取连接健康状态
    /// </summary>
    /// <returns>连接健康状态</returns>
    public S7ConnectionHealth GetHealth()
    {
        return new S7ConnectionHealth
        {
            IsConnected = _health.IsConnected,
            LastSuccessfulRead = _health.LastSuccessfulRead,
            LastSuccessfulWrite = _health.LastSuccessfulWrite,
            ConsecutiveFailures = _health.ConsecutiveFailures,
            AverageReadTime = _health.AverageReadTime
        };
    }

    /// <summary>
    /// 获取性能指标
    /// </summary>
    /// <returns>性能指标</returns>
    public S7PerformanceMetrics GetMetrics()
    {
        return new S7PerformanceMetrics
        {
            TotalReads = _metrics.TotalReads,
            TotalWrites = _metrics.TotalWrites,
            FailedReads = _metrics.FailedReads,
            FailedWrites = _metrics.FailedWrites,
            TotalReadTime = _metrics.TotalReadTime,
            TotalWriteTime = _metrics.TotalWriteTime
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

        _healthCheckTimer?.Dispose();
        Disconnect();
        _connectionLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// S7连接健康状态
/// </summary>
public class S7ConnectionHealth
{
    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// 最后一次成功读取时间
    /// </summary>
    public DateTime LastSuccessfulRead { get; set; }

    /// <summary>
    /// 最后一次成功写入时间
    /// </summary>
    public DateTime LastSuccessfulWrite { get; set; }

    /// <summary>
    /// 连续失败次数
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// 平均读取时间
    /// </summary>
    public TimeSpan AverageReadTime { get; set; }
}

/// <summary>
/// S7性能指标
/// </summary>
public class S7PerformanceMetrics
{
    /// <summary>
    /// 总读取次数
    /// </summary>
    public long TotalReads { get; set; }

    /// <summary>
    /// 总写入次数
    /// </summary>
    public long TotalWrites { get; set; }

    /// <summary>
    /// 失败读取次数
    /// </summary>
    public long FailedReads { get; set; }

    /// <summary>
    /// 失败写入次数
    /// </summary>
    public long FailedWrites { get; set; }

    /// <summary>
    /// 总读取时间
    /// </summary>
    public TimeSpan TotalReadTime { get; set; }

    /// <summary>
    /// 总写入时间
    /// </summary>
    public TimeSpan TotalWriteTime { get; set; }

    /// <summary>
    /// 平均读取时间（毫秒）
    /// </summary>
    public double AverageReadTimeMs =>
        TotalReads > 0 ? TotalReadTime.TotalMilliseconds / TotalReads : 0;

    /// <summary>
    /// 平均写入时间（毫秒）
    /// </summary>
    public double AverageWriteTimeMs =>
        TotalWrites > 0 ? TotalWriteTime.TotalMilliseconds / TotalWrites : 0;

    /// <summary>
    /// 读取成功率（百分比）
    /// </summary>
    public double ReadSuccessRate =>
        TotalReads > 0 ? (TotalReads - FailedReads) * 100.0 / TotalReads : 100;

    /// <summary>
    /// 写入成功率（百分比）
    /// </summary>
    public double WriteSuccessRate =>
        TotalWrites > 0 ? (TotalWrites - FailedWrites) * 100.0 / TotalWrites : 100;
}
