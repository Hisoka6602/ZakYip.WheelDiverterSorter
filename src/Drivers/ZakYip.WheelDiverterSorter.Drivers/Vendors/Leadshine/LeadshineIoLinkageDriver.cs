using csLTDMC;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛 IO 联动驱动实现。
/// 通过雷赛控制卡控制中段皮带等设备的 IO 联动。
/// </summary>
public class LeadshineIoLinkageDriver : IIoLinkageDriver
{
    /// <summary>
    /// 雷赛控制卡错误码：控制卡未初始化
    /// </summary>
    private const short ErrorCodeCardNotInitialized = 9;

    /// <summary>
    /// 设置 IO 点时的最大重试次数（针对瞬时错误）
    /// </summary>
    private const int MaxSetIoRetries = 3;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    private const int RetryDelayMs = 50;

    private readonly ILogger<LeadshineIoLinkageDriver> _logger;
    private readonly IEmcController _emcController;
    private readonly IInputPort _inputPort;

    public LeadshineIoLinkageDriver(
        ILogger<LeadshineIoLinkageDriver> logger,
        IEmcController emcController,
        IInputPort inputPort)
    {
        _logger = logger;
        _emcController = emcController;
        _inputPort = inputPort ?? throw new ArgumentNullException(nameof(inputPort));
    }

    /// <inheritdoc/>
    public async Task SetIoPointAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        Exception? lastException = null;

        // TD-IOLINKAGE-002: 添加重试逻辑处理瞬时错误
        // 原因：网络通信或硬件瞬时故障可能导致单次操作失败，但重试可能成功
        while (attempt < MaxSetIoRetries)
        {
            attempt++;
            
            try
            {
                await SetIoPointInternalAsync(ioPoint, cancellationToken);
                
                // 成功则返回
                if (attempt > 1)
                {
                    _logger.LogInformation(
                        "设置 IO 点成功（第 {Attempt} 次尝试）: BitNumber={BitNumber}",
                        attempt,
                        ioPoint.BitNumber);
                }
                return;
            }
            catch (InvalidOperationException ex)
            {
                lastException = ex;
                
                // 如果是最后一次尝试，抛出异常
                if (attempt >= MaxSetIoRetries)
                {
                    _logger.LogError(
                        ex,
                        "设置 IO 点失败（已重试 {Attempts} 次）: BitNumber={BitNumber}",
                        MaxSetIoRetries,
                        ioPoint.BitNumber);
                    throw;
                }
                
                // 否则记录警告并重试
                _logger.LogWarning(
                    ex,
                    "设置 IO 点失败（第 {Attempt}/{MaxRetries} 次尝试），{DelayMs}ms 后重试: BitNumber={BitNumber}",
                    attempt,
                    MaxSetIoRetries,
                    RetryDelayMs,
                    ioPoint.BitNumber);
                
                await Task.Delay(RetryDelayMs, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 对于非预期异常，记录并立即抛出
                _logger.LogError(ex, "设置 IO 点时发生非预期异常: BitNumber={BitNumber}", ioPoint.BitNumber);
                throw;
            }
        }

        // 理论上不会到达这里，但为了安全起见
        throw lastException ?? new InvalidOperationException($"设置 IO 点 {ioPoint.BitNumber} 失败");
    }

