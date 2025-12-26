using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// IO 联动驱动装饰器，支持延迟执行和状态检查。
/// </summary>
/// <remarks>
/// 此装饰器为 IO 联动驱动添加延迟执行功能：
/// - 如果 IO 点配置了延迟时间（DelayMilliseconds > 0），则等待指定时间后再执行
/// - 延迟期间监控系统状态，如果状态发生变化（如急停/停止），则取消执行
/// - 优先级：急停 > 停止 > 运行
/// - 使用 ISafeExecutionService 确保异常不会导致进程崩溃
/// </remarks>
public class DelayedIoLinkageDriverDecorator : IIoLinkageDriver
{
    private readonly IIoLinkageDriver _innerDriver;
    private readonly ISystemStateManager _systemStateManager;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<DelayedIoLinkageDriverDecorator> _logger;
    private readonly SemaphoreSlim _executionSemaphore;
    private const int MaxConcurrentDelayedOperations = 50;

    public DelayedIoLinkageDriverDecorator(
        IIoLinkageDriver innerDriver,
        ISystemStateManager systemStateManager,
        ISafeExecutionService safeExecutor,
        ILogger<DelayedIoLinkageDriverDecorator> logger)
    {
        _innerDriver = innerDriver ?? throw new ArgumentNullException(nameof(innerDriver));
        _systemStateManager = systemStateManager ?? throw new ArgumentNullException(nameof(systemStateManager));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executionSemaphore = new SemaphoreSlim(MaxConcurrentDelayedOperations, MaxConcurrentDelayedOperations);
    }

