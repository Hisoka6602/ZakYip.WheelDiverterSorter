using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;

/// <summary>
/// 摆轮连接管理服务实现
/// </summary>
/// <remarks>
/// 负责在系统启动时连接摆轮设备，并更新健康状态。
/// 所有后台操作通过ISafeExecutionService执行以确保异常不会导致进程崩溃。
/// </remarks>
public sealed class WheelDiverterConnectionService : IWheelDiverterConnectionService
{
    /// <summary>
    /// Ping 超时时间（毫秒）
    /// </summary>
    private const int PingTimeoutMs = 2000;

    private readonly IWheelDiverterConfigurationRepository _configRepository;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly IWheelDiverterDriverManager _driverManager;
    private readonly INodeHealthRegistry _healthRegistry;
    private readonly ISystemClock _clock;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<WheelDiverterConnectionService> _logger;

    public WheelDiverterConnectionService(
        IWheelDiverterConfigurationRepository configRepository,
        ISystemConfigurationRepository systemConfigRepository,
        IWheelDiverterDriverManager driverManager,
        INodeHealthRegistry healthRegistry,
        ISystemClock clock,
        ISafeExecutionService safeExecutor,
        ILogger<WheelDiverterConnectionService> logger)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _driverManager = driverManager ?? throw new ArgumentNullException(nameof(driverManager));
        _healthRegistry = healthRegistry ?? throw new ArgumentNullException(nameof(healthRegistry));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<WheelDiverterConnectionResult> ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("开始连接所有摆轮设备...");

