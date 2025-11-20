using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Core.Chaos;

/// <summary>
/// 混沌注入服务实现
/// Chaos injection service implementation
/// </summary>
/// <remarks>
/// PR-41: Implements chaos engineering for system resilience testing
/// </remarks>
public class ChaosInjectionService : IChaosInjector
{
    private readonly ILogger<ChaosInjectionService> _logger;
    private readonly Random _random;
    private ChaosInjectionOptions _options;
    private bool _isEnabled;
    private readonly object _lock = new();

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">混沌选项（可选）</param>
    public ChaosInjectionService(ILogger<ChaosInjectionService> logger, ChaosInjectionOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? ChaosProfiles.Disabled;
        _isEnabled = _options.Enabled;
        _random = _options.Seed.HasValue ? new Random(_options.Seed.Value) : new Random();

        if (_isEnabled)
        {
            _logger.LogWarning("⚠️ CHAOS TESTING MODE ENABLED - System is running with chaos injection for resilience testing");
        }
    }

    /// <inheritdoc/>
    public bool IsEnabled
    {
        get
        {
            lock (_lock)
            {
                return _isEnabled;
            }
        }
    }

    /// <inheritdoc/>
    public void Enable()
    {
        lock (_lock)
        {
            _isEnabled = true;
            _logger.LogWarning("⚠️ CHAOS TESTING ENABLED - System is now in chaos testing mode");
        }
    }

    /// <inheritdoc/>
    public void Disable()
    {
        lock (_lock)
        {
            _isEnabled = false;
            _logger.LogInformation("✓ CHAOS TESTING DISABLED - System returned to normal mode");
        }
    }

    /// <inheritdoc/>
    public void Configure(ChaosInjectionOptions options)
    {
        lock (_lock)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _isEnabled = _options.Enabled;
            
            _logger.LogInformation(
                "Chaos injection configuration updated - Enabled: {Enabled}, " +
                "Communication(Ex:{CommEx}, Delay:{CommDelay}, Disc:{CommDisc}), " +
                "Driver(Ex:{DrvEx}, Delay:{DrvDelay}), " +
                "IO(Dropout:{IoDropout})",
                _isEnabled,
                _options.Communication.ExceptionProbability,
                _options.Communication.DelayProbability,
                _options.Communication.DisconnectProbability,
                _options.Driver.ExceptionProbability,
                _options.Driver.DelayProbability,
                _options.Io.DropoutProbability);
        }
    }

    /// <inheritdoc/>
    public Task<bool> ShouldInjectCommunicationChaosAsync(string operationName)
    {
        if (!IsEnabled)
        {
            return Task.FromResult(false);
        }

        lock (_lock)
        {
            var shouldInject = ShouldInjectFailure(_options.Communication.ExceptionProbability);
            
            if (shouldInject)
            {
                _logger.LogWarning(
                    "[CHAOS] Injecting communication exception for operation: {Operation}",
                    operationName);
            }
            
            return Task.FromResult(shouldInject);
        }
    }

    /// <inheritdoc/>
    public Task<bool> ShouldInjectDriverChaosAsync(string driverName)
    {
        if (!IsEnabled)
        {
            return Task.FromResult(false);
        }

        lock (_lock)
        {
            var shouldInject = ShouldInjectFailure(_options.Driver.ExceptionProbability);
            
            if (shouldInject)
            {
                _logger.LogWarning(
                    "[CHAOS] Injecting driver exception for driver: {Driver}",
                    driverName);
            }
            
            return Task.FromResult(shouldInject);
        }
    }

    /// <inheritdoc/>
    public Task<bool> ShouldInjectIoChaosAsync(string sensorId)
    {
        if (!IsEnabled)
        {
            return Task.FromResult(false);
        }

        lock (_lock)
        {
            var shouldInject = ShouldInjectFailure(_options.Io.DropoutProbability);
            
            if (shouldInject)
            {
                _logger.LogWarning(
                    "[CHAOS] Injecting IO dropout for sensor: {SensorId}",
                    sensorId);
            }
            
            return Task.FromResult(shouldInject);
        }
    }

    /// <inheritdoc/>
    public Task<int> GetCommunicationDelayAsync()
    {
        if (!IsEnabled)
        {
            return Task.FromResult(0);
        }

        lock (_lock)
        {
            if (ShouldInjectFailure(_options.Communication.DelayProbability))
            {
                var delay = _random.Next(
                    _options.Communication.MinDelayMs, 
                    _options.Communication.MaxDelayMs + 1);
                
                _logger.LogDebug(
                    "[CHAOS] Injecting communication delay: {Delay}ms",
                    delay);
                
                return Task.FromResult(delay);
            }
            
            return Task.FromResult(0);
        }
    }

    /// <inheritdoc/>
    public Task<int> GetDriverDelayAsync()
    {
        if (!IsEnabled)
        {
            return Task.FromResult(0);
        }

        lock (_lock)
        {
            if (ShouldInjectFailure(_options.Driver.DelayProbability))
            {
                var delay = _random.Next(
                    _options.Driver.MinDelayMs, 
                    _options.Driver.MaxDelayMs + 1);
                
                _logger.LogDebug(
                    "[CHAOS] Injecting driver delay: {Delay}ms",
                    delay);
                
                return Task.FromResult(delay);
            }
            
            return Task.FromResult(0);
        }
    }

    /// <summary>
    /// 检查是否应该注入断连
    /// Check if disconnect should be injected
    /// </summary>
    /// <param name="layer">层级名称</param>
    /// <returns>是否应该断连</returns>
    public bool ShouldInjectDisconnect(string layer)
    {
        if (!IsEnabled)
        {
            return false;
        }

        lock (_lock)
        {
            var probability = layer.ToLowerInvariant() switch
            {
                "communication" => _options.Communication.DisconnectProbability,
                "driver" => _options.Driver.DisconnectProbability,
                _ => 0.0
            };

            var shouldInject = ShouldInjectFailure(probability);
            
            if (shouldInject)
            {
                _logger.LogWarning(
                    "[CHAOS] Injecting disconnect in layer: {Layer}",
                    layer);
            }
            
            return shouldInject;
        }
    }

    /// <summary>
    /// 根据概率判断是否注入故障
    /// Determine if failure should be injected based on probability
    /// </summary>
    private bool ShouldInjectFailure(double probability)
    {
        if (probability <= 0)
        {
            return false;
        }

        if (probability >= 1.0)
        {
            return true;
        }

        return _random.NextDouble() < probability;
    }
}

/// <summary>
/// 混沌异常
/// Chaos exception
/// </summary>
/// <remarks>
/// Thrown when chaos injection simulates a failure
/// </remarks>
public class ChaosInjectedException : Exception
{
    /// <summary>
    /// 注入层级
    /// Injection layer
    /// </summary>
    public string Layer { get; }

    /// <summary>
    /// 注入类型
    /// Injection type
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    public ChaosInjectedException(string layer, string type, string message) 
        : base($"[CHAOS-{layer.ToUpperInvariant()}] {message}")
    {
        Layer = layer;
        Type = type;
    }

    /// <summary>
    /// 构造函数（带内部异常）
    /// Constructor with inner exception
    /// </summary>
    public ChaosInjectedException(string layer, string type, string message, Exception innerException)
        : base($"[CHAOS-{layer.ToUpperInvariant()}] {message}", innerException)
    {
        Layer = layer;
        Type = type;
    }
}