    /// <inheritdoc/>
    public async Task SetIoPointAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken = default)
    {
        // 如果配置了延迟，使用信号量限制并发和 SafeExecutionService 包装
        if (ioPoint.DelayMilliseconds > 0)
        {
            await _executionSemaphore.WaitAsync(cancellationToken);
            try
            {
                await _safeExecutor.ExecuteAsync(
                    async () => await ExecuteDelayedIoAsync(ioPoint, cancellationToken),
                    $"DelayedIO-{ioPoint.BitNumber}",
                    cancellationToken);
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }
        else
        {
            // 无延迟，直接执行
            await _innerDriver.SetIoPointAsync(ioPoint, cancellationToken);
        }
    }

    /// <summary>
    /// 执行延迟IO操作的核心逻辑
    /// </summary>
    private async Task ExecuteDelayedIoAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken)
    {
        var originalState = _systemStateManager.CurrentState;
        _logger.LogInformation(
            "IO 联动延迟执行: IO {BitNumber} 将延迟 {DelayMilliseconds} 毫秒执行，当前系统状态: {SystemState}",
            ioPoint.BitNumber,
            ioPoint.DelayMilliseconds,
            originalState);

        try
        {
            await Task.Delay(ioPoint.DelayMilliseconds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "IO 联动延迟执行被取消令牌中止: IO {BitNumber}",
                ioPoint.BitNumber);
            throw; // 重新抛出以便调用者知道操作被取消
        }

        // 延迟后立即检查系统状态（最小化竞态窗口）
        var currentState = _systemStateManager.CurrentState;
        if (!ShouldExecuteIo(originalState, currentState))
        {
            _logger.LogWarning(
                "IO 联动延迟执行被状态变化取消: IO {BitNumber}，系统状态已从 {OriginalState} 变更为 {CurrentState}",
                ioPoint.BitNumber,
                originalState,
                currentState);
            return;
        }

        _logger.LogDebug(
            "IO 联动延迟执行继续: IO {BitNumber}，延迟 {DelayMilliseconds} 毫秒后系统状态为 {SystemState}",
            ioPoint.BitNumber,
            ioPoint.DelayMilliseconds,
            currentState);

        // 执行前再次检查状态，进一步减小竞态窗口
        var finalState = _systemStateManager.CurrentState;
        if (!ShouldExecuteIo(originalState, finalState))
        {
            _logger.LogWarning(
                "IO 联动延迟执行在最终检查时被取消: IO {BitNumber}，系统状态已从 {OriginalState} 变更为 {FinalState}",
                ioPoint.BitNumber,
                originalState,
                finalState);
            return;
        }

        // 委托给内部驱动执行
        await _innerDriver.SetIoPointAsync(ioPoint, cancellationToken);
        
        _logger.LogInformation(
            "IO 联动延迟执行成功: IO {BitNumber}，延迟 {DelayMilliseconds} 毫秒后执行完成",
            ioPoint.BitNumber,
            ioPoint.DelayMilliseconds);
    }

    /// <inheritdoc/>
    public async Task SetIoPointsAsync(IEnumerable<IoLinkagePoint> ioPoints, CancellationToken cancellationToken = default)
    {
        // 使用信号量限制并发执行，避免任务爆炸
        // 立即执行的IO点不消耗信号量，只有延迟的IO点才会限制并发
        var tasks = ioPoints.Select(ioPoint => SetIoPointAsync(ioPoint, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc/>
    public Task<bool> ReadIoPointAsync(int bitNumber, CancellationToken cancellationToken = default)
    {
        // 读取操作直接委托，不需要延迟
        return _innerDriver.ReadIoPointAsync(bitNumber, cancellationToken);
    }

    /// <inheritdoc/>
    public Task ResetAllIoPointsAsync(CancellationToken cancellationToken = default)
    {
        // 复位操作直接委托，不需要延迟
        return _innerDriver.ResetAllIoPointsAsync(cancellationToken);
    }

    /// <summary>
    /// 判断是否应该执行IO操作
    /// </summary>
    /// <param name="originalState">延迟前的系统状态</param>
    /// <param name="currentState">当前系统状态</param>
    /// <returns>如果应该执行返回true，否则返回false</returns>
    /// <remarks>
    /// 优先级：EmergencyStop > Faulted/Paused > Ready > Running > Booting
    /// 
    /// 规则：
    /// 1. 如果当前状态是 EmergencyStop，且原状态不是 EmergencyStop，则取消执行（急停优先级最高）
    /// 2. 如果当前状态优先级高于原状态，则取消执行（防止低优先级状态的IO在高优先级状态执行）
    /// 3. 如果当前状态与原状态相同，允许执行
    /// 4. 如果当前状态优先级低于或等于原状态，允许执行
    /// 
    /// 状态优先级定义（从高到低）：
    /// - EmergencyStop: 5 (最高，紧急停止）
    /// - Faulted: 4 (故障状态）
    /// - Paused: 3 (暂停状态）
    /// - Ready: 2 (就绪状态）
    /// - Running: 1 (运行状态）
    /// - Booting: 0 (启动中，最低）
    /// </remarks>
    private static bool ShouldExecuteIo(SystemState originalState, SystemState currentState)
    {
        // 如果状态没有变化，允许执行
        if (originalState == currentState)
        {
            return true;
        }

        // 获取状态优先级
        int originalPriority = GetStatePriority(originalState);
        int currentPriority = GetStatePriority(currentState);

        // 如果当前状态优先级高于原状态，取消执行
        // 例如：Running → EmergencyStop, Running → Faulted, Running → Paused, Running → Ready
        if (currentPriority > originalPriority)
        {
            return false;
        }

        // 其他情况允许执行（包括优先级降低或相同的情况）
        return true;
    }

    /// <summary>
    /// 获取系统状态的优先级
    /// </summary>
    /// <param name="state">系统状态</param>
    /// <returns>优先级值（数值越大优先级越高）</returns>
    private static int GetStatePriority(SystemState state)
    {
        return state switch
        {
            SystemState.EmergencyStop => 5,  // 最高优先级
            SystemState.Faulted => 4,
            SystemState.Paused => 3,
            SystemState.Ready => 2,
            SystemState.Running => 1,
            SystemState.Booting => 0,        // 最低优先级
            _ => 0
        };
    }
}
