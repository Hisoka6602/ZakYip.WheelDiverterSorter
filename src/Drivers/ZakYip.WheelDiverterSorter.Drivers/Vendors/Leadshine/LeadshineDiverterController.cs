using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using csLTDMC;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 基于雷赛（Leadshine）运动控制器的摆轮控制器实现
/// 使用IO输出端口控制摆轮角度
/// </summary>
public class LeadshineDiverterController : IDiverterController
{
    private readonly ILogger<LeadshineDiverterController> _logger;
    private readonly ushort _cardNo;
    private readonly LeadshineDiverterConfig _config;
    private int _currentAngle;

    /// <summary>
    /// 摆轮ID
    /// </summary>
    public string DiverterId => _config.DiverterId.ToString();

    /// <summary>
    /// 初始化雷赛摆轮控制器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">控制器卡号</param>
    /// <param name="config">摆轮配置</param>
    public LeadshineDiverterController(
        ILogger<LeadshineDiverterController> logger,
        ushort cardNo,
        LeadshineDiverterConfig config)
    {
        _logger = logger;
        _cardNo = cardNo;
        _config = config;
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
                "[摆轮通信-发送] 摆轮 {DiverterId} 开始设置角度 | 目标角度={Angle}度 | 卡号={CardNo}",
                DiverterId,
                angle,
                _cardNo);

            // 根据角度映射到对应的输出端口组合
            var outputBits = MapAngleToOutputBits(angle);
            
            // 写入输出端口
            foreach (var (bitIndex, value) in outputBits)
            {
                var result = LTDMC.dmc_write_outbit(_cardNo, (ushort)bitIndex, (ushort)(value ? 1 : 0));
                
                // 记录每个IO写入操作
                _logger.LogInformation(
                    "[摆轮通信-IO写入] 摆轮 {DiverterId} 写入输出位 | 位索引={BitIndex} | 值={Value} | 返回码={ResultCode}",
                    DiverterId,
                    bitIndex,
                    value ? 1 : 0,
                    result);
                
                if (result != 0)
                {
                    _logger.LogError(
                        "[摆轮通信-IO写入] 摆轮 {DiverterId} 写入输出位失败 | 位索引={BitIndex} | 错误码={ErrorCode}",
                        DiverterId,
                        bitIndex,
                        result);
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
    /// 这是一个简化的示例，实际应用中需要根据硬件接线配置
    /// </summary>
    /// <param name="angle">目标角度</param>
    /// <returns>输出位索引和值的列表</returns>
    private List<(int bitIndex, bool value)> MapAngleToOutputBits(int angle)
    {
        var bits = new List<(int, bool)>();
        
        // 根据配置的起始输出位和角度计算输出组合
        // 这里使用二进制编码方式：
        // 0度 = 00 (bit0=0, bit1=0)
        // 30度 = 01 (bit0=1, bit1=0)
        // 45度 = 10 (bit0=0, bit1=1)
        // 90度 = 11 (bit0=1, bit1=1)
        
        int startBit = _config.OutputStartBit;
        
        switch (angle)
        {
            case 0:
                bits.Add((startBit, false));
                bits.Add((startBit + 1, false));
                break;
            case 30:
                bits.Add((startBit, true));
                bits.Add((startBit + 1, false));
                break;
            case 45:
                bits.Add((startBit, false));
                bits.Add((startBit + 1, true));
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
