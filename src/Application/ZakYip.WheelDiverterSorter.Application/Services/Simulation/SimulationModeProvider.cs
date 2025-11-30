using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;

namespace ZakYip.WheelDiverterSorter.Application.Services.Simulation;

/// <summary>
/// 仿真模式提供者接口
/// </summary>
/// <remarks>
/// 用于判断系统当前是否运行在仿真模式下
/// </remarks>
public interface ISimulationModeProvider
{
    /// <summary>
    /// 判断当前是否为仿真模式
    /// </summary>
    /// <returns>如果当前使用仿真驱动器，返回 true；否则返回 false</returns>
    bool IsSimulationMode();
}

/// <summary>
/// 仿真模式提供者实现
/// </summary>
/// <remarks>
/// 委托给 IRuntimeProfile 判断是否为仿真模式。
/// 如果 IRuntimeProfile 未注册，回退到默认的非仿真模式。
/// Delegates to IRuntimeProfile to determine if in simulation mode.
/// Falls back to default non-simulation mode if IRuntimeProfile is not registered.
/// </remarks>
public class SimulationModeProvider : ISimulationModeProvider
{
    private readonly IRuntimeProfile? _runtimeProfile;

    /// <summary>
    /// 初始化仿真模式提供者
    /// </summary>
    /// <param name="runtimeProfile">运行时配置文件，用于统一模式判断</param>
    public SimulationModeProvider(IRuntimeProfile? runtimeProfile)
    {
        _runtimeProfile = runtimeProfile;
    }

    /// <inheritdoc />
    public bool IsSimulationMode()
    {
        // 优先使用 IRuntimeProfile 判断
        if (_runtimeProfile != null)
        {
            return _runtimeProfile.IsSimulationMode;
        }

        // 向后兼容：如果 IRuntimeProfile 未注册，默认为非仿真模式
        return false;
    }
}
