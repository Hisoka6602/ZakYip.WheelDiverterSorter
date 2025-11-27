using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 仿真模式提供者实现
/// </summary>
/// <remarks>
/// 委托给 IRuntimeProfile 判断是否为仿真模式。
/// 如果 IRuntimeProfile 未注册，回退到检查驱动器实现。
/// Delegates to IRuntimeProfile to determine if in simulation mode.
/// Falls back to checking driver implementations if IRuntimeProfile is not registered.
/// </remarks>
public class SimulationModeProvider : ISimulationModeProvider
{
    private readonly IRuntimeProfile? _runtimeProfile;

    /// <summary>
    /// 初始化仿真模式提供者
    /// </summary>
    /// <param name="runtimeProfile">运行时配置文件（可选，用于新的统一模式判断）</param>
    public SimulationModeProvider(IRuntimeProfile? runtimeProfile = null)
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
