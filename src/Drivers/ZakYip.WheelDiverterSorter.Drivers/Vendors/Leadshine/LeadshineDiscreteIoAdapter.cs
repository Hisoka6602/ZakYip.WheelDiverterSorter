using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛离散IO端口 HAL适配器
/// Leadshine Discrete IO Port HAL Adapter
/// </summary>
/// <remarks>
/// 此类将雷赛 <see cref="IInputPort"/> 和 <see cref="IOutputPort"/> 
/// 适配为统一的 <see cref="IDiscreteIoPort"/> HAL接口。
/// </remarks>
public sealed class LeadshineDiscreteIoPort : IDiscreteIoPort
{
    private readonly IInputPort _inputPort;
    private readonly IOutputPort _outputPort;
    private readonly ILogger<LeadshineDiscreteIoPort>? _logger;

    /// <inheritdoc/>
    public int PortNumber { get; }

    /// <summary>
    /// 创建雷赛IO端口HAL适配器
    /// </summary>
    /// <param name="portNumber">端口编号</param>
    /// <param name="inputPort">输入端口</param>
    /// <param name="outputPort">输出端口</param>
    /// <param name="logger">日志记录器</param>
    public LeadshineDiscreteIoPort(
        int portNumber,
        IInputPort inputPort,
        IOutputPort outputPort,
        ILogger<LeadshineDiscreteIoPort>? logger = null)
    {
        PortNumber = portNumber;
        _inputPort = inputPort ?? throw new ArgumentNullException(nameof(inputPort));
        _outputPort = outputPort ?? throw new ArgumentNullException(nameof(outputPort));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SetAsync(bool isOn, CancellationToken cancellationToken = default)
    {
        await _outputPort.WriteAsync(PortNumber, isOn);
        _logger?.LogDebug("[IO-雷赛HAL] 端口 {Port} 设置为 {State}", PortNumber, isOn ? "ON" : "OFF");
    }

    /// <inheritdoc/>
    public async Task<bool> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _inputPort.ReadAsync(PortNumber);
    }
}

/// <summary>
/// 雷赛离散IO端口组 HAL适配器
/// Leadshine Discrete IO Group HAL Adapter
/// </summary>
/// <remarks>
/// 此类将雷赛控制器的IO端口集合适配为统一的 <see cref="IDiscreteIoGroup"/> HAL接口。
/// </remarks>
public sealed class LeadshineDiscreteIoGroup : IDiscreteIoGroup
{
    private readonly Dictionary<int, LeadshineDiscreteIoPort> _ports;
    private readonly ILogger<LeadshineDiscreteIoGroup>? _logger;

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IDiscreteIoPort> Ports => _ports.Values.ToList();

    /// <summary>
    /// 创建雷赛IO端口组HAL适配器
    /// </summary>
    /// <param name="name">组名称</param>
    /// <param name="inputPort">输入端口</param>
    /// <param name="outputPort">输出端口</param>
    /// <param name="portCount">端口数量</param>
    /// <param name="portLogger">端口日志记录器</param>
    /// <param name="groupLogger">组日志记录器</param>
    public LeadshineDiscreteIoGroup(
        string name,
        IInputPort inputPort,
        IOutputPort outputPort,
        int portCount,
        ILogger<LeadshineDiscreteIoPort>? portLogger = null,
        ILogger<LeadshineDiscreteIoGroup>? groupLogger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = groupLogger;
        
        _ports = Enumerable.Range(0, portCount)
            .ToDictionary(
                i => i,
                i => new LeadshineDiscreteIoPort(i, inputPort, outputPort, portLogger));
    }

    /// <inheritdoc/>
    public IDiscreteIoPort? GetPort(int portNumber)
    {
        return _ports.TryGetValue(portNumber, out var port) ? port : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, bool>> ReadBatchAsync(
        IEnumerable<int> portNumbers,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, bool>();
        foreach (var portNumber in portNumbers)
        {
            if (_ports.TryGetValue(portNumber, out var port))
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
            if (_ports.TryGetValue(portNumber, out var port))
            {
                await port.SetAsync(state, cancellationToken);
            }
        }

        _logger?.LogDebug("[IO-雷赛HAL] 批量写入 {GroupName} 端口数={Count}", Name, portStates.Count);
    }
}
