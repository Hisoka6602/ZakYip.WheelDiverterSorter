using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using csLTDMC;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 基于雷赛（Leadshine）运动控制器的摆轮驱动器实现
/// </summary>
/// <remarks>
/// 直接实现 <see cref="IWheelDiverterDriver"/> 接口，
/// 内部使用 IO 输出端口控制摆轮方向，通过二进制编码映射方向到输出位。
/// </remarks>
public class LeadshineWheelDiverterDriver : IWheelDiverterDriver
{
    private readonly ILogger<LeadshineWheelDiverterDriver> _logger;
    private readonly ushort _cardNo;
    private readonly LeadshineDiverterConfig _config;
    private readonly IEmcController _emcController;
    private string _currentStatus = "未知";

    /// <inheritdoc/>
    public string DiverterId => _config.DiverterId.ToString();

    /// <summary>
    /// 初始化雷赛摆轮驱动器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">控制器卡号</param>
    /// <param name="config">摆轮配置</param>
    /// <param name="emcController">EMC 控制器实例</param>
    public LeadshineWheelDiverterDriver(
        ILogger<LeadshineWheelDiverterDriver> logger,
        ushort cardNo,
        LeadshineDiverterConfig config,
        IEmcController emcController)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cardNo = cardNo;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _emcController = emcController ?? throw new ArgumentNullException(nameof(emcController));
        
        _logger.LogInformation(
            "已初始化雷赛摆轮驱动器 {DiverterId}，卡号={CardNo}，输出起始位={StartBit}",
            DiverterId, _cardNo, _config.OutputStartBit);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TurnLeftAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[摆轮通信-发送] 摆轮 {DiverterId} 执行左转", DiverterId);
        
        var result = await SetDirectionInternalAsync(DiverterDirection.Left, cancellationToken);
        if (result)
        {
            _currentStatus = "左转";
            _logger.LogInformation(
                "[摆轮通信-发送完成] 摆轮 {DiverterId} 左转成功 | 当前状态={Status}",
                DiverterId, _currentStatus);
        }
        else
        {
            _logger.LogWarning("[摆轮通信-发送] 摆轮 {DiverterId} 左转失败", DiverterId);
        }
        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TurnRightAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[摆轮通信-发送] 摆轮 {DiverterId} 执行右转", DiverterId);
        
        var result = await SetDirectionInternalAsync(DiverterDirection.Right, cancellationToken);
        if (result)
        {
            _currentStatus = "右转";
            _logger.LogInformation(
                "[摆轮通信-发送完成] 摆轮 {DiverterId} 右转成功 | 当前状态={Status}",
                DiverterId, _currentStatus);
        }
        else
        {
            _logger.LogWarning("[摆轮通信-发送] 摆轮 {DiverterId} 右转失败", DiverterId);
        }
        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> PassThroughAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[摆轮通信-发送] 摆轮 {DiverterId} 执行直通", DiverterId);
        
        var result = await SetDirectionInternalAsync(DiverterDirection.Straight, cancellationToken);
        if (result)
        {
            _currentStatus = "直通";
            _logger.LogInformation(
                "[摆轮通信-发送完成] 摆轮 {DiverterId} 直通成功 | 当前状态={Status}",
                DiverterId, _currentStatus);
        }
        else
        {
            _logger.LogWarning("[摆轮通信-发送] 摆轮 {DiverterId} 直通失败", DiverterId);
        }
        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[摆轮通信-发送] 摆轮 {DiverterId} 执行停止", DiverterId);
        
