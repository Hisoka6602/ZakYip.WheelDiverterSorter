using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using csLTDMC;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛控制器输出端口实现
/// </summary>
public class LeadshineOutputPort : OutputPortBase
{
    private readonly ILogger<LeadshineOutputPort> _logger;
    private readonly ushort _cardNo;
    private readonly IEmcController _emcController;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">控制器卡号</param>
    /// <param name="emcController">EMC 控制器实例</param>
    public LeadshineOutputPort(ILogger<LeadshineOutputPort> logger, ushort cardNo, IEmcController emcController)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cardNo = cardNo;
        _emcController = emcController ?? throw new ArgumentNullException(nameof(emcController));
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
            // 检查 EMC 控制器是否已初始化
            if (!_emcController.IsAvailable())
            {
                _logger.LogError(
                    "无法写入输出位 {BitIndex}：EMC 控制器未初始化或不可用",
                    bitIndex);
                return Task.FromResult(false);
            }

            var result = LTDMC.dmc_write_outbit(_cardNo, (ushort)bitIndex, (ushort)(value ? 1 : 0));
            
            if (result != 0)
            {
                _logger.LogWarning(
                    "写入输出位 {BitIndex} 失败，错误码: {ErrorCode} | 提示：ErrorCode=9 表示控制卡未初始化", 
                    bitIndex, result);
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
