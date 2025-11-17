using Microsoft.Extensions.Logging;
using S7.Net;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.S7;

/// <summary>
/// S7 PLC 输出端口实现
/// </summary>
public class S7OutputPort : OutputPortBase
{
    private readonly ILogger<S7OutputPort> _logger;
    private readonly S7Connection _connection;
    private readonly int _dbNumber;

    /// <summary>
    /// 初始化S7输出端口
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="connection">S7连接管理器</param>
    /// <param name="dbNumber">数据块编号</param>
    public S7OutputPort(ILogger<S7OutputPort> logger, S7Connection connection, int dbNumber)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _dbNumber = dbNumber;
    }

    /// <summary>
    /// 写入单个输出位
    /// </summary>
    /// <param name="bitIndex">位索引（字节地址*8 + 位偏移）</param>
    /// <param name="value">值（true为高电平，false为低电平）</param>
    /// <returns>是否成功</returns>
    public override async Task<bool> WriteAsync(int bitIndex, bool value)
    {
        try
        {
            // 确保连接已建立
            if (!await _connection.EnsureConnectedAsync())
            {
                _logger.LogWarning("无法连接到PLC，写入输出位 {BitIndex} 失败", bitIndex);
                return false;
            }

            var plc = _connection.GetPlc();
            if (plc == null)
            {
                _logger.LogWarning("PLC实例为空，无法写入输出位 {BitIndex}", bitIndex);
                return false;
            }

            // 计算字节地址和位偏移
            int byteAddress = bitIndex / 8;
            int bitOffset = bitIndex % 8;

            // 写入位值
            await Task.Run(() => 
                plc.Write(DataType.DataBlock, _dbNumber, byteAddress, value, (byte)bitOffset));

            _logger.LogTrace("写入输出位 DB{DbNumber}.DBX{ByteAddress}.{BitOffset} = {Value}", 
                _dbNumber, byteAddress, bitOffset, value);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入输出位 {BitIndex} 时发生异常", bitIndex);
            return false;
        }
    }

    /// <summary>
    /// 批量写入多个输出位（重写以添加日志）
    /// </summary>
    /// <param name="startBit">起始位索引</param>
    /// <param name="values">要写入的值数组</param>
    /// <returns>是否成功</returns>
    public override async Task<bool> WriteBatchAsync(int startBit, bool[] values)
    {
        try
        {
            // 确保连接已建立
            if (!await _connection.EnsureConnectedAsync())
            {
                _logger.LogWarning("无法连接到PLC，批量写入输出位失败");
                return false;
            }

            // 使用基类的默认实现
            var allSuccess = await base.WriteBatchAsync(startBit, values);

            _logger.LogTrace("批量写入 {Count} 个输出位，起始位: {StartBit}", values.Length, startBit);
            
            return allSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量写入输出位时发生异常，起始位: {StartBit}, 数量: {Count}", startBit, values.Length);
            return false;
        }
    }
}