                // 获取系统配置以检查启动延迟设置
                var systemConfig = _systemConfigRepository.Get();
                if (systemConfig != null && systemConfig.DriverStartupDelaySeconds > 0)
                {
                    var systemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                    var requiredDelay = TimeSpan.FromSeconds(systemConfig.DriverStartupDelaySeconds);

                    if (systemUptime < requiredDelay)
                    {
                        var remainingDelay = requiredDelay - systemUptime;
                        _logger.LogInformation(
                            "系统运行时间 {Uptime:F1}秒 小于配置的驱动启动延迟 {ConfiguredDelay}秒，将等待 {RemainingDelay:F1}秒后再连接驱动",
                            systemUptime.TotalSeconds,
                            systemConfig.DriverStartupDelaySeconds,
                            remainingDelay.TotalSeconds);

                        await Task.Delay(remainingDelay, cancellationToken);

                        _logger.LogInformation("驱动启动延迟等待完成，现在开始连接驱动");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "系统运行时间 {Uptime:F1}秒 已超过配置的驱动启动延迟 {ConfiguredDelay}秒，立即连接驱动",
                            systemUptime.TotalSeconds,
                            systemConfig.DriverStartupDelaySeconds);
                    }
                }

                // 获取摆轮配置
                var config = _configRepository.Get();
                if (config == null)
                {
                    _logger.LogWarning("摆轮配置未初始化");
                    return new WheelDiverterConnectionResult
                    {
                        IsSuccess = false,
                        ConnectedCount = 0,
                        TotalCount = 0,
                        FailedDriverIds = Array.Empty<string>(),
                        ErrorMessage = "摆轮配置未初始化"
                    };
                }

                // 检查配置是否为数递鸟类型
                if (config.VendorType != Core.Enums.Hardware.WheelDiverterVendorType.ShuDiNiao ||
                    config.ShuDiNiao == null)
                {
                    _logger.LogInformation("未配置数递鸟摆轮设备");
                    return new WheelDiverterConnectionResult
                    {
                        IsSuccess = true,
                        ConnectedCount = 0,
                        TotalCount = 0,
                        FailedDriverIds = Array.Empty<string>()
                    };
                }

                var devices = config.ShuDiNiao.Devices.Where(d => d.IsEnabled).ToList();
                if (devices.Count == 0)
                {
                    _logger.LogInformation("没有启用的摆轮设备");
                    return new WheelDiverterConnectionResult
                    {
                        IsSuccess = true,
                        ConnectedCount = 0,
                        TotalCount = 0,
                        FailedDriverIds = Array.Empty<string>()
                    };
                }

                _logger.LogInformation("找到 {Count} 个启用的摆轮设备", devices.Count);

                // 过滤出IP可达的设备
                var reachableDevices = new List<ShuDiNiaoDeviceEntry>();
                var unreachableDevices = new List<string>();

                foreach (var device in devices)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var isReachable = await PingHostAsync(device.Host, cancellationToken);
                    if (isReachable)
                    {
                        _logger.LogInformation("摆轮 {DiverterId} (主机={Host}) 可达", device.DiverterId, device.Host);
                        reachableDevices.Add(device);
                    }
                    else
                    {
                        _logger.LogWarning("摆轮 {DiverterId} (主机={Host}) 不可达", device.DiverterId, device.Host);
                        unreachableDevices.Add(device.DiverterId.ToString());

                        // 更新健康状态为不健康
                        UpdateHealthStatus(device.DiverterId.ToString(), false, false, "主机不可达");
                    }
                }

                if (reachableDevices.Count == 0)
                {
                    _logger.LogWarning("所有摆轮设备均不可达");
                    return new WheelDiverterConnectionResult
                    {
                        IsSuccess = false,
                        ConnectedCount = 0,
                        TotalCount = devices.Count,
                        FailedDriverIds = unreachableDevices,
                        ErrorMessage = "所有摆轮设备均不可达"
                    };
                }

                // 创建仅包含可达设备的配置
                var reachableConfig = new WheelDiverterConfiguration
                {
                    VendorType = config.VendorType,
                    ShuDiNiao = new ShuDiNiaoWheelDiverterConfig
                    {
                        Mode = config.ShuDiNiao.Mode,
                        Devices = reachableDevices
                    },
                    Version = config.Version,
                    CreatedAt = config.CreatedAt,
                    UpdatedAt = config.UpdatedAt
                };

                // 应用配置（会自动尝试连接）
                var applyResult = await _driverManager.ApplyConfigurationAsync(reachableConfig, cancellationToken);

                // 更新所有设备的健康状态
                var activeDrivers = _driverManager.GetActiveDrivers();
                reachableDevices.Select(device =>
                {
                    var diverterId = device.DiverterId.ToString();
                    var isConnected = activeDrivers.ContainsKey(diverterId);
                    UpdateHealthStatus(diverterId, isConnected, isConnected,
                        isConnected ? "已连接" : "连接失败");
                    return device;
                }).ToList();

                var allFailedDriverIds = unreachableDevices.Concat(applyResult.FailedDriverIds).ToList();
                var isSuccess = allFailedDriverIds.Count == 0;

                _logger.LogInformation(
                    "摆轮连接完成: 成功={ConnectedCount}/{TotalCount}, 失败={FailedCount}",
                    applyResult.ConnectedCount, devices.Count, allFailedDriverIds.Count);

                return new WheelDiverterConnectionResult
                {
                    IsSuccess = isSuccess,
                    ConnectedCount = applyResult.ConnectedCount,
                    TotalCount = devices.Count,
                    FailedDriverIds = allFailedDriverIds,
                    ErrorMessage = isSuccess ? null : $"部分摆轮连接失败: {string.Join(", ", allFailedDriverIds)}"
                };
            },
            operationName: "ConnectAllWheelDiverters",
            defaultValue: new WheelDiverterConnectionResult
            {
                IsSuccess = false,
                ConnectedCount = 0,
                TotalCount = 0,
                FailedDriverIds = Array.Empty<string>(),
                ErrorMessage = "连接摆轮时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<WheelDiverterOperationResult> RunAllAsync(CancellationToken cancellationToken = default)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("开始启动所有已连接的摆轮...");

                var activeDrivers = _driverManager.GetActiveDrivers();
                if (activeDrivers.Count == 0)
                {
                    _logger.LogWarning("没有已连接的摆轮");
                    return new WheelDiverterOperationResult
                    {
                        IsSuccess = true,
                        SuccessCount = 0,
                        TotalCount = 0,
                        FailedDriverIds = Array.Empty<string>()
                    };
                }

                var successCount = 0;
                var failedDriverIds = new List<string>();

                foreach (var kvp in activeDrivers.OrderByDescending(o => o.Value.DiverterId))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var success = await kvp.Value.RunAsync(cancellationToken);
                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation("摆轮 {DiverterId} 启动成功", kvp.Key);
                            UpdateHealthStatus(kvp.Key, true, true, "运行中");
                        }
                        else
                        {
                            failedDriverIds.Add(kvp.Key);
                            _logger.LogWarning("摆轮 {DiverterId} 启动失败", kvp.Key);
                            UpdateHealthStatus(kvp.Key, false, true, "启动命令失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedDriverIds.Add(kvp.Key);
                        _logger.LogError(ex, "摆轮 {DiverterId} 启动异常", kvp.Key);
                        UpdateHealthStatus(kvp.Key, false, true, $"启动异常: {ex.Message}");
                    }
                    finally
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                }

                var isSuccess = failedDriverIds.Count == 0;
                _logger.LogInformation(
                    "摆轮启动完成: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                    successCount, activeDrivers.Count, failedDriverIds.Count);

                return new WheelDiverterOperationResult
                {
                    IsSuccess = isSuccess,
                    SuccessCount = successCount,
                    TotalCount = activeDrivers.Count,
                    FailedDriverIds = failedDriverIds,
                    ErrorMessage = isSuccess ? null : $"部分摆轮启动失败: {string.Join(", ", failedDriverIds)}"
                };
            },
            operationName: "RunAllWheelDiverters",
            defaultValue: new WheelDiverterOperationResult
            {
                IsSuccess = false,
                SuccessCount = 0,
                TotalCount = 0,
                FailedDriverIds = Array.Empty<string>(),
                ErrorMessage = "启动摆轮时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<WheelDiverterOperationResult> StopAllAsync(CancellationToken cancellationToken = default)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("开始停止所有摆轮...");

                var activeDrivers = _driverManager.GetActiveDrivers();
                if (activeDrivers.Count == 0)
                {
                    _logger.LogWarning("没有已连接的摆轮");
                    return new WheelDiverterOperationResult
                    {
                        IsSuccess = true,
                        SuccessCount = 0,
                        TotalCount = 0,
                        FailedDriverIds = Array.Empty<string>()
                    };
                }

                var successCount = 0;
                var failedDriverIds = new List<string>();

                foreach (var kvp in activeDrivers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var success = await kvp.Value.StopAsync(cancellationToken);
                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation("摆轮 {DiverterId} 停止成功", kvp.Key);
                            UpdateHealthStatus(kvp.Key, true, true, "已停止");
                        }
                        else
                        {
                            failedDriverIds.Add(kvp.Key);
                            _logger.LogWarning("摆轮 {DiverterId} 停止失败", kvp.Key);
                            UpdateHealthStatus(kvp.Key, false, true, "停止命令失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedDriverIds.Add(kvp.Key);
                        _logger.LogError(ex, "摆轮 {DiverterId} 停止异常", kvp.Key);
                        UpdateHealthStatus(kvp.Key, false, true, $"停止异常: {ex.Message}");
                    }
                }

                var isSuccess = failedDriverIds.Count == 0;
                _logger.LogInformation(
                    "摆轮停止完成: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                    successCount, activeDrivers.Count, failedDriverIds.Count);

                return new WheelDiverterOperationResult
                {
                    IsSuccess = isSuccess,
                    SuccessCount = successCount,
                    TotalCount = activeDrivers.Count,
                    FailedDriverIds = failedDriverIds,
                    ErrorMessage = isSuccess ? null : $"部分摆轮停止失败: {string.Join(", ", failedDriverIds)}"
                };
            },
            operationName: "StopAllWheelDiverters",
            defaultValue: new WheelDiverterOperationResult
            {
                IsSuccess = false,
                SuccessCount = 0,
                TotalCount = 0,
                FailedDriverIds = Array.Empty<string>(),
                ErrorMessage = "停止摆轮时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<WheelDiverterOperationResult> PassThroughAllAsync(CancellationToken cancellationToken = default)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("开始让所有摆轮向前（直通）...");

                var activeDrivers = _driverManager.GetActiveDrivers();
                if (activeDrivers.Count == 0)
                {
                    _logger.LogWarning("没有已连接的摆轮");
                    return new WheelDiverterOperationResult
                    {
                        IsSuccess = true,
                        SuccessCount = 0,
                        TotalCount = 0,
                        FailedDriverIds = Array.Empty<string>()
                    };
                }

                var successCount = 0;
                var failedDriverIds = new List<string>();

                foreach (var kvp in activeDrivers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var success = await kvp.Value.PassThroughAsync(cancellationToken);
                        if (success)
                        {
                            successCount++;
                            _logger.LogDebug("摆轮 {DiverterId} 设置为向前成功", kvp.Key);
                            UpdateHealthStatus(kvp.Key, true, true, "向前（直通）");
                        }
                        else
                        {
                            failedDriverIds.Add(kvp.Key);
                            _logger.LogWarning("摆轮 {DiverterId} 设置为向前失败", kvp.Key);
                            UpdateHealthStatus(kvp.Key, false, true, "向前命令失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedDriverIds.Add(kvp.Key);
                        _logger.LogError(ex, "摆轮 {DiverterId} 设置为向前异常", kvp.Key);
                        UpdateHealthStatus(kvp.Key, false, true, $"向前异常: {ex.Message}");
                    }
                }

                var isSuccess = failedDriverIds.Count == 0;
                _logger.LogInformation(
                    "摆轮向前设置完成: 成功={SuccessCount}/{TotalCount}, 失败={FailedCount}",
                    successCount, activeDrivers.Count, failedDriverIds.Count);

                return new WheelDiverterOperationResult
                {
                    IsSuccess = isSuccess,
                    SuccessCount = successCount,
                    TotalCount = activeDrivers.Count,
                    FailedDriverIds = failedDriverIds,
                    ErrorMessage = isSuccess ? null : $"部分摆轮设置向前失败: {string.Join(", ", failedDriverIds)}"
                };
            },
            operationName: "PassThroughAllWheelDiverters",
            defaultValue: new WheelDiverterOperationResult
            {
                IsSuccess = false,
                SuccessCount = 0,
                TotalCount = 0,
                FailedDriverIds = Array.Empty<string>(),
                ErrorMessage = "设置摆轮向前时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WheelDiverterHealthInfo>> GetHealthStatusesAsync()
    {
        return await _safeExecutor.ExecuteAsync<IReadOnlyList<WheelDiverterHealthInfo>>(
            async () =>
            {
                await Task.Yield();

                var activeDrivers = _driverManager.GetActiveDrivers();
                var healthInfos = new List<WheelDiverterHealthInfo>();

                foreach (var kvp in activeDrivers)
                {
                    var diverterId = kvp.Key;
                    var driver = kvp.Value;

                    var nodeHealth = _healthRegistry.GetNodeHealth(long.Parse(diverterId));
                    var status = await driver.GetStatusAsync();

                    healthInfos.Add(new WheelDiverterHealthInfo
                    {
                        DiverterId = diverterId,
                        IsHealthy = nodeHealth?.IsHealthy ?? true,
                        IsConnected = true,
                        Status = status,
                        ErrorMessage = nodeHealth?.ErrorMessage,
                        LastUpdated = nodeHealth?.CheckedAt ?? new DateTimeOffset(_clock.LocalNow)
                    });
                }

                return (IReadOnlyList<WheelDiverterHealthInfo>)healthInfos;
            },
            operationName: "GetWheelDiverterHealthStatuses",
            defaultValue: Array.Empty<WheelDiverterHealthInfo>(),
            cancellationToken: default
        );
    }

    /// <summary>
    /// Ping主机检查是否可达
    /// </summary>
    private async Task<bool> PingHostAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, PingTimeoutMs);
            var isReachable = reply.Status == IPStatus.Success;

            if (isReachable)
            {
                _logger.LogDebug("主机 {Host} 可达，往返时间={RoundtripTime}ms", host, reply.RoundtripTime);
            }
            else
            {
                _logger.LogDebug("主机 {Host} 不可达，状态={Status}", host, reply.Status);
            }

            return isReachable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ping主机 {Host} 失败", host);
            return false;
        }
    }

    /// <summary>
    /// 更新摆轮健康状态到NodeHealthRegistry
    /// </summary>
    private void UpdateHealthStatus(string diverterId, bool isHealthy, bool isConnected, string statusMessage)
    {
        if (!long.TryParse(diverterId, out var nodeId))
        {
            _logger.LogWarning("无法将摆轮ID {DiverterId} 转换为数字", diverterId);
            return;
        }

        var healthStatus = new NodeHealthStatus
        {
            NodeId = nodeId,
            IsHealthy = isHealthy,
            ErrorCode = isHealthy ? null : "WHEEL_DIVERTER_UNHEALTHY",
            ErrorMessage = isHealthy ? null : statusMessage,
            NodeType = "摆轮",
            CheckedAt = new DateTimeOffset(_clock.LocalNow)
        };

        _healthRegistry.UpdateNodeHealth(healthStatus);

        _logger.LogDebug(
            "更新摆轮 {DiverterId} 健康状态: 健康={IsHealthy}, 已连接={IsConnected}, 状态={Status}",
            diverterId, isHealthy, isConnected, statusMessage);
    }
}
