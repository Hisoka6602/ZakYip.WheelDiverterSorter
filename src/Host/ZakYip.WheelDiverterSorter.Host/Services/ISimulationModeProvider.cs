namespace ZakYip.WheelDiverterSorter.Host.Services;

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
