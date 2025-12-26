using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// IO 联动驱动装饰器，支持延迟执行和状态检查。
/// </summary>
/// <remarks>
/// 此装饰器为 IO 联动驱动添加延迟执行功能：
/// - 如果 IO 点配置了延迟时间（DelaySeconds > 0），则等待指定时间后再执行
/// - 延迟期间监控系统状态，如果状态发生变化（如急停/停止），则取消执行
/// - 优先级：急停 > 停止 > 运行
/// </remarks>
public class DelayedIoLinkageDriverDecorator : IIoLinkageDriver
{
    private readonly IIoLinkageDriver _innerDriver;
    private readonly ISystemStateManager _systemStateManager;
    private readonly ILogger<DelayedIoLinkageDriverDecorator> _logger;

    public DelayedIoLinkageDriverDecorator(
        IIoLinkageDriver innerDriver,
        ISystemStateManager systemStateManager,
        ILogger<DelayedIoLinkageDriverDecorator> logger)
    {
        _innerDriver = innerDriver ?? throw new ArgumentNullException(nameof(innerDriver));
        _systemStateManager = systemStateManager ?? throw new ArgumentNullException(nameof(systemStateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task SetIoPointAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken = default)
    {
        // 如果配置了延迟，先等待并检查状态
        if (ioPoint.DelayMilliseconds > 0)
        {
            var originalState = _systemStateManager.CurrentState;
            _logger.LogInformation(
                "IO 联动延迟执行: IO {BitNumber} 将延迟 {DelayMilliseconds} 毫秒执行，当前系统状态: {SystemState}",
                ioPoint.BitNumber,
                ioPoint.DelayMilliseconds,
                originalState);

            await Task.Delay(ioPoint.DelayMilliseconds, cancellationToken);

            // 延迟后检查系统状态是否改变
            var currentState = _systemStateManager.CurrentState;
            if (!ShouldExecuteIo(originalState, currentState))
            {
                _logger.LogWarning(
                    "IO 联动延迟执行被取消: IO {BitNumber}，系统状态已从 {OriginalState} 变更为 {CurrentState}",
                    ioPoint.BitNumber,
                    originalState,
                    currentState);
                return;
            }

            _logger.LogInformation(
                "IO 联动延迟执行继续: IO {BitNumber}，延迟 {DelayMilliseconds} 毫秒后系统状态保持为 {SystemState}",
                ioPoint.BitNumber,
                ioPoint.DelayMilliseconds,
                currentState);
        }

        // 委托给内部驱动执行
        await _innerDriver.SetIoPointAsync(ioPoint, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SetIoPointsAsync(IEnumerable<IoLinkagePoint> ioPoints, CancellationToken cancellationToken = default)
    {
        // 并行执行所有IO点（包括延迟的）
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
    /// 优先级：EmergencyStop > Ready/Paused/Faulted > Running
    /// - 如果当前状态是急停，且原状态不是急停，则取消执行
    /// - 如果当前状态是停止相关状态（Ready/Paused/Faulted），且原状态是运行，则取消执行
    /// - 其他情况允许执行
    /// </remarks>
    private static bool ShouldExecuteIo(SystemState originalState, SystemState currentState)
    {
        // 如果状态没有变化，允许执行
        if (originalState == currentState)
        {
            return true;
        }

        // 优先级检查：EmergencyStop > Ready/Paused/Faulted > Running
        
        // 如果当前是急停，且原来不是急停，取消执行（急停优先级最高）
        if (currentState == SystemState.EmergencyStop && originalState != SystemState.EmergencyStop)
        {
            return false;
        }

        // 如果当前是停止相关状态（Ready/Paused/Faulted），且原来是运行，取消执行
        if ((currentState == SystemState.Ready || currentState == SystemState.Paused || currentState == SystemState.Faulted)
            && originalState == SystemState.Running)
        {
            return false;
        }

        // 其他情况允许执行
        return true;
    }
}
