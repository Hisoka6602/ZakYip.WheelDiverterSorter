using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using csLTDMC;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛控制器输入端口实现
/// </summary>
public class LeadshineInputPort : InputPortBase
{
    private readonly ILogger<LeadshineInputPort> _logger;
    private readonly ushort _cardNo;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">控制器卡号</param>
    public LeadshineInputPort(ILogger<LeadshineInputPort> logger, ushort cardNo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cardNo = cardNo;
    }

    /// <summary>
    /// 读取单个输入位
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <returns>位的值（true为高电平，false为低电平）</returns>
    public override Task<bool> ReadAsync(int bitIndex)
    {
        try
        {
            // dmc_read_inbit 返回位的值（0或1），如果出错返回负数
            var result = LTDMC.dmc_read_inbit(_cardNo, (ushort)bitIndex);
            
            if (result < 0)
            {
                _logger.LogWarning("读取输入位 {BitIndex} 失败，错误码: {ErrorCode}", bitIndex, result);
                return Task.FromResult(false);
            }

            return Task.FromResult(result != 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取输入位 {BitIndex} 时发生异常", bitIndex);
            return Task.FromResult(false);
        }
    }
}
