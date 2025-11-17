using Microsoft.Extensions.Logging;
using S7.Net;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.S7;

/// <summary>
/// S7 PLC 输入端口实现
/// </summary>
public class S7InputPort : InputPortBase
{
    private readonly ILogger<S7InputPort> _logger;
    private readonly S7Connection _connection;
    private readonly int _dbNumber;

    /// <summary>
    /// 初始化S7输入端口
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="connection">S7连接管理器</param>
    /// <param name="dbNumber">数据块编号</param>
    public S7InputPort(ILogger<S7InputPort> logger, S7Connection connection, int dbNumber)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _dbNumber = dbNumber;
    }

    /// <summary>
    /// 读取单个输入位
    /// </summary>
    /// <param name="bitIndex">位索引（字节地址*8 + 位偏移）</param>
    /// <returns>位的值（true为高电平，false为低电平）</returns>
    public override async Task<bool> ReadAsync(int bitIndex)
    {
        try
        {
            // 确保连接已建立
            if (!await _connection.EnsureConnectedAsync())
            {
                _logger.LogWarning("无法连接到PLC，读取输入位 {BitIndex} 失败", bitIndex);
                return false;
            }

            var plc = _connection.GetPlc();
            if (plc == null)
            {
                _logger.LogWarning("PLC实例为空，无法读取输入位 {BitIndex}", bitIndex);
                return false;
            }

            // 计算字节地址和位偏移
            int byteAddress = bitIndex / 8;
            int bitOffset = bitIndex % 8;

            // 读取位值
            var result = await Task.Run(() => 
                plc.Read(DataType.DataBlock, _dbNumber, byteAddress, VarType.Bit, 1, (byte)bitOffset));

            bool value = result != null && Convert.ToBoolean(result);
            
            _logger.LogTrace("读取输入位 DB{DbNumber}.DBX{ByteAddress}.{BitOffset} = {Value}", 
                _dbNumber, byteAddress, bitOffset, value);

            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取输入位 {BitIndex} 时发生异常", bitIndex);
            return false;
        }
    }

    /// <summary>
    /// 批量读取多个输入位（重写以添加日志）
    /// </summary>
    /// <param name="startBit">起始位索引</param>
    /// <param name="count">要读取的位数</param>
    /// <returns>位值数组</returns>
    public override async Task<bool[]> ReadBatchAsync(int startBit, int count)
    {
        try
        {
            // 确保连接已建立
            if (!await _connection.EnsureConnectedAsync())
            {
                _logger.LogWarning("无法连接到PLC，批量读取输入位失败");
                return new bool[count];
            }

            // 使用基类的默认实现
            var results = await base.ReadBatchAsync(startBit, count);

            _logger.LogTrace("批量读取 {Count} 个输入位，起始位: {StartBit}", count, startBit);
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量读取输入位时发生异常，起始位: {StartBit}, 数量: {Count}", startBit, count);
            return new bool[count];
        }
    }
}
