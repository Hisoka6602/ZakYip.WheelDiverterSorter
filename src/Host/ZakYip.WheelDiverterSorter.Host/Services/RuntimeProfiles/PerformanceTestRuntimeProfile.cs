using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;

namespace ZakYip.WheelDiverterSorter.Host.Services.RuntimeProfiles;

/// <summary>
/// 性能测试模式配置文件
/// Performance test runtime profile skipping actual IO to focus on path/algorithm performance
/// </summary>
public sealed class PerformanceTestRuntimeProfile : IRuntimeProfile
{
    /// <inheritdoc />
    public RuntimeMode Mode => RuntimeMode.PerformanceTest;

    /// <inheritdoc />
    public bool UseHardwareDriver => false;

    /// <inheritdoc />
    public bool IsSimulationMode => false;

    /// <inheritdoc />
    public bool IsPerformanceTestMode => true;

    /// <inheritdoc />
    public bool EnableIoOperations => false;

    /// <inheritdoc />
    public bool EnableUpstreamCommunication => false;

    /// <inheritdoc />
    public bool EnableHealthCheckTasks => false;

    /// <inheritdoc />
    public bool EnablePerformanceMonitoring => true;

    /// <inheritdoc />
    public string GetModeDescription() => "性能测试模式 - 跳过实际 IO，专注于路径/算法性能测试";
}
