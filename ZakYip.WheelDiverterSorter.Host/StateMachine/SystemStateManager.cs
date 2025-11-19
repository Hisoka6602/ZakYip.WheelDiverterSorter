using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Host.StateMachine;

/// <summary>
/// 系统状态管理器实现
/// </summary>
/// <remarks>
/// 实现系统状态机逻辑，管理状态转移和状态历史。
/// 确保所有状态转移遵循预定义规则，非法转移会被拒绝。
/// </remarks>
public class SystemStateManager : ISystemStateManager
{
    private readonly ILogger<SystemStateManager> _logger;
    private readonly object _lock = new();
    private SystemState _currentState;
    private readonly List<StateTransitionRecord> _transitionHistory = new();
    private const int MaxHistorySize = 100;
    private ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health.SystemSelfTestReport? _lastSelfTestReport;

    /// <inheritdoc/>
    public SystemState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    /// <inheritdoc/>
    public ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health.SystemSelfTestReport? LastSelfTestReport
    {
        get
        {
            lock (_lock)
            {
                return _lastSelfTestReport;
            }
        }
    }

    /// <summary>
    /// 初始化系统状态管理器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="initialState">初始状态（默认为Booting）</param>
    public SystemStateManager(ILogger<SystemStateManager> logger, SystemState initialState = SystemState.Booting)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentState = initialState;
        _logger.LogInformation("系统状态管理器已初始化，初始状态: {State}", _currentState);
    }

    /// <inheritdoc/>
    public async Task<StateChangeResult> ChangeStateAsync(SystemState targetState, CancellationToken cancellationToken = default)
    {
        // 状态转移必须在锁内执行，确保线程安全
        StateChangeResult result;
        
        lock (_lock)
        {
            var previousState = _currentState;

            // 如果已经是目标状态，直接返回成功
            if (_currentState == targetState)
            {
                _logger.LogDebug("系统已处于目标状态 {State}，无需切换", targetState);
                return StateChangeResult.CreateSuccess(previousState, _currentState);
            }

            // 检查状态转移是否合法
            if (!IsTransitionValid(_currentState, targetState, out var errorMessage))
            {
                _logger.LogWarning(
                    "非法状态转移: {FromState} -> {ToState}，原因: {Reason}",
                    _currentState, targetState, errorMessage);

                // 记录失败的转移尝试
                RecordTransition(previousState, targetState, false, errorMessage);

                return StateChangeResult.CreateFailure(errorMessage, _currentState);
            }

            // 执行状态转移
            _currentState = targetState;

            _logger.LogInformation(
                "状态转移成功: {FromState} -> {ToState}",
                previousState, _currentState);

            // 记录成功的转移
            RecordTransition(previousState, _currentState, true, null);

            result = StateChangeResult.CreateSuccess(previousState, _currentState);
        }

        // 在锁外执行异步操作（如果需要）
        await Task.CompletedTask;

        return result;
    }

    /// <inheritdoc/>
    public bool CanTransitionTo(SystemState targetState)
    {
        lock (_lock)
        {
            return IsTransitionValid(_currentState, targetState, out _);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<StateTransitionRecord> GetTransitionHistory(int count = 10)
    {
        lock (_lock)
        {
            return _transitionHistory
                .TakeLast(Math.Min(count, _transitionHistory.Count))
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// 验证状态转移是否合法
    /// </summary>
    /// <param name="fromState">源状态</param>
    /// <param name="toState">目标状态</param>
    /// <param name="errorMessage">错误消息（如果非法）</param>
    /// <returns>是否合法</returns>
    /// <remarks>
    /// 状态转移规则：
    /// - Booting → Ready（启动完成）
    /// - Ready → Running（启动系统）
    /// - Running → Paused（暂停系统）
    /// - Paused → Running（恢复运行）
    /// - Running/Paused/Ready → Ready（停止系统）
    /// - 任何状态 → EmergencyStop（急停）
    /// - EmergencyStop → Ready（急停解除）
    /// - 任何状态 → Faulted（故障发生）
    /// - Faulted → Ready（故障恢复）
    /// </remarks>
    private static bool IsTransitionValid(SystemState fromState, SystemState toState, out string errorMessage)
    {
        errorMessage = string.Empty;

        // 急停和故障可以从任何状态触发
        if (toState == SystemState.EmergencyStop)
        {
            return true; // 任何状态都可以急停
        }

        if (toState == SystemState.Faulted)
        {
            return true; // 任何状态都可能发生故障
        }

        // 其他转移规则
        var isValid = (fromState, toState) switch
        {
            // 启动完成: Booting → Ready
            (SystemState.Booting, SystemState.Ready) => true,

            // 启动系统: Ready → Running
            (SystemState.Ready, SystemState.Running) => true,

            // 暂停系统: Running → Paused
            (SystemState.Running, SystemState.Paused) => true,

            // 恢复运行: Paused → Running
            (SystemState.Paused, SystemState.Running) => true,

            // 停止系统: Running/Paused → Ready
            (SystemState.Running, SystemState.Ready) => true,
            (SystemState.Paused, SystemState.Ready) => true,

            // 急停解除: EmergencyStop → Ready
            (SystemState.EmergencyStop, SystemState.Ready) => true,

            // 故障恢复: Faulted → Ready
            (SystemState.Faulted, SystemState.Ready) => true,

            // 其他转移都是非法的
            _ => false
        };

        if (!isValid)
        {
            errorMessage = $"不允许从 {GetStateDescription(fromState)} 切换到 {GetStateDescription(toState)}";
        }

        return isValid;
    }

    /// <summary>
    /// 获取状态的中文描述
    /// </summary>
    private static string GetStateDescription(SystemState state)
    {
        return state switch
        {
            SystemState.Booting => "启动中",
            SystemState.Ready => "就绪",
            SystemState.Running => "运行中",
            SystemState.Paused => "暂停",
            SystemState.Faulted => "故障",
            SystemState.EmergencyStop => "急停",
            _ => state.ToString()
        };
    }

    /// <summary>
    /// 记录状态转移
    /// </summary>
    private void RecordTransition(SystemState fromState, SystemState toState, bool success, string? failureReason)
    {
        var record = new StateTransitionRecord
        {
            FromState = fromState,
            ToState = toState,
            TransitionTime = DateTimeOffset.UtcNow,
            Success = success,
            FailureReason = failureReason
        };

        _transitionHistory.Add(record);

        // 限制历史记录大小
        if (_transitionHistory.Count > MaxHistorySize)
        {
            _transitionHistory.RemoveAt(0);
        }
    }

    /// <inheritdoc/>
    public async Task<ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health.SystemSelfTestReport> BootAsync(CancellationToken cancellationToken = default)
    {
        // 注意：BootAsync需要ISelfTestCoordinator依赖
        // 由于这是一个可选的循环依赖，我们通过构造函数注入解决
        // 这里暂时返回一个简单的报告，实际实现将在注入ISelfTestCoordinator后完成
        _logger.LogWarning("BootAsync called but ISelfTestCoordinator not injected. Using default behavior.");
        
        lock (_lock)
        {
            _currentState = SystemState.Booting;
        }

        // 创建一个默认的成功报告
        var report = new ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health.SystemSelfTestReport
        {
            IsSuccess = true,
            Drivers = new List<ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health.DriverHealthStatus>().AsReadOnly(),
            Upstreams = new List<ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health.UpstreamHealthStatus>().AsReadOnly(),
            Config = new ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health.ConfigHealthStatus { IsValid = true },
            PerformedAt = DateTimeOffset.UtcNow
        };

        lock (_lock)
        {
            _lastSelfTestReport = report;
            _currentState = SystemState.Ready;
        }

        await Task.CompletedTask;
        return report;
    }
}

