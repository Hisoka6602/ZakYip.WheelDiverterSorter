using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

/// <summary>
/// 模拟离散IO端口实现 - HAL实现
/// Simulated Discrete IO Port - HAL Implementation
/// </summary>
public sealed class SimulatedDiscreteIoPort : IDiscreteIoPort
{
    private bool _state;

    /// <inheritdoc/>
    public int PortNumber { get; }

    /// <summary>
    /// 创建模拟IO端口
    /// </summary>
    /// <param name="portNumber">端口编号</param>
    /// <param name="initialState">初始状态</param>
    public SimulatedDiscreteIoPort(int portNumber, bool initialState = false)
    {
        PortNumber = portNumber;
        _state = initialState;
    }

    /// <inheritdoc/>
    public Task SetAsync(bool isOn, CancellationToken cancellationToken = default)
    {
        _state = isOn;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_state);
    }

    /// <summary>
    /// 直接设置内部状态（仅用于测试）
    /// </summary>
    internal void SetInternalState(bool state) => _state = state;
}

/// <summary>
/// 模拟离散IO端口组实现 - HAL实现
/// Simulated Discrete IO Group - HAL Implementation
/// </summary>
public sealed class SimulatedDiscreteIoGroup : IDiscreteIoGroup
{
    private readonly List<SimulatedDiscreteIoPort> _ports;
    private readonly ILogger<SimulatedDiscreteIoGroup>? _logger;

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IDiscreteIoPort> Ports => _ports;

    /// <summary>
    /// 创建模拟IO端口组
    /// </summary>
    /// <param name="name">组名称</param>
    /// <param name="portCount">端口数量</param>
    /// <param name="logger">日志记录器</param>
    public SimulatedDiscreteIoGroup(
        string name,
        int portCount,
        ILogger<SimulatedDiscreteIoGroup>? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger;
        _ports = Enumerable.Range(0, portCount)
            .Select(i => new SimulatedDiscreteIoPort(i))
            .ToList();
    }

    /// <inheritdoc/>
    public IDiscreteIoPort? GetPort(int portNumber)
    {
        return GetSimulatedPort(portNumber);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, bool>> ReadBatchAsync(
        IEnumerable<int> portNumbers,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, bool>();
        foreach (var portNumber in portNumbers)
        {
            var port = GetSimulatedPort(portNumber);
            if (port != null)
            {
                result[portNumber] = await port.GetAsync(cancellationToken);
            }
        }
        return result;
    }

    /// <inheritdoc/>
    public async Task WriteBatchAsync(
        IReadOnlyDictionary<int, bool> portStates,
        CancellationToken cancellationToken = default)
    {
        foreach (var (portNumber, state) in portStates)
        {
            var port = GetSimulatedPort(portNumber);
            if (port != null)
            {
                await port.SetAsync(state, cancellationToken);
            }
        }

        _logger?.LogDebug("[IO-模拟] 批量写入 {GroupName} 端口数={Count}", Name, portStates.Count);
    }

    /// <summary>
    /// 获取模拟端口（用于测试注入状态）
    /// </summary>
    /// <remarks>
    /// 此方法返回具体的 <see cref="SimulatedDiscreteIoPort"/> 类型，
    /// 允许测试代码访问测试专用方法如 <see cref="SimulatedDiscreteIoPort.SetInternalState"/>。
    /// </remarks>
    /// <param name="portNumber">端口编号</param>
    /// <returns>模拟端口实例，如果不存在则返回 null</returns>
    internal SimulatedDiscreteIoPort? GetSimulatedPort(int portNumber)
    {
        return _ports.FirstOrDefault(p => p.PortNumber == portNumber);
    }
}
