using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using csLTDMC;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 基于雷赛（Leadshine）运动控制器的摆轮驱动器实现
/// </summary>
/// <remarks>
/// 直接实现 <see cref="IWheelDiverterDriver"/> 接口，
/// 内部使用 IO 输出端口控制摆轮角度，将方向操作映射为角度操作。
/// </remarks>
public class LeadshineWheelDiverterDriver : IWheelDiverterDriver
{
    private readonly ILogger<LeadshineWheelDiverterDriver> _logger;
    private readonly ushort _cardNo;
    private readonly LeadshineDiverterConfig _config;
    private readonly IEmcController _emcController;
    private string _currentStatus = "未知";
    
    /// <summary>
    /// 继电器通道映射配置 - 角度定义
    /// </summary>
    private const int LeftTurnAngle = 45;
    private const int RightTurnAngle = -45;
    private const int PassThroughAngle = 0;
    private const int StopAngle = 0;

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
            "已初始化雷赛摆轮驱动器 {DiverterId}，卡号={CardNo}，左转={LeftAngle}°，右转={RightAngle}°，直通={PassAngle}°",
            DiverterId, _cardNo, LeftTurnAngle, RightTurnAngle, PassThroughAngle);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TurnLeftAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[摆轮通信-发送] 摆轮 {DiverterId} 执行左转 | 目标角度={Angle}度",
            DiverterId, LeftTurnAngle);
        
        var result = await SetAngleInternalAsync(LeftTurnAngle, cancellationToken);
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
        _logger.LogInformation(
            "[摆轮通信-发送] 摆轮 {DiverterId} 执行右转 | 目标角度={Angle}度",
            DiverterId, RightTurnAngle);
        
        var result = await SetAngleInternalAsync(RightTurnAngle, cancellationToken);
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
        _logger.LogInformation(
            "[摆轮通信-发送] 摆轮 {DiverterId} 执行直通 | 目标角度={Angle}度",
            DiverterId, PassThroughAngle);
        
        var result = await SetAngleInternalAsync(PassThroughAngle, cancellationToken);
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
        _logger.LogInformation(
            "[摆轮通信-发送] 摆轮 {DiverterId} 执行停止 | 目标角度={Angle}度",
            DiverterId, StopAngle);
        
        var result = await SetAngleInternalAsync(StopAngle, cancellationToken);
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
    /// 内部方法：设置摆轮角度
    /// </summary>
    private async Task<bool> SetAngleInternalAsync(int angle, CancellationToken cancellationToken)
    {
        try
        {
            // 检查 EMC 控制器是否已初始化
            if (!_emcController.IsAvailable())
            {
                _logger.LogError(
                    "[摆轮通信-发送] 摆轮 {DiverterId} 无法设置角度：EMC 控制器未初始化或不可用 | 目标角度={Angle}度 | 卡号={CardNo}",
                    DiverterId, angle, _cardNo);
                return false;
            }

            _logger.LogInformation(
                "[摆轮通信-发送] 摆轮 {DiverterId} 开始设置角度 | 目标角度={Angle}度 | 卡号={CardNo}",
                DiverterId, angle, _cardNo);

            var outputBits = MapAngleToOutputBits(angle);
            
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

            await Task.Delay(100, cancellationToken);

            _logger.LogInformation(
                "[摆轮通信-发送完成] 摆轮 {DiverterId} 角度设置成功 | 目标角度={Angle}度",
                DiverterId, angle);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[摆轮通信-发送] 摆轮 {DiverterId} 设置角度失败 | 目标角度={Angle}度",
                DiverterId, angle);
            return false;
        }
    }

    /// <summary>
    /// 将角度映射到输出位组合
    /// </summary>
    private List<(int bitIndex, bool value)> MapAngleToOutputBits(int angle)
    {
        var bits = new List<(int, bool)>();
        int startBit = _config.OutputStartBit;
        
        // 使用二进制编码方式：
        // 0度 = 00 (bit0=0, bit1=0)
        // 45度/-45度 = 根据方向使用不同编码
        switch (angle)
        {
            case 0:
                bits.Add((startBit, false));
                bits.Add((startBit + 1, false));
                break;
            case 45: // 左转
                bits.Add((startBit, false));
                bits.Add((startBit + 1, true));
                break;
            case -45: // 右转
                bits.Add((startBit, true));
                bits.Add((startBit + 1, false));
                break;
            case 90:
                bits.Add((startBit, true));
                bits.Add((startBit + 1, true));
                break;
            default:
                _logger.LogWarning("不支持的角度 {Angle}，使用0度", angle);
                bits.Add((startBit, false));
                bits.Add((startBit + 1, false));
                break;
        }
        
        return bits;
    }
}
