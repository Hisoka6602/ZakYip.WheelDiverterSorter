using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 仿真场景运行器实现
/// </summary>
/// <remarks>
/// 负责按配置运行各种仿真场景，与系统状态机集成
/// </remarks>
public class SimulationScenarioRunner
{
    private readonly ILineTopologyConfigProvider _topologyProvider;
    private readonly IOptionsMonitor<SimulationOptions> _simulationOptions;
    private readonly SimulationRunner _simulationRunner;
    private readonly ILogger<SimulationScenarioRunner> _logger;
    private static SimulationOptions? _runtimeOptions;

    public SimulationScenarioRunner(
        ILineTopologyConfigProvider topologyProvider,
        IOptionsMonitor<SimulationOptions> simulationOptions,
        SimulationRunner simulationRunner,
        ILogger<SimulationScenarioRunner> logger)
    {
        _topologyProvider = topologyProvider;
        _simulationOptions = simulationOptions;
        _simulationRunner = simulationRunner;
        _logger = logger;
    }

    /// <summary>
    /// 设置运行时配置（内部使用，由 SimulationConfigController 调用）
    /// </summary>
    public static void SetRuntimeOptions(SimulationOptions? options)
    {
        _runtimeOptions = options;
    }

    /// <summary>
    /// 获取运行时配置
    /// </summary>
    public static SimulationOptions? GetRuntimeOptions()
    {
        return _runtimeOptions;
    }

    /// <summary>
    /// 运行场景 E 长跑仿真
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>仿真任务</returns>
    /// <remarks>
    /// 场景 E 配置：
    /// - 从 ILineTopologyConfigProvider 读取拓扑配置
    /// - 从 SimulationOptions 读取仿真参数
    /// - 每 300ms 创建一个包裹
    /// - 总共 1000 个包裹
    /// - 目标格口随机分配（在拓扑配置的格口范围内）
    /// - 异常格口为配置中的 ExceptionChuteId
    /// </remarks>
    public async Task RunScenarioEAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("准备运行场景 E 长跑仿真...");

            // 读取拓扑配置
            var topology = await _topologyProvider.GetTopologyAsync();
            _logger.LogInformation(
                "已加载拓扑配置: {TopologyId}, 摆轮数: {WheelCount}, 格口数: {ChuteCount}",
                topology.TopologyId,
                topology.WheelNodes.Count,
                topology.Chutes.Count);

            // 读取仿真配置（优先使用运行时配置）
            var options = _runtimeOptions ?? _simulationOptions.CurrentValue;

            _logger.LogInformation(
                "使用仿真配置: 包裹数={ParcelCount}, 间隔={IntervalMs}ms, 模式={SortingMode}",
                options.ParcelCount,
                options.ParcelInterval.TotalMilliseconds,
                options.SortingMode);

            // 验证配置
            if (options.ParcelCount <= 0)
            {
                throw new InvalidOperationException("包裹数量必须大于 0");
            }

            if (options.ParcelInterval.TotalMilliseconds <= 0)
            {
                throw new InvalidOperationException("包裹间隔必须大于 0");
            }

            // 运行仿真
            _logger.LogInformation("开始执行场景 E 仿真...");
            var summary = await _simulationRunner.RunAsync(cancellationToken);

            _logger.LogInformation(
                "场景 E 仿真完成: 正常={Normal}, 超时={Timeout}, 掉包={Dropped}, 错分={MisSort}, 最大并发={MaxConcurrent}",
                summary.SortedToTargetChuteCount,
                summary.TimeoutCount,
                summary.DroppedCount,
                summary.SortedToWrongChuteCount,
                _simulationRunner.MaxConcurrentParcelsObserved);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("场景 E 仿真被取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "场景 E 仿真执行失败");
            throw;
        }
    }
}
