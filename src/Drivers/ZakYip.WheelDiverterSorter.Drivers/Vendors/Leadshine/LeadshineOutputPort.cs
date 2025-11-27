using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using csLTDMC;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛控制器输出端口实现
/// </summary>
public class LeadshineOutputPort : OutputPortBase
{
    private readonly ILogger<LeadshineOutputPort> _logger;
    private readonly ushort _cardNo;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">控制器卡号</param>
    public LeadshineOutputPort(ILogger<LeadshineOutputPort> logger, ushort cardNo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cardNo = cardNo;
    }

    /// <summary>
    /// 写入单个输出位
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <param name="value">值（true为高电平，false为低电平）</param>
    /// <returns>是否成功</returns>
    public override Task<bool> WriteAsync(int bitIndex, bool value)
    {
        try
        {
            var result = LTDMC.dmc_write_outbit(_cardNo, (ushort)bitIndex, (ushort)(value ? 1 : 0));
            
            if (result != 0)
            {
                _logger.LogWarning("写入输出位 {BitIndex} 失败，错误码: {ErrorCode}", bitIndex, result);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入输出位 {BitIndex} 时发生异常", bitIndex);
            return Task.FromResult(false);
        }
    }
}
