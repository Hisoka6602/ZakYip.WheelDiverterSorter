using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 摆轮心跳监控后台服务
/// </summary>
/// <remarks>
/// 定期检查所有摆轮的连接状态和心跳。
/// 当检测到摆轮心跳异常时：
/// 1. 更新NodeHealthRegistry中的健康状态
/// 2. (TD-071已移除告警功能 - 由IO联动替代)
/// </remarks>
public sealed class WheelDiverterHeartbeatMonitor : BackgroundService
{
    private readonly IWheelDiverterDriverManager _driverManager;
    private readonly IWheelDiverterConfigurationRepository _configRepository;
    private readonly INodeHealthRegistry _healthRegistry;
    private readonly ISystemClock _clock;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<WheelDiverterHeartbeatMonitor> _logger;
    
    private static readonly TimeSpan HeartbeatCheckInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AlarmDuration = TimeSpan.FromSeconds(3);
    
    /// <summary>
    /// Ping 超时时间（毫秒）
    /// </summary>
    private const int PingTimeoutMs = 2000;
    
    /// <summary>
    /// 初始化延迟时间（等待系统初始化）
    /// </summary>
    private static readonly TimeSpan InitializationDelay = TimeSpan.FromSeconds(10);
    
    private readonly Dictionary<string, DateTimeOffset> _lastSuccessfulCheck = new();
    private readonly Dictionary<string, bool> _lastHealthStatus = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastLogTime = new();
    
    /// <summary>
    /// 日志输出最小间隔（秒）- 防止日志洪水
    /// </summary>
    private static readonly TimeSpan MinLogInterval = TimeSpan.FromSeconds(60);

    public WheelDiverterHeartbeatMonitor(
        IWheelDiverterDriverManager driverManager,
        IWheelDiverterConfigurationRepository configRepository,
        INodeHealthRegistry healthRegistry,
        ISystemClock clock,
        ISafeExecutionService safeExecutor,
        ILogger<WheelDiverterHeartbeatMonitor> logger)
    {
        _driverManager = driverManager ?? throw new ArgumentNullException(nameof(driverManager));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _healthRegistry = healthRegistry ?? throw new ArgumentNullException(nameof(healthRegistry));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("========== 摆轮心跳监控服务已启动 ==========");
        
        // 等待一小段时间让系统初始化完成
        await Task.Delay(InitializationDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _safeExecutor.ExecuteAsync(
                async () =>
                {
                    await CheckAllWheelDivertersHeartbeatAsync(stoppingToken);
                    await Task.Delay(HeartbeatCheckInterval, stoppingToken);
                },
                operationName: "WheelDiverterHeartbeatCheck",
                cancellationToken: stoppingToken
            );
        }

        _logger.LogInformation("摆轮心跳监控服务已停止");
    }