    /// <summary>
    /// 内部实现：设置单个 IO 点（不包含重试逻辑）
    /// </summary>
    private Task SetIoPointInternalAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken)
    {
        try
        {
            // TD-044: 检查 EMC 控制器是否已初始化
            if (!_emcController.IsAvailable())
            {
                var errorMessage = $"无法设置 IO 点 {ioPoint.BitNumber}：EMC 控制器未初始化或不可用";
                _logger.LogError(
                    "设置 IO 点失败: BitNumber={BitNumber}, Level={Level}, Reason={Reason}",
                    ioPoint.BitNumber,
                    ioPoint.Level,
                    "EMC控制器未初始化");
                throw new InvalidOperationException(errorMessage);
            }

            // 根据 TriggerLevel 确定输出电平
            // ActiveHigh = 高电平 = 1
            // ActiveLow = 低电平 = 0
            ushort outputValue = ioPoint.Level == TriggerLevel.ActiveHigh ? (ushort)1 : (ushort)0;

            // 记录详细的调用信息以便诊断
            _logger.LogDebug(
                "准备调用 dmc_write_outbit: CardNo={CardNo}, BitNumber={BitNumber}, OutputValue={OutputValue}",
                _emcController.CardNo,
                ioPoint.BitNumber,
                outputValue);

            // 调用雷赛 API 设置输出端口
            short result = LTDMC.dmc_write_outbit(
                _emcController.CardNo,
                (ushort)ioPoint.BitNumber,
                outputValue);

            if (result != 0)
            {
                // 特殊处理错误码9：控制卡未初始化
                if (result == ErrorCodeCardNotInitialized)
                {
                    _logger.LogError(
                        "【严重】设置 IO 点失败：控制卡未初始化 | CardNo={CardNo}, BitNumber={BitNumber}, Level={Level}, OutputValue={OutputValue}, ErrorCode={ErrorCode} | " +
                        "可能原因：1) dmc_board_close() 被意外调用导致控制卡关闭；2) 控制卡硬件断开连接；3) 多进程/线程竞争导致初始化状态不一致。" +
                        "建议：检查是否有其他代码调用了 dmc_board_close()，或者控制卡是否被其他程序占用",
                        _emcController.CardNo,
                        ioPoint.BitNumber,
                        ioPoint.Level,
                        outputValue,
                        result);
                }
                else
                {
                    _logger.LogError(
                        "设置 IO 点失败: CardNo={CardNo}, BitNumber={BitNumber}, Level={Level}, OutputValue={OutputValue}, ErrorCode={ErrorCode}",
                        _emcController.CardNo,
                        ioPoint.BitNumber,
                        ioPoint.Level,
                        outputValue,
                        result);
                }

                throw new InvalidOperationException(
                    $"设置 IO 点 {ioPoint.BitNumber} 失败，错误码: {result}。CardNo={_emcController.CardNo}");
            }

            _logger.LogDebug(
                "成功设置 IO 点: CardNo={CardNo}, BitNumber={BitNumber}, Level={Level}, OutputValue={OutputValue}",
                _emcController.CardNo,
                ioPoint.BitNumber,
                ioPoint.Level,
                outputValue);

            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "设置 IO 点时发生异常: BitNumber={BitNumber}", ioPoint.BitNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SetIoPointsAsync(IEnumerable<IoLinkagePoint> ioPoints, CancellationToken cancellationToken = default)
    {
        var ioPointsList = ioPoints.ToList();
        var errors = new List<(int BitNumber, Exception Exception)>();
        int successCount = 0;

        _logger.LogInformation("批量设置 IO 点，共 {Count} 个", ioPointsList.Count);

        // TD-IOLINKAGE-001: 改进批量设置逻辑 - 即使部分失败也继续处理其他 IO 点
        // 原因：避免一个 IO 点失败导致整个批次失败，造成设备状态不一致
        foreach (var ioPoint in ioPointsList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                await SetIoPointAsync(ioPoint, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                // 记录错误但继续处理其他 IO 点
                _logger.LogWarning(
                    ex,
                    "设置 IO 点 {BitNumber} 失败，继续处理其他 IO 点（已成功: {SuccessCount}, 失败: {FailureCount}）",
                    ioPoint.BitNumber,
                    successCount,
                    errors.Count + 1);
                errors.Add((ioPoint.BitNumber, ex));
            }
        }

        // 如果有错误，记录汇总信息
        if (errors.Count > 0)
        {
            var failedBits = string.Join(", ", errors.Select(e => e.BitNumber));
            _logger.LogError(
                "批量设置 IO 点部分失败: 成功 {SuccessCount}/{TotalCount}, 失败 IO 点: {FailedBits}",
                successCount,
                ioPointsList.Count,
                failedBits);

            // 抛出聚合异常，包含所有失败的详细信息
            throw new AggregateException(
                $"批量设置 IO 点失败: 成功 {successCount}/{ioPointsList.Count}, 失败 IO 点: {failedBits}",
                errors.Select(e => e.Exception));
        }

        _logger.LogInformation("批量设置 IO 点全部成功，共 {Count} 个", successCount);
    }

    /// <inheritdoc/>
    public async Task<bool> ReadIoPointAsync(int bitNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            // TD-044: 检查 EMC 控制器是否已初始化
            if (!_emcController.IsAvailable())
            {
                var errorMessage = $"无法读取 IO 点 {bitNumber}：EMC 控制器未初始化或不可用";
                _logger.LogError(
                    "读取 IO 点失败: BitNumber={BitNumber}, Reason={Reason}",
                    bitNumber,
                    "EMC控制器未初始化");
                throw new InvalidOperationException(errorMessage);
            }

            // 从IO状态缓存读取（非阻塞）
            bool state = await _inputPort.ReadAsync(bitNumber);
            
            _logger.LogDebug(
                "读取 IO 点成功（从缓存）: BitNumber={BitNumber}, State={State}",
                bitNumber,
                state);
            return state;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "读取 IO 点时发生异常: BitNumber={BitNumber}", bitNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task ResetAllIoPointsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("复位所有 IO 联动点（此操作在雷赛驱动中为空实现）");
        // 注意: 雷赛控制器通常不需要显式复位所有 IO
        // IO 状态由硬件和配置决定
        // 如需复位特定 IO，应通过 SetIoPointsAsync 显式设置
        return Task.CompletedTask;
    }
}
