using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;

namespace ZakYip.WheelDiverterSorter.Host.Services.RuntimeProfiles;

/// <summary>
/// 仿真模式配置文件
/// Simulation runtime profile using simulated drivers and virtual sensors
/// </summary>
public sealed class SimulationRuntimeProfile : IRuntimeProfile
{
    /// <inheritdoc />
    public RuntimeMode Mode => RuntimeMode.Simulation;

    /// <inheritdoc />
    public bool UseHardwareDriver => false;

    /// <inheritdoc />
    public bool IsSimulationMode => true;

    /// <inheritdoc />
    public bool IsPerformanceTestMode => false;

    /// <inheritdoc />
    public bool EnableIoOperations => true;

    /// <inheritdoc />
    public bool EnableUpstreamCommunication => true;

    /// <inheritdoc />
    public bool EnableHealthCheckTasks => true;

    /// <inheritdoc />
    public bool EnablePerformanceMonitoring => true;

    /// <inheritdoc />
    public string GetModeDescription() => "仿真模式 - 使用模拟驱动器，虚拟传感器和条码源";
}
