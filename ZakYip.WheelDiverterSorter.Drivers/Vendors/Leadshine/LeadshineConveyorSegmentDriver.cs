using Microsoft.Extensions.Logging;
using csLTDMC;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;


using ZakYip.WheelDiverterSorter.Core.LineModel.Segments;namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛（EMC）中段皮带段驱动实现。
/// 通过雷赛控制卡的数字量 IO 控制中段皮带的启停。
/// </summary>
public sealed class LeadshineConveyorSegmentDriver : IConveyorSegmentDriver
{
    private readonly ILogger<LeadshineConveyorSegmentDriver> _logger;
    private readonly IEmcController _emcController;

    public ConveyorIoMapping Mapping { get; }

    public LeadshineConveyorSegmentDriver(
        ConveyorIoMapping mapping,
        IEmcController emcController,
        ILogger<LeadshineConveyorSegmentDriver> logger)
    {
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _emcController = emcController ?? throw new ArgumentNullException(nameof(emcController));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task WriteStartSignalAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 启动信号：设置为高电平（1）
            short result = LTDMC.dmc_write_outbit(
                _emcController.CardNo,
                (ushort)Mapping.StartOutputChannel,
                1);

            if (result != 0)
            {
                var errorMsg = $"写入启动信号失败: 皮带段={Mapping.SegmentKey}, 点位={Mapping.StartOutputChannel}, 错误码={result}";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            _logger.LogInformation(
                "成功写入启动信号: 皮带段={SegmentKey}, 点位={Channel}",
                Mapping.SegmentKey,
                Mapping.StartOutputChannel);

            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "写入启动信号时发生异常: 皮带段={SegmentKey}", Mapping.SegmentKey);
            throw;
        }
    }

    public Task WriteStopSignalAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            int channel;
            if (Mapping.StopOutputChannel.HasValue)
            {
                // 如果有单独的停止输出点位，设置停止点位为高电平
                channel = Mapping.StopOutputChannel.Value;
                short result = LTDMC.dmc_write_outbit(
                    _emcController.CardNo,
                    (ushort)channel,
                    1);

                if (result != 0)
                {
                    var errorMsg = $"写入停止信号失败: 皮带段={Mapping.SegmentKey}, 点位={channel}, 错误码={result}";
                    _logger.LogError(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }
            }
            else
            {
                // 没有单独停止点位，将启动点位设为低电平（0）即停止
                channel = Mapping.StartOutputChannel;
                short result = LTDMC.dmc_write_outbit(
                    _emcController.CardNo,
                    (ushort)channel,
                    0);

                if (result != 0)
                {
                    var errorMsg = $"写入停止信号失败（清除启动信号）: 皮带段={Mapping.SegmentKey}, 点位={channel}, 错误码={result}";
                    _logger.LogError(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }
            }

            _logger.LogInformation(
                "成功写入停止信号: 皮带段={SegmentKey}, 点位={Channel}",
                Mapping.SegmentKey,
                channel);

            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "写入停止信号时发生异常: 皮带段={SegmentKey}", Mapping.SegmentKey);
            throw;
        }
    }

    public Task<bool> ReadFaultStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!Mapping.FaultInputChannel.HasValue)
        {
            // 未配置故障输入点位，默认无故障
            return Task.FromResult(false);
        }

        try
        {
            short result = LTDMC.dmc_read_inbit(
                _emcController.CardNo,
                (ushort)Mapping.FaultInputChannel.Value);

            if (result < 0)
            {
                _logger.LogWarning(
                    "读取故障状态失败: 皮带段={SegmentKey}, 点位={Channel}, 错误码={ErrorCode}",
                    Mapping.SegmentKey,
                    Mapping.FaultInputChannel.Value,
                    result);
                // 读取失败时假定无故障，避免误报
                return Task.FromResult(false);
            }

            // 通常故障信号为高电平（1）表示故障
            bool hasFault = result == 1;
            if (hasFault)
            {
                _logger.LogWarning(
                    "检测到皮带段故障: 皮带段={SegmentKey}, 点位={Channel}",
                    Mapping.SegmentKey,
                    Mapping.FaultInputChannel.Value);
            }

            return Task.FromResult(hasFault);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取故障状态时发生异常: 皮带段={SegmentKey}", Mapping.SegmentKey);
            // 异常时假定无故障
            return Task.FromResult(false);
        }
    }

    public Task<bool> ReadRunningStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!Mapping.RunningInputChannel.HasValue)
        {
            // 未配置运行反馈点位，默认为未运行
            return Task.FromResult(false);
        }

        try
        {
            short result = LTDMC.dmc_read_inbit(
                _emcController.CardNo,
                (ushort)Mapping.RunningInputChannel.Value);

            if (result < 0)
            {
                _logger.LogWarning(
                    "读取运行状态失败: 皮带段={SegmentKey}, 点位={Channel}, 错误码={ErrorCode}",
                    Mapping.SegmentKey,
                    Mapping.RunningInputChannel.Value,
                    result);
                return Task.FromResult(false);
            }

            // 通常运行反馈为高电平（1）表示正在运行
            bool isRunning = result == 1;
            return Task.FromResult(isRunning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取运行状态时发生异常: 皮带段={SegmentKey}", Mapping.SegmentKey);
            return Task.FromResult(false);
        }
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 复位：清除启动信号
            short result = LTDMC.dmc_write_outbit(
                _emcController.CardNo,
                (ushort)Mapping.StartOutputChannel,
                0);

            if (result != 0)
            {
                _logger.LogWarning(
                    "复位启动信号失败: 皮带段={SegmentKey}, 点位={Channel}, 错误码={ErrorCode}",
                    Mapping.SegmentKey,
                    Mapping.StartOutputChannel,
                    result);
            }

            // 如果有单独停止点位，也清除停止信号
            if (Mapping.StopOutputChannel.HasValue)
            {
                result = LTDMC.dmc_write_outbit(
                    _emcController.CardNo,
                    (ushort)Mapping.StopOutputChannel.Value,
                    0);

                if (result != 0)
                {
                    _logger.LogWarning(
                        "复位停止信号失败: 皮带段={SegmentKey}, 点位={Channel}, 错误码={ErrorCode}",
                        Mapping.SegmentKey,
                        Mapping.StopOutputChannel.Value,
                        result);
                }
            }

            _logger.LogInformation("复位中段皮带段驱动: 皮带段={SegmentKey}", Mapping.SegmentKey);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "复位驱动时发生异常: 皮带段={SegmentKey}", Mapping.SegmentKey);
            throw;
        }
    }
}
