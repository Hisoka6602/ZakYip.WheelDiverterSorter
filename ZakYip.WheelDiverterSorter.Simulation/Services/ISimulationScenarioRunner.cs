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
}
