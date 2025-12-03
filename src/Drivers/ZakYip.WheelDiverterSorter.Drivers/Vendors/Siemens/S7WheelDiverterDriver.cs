using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using S7DiverterConfig = ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration.S7DiverterConfigDto;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;

/// <summary>
/// 基于S7 PLC的摆轮驱动器实现
/// </summary>
/// <remarks>
/// 直接实现 <see cref="IWheelDiverterDriver"/> 接口，
/// 使用数据块(DB)的位输出控制摆轮角度，将方向操作映射为角度操作。
/// </remarks>
public class S7WheelDiverterDriver : IWheelDiverterDriver
{
    private readonly ILogger<S7WheelDiverterDriver> _logger;
    private readonly S7OutputPort _outputPort;
    private readonly S7DiverterConfig _config;
    private string _currentStatus = "未知";

    /// <summary>
    /// 继电器通道映射配置 - 角度定义
    /// </summary>
    private const int LeftTurnAngle = 45;
    private const int RightTurnAngle = -45;
    private const int PassThroughAngle = 0;
    private const int StopAngle = 0;

    /// <inheritdoc/>
    public string DiverterId => _config.DiverterId;

    /// <summary>
    /// 初始化S7摆轮驱动器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="outputPort">输出端口</param>
    /// <param name="config">摆轮配置</param>
    public S7WheelDiverterDriver(
        ILogger<S7WheelDiverterDriver> logger,
        S7OutputPort outputPort,
        S7DiverterConfig config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outputPort = outputPort ?? throw new ArgumentNullException(nameof(outputPort));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        _logger.LogInformation(
            "已初始化S7摆轮驱动器 {DiverterId}，DB地址=DB{DbNumber}.{StartByte}.{StartBit}，左转={LeftAngle}°，右转={RightAngle}°，直通={PassAngle}°",
            DiverterId, _config.OutputDbNumber, _config.OutputStartByte, _config.OutputStartBit,
            LeftTurnAngle, RightTurnAngle, PassThroughAngle);
    }

    /// <inheritdoc/>
    public async Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default)
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
    public async Task<bool> TurnRightAsync(CancellationToken cancellationToken = default)
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
    public async Task<bool> PassThroughAsync(CancellationToken cancellationToken = default)
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
    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
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
    public Task<string> GetStatusAsync()
    {
        return Task.FromResult(_currentStatus);
    }

    /// <summary>
    /// 内部方法：设置摆轮角度
    /// </summary>
    private async Task<bool> SetAngleInternalAsync(int angle, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "[摆轮通信-发送] 摆轮 {DiverterId} 开始设置角度 | 目标角度={Angle}度 | DB地址=DB{DbNumber}.{StartByte}.{StartBit}",
                DiverterId, angle, _config.OutputDbNumber, _config.OutputStartByte, _config.OutputStartBit);

            var outputBits = MapAngleToOutputBits(angle);
            int baseBitIndex = _config.OutputStartByte * 8 + _config.OutputStartBit;

            foreach (var (bitOffset, value) in outputBits)
            {
                var absoluteBitIndex = baseBitIndex + bitOffset;
                
                _logger.LogInformation(
                    "[摆轮通信-IO写入] 摆轮 {DiverterId} 写入S7输出位 | 位索引={BitIndex} | 值={Value}",
                    DiverterId, absoluteBitIndex, value);

                var success = await _outputPort.WriteAsync(absoluteBitIndex, value);
                if (!success)
                {
                    _logger.LogError(
                        "[摆轮通信-IO写入] 摆轮 {DiverterId} 写入S7输出位失败 | 位索引={BitIndex}",
                        DiverterId, absoluteBitIndex);
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
    private List<(int bitOffset, bool value)> MapAngleToOutputBits(int angle)
    {
        var bits = new List<(int, bool)>();

        switch (angle)
        {
            case 0:
                bits.Add((0, false));
                bits.Add((1, false));
                break;
            case 45: // 左转
                bits.Add((0, false));
                bits.Add((1, true));
                break;
            case -45: // 右转
                bits.Add((0, true));
                bits.Add((1, false));
                break;
            case 90:
                bits.Add((0, true));
                bits.Add((1, true));
                break;
            default:
                _logger.LogWarning("不支持的角度 {Angle}，使用0度", angle);
                bits.Add((0, false));
                bits.Add((1, false));
                break;
        }

        return bits;
    }
}
