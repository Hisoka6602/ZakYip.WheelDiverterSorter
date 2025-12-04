using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;

/// <summary>
/// 西门子 S7 PLC IO 联动驱动实现
/// </summary>
/// <remarks>
/// 通过 S7 PLC 控制 IO 联动，支持设置和读取 IO 点的电平状态。
/// </remarks>
public class S7IoLinkageDriver : IIoLinkageDriver
{
    private readonly ILogger<S7IoLinkageDriver> _logger;
    private readonly S7Connection _connection;
    private readonly S7OutputPort _outputPort;
    private readonly S7InputPort _inputPort;

    /// <summary>
    /// S7 PLC 输出点范围上限
    /// </summary>
    private const int MaxIoPoints = 256;

    /// <summary>
    /// 初始化 S7 IO 联动驱动
    /// </summary>
    /// <param name="connection">S7 连接</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="loggerFactory">日志工厂</param>
    public S7IoLinkageDriver(
        S7Connection connection,
        ILogger<S7IoLinkageDriver> logger,
        ILoggerFactory loggerFactory)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // 创建输出端口（DB1）和输入端口（DB2）
        _outputPort = new S7OutputPort(
            loggerFactory.CreateLogger<S7OutputPort>(),
            _connection,
            dbNumber: 1);
        
        _inputPort = new S7InputPort(
            loggerFactory.CreateLogger<S7InputPort>(),
            _connection,
            dbNumber: 2);
    }

    /// <inheritdoc/>
    public async Task SetIoPointAsync(IoLinkagePoint ioPoint, CancellationToken cancellationToken = default)
    {
        try
        {
            // 根据 TriggerLevel 确定输出电平
            // ActiveHigh = 高电平 = true
            // ActiveLow = 低电平 = false
            bool value = ioPoint.Level == TriggerLevel.ActiveHigh;

            _logger.LogDebug(
                "设置 S7 IO 点: BitNumber={BitNumber}, Level={Level}, Value={Value}",
                ioPoint.BitNumber,
                ioPoint.Level,
                value);

            // 调用 S7 输出端口写入
            bool success = await _outputPort.WriteAsync(ioPoint.BitNumber, value);

            if (!success)
            {
                _logger.LogError(
                    "设置 S7 IO 点失败: BitNumber={BitNumber}, Level={Level}",
                    ioPoint.BitNumber,
                    ioPoint.Level);
                throw new InvalidOperationException(
                    $"设置 S7 IO 点 {ioPoint.BitNumber} 失败");
            }

            _logger.LogDebug(
                "成功设置 S7 IO 点: BitNumber={BitNumber}, Level={Level}",
                ioPoint.BitNumber,
                ioPoint.Level);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "设置 S7 IO 点时发生异常: BitNumber={BitNumber}", ioPoint.BitNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SetIoPointsAsync(IEnumerable<IoLinkagePoint> ioPoints, CancellationToken cancellationToken = default)
    {
        var ioPointsList = ioPoints.ToList();
        
        _logger.LogInformation("批量设置 S7 IO 点，共 {Count} 个", ioPointsList.Count);

        foreach (var ioPoint in ioPointsList)
        {
            await SetIoPointAsync(ioPoint, cancellationToken);
        }

        _logger.LogInformation("批量设置 S7 IO 点完成");
    }

    /// <inheritdoc/>
    public async Task<bool> ReadIoPointAsync(int bitNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("读取 S7 IO 点: BitNumber={BitNumber}", bitNumber);

            bool value = await _inputPort.ReadAsync(bitNumber);

            _logger.LogDebug("成功读取 S7 IO 点: BitNumber={BitNumber}, Value={Value}", bitNumber, value);

            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 S7 IO 点时发生异常: BitNumber={BitNumber}", bitNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ResetAllIoPointsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始复位所有 S7 IO 联动点");

        try
        {
            // 将所有输出点复位为低电平（false）
            // 输出点范围：0 到 MaxIoPoints-1
            for (int bitNumber = 0; bitNumber < MaxIoPoints; bitNumber++)
            {
                await _outputPort.WriteAsync(bitNumber, false);
            }

            _logger.LogInformation("成功复位所有 S7 IO 联动点");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "复位 S7 IO 联动点时发生异常");
            throw;
        }
    }
}
