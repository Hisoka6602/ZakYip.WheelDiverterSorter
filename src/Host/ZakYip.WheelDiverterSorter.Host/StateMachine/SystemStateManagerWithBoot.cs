using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Execution.SelfTest;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Host.StateMachine;

/// <summary>
/// 系统状态管理器扩展 - 集成自检协调器
/// </summary>
/// <remarks>
/// 这个类扩展SystemStateManager，添加完整的BootAsync实现。
/// 使用装饰器模式避免修改原有SystemStateManager的逻辑。
/// </remarks>
public class SystemStateManagerWithBoot : ISystemStateManager
{
    private readonly SystemStateManager _inner;
    private readonly ISelfTestCoordinator? _coordinator;
    private readonly ISystemClock _clock;
    private readonly ILogger<SystemStateManagerWithBoot> _logger;

    public SystemStateManagerWithBoot(
        SystemStateManager inner,
        ISelfTestCoordinator? coordinator,
        ISystemClock clock,
        ILogger<SystemStateManagerWithBoot> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _coordinator = coordinator;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public SystemState CurrentState => _inner.CurrentState;
    
    /// <inheritdoc/>
    public event EventHandler<StateChangeEventArgs>? StateChanged
    {
        add => _inner.StateChanged += value;
        remove => _inner.StateChanged -= value;
    }

    /// <inheritdoc/>
    public SystemSelfTestReport? LastSelfTestReport => _inner.LastSelfTestReport;

    /// <inheritdoc/>
    public BootstrapStageInfo? CurrentBootstrapStage => _inner.CurrentBootstrapStage;

    /// <inheritdoc/>
    public IReadOnlyList<BootstrapStageInfo> GetBootstrapHistory(int count = 10)
        => _inner.GetBootstrapHistory(count);

    /// <inheritdoc/>
    public Task<StateChangeResult> ChangeStateAsync(SystemState targetState, CancellationToken cancellationToken = default)
        => _inner.ChangeStateAsync(targetState, cancellationToken);

    /// <inheritdoc/>
    public bool CanTransitionTo(SystemState targetState)
        => _inner.CanTransitionTo(targetState);

    /// <inheritdoc/>
    public IReadOnlyList<StateTransitionRecord> GetTransitionHistory(int count = 10)
        => _inner.GetTransitionHistory(count);

    /// <inheritdoc/>
    public async Task<SystemSelfTestReport> BootAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始系统启动自检流程...");

        // 设置状态为Booting
        await _inner.ChangeStateAsync(SystemState.Booting, cancellationToken);

        SystemSelfTestReport report;

        if (_coordinator == null)
        {
            _logger.LogWarning("自检协调器未配置，跳过自检，直接进入Ready状态");
            report = new SystemSelfTestReport
            {
                IsSuccess = true,
                Drivers = new List<DriverHealthStatus>().AsReadOnly(),
                Upstreams = new List<UpstreamHealthStatus>().AsReadOnly(),
                Config = new ConfigHealthStatus { IsValid = true, ErrorMessage = "自检协调器未配置" },
                PerformedAt = new DateTimeOffset(_clock.LocalNow)
            };
        }
        else
        {
            // 执行自检
            report = await _coordinator.RunAsync(cancellationToken);
        }

        // 根据自检结果切换状态
        if (report.IsSuccess)
        {
            _logger.LogInformation("自检通过，系统状态: Ready");
            await _inner.ChangeStateAsync(SystemState.Ready, cancellationToken);
        }
        else
        {
            _logger.LogError("自检失败，系统状态: Faulted");
            await _inner.ChangeStateAsync(SystemState.Faulted, cancellationToken);
        }

        return report;
    }
}
