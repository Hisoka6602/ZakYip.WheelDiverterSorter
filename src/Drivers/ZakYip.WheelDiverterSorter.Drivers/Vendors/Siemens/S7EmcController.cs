using Microsoft.Extensions.Logging;
using S7.Net;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;

/// <summary>
/// 西门子S7 EMC控制器实现
/// </summary>
/// <remarks>
/// 提供对西门子S7-1200/1500 PLC的基本操作，与雷赛EMC控制器功能对等
/// </remarks>
public class S7EmcController : IEmcController
{
    private readonly ILogger<S7EmcController> _logger;
    private readonly S7Connection _connection;
    private readonly ushort _cardNo;
    private bool _isAvailable;

    /// <inheritdoc/>
    public ushort CardNo => _cardNo;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="connection">S7连接管理器</param>
    /// <param name="cardNo">卡号（用于多PLC场景）</param>
    public S7EmcController(
        ILogger<S7EmcController> logger,
        S7Connection connection,
        ushort cardNo = 0)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _cardNo = cardNo;
        _isAvailable = false;
    }

    /// <inheritdoc/>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("正在初始化S7 EMC控制器 (CardNo: {CardNo})...", _cardNo);

            // 尝试连接PLC
            var connected = await _connection.ConnectAsync(cancellationToken);
            if (!connected)
            {
                _logger.LogError("S7 EMC控制器初始化失败: 无法连接到PLC");
                _isAvailable = false;
                return false;
            }

            // 测试读取一个位来验证连接
            try
            {
                await _connection.ReadBitAsync("DB1", 0, 0, cancellationToken);
                _isAvailable = true;
                _logger.LogInformation("S7 EMC控制器初始化成功 (CardNo: {CardNo})", _cardNo);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "S7 EMC控制器初始化失败: 测试读取失败");
                _isAvailable = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S7 EMC控制器初始化过程中发生异常");
            _isAvailable = false;
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_isAvailable && _connection.IsConnected);
    }

    /// <inheritdoc/>
    public async Task<bool> ReadInputAsync(ushort bitNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            // 计算字节地址和位地址
            int byteAddress = bitNumber / 8;
            int bitAddress = bitNumber % 8;

            return await _connection.ReadBitAsync("DB1", byteAddress, bitAddress, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取S7输入位 {BitNumber} 失败", bitNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task WriteOutputAsync(ushort bitNumber, bool value, CancellationToken cancellationToken = default)
    {
        try
        {
            // 计算字节地址和位地址
            int byteAddress = bitNumber / 8;
            int bitAddress = bitNumber % 8;

            await _connection.WriteBitAsync("DB2", byteAddress, bitAddress, value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入S7输出位 {BitNumber} = {Value} 失败", bitNumber, value);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<ushort, bool>> ReadMultipleInputsAsync(
        IEnumerable<ushort> bitNumbers,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<ushort, bool>();

        try
        {
            // 批量读取优化：按字节分组
            var grouped = bitNumbers
                .GroupBy(bit => bit / 8)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                int byteAddress = group.Key;
                var byteValue = await _connection.ReadByteAsync("DB1", byteAddress, cancellationToken);

                foreach (var bitNumber in group)
                {
                    int bitAddress = bitNumber % 8;
                    bool value = (byteValue & (1 << bitAddress)) != 0;
                    results[bitNumber] = value;
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量读取S7输入失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task WriteMultipleOutputsAsync(
        Dictionary<ushort, bool> outputs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 批量写入优化：按字节分组
            var grouped = outputs
                .GroupBy(kvp => kvp.Key / 8)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                int byteAddress = group.Key;
                
                // 先读取当前字节值
                byte byteValue = await _connection.ReadByteAsync("DB2", byteAddress, cancellationToken);

                // 修改对应的位
                foreach (var kvp in group)
                {
                    int bitAddress = kvp.Key % 8;
                    if (kvp.Value)
                    {
                        byteValue |= (byte)(1 << bitAddress); // 置1
                    }
                    else
                    {
                        byteValue &= (byte)~(1 << bitAddress); // 清0
                    }
                }

                // 写回字节
                await _connection.WriteByteAsync("DB2", byteAddress, byteValue, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量写入S7输出失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("重置S7 EMC控制器 (CardNo: {CardNo})", _cardNo);
        _isAvailable = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // S7Connection由DI容器管理，这里不需要dispose
        _isAvailable = false;
    }
}
