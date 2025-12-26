using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using csLTDMC;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛IO状态缓存服务实现
/// </summary>
/// <remarks>
/// 这是系统中唯一读取雷赛硬件IO的地方。
/// 后台服务每10ms批量读取所有输入端口并缓存，其他组件从缓存读取。
/// 
/// 架构约束（Critical for Real-time Performance）：
/// - 禁止在此服务外部调用 dmc_read_inbit
/// - 禁止在此服务外部调用 dmc_read_inport
/// - 禁止在此服务外部调用 dmc_read_inport_array
/// - 传感器IO实时性是整个项目的核心，失实将导致整个项目失败
/// </remarks>
public sealed class LeadshineIoStateCache : BackgroundService, ILeadshineIoStateCache
{
    private readonly ILogger<LeadshineIoStateCache> _logger;
    private readonly IEmcController _emcController;
    private readonly ISystemClock _systemClock;
    private readonly ISafeExecutionService _safeExecutor;
    
    /// <summary>
    /// IO刷新间隔（毫秒），硬编码为10ms以确保实时性
    /// </summary>
    private const int RefreshIntervalMs = 10;
    
    /// <summary>
    /// 每个端口的位数（雷赛控制器每个端口为32位）
    /// </summary>
    private const int BitsPerPort = 32;
    
    /// <summary>
    /// 输入端口缓存（端口号 -> 端口值）
    /// 使用ConcurrentDictionary确保线程安全
    /// </summary>
    private readonly ConcurrentDictionary<ushort, uint> _inputPortCache = new();
    
    /// <summary>
    /// 输入端口总数
    /// </summary>
    private ushort _totalInputPorts;
    
    /// <summary>
    /// 最后一次刷新时间
    /// </summary>
    private DateTimeOffset _lastRefreshTime;
    
    /// <summary>
    /// 服务是否可用
    /// </summary>
    private volatile bool _isAvailable;

    public LeadshineIoStateCache(
        ILogger<LeadshineIoStateCache> logger,
        IEmcController emcController,
        ISystemClock systemClock,
        ISafeExecutionService safeExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emcController = emcController ?? throw new ArgumentNullException(nameof(emcController));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
    }

    /// <inheritdoc/>
    public DateTimeOffset LastRefreshTime => _lastRefreshTime;

    /// <inheritdoc/>
    public bool IsAvailable => _isAvailable;

    /// <inheritdoc/>
    public bool ReadInputBit(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex > ushort.MaxValue)
        {
            _logger.LogWarning("位索引超出范围: {BitIndex}", bitIndex);
            return false;
        }

        ushort portNo = (ushort)(bitIndex / BitsPerPort);
        int bitInPort = bitIndex % BitsPerPort;

        if (_inputPortCache.TryGetValue(portNo, out uint portValue))
        {
            return ((portValue >> bitInPort) & 1) != 0;
        }

        _logger.LogWarning("端口 {PortNo} 未在缓存中找到", portNo);
        return false;
    }

    /// <inheritdoc/>
    public IDictionary<int, bool> ReadInputBits(IEnumerable<int> bitIndices)
    {
        var results = new Dictionary<int, bool>();
        
        foreach (var bitIndex in bitIndices)
        {
            results[bitIndex] = ReadInputBit(bitIndex);
        }
        
        return results;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                // 初始化：获取输入端口总数
                _totalInputPorts = GetTotalInputPorts();
                
                _logger.LogInformation(
                    "雷赛IO状态缓存服务启动，刷新间隔={RefreshMs}ms，输入端口数={PortCount}",
                    RefreshIntervalMs,
                    _totalInputPorts);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!_emcController.IsAvailable())
                        {
                            _logger.LogWarning("EMC控制器不可用，跳过此次IO刷新");
                            _isAvailable = false;
                            await Task.Delay(RefreshIntervalMs, stoppingToken);
                            continue;
                        }

                        // 这是系统中唯一调用雷赛IO读取函数的地方
                        // 批量读取所有输入端口
                        await RefreshAllInputPortsAsync();
                        
                        _isAvailable = true;
                        _lastRefreshTime = _systemClock.LocalNowOffset;
                        
                        // 等待下一次刷新周期
                        await Task.Delay(RefreshIntervalMs, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "刷新IO状态时发生异常");
                        _isAvailable = false;
                        
                        // 发生异常后稍作延迟再重试
                        await Task.Delay(RefreshIntervalMs, stoppingToken);
                    }
                }

                _logger.LogInformation("雷赛IO状态缓存服务已停止");
            },
            "LeadshineIoStateCacheLoop",
            stoppingToken);
    }

    /// <summary>
    /// 刷新所有输入端口状态
    /// </summary>
    /// <remarks>
    /// 这是系统中唯一允许调用 dmc_read_inport_array 的方法。
    /// 禁止在其他任何地方调用雷赛IO读取函数（dmc_read_inbit/dmc_read_inport/dmc_read_inport_array）。
    /// </remarks>
    private Task RefreshAllInputPortsAsync()
    {
        if (_totalInputPorts == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            // 批量读取所有输入端口（这是唯一允许的硬件IO调用点）
            uint[] portValues = new uint[_totalInputPorts];
            short result = LTDMC.dmc_read_inport_array(_emcController.CardNo, _totalInputPorts, portValues);

            if (result < 0)
            {
                _logger.LogWarning(
                    "批量读取输入端口失败，错误码={ErrorCode}",
                    result);
                return Task.CompletedTask;
            }

            // 更新缓存
            for (ushort i = 0; i < _totalInputPorts; i++)
            {
                _inputPortCache[i] = portValues[i];
            }

            _logger.LogDebug(
                "成功刷新{PortCount}个输入端口状态",
                _totalInputPorts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新输入端口状态时发生异常");
        }

        return Task.CompletedTask;
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
            short result = LTDMC.dmc_get_total_ionum(_emcController.CardNo, ref totalIn, ref totalOut);
            
            if (result < 0)
            {
                _logger.LogWarning(
                    "获取IO端口总数失败，卡号={CardNo}, 错误码={ErrorCode}，使用默认值8个端口",
                    _emcController.CardNo,
                    result);
                return 8; // 默认8个端口（256位）
            }

            // totalIn 是输入位总数，需要转换为端口数
            ushort portCount = (ushort)((totalIn + BitsPerPort - 1) / BitsPerPort);
            
            _logger.LogInformation(
                "雷赛控制器 {CardNo} 输入端口信息: 输入位总数={TotalIn}, 端口数={PortCount}",
                _emcController.CardNo,
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
