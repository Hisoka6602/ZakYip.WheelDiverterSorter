using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛控制器输入端口实现
/// </summary>
/// <remarks>
/// 从IO状态缓存服务读取，不直接调用硬件IO函数。
/// 所有硬件IO读取由 LeadshineIoStateCache 后台服务集中处理。
/// </remarks>
public class LeadshineInputPort : InputPortBase
{
    private readonly ILogger<LeadshineInputPort> _logger;
    private readonly ILeadshineIoStateCache _ioStateCache;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="ioStateCache">IO状态缓存服务</param>
    public LeadshineInputPort(
        ILogger<LeadshineInputPort> logger,
        ILeadshineIoStateCache ioStateCache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ioStateCache = ioStateCache ?? throw new ArgumentNullException(nameof(ioStateCache));
    }

    /// <summary>
    /// 读取单个输入位
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <returns>位的值（true为高电平，false为低电平）</returns>
    /// <remarks>
    /// 从IO状态缓存读取，非阻塞操作。
    /// </remarks>
    public override Task<bool> ReadAsync(int bitIndex)
    {
        try
        {
            bool value = _ioStateCache.ReadInputBit(bitIndex);
            return Task.FromResult(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取输入位 {BitIndex} 时发生异常", bitIndex);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 批量读取多个输入位
    /// </summary>
    /// <param name="startBit">起始位索引</param>
    /// <param name="count">要读取的位数</param>
    /// <returns>位值数组</returns>
    /// <remarks>
    /// 从IO状态缓存批量读取，非阻塞操作。
    /// </remarks>
    public override Task<bool[]> ReadBatchAsync(int startBit, int count)
    {
        try
        {
            if (count <= 0)
            {
                return Task.FromResult(Array.Empty<bool>());
            }

            var bitIndices = Enumerable.Range(startBit, count);
            var results = _ioStateCache.ReadInputBits(bitIndices);
            
            var array = new bool[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = results.TryGetValue(startBit + i, out bool value) ? value : false;
            }

            return Task.FromResult(array);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "批量读取输入位异常，起始位={StartBit}, 数量={Count}",
                startBit,
                count);
            return Task.FromResult(new bool[count]);
        }
    }
}