    /// <summary>
    /// 检查所有摆轮的心跳状态
    /// </summary>
    private async Task CheckAllWheelDivertersHeartbeatAsync(CancellationToken cancellationToken)
    {
        var activeDrivers = _driverManager.GetActiveDrivers();
        if (activeDrivers.Count == 0)
        {
            // 没有活跃的摆轮驱动器
            return;
        }

        var now = new DateTimeOffset(_clock.LocalNow);

        foreach (var kvp in activeDrivers)
        {
            var diverterId = kvp.Key;
            var driver = kvp.Value;

            try
            {
                bool isAlive;
                
                // TD-071: IHeartbeatCapable 接口已移除，统一使用Ping检查
                var host = GetHostForDiverter(diverterId);
                if (!string.IsNullOrEmpty(host))
                {
                    isAlive = await PingHostAsync(host, cancellationToken);
                    
                    // 只在状态转换时记录日志
                    var wasHealthy = _lastHealthStatus.GetValueOrDefault(diverterId, true);
                    if (wasHealthy != isAlive)
                    {
                        if (isAlive)
                        {
                            _logger.LogInformation("摆轮 {DiverterId} 使用Ping检查 (主机={Host})，结果=可达", 
                                diverterId, host);
                        }
                        else
                        {
                            _logger.LogWarning("摆轮 {DiverterId} 使用Ping检查 (主机={Host})，结果=不可达", 
                                diverterId, host);
                        }
                        _lastLogTime[diverterId] = now;
                    }
                }
                else
                {
                    // 无法获取主机信息，尝试使用GetStatusAsync作为后备
                    await driver.GetStatusAsync();
                    isAlive = true;
                    // 心跳正常不记录日志
                }
                
                if (isAlive)
                {
                    // 成功获取状态，记录最后成功时间
                    _lastSuccessfulCheck[diverterId] = now;
                    
                    // 检查之前是否不健康，如果是则记录恢复日志
                    if (_lastHealthStatus.TryGetValue(diverterId, out var wasHealthy) && !wasHealthy)
                    {
                        _logger.LogInformation("摆轮 {DiverterId} 心跳恢复正常", diverterId);
                    }
                    
                    _lastHealthStatus[diverterId] = true;
                    
                    // 更新健康状态为健康
                    var status = await driver.GetStatusAsync();
                    UpdateWheelDiverterHealth(diverterId, true, "心跳正常", status);
                }
                else
                {
                    // 心跳检查失败，触发超时检查逻辑
                    throw new Exception("心跳检查失败");
                }
            }
            catch (Exception ex)
            {
                // 只在状态转换或达到最小日志间隔时记录警告日志
                var shouldLogWarning = false;
                var wasHealthy = _lastHealthStatus.GetValueOrDefault(diverterId, true);
                
                if (wasHealthy)
                {
                    // 从健康变为不健康，必须记录
                    shouldLogWarning = true;
                }
                else if (_lastLogTime.TryGetValue(diverterId, out var lastLog) && (now - lastLog) >= MinLogInterval)
                {
                    // 持续不健康，检查是否达到最小日志间隔
                    shouldLogWarning = true;
                }
                
                if (shouldLogWarning)
                {
                    _logger.LogWarning(ex, "摆轮 {DiverterId} 心跳检查失败", diverterId);
                    _lastLogTime[diverterId] = now;
                }
                
                // 检查是否超时
                if (_lastSuccessfulCheck.TryGetValue(diverterId, out var lastSuccess))
                {
                    var elapsed = now - lastSuccess;
                    if (elapsed > HeartbeatTimeout)
                    {
                        // 心跳超时，标记为不健康
                        if (_lastHealthStatus.TryGetValue(diverterId, out var wasPreviouslyHealthy) && wasPreviouslyHealthy)
                        {
                            _logger.LogError(
                                "摆轮 {DiverterId} 心跳超时！最后成功时间: {LastSuccess}, 已超时: {Elapsed}",
                                diverterId, lastSuccess, elapsed);
                        }
                        
                        _lastHealthStatus[diverterId] = false;
                        
                        // 更新健康状态为不健康
                        UpdateWheelDiverterHealth(diverterId, false, 
                            $"心跳超时: {elapsed.TotalSeconds:F1}秒", "连接异常");
                        
                        // 触发重连（数递鸟驱动器支持 ReconnectAsync）
                        if (driver is Drivers.Vendors.ShuDiNiao.ShuDiNiaoWheelDiverterDriver shuDiNiaoDriver)
                        {
                            _logger.LogInformation("摆轮 {DiverterId} 心跳超时，触发自动重连", diverterId);
                            shuDiNiaoDriver.ReconnectAsync();
                        }
                    }
                }
                else
                {
                    // 首次检查失败，记录但不立即标记为不健康
                    _lastSuccessfulCheck[diverterId] = now;
                    _logger.LogDebug("摆轮 {DiverterId} 首次心跳检查，记录基准时间", diverterId);
                }
            }
        }

        // TD-071: 告警功能已移除，健康状态通过 UpdateWheelDiverterHealth 更新到 NodeHealthRegistry
    }

    /// <summary>
    /// 更新摆轮健康状态到NodeHealthRegistry
    /// </summary>
    private void UpdateWheelDiverterHealth(string diverterId, bool isHealthy, string message, string status)
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
            ErrorCode = isHealthy ? null : "WHEEL_DIVERTER_HEARTBEAT_TIMEOUT",
            ErrorMessage = isHealthy ? null : message,
            NodeType = "摆轮",
            CheckedAt = new DateTimeOffset(_clock.LocalNow)
        };

        _healthRegistry.UpdateNodeHealth(healthStatus);
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
            
            if (!isReachable)
            {
                _logger.LogDebug("Ping主机 {Host} 失败，状态={Status}", host, reply.Status);
            }
            
            return isReachable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ping主机 {Host} 异常", host);
            return false;
        }
    }

    /// <summary>
    /// 从配置中获取摆轮的主机地址
    /// </summary>
    private string? GetHostForDiverter(string diverterId)
    {
        try
        {
            var config = _configRepository.Get();
            if (config == null)
            {
                return null;
            }

            if (!long.TryParse(diverterId, out var diverterIdNum))
            {
                return null;
            }

            // 检查数递鸟配置
            if (config.ShuDiNiao != null)
            {
                var device = config.ShuDiNiao.Devices.FirstOrDefault(d => d.DiverterId == diverterIdNum);
                if (device != null)
                {
                    return device.Host;
                }
            }

            // 未来可以在这里添加其他厂商的配置检查
            // 例如：if (config.Modi != null) { ... }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取摆轮 {DiverterId} 主机地址失败", diverterId);
            return null;
        }
    }

    public override void Dispose()
    {
        // 清理资源
        _lastSuccessfulCheck.Clear();
        _lastHealthStatus.Clear();
        _lastLogTime.Clear();
        base.Dispose();
    }
}
