using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;

namespace ZakYip.WheelDiverterSorter.Host.Services.RuntimeProfiles;

/// <summary>
/// 生产模式配置文件
/// Production runtime profile using real hardware drivers and upstream systems
/// </summary>
public sealed class ProductionRuntimeProfile : IRuntimeProfile
{
    /// <inheritdoc />
    public RuntimeMode Mode => RuntimeMode.Production;

    /// <inheritdoc />
    public bool UseHardwareDriver => true;

    /// <inheritdoc />
    public bool IsSimulationMode => false;

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
    public string GetModeDescription() => "生产模式 - 使用真实硬件驱动，连接实际上游系统";
}
