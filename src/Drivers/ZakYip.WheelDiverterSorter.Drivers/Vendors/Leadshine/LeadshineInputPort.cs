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
/// <remarks>
/// 优化：使用批量读取API (dmc_read_inport_array) 一次性读取所有IO端口状态，
/// 避免逐位读取导致的IO阻塞问题。
/// </remarks>
public class LeadshineInputPort : InputPortBase
{
    private readonly ILogger<LeadshineInputPort> _logger;
    private readonly ushort _cardNo;
    private readonly ushort _totalInputPorts;

    /// <summary>
    /// 每个端口的位数（雷赛控制器每个端口为32位）
    /// </summary>
    private const int BitsPerPort = 32;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">控制器卡号</param>
    public LeadshineInputPort(ILogger<LeadshineInputPort> logger, ushort cardNo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cardNo = cardNo;
        
        // 获取输入端口总数
        _totalInputPorts = GetTotalInputPorts();
    }

    /// <summary>
    /// 读取单个输入位
    /// </summary>
    /// <param name="bitIndex">位索引</param>
    /// <returns>位的值（true为高电平，false为低电平）</returns>
    /// <remarks>
    /// 优化前：直接使用 dmc_read_inbit 读取单个位
    /// 优化后：计算所在端口号，读取整个端口后提取对应位
    /// </remarks>
    public override Task<bool> ReadAsync(int bitIndex)
    {
        try
        {
            // 计算端口号和端口内位索引
            ushort portNo = (ushort)(bitIndex / BitsPerPort);
            int bitInPort = bitIndex % BitsPerPort;
            
            // 读取整个端口
            uint portValue = LTDMC.dmc_read_inport(_cardNo, portNo);
            
            // 提取指定位
            bool bitValue = ((portValue >> bitInPort) & 1) != 0;
            
            return Task.FromResult(bitValue);
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
    /// 优化实现：使用 dmc_read_inport_array 一次性读取所有涉及的端口，
    /// 然后从缓存中提取所需的位，避免多次调用底层DLL。
    /// </remarks>
    public override Task<bool[]> ReadBatchAsync(int startBit, int count)
    {
        try
        {
            if (count <= 0)
            {
                return Task.FromResult(Array.Empty<bool>());
            }

            // 计算需要读取的端口范围
            int endBit = startBit + count - 1;
            ushort startPort = (ushort)(startBit / BitsPerPort);
            ushort endPort = (ushort)(endBit / BitsPerPort);
            ushort portCount = (ushort)(endPort - startPort + 1);

            // 使用批量读取API一次性读取所有涉及的端口
            uint[] portValues = new uint[portCount];
            short result = LTDMC.dmc_read_inport_array(_cardNo, portCount, portValues);

            if (result < 0)
            {
                _logger.LogWarning(
                    "批量读取输入端口失败，起始端口={StartPort}, 端口数={PortCount}, 错误码={ErrorCode}",
                    startPort,
                    portCount,
                    result);
                return Task.FromResult(new bool[count]);
            }

            // 从读取的端口值中提取所需的位
            var results = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int bitIndex = startBit + i;
                int portOffset = (bitIndex / BitsPerPort) - startPort;
                int bitInPort = bitIndex % BitsPerPort;
                
                results[i] = ((portValues[portOffset] >> bitInPort) & 1) != 0;
            }

            return Task.FromResult(results);
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

    /// <summary>
    /// 获取输入端口总数
    /// </summary>
    private ushort GetTotalInputPorts()
    {
        try
        {
            ushort totalIn = 0;
            ushort totalOut = 0;
            short result = LTDMC.dmc_get_total_ionum(_cardNo, ref totalIn, ref totalOut);
            
            if (result < 0)
            {
                _logger.LogWarning(
                    "获取IO端口总数失败，卡号={CardNo}, 错误码={ErrorCode}，使用默认值8个端口",
                    _cardNo,
                    result);
                return 8; // 默认8个端口（256位）
            }

            // totalIn 是输入位总数，需要转换为端口数
            ushort portCount = (ushort)((totalIn + BitsPerPort - 1) / BitsPerPort);
            
            _logger.LogInformation(
                "雷赛控制器 {CardNo} 输入端口信息: 输入位总数={TotalIn}, 端口数={PortCount}",
                _cardNo,
                totalIn,
                portCount);
            
            return portCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取输入端口总数异常，使用默认值8个端口");
            return 8;
        }
    }
}
