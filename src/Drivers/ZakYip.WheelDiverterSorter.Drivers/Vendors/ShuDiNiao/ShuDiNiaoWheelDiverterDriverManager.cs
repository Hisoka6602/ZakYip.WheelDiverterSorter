using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮驱动管理器
/// </summary>
/// <remarks>
/// 管理数递鸟摆轮驱动器的生命周期，支持热更新配置时的连接/断连操作。
/// 实现 IWheelDiverterDriverManager 接口，提供运行时驱动器管理能力。
/// </remarks>
public sealed class ShuDiNiaoWheelDiverterDriverManager : IWheelDiverterDriverManager, IDisposable
{
    private readonly ILogger<ShuDiNiaoWheelDiverterDriverManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly object _lock = new();
    private Dictionary<string, ShuDiNiaoWheelDiverterDriver> _drivers = new();
    private bool _disposed;

    public ShuDiNiaoWheelDiverterDriverManager(
        ILoggerFactory loggerFactory,
        ILogger<ShuDiNiaoWheelDiverterDriverManager> logger)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IWheelDiverterDriver> GetActiveDrivers()
    {
        lock (_lock)
        {
            return _drivers.ToDictionary(
                kvp => kvp.Key,
                kvp => (IWheelDiverterDriver)kvp.Value);
        }
    }

    /// <inheritdoc/>
    public IWheelDiverterDriver? GetDriver(string diverterId)
    {
        lock (_lock)
        {
            return _drivers.TryGetValue(diverterId, out var driver) ? driver : null;
        }
    }

