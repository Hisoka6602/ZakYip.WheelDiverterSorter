namespace ZakYip.WheelDiverterSorter.Host.Configuration;

/// <summary>
/// 后台工作服务配置选项
/// </summary>
/// <remarks>
/// 统一管理所有 BackgroundService/IHostedService 的轮询间隔和异常恢复延迟配置。
/// 这些参数可通过 appsettings.json 配置，便于在不同环境下调整性能和响应时间。
/// 
/// **TD-053**: 原硬编码的 StateCheckIntervalMs=500ms 和 ErrorRecoveryDelayMs=2000ms 现在可配置化。
/// </remarks>
public class WorkerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Worker";

    /// <summary>
    /// 状态检查轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 设置为 500ms 以实现快速响应状态变化，同时避免过度轮询。
    /// 
    /// **建议范围**：100ms - 1000ms
    /// - 较低值（100-300ms）：更快的状态变化响应，CPU开销略高
    /// - 中等值（400-600ms）：平衡响应时间和资源占用（推荐）
    /// - 较高值（700-1000ms）：降低CPU占用，但状态变化响应较慢
    /// 
    /// **适用场景**：
    /// - SensorActivationWorker: 监控系统状态变化以启停传感器
    /// - SystemStateWheelDiverterCoordinator: 监控系统状态以协调摆轮
    /// </remarks>
    public int StateCheckIntervalMs { get; set; } = 500;

    /// <summary>
    /// 异常恢复延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 当 Worker 发生异常时等待此时间后再重试，避免在故障情况下快速循环消耗资源。
    /// 这个延迟给系统足够时间从瞬时故障中恢复。
    /// 
    /// **建议范围**：1000ms - 5000ms
    /// - 较低值（1000-1500ms）：快速重试，适用于瞬时故障
    /// - 中等值（2000-3000ms）：平衡重试速度和资源保护（推荐）
    /// - 较高值（4000-5000ms）：降低故障期间的资源消耗
    /// 
    /// **适用场景**：
    /// - 硬件通信临时中断
    /// - 网络连接临时故障
    /// - 资源锁冲突临时情况
    /// </remarks>
    public int ErrorRecoveryDelayMs { get; set; } = 2000;
}
