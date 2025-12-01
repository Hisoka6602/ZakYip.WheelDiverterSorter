using ZakYip.WheelDiverterSorter.Simulation.Scenarios.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 仿真场景运行器接口
/// </summary>
public interface ISimulationScenarioRunner
{
    /// <summary>
    /// 运行场景 E 长跑仿真
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>仿真任务</returns>
    Task RunScenarioEAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 运行任意仿真场景
    /// </summary>
    /// <param name="options">仿真配置选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>仿真结果摘要</returns>
    Task<SimulationSummary> RunScenarioAsync(SimulationOptions options, CancellationToken cancellationToken = default);
}
