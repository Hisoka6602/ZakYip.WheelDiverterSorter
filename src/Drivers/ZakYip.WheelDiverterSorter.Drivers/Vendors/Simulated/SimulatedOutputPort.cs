using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 模拟输出端口实现
/// </summary>
/// <remarks>
/// 用于仿真环境的输出端口实现。
/// 记录所有写入操作的历史，便于测试验证。
/// </remarks>
public class SimulatedOutputPort : IOutputPort
{
    private readonly ILogger<SimulatedOutputPort>? _logger;
    private readonly ISystemClock _systemClock;
    private readonly List<(int BitIndex, bool Value, DateTime Timestamp)> _writeHistory = new();
    private readonly object _lock = new();

    /// <summary>
    /// 初始化 SimulatedOutputPort
    /// </summary>
    /// <param name="logger">日志记录器（可选）</param>
    /// <param name="systemClock">系统时钟</param>
    public SimulatedOutputPort(ILogger<SimulatedOutputPort>? logger, ISystemClock systemClock)
    {
        _logger = logger;
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <inheritdoc/>
    public Task<bool> WriteAsync(int bitIndex, bool value)
    {
        lock (_lock)
        {
            _writeHistory.Add((bitIndex, value, _systemClock.LocalNow));
        }

        _logger?.LogDebug(
            "模拟输出端口写入: Bit={BitIndex}, Value={Value}",
            bitIndex,
            value);

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> WriteBatchAsync(int startBit, bool[] values)
    {
        lock (_lock)
        {
            for (int i = 0; i < values.Length; i++)
            {
                _writeHistory.Add((startBit + i, values[i], _systemClock.LocalNow));
            }
        }

        _logger?.LogDebug(
            "模拟输出端口批量写入: StartBit={StartBit}, Count={Count}",
            startBit,
            values.Length);

        return Task.FromResult(true);
    }

    /// <summary>
    /// 获取写入历史记录
    /// </summary>
    /// <returns>写入历史记录的副本</returns>
    public List<(int BitIndex, bool Value, DateTime Timestamp)> GetWriteHistory()
    {
        lock (_lock)
        {
            return new List<(int, bool, DateTime)>(_writeHistory);
        }
    }

    /// <summary>
    /// 清除写入历史记录
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _writeHistory.Clear();
        }

        _logger?.LogDebug("模拟输出端口历史记录已清除");
    }
}