    /// <inheritdoc/>
    public async Task<WheelDiverterConfigApplyResult> ApplyConfigurationAsync(
        WheelDiverterConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        _logger.LogInformation(
            "开始应用摆轮配置热更新，厂商类型={VendorType}",
            configuration.VendorType);

        try
        {
            // 1. 断开所有现有连接
            await DisconnectAllAsync(cancellationToken);

            // 2. 检查是否是数递鸟配置
            if (configuration.VendorType != WheelDiverterVendorType.ShuDiNiao ||
                configuration.ShuDiNiao == null)
            {
                _logger.LogInformation("配置不是数递鸟厂商类型或无数递鸟配置，清空驱动器列表");
                lock (_lock)
                {
                    _drivers.Clear();
                }
                return new WheelDiverterConfigApplyResult
                {
                    IsSuccess = true,
                    ConnectedCount = 0,
                    TotalCount = 0,
                    FailedDriverIds = Array.Empty<string>()
                };
            }

            // 3. 创建新的驱动器实例
            var newDrivers = new Dictionary<string, ShuDiNiaoWheelDiverterDriver>();
            var failedDriverIds = new List<string>();
            var enabledDevices = configuration.ShuDiNiao.Devices
                .Where(d => d.IsEnabled)
                .ToList();

            foreach (var device in enabledDevices)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var driverLogger = _loggerFactory.CreateLogger<ShuDiNiaoWheelDiverterDriver>();
                    var driver = new ShuDiNiaoWheelDiverterDriver(device, driverLogger);
                    newDrivers[device.DiverterId.ToString()] = driver;

                    _logger.LogInformation(
                        "已创建数递鸟驱动器 {DiverterId}，主机={Host}，端口={Port}，设备地址=0x{DeviceAddress:X2}",
                        device.DiverterId, device.Host, device.Port, device.DeviceAddress);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "创建数递鸟驱动器失败 {DiverterId}，主机={Host}，端口={Port}",
                        device.DiverterId, device.Host, device.Port);
                    failedDriverIds.Add(device.DiverterId.ToString());
                }
            }

            // 5. 更新驱动器字典
            lock (_lock)
            {
                _drivers = newDrivers;
            }

            var isSuccess = failedDriverIds.Count == 0;
            var connectedCount = enabledDevices.Count - failedDriverIds.Count;

            _logger.LogInformation(
                "摆轮配置热更新完成: 成功={IsSuccess}, 已连接={ConnectedCount}/{TotalCount}, 失败={FailedCount}",
                isSuccess, connectedCount, enabledDevices.Count, failedDriverIds.Count);

            return new WheelDiverterConfigApplyResult
            {
                IsSuccess = isSuccess,
                ConnectedCount = connectedCount,
                TotalCount = enabledDevices.Count,
                FailedDriverIds = failedDriverIds,
                ErrorMessage = isSuccess ? null : $"部分驱动器创建失败: {string.Join(", ", failedDriverIds)}"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("摆轮配置热更新被取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮配置热更新失败");
            return new WheelDiverterConfigApplyResult
            {
                IsSuccess = false,
                ConnectedCount = 0,
                TotalCount = 0,
                FailedDriverIds = Array.Empty<string>(),
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, ShuDiNiaoWheelDiverterDriver> driversToDispose;
        
        lock (_lock)
        {
            driversToDispose = new Dictionary<string, ShuDiNiaoWheelDiverterDriver>(_drivers);
        }

        if (driversToDispose.Count == 0)
        {
            return;
        }

        _logger.LogInformation("开始断开所有数递鸟驱动器连接，数量={Count}", driversToDispose.Count);

        var tasks = driversToDispose.Select(async kvp =>
        {
            try
            {
                // 调用停止命令，然后释放资源
                await kvp.Value.StopAsync(cancellationToken);
                kvp.Value.Dispose();
                _logger.LogDebug("已断开并释放数递鸟驱动器 {DiverterId}", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "断开数递鸟驱动器时出现异常 {DiverterId}", kvp.Key);
            }
        });

        await Task.WhenAll(tasks);

        lock (_lock)
        {
            _drivers.Clear();
        }

        _logger.LogInformation("所有数递鸟驱动器连接已断开");
    }

    /// <inheritdoc/>
    public async Task<WheelDiverterReconnectResult> ReconnectAllAsync(CancellationToken cancellationToken = default)
    {
        // 对于数递鸟驱动器，重连是惰性的（在下次发送命令时自动重连）
        // 这里我们只记录日志并返回当前状态

        Dictionary<string, ShuDiNiaoWheelDiverterDriver> currentDrivers;
        lock (_lock)
        {
            currentDrivers = new Dictionary<string, ShuDiNiaoWheelDiverterDriver>(_drivers);
        }

        _logger.LogInformation("触发数递鸟驱动器重连检查，驱动器数量={Count}", currentDrivers.Count);

        var failedDriverIds = new List<string>();

        foreach (var kvp in currentDrivers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 尝试获取状态（会触发连接）
                var status = await kvp.Value.GetStatusAsync();
                _logger.LogDebug("驱动器 {DiverterId} 状态: {Status}", kvp.Key, status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "驱动器 {DiverterId} 重连检查失败", kvp.Key);
                failedDriverIds.Add(kvp.Key);
            }
        }

        return new WheelDiverterReconnectResult
        {
            IsSuccess = failedDriverIds.Count == 0,
            ReconnectedCount = currentDrivers.Count - failedDriverIds.Count,
            TotalCount = currentDrivers.Count,
            FailedDriverIds = failedDriverIds
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

        // 同步释放驱动器资源，避免使用 GetAwaiter().GetResult() 可能导致的死锁
        Dictionary<string, ShuDiNiaoWheelDiverterDriver> driversToDispose;
        lock (_lock)
        {
            driversToDispose = new Dictionary<string, ShuDiNiaoWheelDiverterDriver>(_drivers);
            _drivers.Clear();
        }

        foreach (var kvp in driversToDispose)
        {
            try
            {
                kvp.Value.Dispose();
                _logger.LogDebug("已释放数递鸟驱动器 {DiverterId}", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放数递鸟驱动器时出现异常 {DiverterId}", kvp.Key);
            }
        }
    }
}
