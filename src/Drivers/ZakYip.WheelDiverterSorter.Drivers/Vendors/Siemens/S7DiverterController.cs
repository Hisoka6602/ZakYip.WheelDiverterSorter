using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;

/// <summary>
/// 基于S7 PLC的摆轮控制器实现
/// 使用数据块(DB)的位输出控制摆轮角度
/// </summary>
public class S7DiverterController : IDiverterController
{
    private readonly ILogger<S7DiverterController> _logger;
    private readonly S7OutputPort _outputPort;
    private readonly S7DiverterConfig _config;
    private int _currentAngle;

    /// <summary>
    /// 摆轮ID
    /// </summary>
    public string DiverterId => _config.DiverterId;

    /// <summary>
    /// 初始化S7摆轮控制器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="outputPort">输出端口</param>
    /// <param name="config">摆轮配置</param>
    public S7DiverterController(
        ILogger<S7DiverterController> logger,
        S7OutputPort outputPort,
        S7DiverterConfig config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outputPort = outputPort ?? throw new ArgumentNullException(nameof(outputPort));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _currentAngle = 0;
    }

    /// <summary>
    /// 设置摆轮角度
    /// </summary>
    /// <param name="angle">目标角度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> SetAngleAsync(int angle, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "[摆轮通信-发送] 摆轮 {DiverterId} 开始设置角度 | 目标角度={Angle}度 | DB地址=DB{DbNumber}.{StartByte}.{StartBit}",
                DiverterId,
                angle,
                _config.OutputDbNumber,
                _config.OutputStartByte,
                _config.OutputStartBit);

            // 根据角度映射到对应的输出位组合
            var outputBits = MapAngleToOutputBits(angle);

            // 计算绝对位索引（字节地址 * 8 + 位偏移）
            int baseBitIndex = _config.OutputStartByte * 8 + _config.OutputStartBit;

            // 写入输出端口
            foreach (var (bitOffset, value) in outputBits)
            {
                var absoluteBitIndex = baseBitIndex + bitOffset;
                
                // 记录每个IO写入操作
                _logger.LogInformation(
                    "[摆轮通信-IO写入] 摆轮 {DiverterId} 写入S7输出位 | 位索引={BitIndex} | 值={Value}",
                    DiverterId,
                    absoluteBitIndex,
                    value);

                var success = await _outputPort.WriteAsync(absoluteBitIndex, value);
                if (!success)
                {
                    _logger.LogError(
                        "[摆轮通信-IO写入] 摆轮 {DiverterId} 写入S7输出位失败 | 位索引={BitIndex}",
                        DiverterId,
                        absoluteBitIndex);
                    return false;
                }
            }

            // 等待摆轮响应（实际应用中可能需要读取反馈信号）
            await Task.Delay(100, cancellationToken);

            _currentAngle = angle;
            _logger.LogInformation(
                "[摆轮通信-发送完成] 摆轮 {DiverterId} 角度设置成功 | 目标角度={Angle}度",
                DiverterId,
                angle);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[摆轮通信-发送] 摆轮 {DiverterId} 设置角度失败 | 目标角度={Angle}度",
                DiverterId,
                angle);
            return false;
        }
    }

    /// <summary>
    /// 获取当前摆轮角度
    /// </summary>
    /// <returns>当前角度</returns>
    public Task<int> GetCurrentAngleAsync()
    {
        return Task.FromResult(_currentAngle);
    }

    /// <summary>
    /// 复位摆轮到0度
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> ResetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("复位摆轮 {DiverterId} 到0度", DiverterId);
        return await SetAngleAsync(0, cancellationToken);
    }

    /// <summary>
    /// 将角度映射到输出位组合
    /// 使用二进制编码方式，与Leadshine驱动器保持一致
    /// </summary>
    /// <param name="angle">目标角度</param>
    /// <returns>位偏移和值的列表</returns>
    private List<(int bitOffset, bool value)> MapAngleToOutputBits(int angle)
    {
        var bits = new List<(int, bool)>();

        // 使用二进制编码方式：
        // 0度 = 00 (bit0=0, bit1=0)
        // 30度 = 01 (bit0=1, bit1=0)
        // 45度 = 10 (bit0=0, bit1=1)
        // 90度 = 11 (bit0=1, bit1=1)

        switch (angle)
        {
            case 0:
                bits.Add((0, false));
                bits.Add((1, false));
                break;
            case 30:
                bits.Add((0, true));
                bits.Add((1, false));
                break;
            case 45:
                bits.Add((0, false));
                bits.Add((1, true));
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
