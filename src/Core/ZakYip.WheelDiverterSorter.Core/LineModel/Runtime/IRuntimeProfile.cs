using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;

/// <summary>
/// 运行时配置文件接口
/// Runtime profile interface describing driver/upstream implementation combinations
/// </summary>
/// <remarks>
/// 每种运行模式（生产/性能测试）对应一个 IRuntimeProfile 实现，
/// 负责决定使用何种驱动实现、是否启用某些后台任务/观测器等。
/// Each runtime mode (Production/PerformanceTest) corresponds to an IRuntimeProfile implementation,
/// responsible for determining which driver implementations to use and whether to enable certain background tasks/observers.
/// </remarks>
public interface IRuntimeProfile
{
    /// <summary>
    /// 获取当前运行模式
    /// Gets the current runtime mode
    /// </summary>
    RuntimeMode Mode { get; }

    /// <summary>
    /// 是否为性能测试模式
    /// Whether in performance test mode
    /// </summary>
    /// <remarks>
    /// 性能测试模式返回 true，跳过实际 IO 操作以专注于算法性能。
    /// Performance test mode returns true, skipping actual IO operations to focus on algorithm performance.
    /// </remarks>
    bool IsPerformanceTestMode { get; }

    /// <summary>
    /// 是否启用实际 IO 操作
    /// Whether actual IO operations are enabled
    /// </summary>
    /// <remarks>
    /// 生产模式返回 true，性能测试模式返回 false（完全跳过 IO）。
    /// Production mode returns true, PerformanceTest mode returns false (completely skips IO).
    /// </remarks>
    bool EnableIoOperations { get; }

    /// <summary>
    /// 是否启用上游通信
    /// Whether upstream communication is enabled
    /// </summary>
    /// <remarks>
    /// 生产模式连接真实上游，性能测试模式可能禁用上游通信以专注于本地性能。
    /// Production connects to real upstream, PerformanceTest may disable upstream communication to focus on local performance.
    /// </remarks>
    bool EnableUpstreamCommunication { get; }

    /// <summary>
    /// 是否启用健康检查后台任务
    /// Whether health check background tasks are enabled
    /// </summary>
    bool EnableHealthCheckTasks { get; }

    /// <summary>
    /// 是否启用性能监控
    /// Whether performance monitoring is enabled
    /// </summary>
    bool EnablePerformanceMonitoring { get; }

    /// <summary>
    /// 获取当前模式的描述
    /// Gets the description for the current mode
    /// </summary>
    string GetModeDescription();
}
