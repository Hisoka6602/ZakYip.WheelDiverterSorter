using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Ingress.Configuration;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;

namespace ZakYip.WheelDiverterSorter.Ingress.Services;

/// <summary>
/// 包裹检测服务
/// </summary>
/// <remarks>
/// 负责监听传感器事件，检测包裹到达，并生成唯一包裹ID。
/// 
/// <para><b>重要规则（PR-fix-sensor-type-filtering）</b>：</para>
/// <list type="bullet">
///   <item>只有 `ParcelCreation` 类型的传感器会触发 `ParcelDetected` 事件（创建包裹）</item>
///   <item>`WheelFront` 和 `ChuteLock` 类型的传感器只用于监控和日志记录</item>
///   <item>系统中只能有一个 `ParcelCreation` 类型的传感器处于激活状态</item>
///   <item>只有系统处于运行状态（Running）时才创建包裹</item>
/// </list>
/// </remarks>
public class ParcelDetectionService : IParcelDetectionService, IDisposable
{
    private readonly IEnumerable<ISensor> _sensors;
    private readonly ILogger<ParcelDetectionService>? _logger;
    private readonly Services.ISensorHealthMonitor? _healthMonitor;
    private readonly ParcelDetectionOptions _options;
    private readonly ISensorConfigurationRepository? _sensorConfigRepository;
    private readonly ISystemStateManager? _systemStateManager;
    // PR-44: 使用 ConcurrentDictionary 替代 Dictionary + lock
    private readonly ConcurrentDictionary<long, DateTimeOffset> _lastTriggerTimes = new();
    // PR-44: 使用 ConcurrentQueue 和 ConcurrentDictionary 替代 Queue + HashSet + lock
    private readonly ConcurrentQueue<long> _recentParcelIds = new();
    private readonly ConcurrentDictionary<long, byte> _parcelIdSet = new(); // 使用 byte 作为 dummy value
    private bool _isRunning;

    /// <summary>
    /// 包裹检测事件
    /// </summary>
    public event EventHandler<ParcelDetectedEventArgs>? ParcelDetected;

