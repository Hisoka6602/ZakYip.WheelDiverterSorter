using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 默认系统运行状态服务实现。
/// 按照问题陈述中的业务规则实现状态机转换。
/// </summary>
public class DefaultSystemRunStateService : ISystemRunStateService
{
    private readonly object _lock = new();
    private SystemOperatingState _currentState;
    private readonly ILogger<DefaultSystemRunStateService> _logger;

    public DefaultSystemRunStateService(ILogger<DefaultSystemRunStateService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // 默认状态为待机（就绪）
        _currentState = SystemOperatingState.Standby;
    }

    /// <inheritdoc/>
    public SystemOperatingState Current
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
    public OperationResult TryHandleStart()
    {
        lock (_lock)
        {
            // 规则：只有待机、已停止、暂停状态可以启动
            if (_currentState is SystemOperatingState.Standby 
                               or SystemOperatingState.Stopped 
                               or SystemOperatingState.Paused)
            {
                var oldState = _currentState;
                _currentState = SystemOperatingState.Running;
                _logger.LogInformation("系统状态从 {OldState} 切换为 Running", oldState);
                return OperationResult.Success();
            }

            // 规则：运行状态下重复按启动无效
            if (_currentState == SystemOperatingState.Running)
            {
                _logger.LogDebug("系统已处于运行状态，启动按钮无效");
                return OperationResult.Failure("系统已处于运行状态");
            }

            // 规则：故障状态下所有按钮无效（包括启动）
            if (_currentState == SystemOperatingState.EmergencyStopped)
            {
                _logger.LogWarning("系统处于急停状态，启动按钮无效");
                return OperationResult.Failure("系统当前处于急停状态，无法启动");
            }

            if (_currentState == SystemOperatingState.Faulted)
            {
                _logger.LogWarning("系统处于故障状态，启动按钮无效");
                return OperationResult.Failure("系统当前处于故障状态，无法启动");
            }

            // 其他状态也不允许启动
            _logger.LogWarning("系统当前状态 {CurrentState} 不允许启动", _currentState);
            return OperationResult.Failure($"系统当前状态 {_currentState} 不允许启动");
        }
    }

    /// <inheritdoc/>
    public OperationResult TryHandleStop()
    {
        lock (_lock)
        {
            // 规则：只有运行、暂停状态可以停止
            if (_currentState is SystemOperatingState.Running 
                               or SystemOperatingState.Paused)
            {
                var oldState = _currentState;
                _currentState = SystemOperatingState.Stopped;
                _logger.LogInformation("系统状态从 {OldState} 切换为 Stopped", oldState);
                return OperationResult.Success();
            }

            // 规则：停止状态下重复按停止无效
            if (_currentState == SystemOperatingState.Stopped)
            {
                _logger.LogDebug("系统已处于停止状态，停止按钮无效");
                return OperationResult.Failure("系统已处于停止状态");
            }

            // 规则：故障状态下所有按钮无效（包括停止）
            if (_currentState == SystemOperatingState.EmergencyStopped)
            {
                _logger.LogWarning("系统处于急停状态，停止按钮无效");
                return OperationResult.Failure("系统当前处于急停状态，停止按钮无效");
            }

            if (_currentState == SystemOperatingState.Faulted)
            {
                _logger.LogWarning("系统处于故障状态，停止按钮无效");
                return OperationResult.Failure("系统当前处于故障状态，停止按钮无效");
            }

            // 其他状态也不允许停止
            _logger.LogWarning("系统当前状态 {CurrentState} 不允许停止", _currentState);
            return OperationResult.Failure($"系统当前状态 {_currentState} 不允许停止");
        }
    }

    /// <inheritdoc/>
    public OperationResult TryHandleEmergencyStop()
    {
        lock (_lock)
        {
            // 规则：急停状态下不能再次急停
            if (_currentState == SystemOperatingState.EmergencyStopped)
            {
                _logger.LogDebug("系统已处于急停状态，急停按钮无效");
                return OperationResult.Failure("系统已处于急停状态");
            }

            // 规则：其他任何状态都可以急停
            var oldState = _currentState;
            _currentState = SystemOperatingState.EmergencyStopped;
            _logger.LogWarning("触发急停！系统状态从 {OldState} 切换为 EmergencyStopped", oldState);
            return OperationResult.Success();
        }
    }

    /// <inheritdoc/>
    public OperationResult TryHandleEmergencyReset()
    {
        lock (_lock)
        {
            // 规则：只有急停状态才能执行急停复位
            if (_currentState == SystemOperatingState.EmergencyStopped)
            {
                _currentState = SystemOperatingState.Standby;
                _logger.LogInformation("急停已解除，系统状态切换为 Standby（就绪）");
                return OperationResult.Success();
            }

            // 非急停状态不需要复位
            _logger.LogDebug("系统当前不在急停状态，无需执行急停复位");
            return OperationResult.Failure("系统当前不在急停状态");
        }
    }

    /// <inheritdoc/>
    public OperationResult ValidateParcelCreation()
    {
        lock (_lock)
        {
            // 规则：只有运行状态才能创建包裹
            if (_currentState == SystemOperatingState.Running)
            {
                return OperationResult.Success();
            }

            // 根据不同状态返回不同的错误消息
            var errorMessage = _currentState switch
            {
                SystemOperatingState.Standby => "系统当前未处于运行状态，禁止创建包裹。当前状态: 待机",
                SystemOperatingState.Stopped => "系统当前未处于运行状态，禁止创建包裹。当前状态: 停止",
                SystemOperatingState.EmergencyStopped => "系统当前处于急停状态，禁止创建包裹。",
                SystemOperatingState.Faulted => "系统当前处于故障状态，禁止创建包裹。",
                SystemOperatingState.Paused => "系统当前处于暂停状态，禁止创建包裹。",
                SystemOperatingState.Stopping => "系统当前正在停止，禁止创建包裹。",
                SystemOperatingState.Initializing => "系统当前正在初始化，禁止创建包裹。",
                SystemOperatingState.WaitingUpstream => "系统当前正在等待上游，禁止创建包裹。",
                _ => $"系统当前状态 {_currentState} 不允许创建包裹。"
            };

            _logger.LogWarning("包裹创建被拒绝：{ErrorMessage}", errorMessage);
            return OperationResult.Failure(errorMessage);
        }
    }
}
