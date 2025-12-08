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
/// 2. 触发红灯闪烁和蜂鸣器告警3秒
/// </remarks>
public sealed class WheelDiverterHeartbeatMonitor : BackgroundService
{
    private readonly IWheelDiverterDriverManager _driverManager;
    private readonly IWheelDiverterConfigurationRepository _configRepository;
    private readonly INodeHealthRegistry _healthRegistry;
    private readonly IAlarmOutputController? _alarmController;
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
    private bool _isAlarming = false;
    private DateTimeOffset? _alarmStartTime = null;
    
    /// <summary>
    /// 日志输出最小间隔（秒）- 防止日志洪水
    /// </summary>
    private static readonly TimeSpan MinLogInterval = TimeSpan.FromSeconds(30);

    public WheelDiverterHeartbeatMonitor(
        IWheelDiverterDriverManager driverManager,
        IWheelDiverterConfigurationRepository configRepository,
        INodeHealthRegistry healthRegistry,
        ISystemClock clock,
        ISafeExecutionService safeExecutor,
        ILogger<WheelDiverterHeartbeatMonitor> logger,
        IAlarmOutputController? alarmController = null)
    {
        _driverManager = driverManager ?? throw new ArgumentNullException(nameof(driverManager));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _healthRegistry = healthRegistry ?? throw new ArgumentNullException(nameof(healthRegistry));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alarmController = alarmController;
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
                    await UpdateAlarmStateAsync(stoppingToken);
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
        var anyUnhealthy = false;

        foreach (var kvp in activeDrivers)
        {
            var diverterId = kvp.Key;
            var driver = kvp.Value;

            try
            {
                bool isAlive;
                
                // 检查驱动是否支持心跳协议
                if (driver is IHeartbeatCapable heartbeatDriver)
                {
                    // 使用驱动提供的心跳机制（如TCP连接状态检查）
                    isAlive = await heartbeatDriver.CheckHeartbeatAsync(cancellationToken);
                    
                    // 只在状态转换或达到最小日志间隔时才记录日志
                    var shouldLog = false;
                    var wasHealthy = _lastHealthStatus.GetValueOrDefault(diverterId, true);
                    
                    if (wasHealthy != isAlive)
                    {
                        // 状态转换，必须记录
                        shouldLog = true;
                    }
                    else if (_lastLogTime.TryGetValue(diverterId, out var lastLog))
                    {
                        // 检查是否达到最小日志间隔
                        if ((now - lastLog) >= MinLogInterval)
                        {
                            shouldLog = true;
                        }
                    }
                    else
                    {
                        // 首次检查
                        shouldLog = true;
                    }
                    
                    if (shouldLog)
                    {
                        if (!isAlive)
                        {
                            // 只记录心跳异常，心跳正常不记录日志
                            _logger.LogWarning("摆轮 {DiverterId} 使用协议心跳检查，结果=离线", diverterId);
                        }
                        // 即使心跳正常未记录日志，也要更新时间戳，以避免后续频繁触发 shouldLog 条件。
                        // 这样可确保首次检查和日志间隔逻辑一致，防止误判为需要记录日志。
                        _lastLogTime[diverterId] = now;
                    }
                }
                else
                {
                    // 驱动不支持心跳协议，使用Ping作为后备
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
                        anyUnhealthy = true;
                        
                        // 更新健康状态为不健康
                        UpdateWheelDiverterHealth(diverterId, false, 
                            $"心跳超时: {elapsed.TotalSeconds:F1}秒", "连接异常");
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

        // 如果有不健康的摆轮且当前未在告警，开始告警
        if (anyUnhealthy && !_isAlarming)
        {
            await StartAlarmAsync(cancellationToken);
        }
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
    /// 开始告警（红灯闪烁 + 蜂鸣3秒）
    /// </summary>
    private async Task StartAlarmAsync(CancellationToken cancellationToken)
    {
        if (_alarmController == null)
        {
            _logger.LogWarning("IAlarmOutputController未注入，无法执行告警");
            return;
        }

        _isAlarming = true;
        _alarmStartTime = new DateTimeOffset(_clock.LocalNow);
        
        _logger.LogWarning("⚠️ 摆轮心跳异常！开始告警：红灯闪烁 + 蜂鸣3秒");

        try
        {
            // 打开红灯
            await _alarmController.SetRedLightAsync(true, cancellationToken);
            
            // 打开蜂鸣器
            await _alarmController.SetBuzzerAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动告警失败");
        }
    }

    /// <summary>
    /// 更新告警状态（处理3秒后自动停止）
    /// </summary>
    private async Task UpdateAlarmStateAsync(CancellationToken cancellationToken)
    {
        if (!_isAlarming || _alarmStartTime == null || _alarmController == null)
        {
            return;
        }

        var now = new DateTimeOffset(_clock.LocalNow);
        var elapsed = now - _alarmStartTime.Value;

        // 检查是否已经过了3秒
        if (elapsed >= AlarmDuration)
        {
            _logger.LogInformation("告警持续时间已达3秒，停止告警");
            
            try
            {
                // 关闭蜂鸣器
                await _alarmController.SetBuzzerAsync(false, cancellationToken);
                
                // 红灯保持闪烁直到所有摆轮恢复健康
                var unhealthyDrivers = _lastHealthStatus.Where(kvp => !kvp.Value).ToList();
                if (unhealthyDrivers.Count == 0)
                {
                    // 所有摆轮都健康了，关闭红灯
                    await _alarmController.SetRedLightAsync(false, cancellationToken);
                    _logger.LogInformation("所有摆轮恢复健康，关闭红灯");
                }
                else
                {
                    _logger.LogWarning("仍有 {Count} 个摆轮不健康，红灯保持闪烁", unhealthyDrivers.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止告警失败");
            }
            
            _isAlarming = false;
            _alarmStartTime = null;
        }
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
