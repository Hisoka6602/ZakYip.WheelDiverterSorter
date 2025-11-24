using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 简单熔断器实现
/// </summary>
public class SimpleCircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private readonly ICommunicationLogger _logger;
    private readonly ISystemClock _systemClock;
    private int _consecutiveFailures;
    private DateTime _lastFailureTime;
    private CircuitState _state;
    private readonly object _lock = new();

    public SimpleCircuitBreaker(RuleEngineConnectionOptions options, ICommunicationLogger logger, ISystemClock systemClock)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // 失败阈值默认为重试次数的2倍
        _failureThreshold = options.RetryCount * 2;
        // 熔断持续时间默认为超时时间的3倍
        _breakDuration = TimeSpan.FromMilliseconds(options.TimeoutMs * 3);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _state = CircuitState.Closed;
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                // 如果熔断器打开，检查是否应该进入半开状态
                if (_state == CircuitState.Open && 
                    _systemClock.LocalNow - _lastFailureTime >= _breakDuration)
                {
                    _state = CircuitState.HalfOpen;
                    _logger.LogInformation("Circuit breaker entering HalfOpen state");
                }
                return _state;
            }
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        if (State == CircuitState.Open)
        {
            throw new InvalidOperationException(
                $"Circuit breaker is open. Service is temporarily unavailable. Will retry after {_breakDuration.TotalSeconds}s.");
        }

        try
        {
            var result = await operation();
            OnSuccess();
            return result;
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
            _logger.LogInformation("Circuit breaker reset to Closed state");
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _logger.LogInformation("Circuit breaker closing after successful operation");
            }
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            _lastFailureTime = _systemClock.LocalNow;

            if (_consecutiveFailures >= _failureThreshold && _state != CircuitState.Open)
            {
                _state = CircuitState.Open;
                _logger.LogWarning(
                    "Circuit breaker opened after {FailureCount} consecutive failures. Will retry after {Duration}s",
                    _consecutiveFailures, _breakDuration.TotalSeconds);
            }
        }
    }
}
