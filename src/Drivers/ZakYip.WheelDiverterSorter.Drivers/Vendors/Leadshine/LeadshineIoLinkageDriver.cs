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
    public async Task SetIoPointAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken = default)
    {
        try
        {
            // 根据 TriggerLevel 确定输出电平
            // ActiveHigh = 高电平 = 1
            // ActiveLow = 低电平 = 0
            ushort outputValue = ioPoint.Level == TriggerLevel.ActiveHigh ? (ushort)1 : (ushort)0;

            // 调用雷赛 API 设置输出端口
            short result = LTDMC.dmc_write_outbit(
                _emcController.CardNo,
                (ushort)ioPoint.BitNumber,
                outputValue);

            if (result != 0)
            {
                _logger.LogError(
                    "设置 IO 点失败: BitNumber={BitNumber}, Level={Level}, ErrorCode={ErrorCode}",
                    ioPoint.BitNumber,
                    ioPoint.Level,
                    result);
                throw new InvalidOperationException(
                    $"设置 IO 点 {ioPoint.BitNumber} 失败，错误码: {result}");
            }

            _logger.LogDebug(
                "成功设置 IO 点: BitNumber={BitNumber}, Level={Level}",
                ioPoint.BitNumber,
                ioPoint.Level);

            await Task.CompletedTask;
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
            // 调用雷赛 API 读取输入端口
            short result = LTDMC.dmc_read_inbit(
                _emcController.CardNo,
                (ushort)bitNumber);

            if (result < 0)
            {
                _logger.LogError(
                    "读取 IO 点失败: BitNumber={BitNumber}, ErrorCode={ErrorCode}",
                    bitNumber,
                    result);
                throw new InvalidOperationException(
                    $"读取 IO 点 {bitNumber} 失败，错误码: {result}");
            }

            var state = result == 1;
            _logger.LogDebug("读取 IO 点: BitNumber={BitNumber}, State={State}", bitNumber, state);
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