        var result = await SetDirectionInternalAsync(DiverterDirection.Straight, cancellationToken);
        if (result)
        {
            _currentStatus = "已停止";
            _logger.LogInformation(
                "[摆轮通信-发送完成] 摆轮 {DiverterId} 停止成功 | 当前状态={Status}",
                DiverterId, _currentStatus);
        }
        else
        {
            _logger.LogWarning("[摆轮通信-发送] 摆轮 {DiverterId} 停止失败", DiverterId);
        }
        return result;
    }

    /// <inheritdoc/>
    public ValueTask<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        // Leadshine driver doesn't support Run command - this is specific to ShuDiNiao
        _logger.LogWarning(
            "摆轮 {DiverterId} (Leadshine) 不支持 Run 命令",
            DiverterId);
        throw new NotSupportedException("Leadshine 摆轮驱动器不支持 Run 命令");
    }

    /// <inheritdoc/>
    public ValueTask<string> GetStatusAsync()
    {
        return ValueTask.FromResult(_currentStatus);
    }

    /// <summary>
    /// 内部方法：设置摆轮方向
    /// </summary>
    private async Task<bool> SetDirectionInternalAsync(DiverterDirection direction, CancellationToken cancellationToken)
    {
        try
        {
            // 检查 EMC 控制器是否已初始化
            if (!_emcController.IsAvailable())
            {
                _logger.LogError(
                    "[摆轮通信-发送] 摆轮 {DiverterId} 无法设置方向：EMC 控制器未初始化或不可用 | 目标方向={Direction} | 卡号={CardNo}",
                    DiverterId, direction, _cardNo);
                return false;
            }

            _logger.LogInformation(
                "[摆轮通信-发送] 摆轮 {DiverterId} 开始设置方向 | 目标方向={Direction} | 卡号={CardNo}",
                DiverterId, direction, _cardNo);

            var outputBits = MapDirectionToOutputBits(direction);
            
            foreach (var (bitIndex, value) in outputBits)
            {
                var result = LTDMC.dmc_write_outbit(_cardNo, (ushort)bitIndex, (ushort)(value ? 1 : 0));
                
                _logger.LogInformation(
                    "[摆轮通信-IO写入] 摆轮 {DiverterId} 写入输出位 | 位索引={BitIndex} | 值={Value} | 返回码={ResultCode}",
                    DiverterId, bitIndex, value ? 1 : 0, result);
                
                if (result != 0)
                {
                    _logger.LogError(
                        "[摆轮通信-IO写入] 摆轮 {DiverterId} 写入输出位失败 | 位索引={BitIndex} | 错误码={ErrorCode} | 提示：ErrorCode=9 表示控制卡未初始化",
                        DiverterId, bitIndex, result);
                    return false;
                }
            }

            await Task.Delay(1, cancellationToken);

            _logger.LogInformation(
                "[摆轮通信-发送完成] 摆轮 {DiverterId} 方向设置成功 | 目标方向={Direction}",
                DiverterId, direction);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[摆轮通信-发送] 摆轮 {DiverterId} 设置方向失败 | 目标方向={Direction}",
                DiverterId, direction);
            return false;
        }
    }

    /// <summary>
    /// 将方向映射到输出位组合
    /// </summary>
    /// <remarks>
    /// 使用二进制编码方式：
    /// - Straight (直通) = 00 (bit0=0, bit1=0)
    /// - Left (左转)     = 01 (bit0=0, bit1=1)
    /// - Right (右转)    = 10 (bit0=1, bit1=0)
    /// </remarks>
    private List<(int bitIndex, bool value)> MapDirectionToOutputBits(DiverterDirection direction)
    {
        var bits = new List<(int, bool)>();
        int startBit = _config.OutputStartBit;
        
        switch (direction)
        {
            case DiverterDirection.Straight:
                bits.Add((startBit, false));
                bits.Add((startBit + 1, false));
                break;
            case DiverterDirection.Left:
                bits.Add((startBit, false));
                bits.Add((startBit + 1, true));
                break;
            case DiverterDirection.Right:
                bits.Add((startBit, true));
                bits.Add((startBit + 1, false));
                break;
            default:
                _logger.LogWarning("不支持的方向 {Direction}，使用直通", direction);
                bits.Add((startBit, false));
                bits.Add((startBit + 1, false));
                break;
        }
        
        return bits;
    }
}
