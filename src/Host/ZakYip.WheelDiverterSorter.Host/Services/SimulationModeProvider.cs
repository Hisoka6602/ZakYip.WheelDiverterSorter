using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 仿真模式提供者实现
/// </summary>
/// <remarks>
/// 通过检查是否使用 SimulatedPanelInputReader 或 SimulatedSignalTowerOutput 来判断是否为仿真模式
/// </remarks>
public class SimulationModeProvider : ISimulationModeProvider
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 初始化仿真模式提供者
    /// </summary>
    /// <param name="serviceProvider">服务提供者，用于延迟解析可选的面板和信号塔服务</param>
    public SimulationModeProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public bool IsSimulationMode()
    {
        // 尝试获取面板读取器和信号塔输出（这些服务可能不存在）
        var panelInputReader = _serviceProvider.GetService(typeof(IPanelInputReader));
        var signalTowerOutput = _serviceProvider.GetService(typeof(ISignalTowerOutput));

        // 如果面板读取器或信号塔输出是仿真实现，则认为是仿真模式
        return panelInputReader is SimulatedPanelInputReader ||
               signalTowerOutput is SimulatedSignalTowerOutput;
    }
}
