namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// Worker 后台服务配置
/// </summary>
/// <remarks>
/// 统一管理所有 BackgroundService/IHostedService 的轮询间隔和异常恢复延迟配置。
/// TD-054: 从 appsettings.json (WorkerOptions) 迁移到 SystemConfiguration，支持 API 动态管理。
/// </remarks>
public class WorkerConfiguration
{
    /// <summary>
    /// 状态检查轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// <para>设置为 500ms 以实现快速响应状态变化，同时避免过度轮询。</para>
    /// 
    /// <para>**建议范围**：100ms - 1000ms</para>
    /// <list type="bullet">
    /// <item>较低值（100-300ms）：更快的状态变化响应，CPU开销略高</item>
    /// <item>中等值（400-600ms）：平衡响应时间和资源占用（推荐）</item>
    /// <item>较高值（700-1000ms）：降低CPU占用，但状态变化响应较慢</item>
    /// </list>
    /// 
    /// <para>**适用场景**：</para>
    /// <list type="bullet">
    /// <item>SensorActivationWorker: 监控系统状态变化以启停传感器</item>
    /// <item>SystemStateWheelDiverterCoordinator: 监控系统状态以协调摆轮</item>
    /// </list>
    /// </remarks>
    public int StateCheckIntervalMs { get; set; } = 500;

    /// <summary>
    /// 异常恢复延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// <para>当 Worker 发生异常时等待此时间后再重试，避免在故障情况下快速循环消耗资源。</para>
    /// <para>这个延迟给系统足够时间从瞬时故障中恢复。</para>
    /// 
    /// <para>**建议范围**：1000ms - 5000ms</para>
    /// <list type="bullet">
    /// <item>较低值（1000-1500ms）：快速重试，适用于瞬时故障</item>
    /// <item>中等值（2000-3000ms）：平衡重试速度和资源保护（推荐）</item>
    /// <item>较高值（4000-5000ms）：降低故障期间的资源消耗</item>
    /// </list>
    /// 
    /// <para>**适用场景**：</para>
    /// <list type="bullet">
    /// <item>硬件通信临时中断</item>
    /// <item>网络连接临时故障</item>
    /// <item>资源锁冲突临时情况</item>
    /// </list>
    /// </remarks>
    public int ErrorRecoveryDelayMs { get; set; } = 2000;
}