    /// <summary>
    /// 重复触发异常事件
    /// </summary>
    public event EventHandler<DuplicateTriggerEventArgs>? DuplicateTriggerDetected;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensors">传感器集合</param>
    /// <param name="options">包裹检测配置选项</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="healthMonitor">传感器健康监控器（可选）</param>
    /// <param name="sensorConfigRepository">传感器配置仓储（可选，用于获取 IoType）</param>
    /// <param name="systemRunStateService">系统运行状态服务（可选，用于验证是否允许创建包裹）</param>
    public ParcelDetectionService(
        IEnumerable<ISensor> sensors,
        IOptions<ParcelDetectionOptions>? options = null,
        ILogger<ParcelDetectionService>? logger = null,
        Services.ISensorHealthMonitor? healthMonitor = null,
        ISensorConfigurationRepository? sensorConfigRepository = null,
        ISystemStateManager? systemStateManager = null)
    {
        _sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));
        _options = options?.Value ?? new ParcelDetectionOptions();
        _logger = logger;
        _healthMonitor = healthMonitor;
        _sensorConfigRepository = sensorConfigRepository;
        _systemStateManager = systemStateManager;
    }

    /// <summary>
    /// 启动包裹检测服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return;
        }

        _logger?.LogInformation("启动包裹检测服务");

        // 订阅所有传感器的事件
        foreach (var sensor in _sensors)
        {
            sensor.SensorTriggered += OnSensorTriggered;
            sensor.SensorError += OnSensorError;
            await sensor.StartAsync(cancellationToken);
            _logger?.LogInformation("传感器 {SensorId} ({Type}) 已启动", sensor.SensorId, sensor.Type);
        }

        // 启动健康监控
        if (_healthMonitor != null)
        {
            await _healthMonitor.StartAsync(cancellationToken);
            _logger?.LogInformation("传感器健康监控已启动");
        }

        _isRunning = true;
        
        // 输出启动完成总结
        var sensorCount = _sensors.Count();
        var activeSensorCount = _sensors.Count(s => s != null);
        _logger?.LogInformation(
            "========== 包裹检测服务启动完成 ==========\n" +
            "  - 已注册传感器: {SensorCount} 个\n" +
            "  - 已激活传感器: {ActiveSensorCount} 个\n" +
            "  - 健康监控: {HealthMonitorStatus}\n" +
            "  - 服务状态: 运行中，等待包裹触发事件\n" +
            "========================================",
            sensorCount,
            activeSensorCount,
            _healthMonitor != null ? "已启用" : "未启用");
    }

    /// <summary>
    /// 停止包裹检测服务
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _logger?.LogInformation("停止包裹检测服务");

        // 停止健康监控
        if (_healthMonitor != null)
        {
            await _healthMonitor.StopAsync();
            _logger?.LogInformation("传感器健康监控已停止");
        }

        // 取消订阅并停止所有传感器
        foreach (var sensor in _sensors)
        {
            sensor.SensorTriggered -= OnSensorTriggered;
            sensor.SensorError -= OnSensorError;
            await sensor.StopAsync();
            _logger?.LogInformation("传感器 {SensorId} ({Type}) 已停止", sensor.SensorId, sensor.Type);
        }

        _isRunning = false;
    }

    /// <summary>
    /// 处理传感器触发事件
    /// </summary>
    private void OnSensorTriggered(object? sender, SensorEvent sensorEvent)
    {
        // 只处理物体遮挡事件（IsTriggered = true）
        if (!sensorEvent.IsTriggered)
        {
            return;
        }

        // PR-44: 不再需要锁，使用 ConcurrentDictionary 和 ConcurrentQueue
        var (isDuplicate, timeSinceLastTriggerMs) = CheckForDuplicateTrigger(sensorEvent);
        UpdateLastTriggerTime(sensorEvent);

        // PR-fix-sensor-type-filtering: 
        // - ParcelCreation 类型：创建包裹并触发 ParcelDetected 事件
        // - WheelFront/ChuteLock 类型：触发 ParcelDetected 事件（但不创建包裹，由 Orchestrator 处理）
        // PR-fix-system-state-check: 所有传感器类型都需要检查系统状态
        
        var sensorType = GetSensorType(sensorEvent.SensorId);
        
        // 所有传感器类型都需要检查系统是否处于运行状态
        if (!IsSystemReadyForParcelCreation())
        {
            _logger?.LogWarning(
                "传感器 {SensorId} ({SensorType}) 触发，但系统未处于运行状态，已忽略",
                sensorEvent.SensorId,
                sensorType);
            return;
        }
        
        if (sensorType == SensorIoType.ParcelCreation)
        {
            // ParcelCreation 传感器：创建包裹并触发事件
            var parcelId = GenerateUniqueParcelId(sensorEvent);
            AddParcelIdToHistory(parcelId);

            if (isDuplicate)
            {
                RaiseDuplicateTriggerEvent(parcelId, sensorEvent, timeSinceLastTriggerMs);
            }
            
            _logger?.LogDebug(
                "传感器 {SensorId} 类型为 {SensorType}，触发包裹创建",
                sensorEvent.SensorId,
                sensorType);
            RaiseParcelDetectedEvent(parcelId, sensorEvent, isDuplicate, sensorType);
        }
        else
        {
            // WheelFront/ChuteLock 传感器：不创建包裹，只触发事件供 Orchestrator 处理
            // 使用 0 作为 ParcelId，表示这不是一个真正的包裹创建事件
            _logger?.LogDebug(
                "传感器 {SensorId} 类型为 {SensorType}，不触发包裹创建（只用于监控）",
                sensorEvent.SensorId,
                sensorType);
            
            // 触发 ParcelDetected 事件，ParcelId=0 表示非包裹创建事件
            RaiseParcelDetectedEvent(0, sensorEvent, isDuplicate, sensorType);
        }
    }

    /// <summary>
    /// 检查系统是否允许创建包裹
    /// </summary>
    /// <returns>true 表示允许创建包裹，false 表示系统未就绪</returns>
    private bool IsSystemReadyForParcelCreation()
    {
        // 如果没有系统状态管理器，默认允许（向后兼容）
        if (_systemStateManager == null)
        {
            _logger?.LogDebug("未注入 ISystemStateManager，默认允许创建包裹（向后兼容模式）");
            return true;
        }

        try
        {
            // 使用系统状态管理器检查当前状态
            var currentState = _systemStateManager.CurrentState;
            
            // 只有 Running 状态才允许创建包裹
            if (currentState != Core.Enums.System.SystemState.Running)
            {
                _logger?.LogDebug(
                    "系统状态验证失败，不允许创建包裹。当前状态: {CurrentState}（需要 Running 状态）",
                    currentState);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "检查系统状态时发生异常，默认不允许创建包裹");
            return false;
        }
    }

    /// <summary>
    /// 获取传感器类型
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <returns>传感器类型</returns>
    private SensorIoType GetSensorType(long sensorId)
    {
        // 如果没有配置仓储，默认为 ParcelCreation 类型（向后兼容）
        if (_sensorConfigRepository == null)
        {
            _logger?.LogWarning(
                "未注入 ISensorConfigurationRepository，传感器 {SensorId} 默认为 ParcelCreation 类型（向后兼容模式）",
                sensorId);
            return SensorIoType.ParcelCreation;
        }

        try
        {
            var config = _sensorConfigRepository.Get();
            var sensor = config.Sensors.FirstOrDefault(s => s.SensorId == sensorId);

            if (sensor == null)
            {
                _logger?.LogWarning(
                    "传感器 {SensorId} 未在配置中找到，默认为 ParcelCreation 类型",
                    sensorId);
                return SensorIoType.ParcelCreation;
            }

            return sensor.IoType;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "获取传感器 {SensorId} 类型时发生异常，默认为 ParcelCreation 类型",
                sensorId);
            return SensorIoType.ParcelCreation;
        }
    }

    /// <summary>
    /// 判断传感器是否应触发包裹创建
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <returns>true 表示应触发包裹创建，false 表示只用于监控</returns>
    private bool ShouldTriggerParcelCreation(long sensorId)
    {
        // 如果没有配置仓储，默认允许所有传感器触发（向后兼容）
        if (_sensorConfigRepository == null)
        {
            _logger?.LogWarning(
                "未注入 ISensorConfigurationRepository，传感器 {SensorId} 默认允许创建包裹（向后兼容模式）",
                sensorId);
            return true;
        }

        try
        {
            var config = _sensorConfigRepository.Get();
            var sensor = config.Sensors.FirstOrDefault(s => s.SensorId == sensorId);

            if (sensor == null)
            {
                _logger?.LogWarning(
                    "传感器 {SensorId} 未在配置中找到，默认不允许创建包裹",
                    sensorId);
                return false;
            }

            if (!sensor.IsEnabled)
            {
                _logger?.LogDebug(
                    "传感器 {SensorId} 已禁用，不触发包裹创建",
                    sensorId);
                return false;
            }

            // 只有 ParcelCreation 类型的传感器才能创建包裹
            bool isParcelCreationType = sensor.IoType == SensorIoType.ParcelCreation;
            
            if (!isParcelCreationType)
            {
                _logger?.LogDebug(
                    "传感器 {SensorId} 类型为 {IoType}，不触发包裹创建（只用于监控）",
                    sensorId,
                    sensor.IoType);
            }

            return isParcelCreationType;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "检查传感器 {SensorId} 是否应创建包裹时发生异常，默认不允许创建",
                sensorId);
            return false;
        }
    }

    /// <summary>
    /// 检查是否为重复触发
    /// </summary>
    private (bool IsDuplicate, double TimeSinceLastTriggerMs) CheckForDuplicateTrigger(SensorEvent sensorEvent)
    {
        if (!_lastTriggerTimes.TryGetValue(sensorEvent.SensorId, out var lastTime))
        {
            return (false, 0);
        }

        var timeSinceLastTrigger = sensorEvent.TriggerTime - lastTime;
        var timeSinceLastTriggerMs = timeSinceLastTrigger.TotalMilliseconds;

        // 从传感器配置读取防抖时间，如果未配置则使用全局默认值
        var deduplicationWindowMs = GetDeduplicationWindowForSensor(sensorEvent.SensorId);

        if (timeSinceLastTriggerMs < deduplicationWindowMs)
        {
            _logger?.LogWarning(
                "检测到重复触发: 传感器 {SensorId}, 距上次触发 {TimeSinceLastMs}ms",
                sensorEvent.SensorId,
                timeSinceLastTriggerMs);
            return (true, timeSinceLastTriggerMs);
        }

        return (false, timeSinceLastTriggerMs);
    }

    /// <summary>
    /// 获取传感器的防抖时间窗口
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <returns>防抖时间窗口（毫秒）</returns>
    private int GetDeduplicationWindowForSensor(long sensorId)
    {
        // 尝试从传感器配置读取
        if (_sensorConfigRepository != null)
        {
            try
            {
                var config = _sensorConfigRepository.Get();
                var sensor = config?.Sensors?.FirstOrDefault(s => s.SensorId == sensorId);
                if (sensor?.DeduplicationWindowMs.HasValue == true)
                {
                    return sensor.DeduplicationWindowMs.Value;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "读取传感器 {SensorId} 的防抖配置失败，使用全局默认值", sensorId);
            }
        }

        // 使用全局默认值
        return _options.DeduplicationWindowMs;
    }

    /// <summary>
    /// 更新传感器最后触发时间
    /// </summary>
    private void UpdateLastTriggerTime(SensorEvent sensorEvent)
    {
        _lastTriggerTimes[sensorEvent.SensorId] = sensorEvent.TriggerTime;
    }

    /// <summary>
    /// 触发重复触发异常事件
    /// </summary>
    private void RaiseDuplicateTriggerEvent(long parcelId, SensorEvent sensorEvent, double timeSinceLastTriggerMs)
    {
        var deduplicationWindowMs = GetDeduplicationWindowForSensor(sensorEvent.SensorId);
        
        var duplicateEventArgs = new DuplicateTriggerEventArgs
        {
            ParcelId = parcelId,
            DetectedAt = sensorEvent.TriggerTime,
            SensorId = sensorEvent.SensorId,
            SensorType = sensorEvent.SensorType,
            TimeSinceLastTriggerMs = timeSinceLastTriggerMs,
            Reason = $"传感器在{deduplicationWindowMs}ms去重窗口内重复触发"
        };

        _logger?.LogWarning(
            "包裹 {ParcelId} 标记为重复触发异常，传感器: {SensorId}，距上次触发: {TimeSinceLastMs}ms",
            parcelId,
            sensorEvent.SensorId,
            timeSinceLastTriggerMs);

        DuplicateTriggerDetected.SafeInvoke(this, duplicateEventArgs, _logger, nameof(DuplicateTriggerDetected));
    }

    /// <summary>
    /// 触发包裹检测事件
    /// </summary>
    /// <param name="parcelId">包裹ID（对于WheelFront/ChuteLock传感器为0，表示非包裹创建事件）</param>
    /// <param name="sensorEvent">传感器事件</param>
    /// <param name="isDuplicate">是否为重复触发</param>
    /// <param name="sensorType">传感器类型（避免重复查询）</param>
    private void RaiseParcelDetectedEvent(long parcelId, SensorEvent sensorEvent, bool isDuplicate, SensorIoType sensorType)
    {
        // 根据传感器类型输出不同的日志
        if (sensorType == SensorIoType.ParcelCreation)
        {
            _logger?.LogInformation(
                "检测到包裹 {ParcelId}，传感器: {SensorId} ({SensorType}){DuplicateFlag}",
                parcelId,
                sensorEvent.SensorId,
                sensorEvent.SensorType,
                isDuplicate ? " [重复触发]" : "");
        }
        else
        {
            // WheelFront/ChuteLock传感器不创建包裹，只记录触发
            _logger?.LogInformation(
                "传感器 {SensorId} ({IoType}) 触发{DuplicateFlag}（不创建包裹，等待Orchestrator处理）",
                sensorEvent.SensorId,
                sensorType,
                isDuplicate ? " [重复触发]" : "");
        }

        var eventArgs = new ParcelDetectedEventArgs
        {
            ParcelId = parcelId,
            DetectedAt = sensorEvent.TriggerTime,
            SensorId = sensorEvent.SensorId,
            SensorType = sensorEvent.SensorType
        };

        _logger?.LogTrace(
            "触发 ParcelDetected 事件: ParcelId={ParcelId}, SensorId={SensorId}, IoType={IoType}",
            parcelId,
            sensorEvent.SensorId,
            sensorType);

        ParcelDetected.SafeInvoke(this, eventArgs, _logger, nameof(ParcelDetected));
    }

    /// <summary>
    /// 生成唯一包裹ID
    /// </summary>
    /// <param name="sensorEvent">传感器事件</param>
    /// <returns>唯一包裹ID (毫秒时间戳)</returns>
    private long GenerateUniqueParcelId(SensorEvent sensorEvent)
    {
        long parcelId;
        int attempts = 0;
        const int maxAttempts = 10;

        do
        {
            // 使用触发时间的毫秒时间戳作为包裹ID
            parcelId = sensorEvent.TriggerTime.ToUnixTimeMilliseconds();
            attempts++;

            // PR-44: 检查是否已存在于历史记录中 (ConcurrentDictionary.ContainsKey 是线程安全的)
            if (!_parcelIdSet.ContainsKey(parcelId))
            {
                break;
            }

            // 如果ID已存在，增加1毫秒来生成新ID
            sensorEvent = sensorEvent with { TriggerTime = sensorEvent.TriggerTime.AddMilliseconds(1) };

            _logger?.LogWarning(
                "生成的包裹ID {ParcelId} 已存在，重新生成 (尝试 {Attempt}/{MaxAttempts})",
                parcelId,
                attempts,
                maxAttempts);

        } while (attempts < maxAttempts);

        return parcelId;
    }

    /// <summary>
    /// 将包裹ID添加到历史记录
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    private void AddParcelIdToHistory(long parcelId)
    {
        // PR-44: ConcurrentDictionary.TryAdd 和 ConcurrentQueue.Enqueue 是线程安全的
        _parcelIdSet.TryAdd(parcelId, 0); // 0 是 dummy value
        _recentParcelIds.Enqueue(parcelId);

        // 如果超过最大历史记录数量，移除最旧的记录
        while (_recentParcelIds.Count > _options.ParcelIdHistorySize)
        {
            if (_recentParcelIds.TryDequeue(out var oldestId))
            {
                _parcelIdSet.TryRemove(oldestId, out _);
            }
        }
    }

    /// <summary>
    /// 处理传感器错误事件
    /// </summary>
    private void OnSensorError(object? sender, SensorErrorEventArgs e)
    {
        _logger?.LogError(
            e.Exception,
            "传感器 {SensorId} 发生错误: {ErrorMessage}",
            e.SensorId,
            e.ErrorMessage);

        // 报告给健康监控器
        _healthMonitor?.ReportError(e.SensorId, e.ErrorMessage);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();

        foreach (var sensor in _sensors)
        {
            if (sensor is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
