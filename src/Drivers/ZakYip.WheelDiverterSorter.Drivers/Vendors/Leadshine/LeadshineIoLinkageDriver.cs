using Microsoft.Extensions.Logging;
using csLTDMC;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

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
    
    private readonly ILogger<LeadshineIoLinkageDriver> _logger;
    private readonly IEmcController _emcController;

    public LeadshineIoLinkageDriver(
        ILogger<LeadshineIoLinkageDriver> logger,
        IEmcController emcController)
    {
        _logger = logger;
        _emcController = emcController;
    }

    /// <inheritdoc/>
    public Task SetIoPointAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken = default)
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
        
        _logger.LogInformation("批量设置 IO 点，共 {Count} 个", ioPointsList.Count);

        foreach (var ioPoint in ioPointsList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SetIoPointAsync(ioPoint, cancellationToken);
        }

        _logger.LogInformation("批量设置 IO 点完成");
    }

    /// <inheritdoc/>
    public Task<bool> ReadIoPointAsync(int bitNumber, CancellationToken cancellationToken = default)
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

            // 记录详细的调用信息以便诊断
            _logger.LogDebug(
                "准备调用 dmc_read_inbit: CardNo={CardNo}, BitNumber={BitNumber}",
                _emcController.CardNo,
                bitNumber);

            // 调用雷赛 API 读取输入端口
            short result = LTDMC.dmc_read_inbit(
                _emcController.CardNo,
                (ushort)bitNumber);

            if (result < 0)
            {
                _logger.LogError(
                    "读取 IO 点失败: CardNo={CardNo}, BitNumber={BitNumber}, ErrorCode={ErrorCode} | " +
                    "提示：负数错误码表示读取失败。请检查：1) CardNo 是否正确；2) BitNumber 是否在有效范围内；3) 控制卡是否已正确初始化",
                    _emcController.CardNo,
                    bitNumber,
                    result);
                throw new InvalidOperationException(
                    $"读取 IO 点 {bitNumber} 失败，错误码: {result}。CardNo={_emcController.CardNo}");
            }

            var state = result == 1;
            _logger.LogDebug(
                "读取 IO 点成功: CardNo={CardNo}, BitNumber={BitNumber}, State={State}, RawResult={RawResult}",
                _emcController.CardNo,
                bitNumber,
                state,
                result);
            return Task.FromResult(state);
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
