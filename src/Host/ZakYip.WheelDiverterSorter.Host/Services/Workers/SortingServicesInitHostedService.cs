using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 分拣服务初始化后台服务
/// </summary>
/// <remarks>
/// 在程序启动时立即启动传感器检测服务和分拣编排服务。
/// 
/// <para>
/// <b>启动流程：</b><br/>
/// 1. 调用 ISortingOrchestrator.StartAsync()<br/>
/// 2. 编排器内部启动传感器监听（调用 ISensorEventProvider.StartAsync()）<br/>
/// 3. 编排器连接到上游系统（调用 IUpstreamRoutingClient.ConnectAsync()）<br/>
/// 4. 传感器开始轮询硬件IO端口，检测包裹到达
/// </para>
/// 
/// <para>
/// <b>设计原则：</b>
/// </para>
/// <list type="bullet">
/// <item><description>传感器和面板IO属于同一性质的监控，都在程序启动时立即生效</description></item>
/// <item><description>取代之前的 SensorActivationWorker，采用更简洁的实现</description></item>
/// <item><description>状态拦截在业务逻辑层实现，而非传感器层</description></item>
/// </list>
/// 
/// <para>
/// <b>实施要求：</b>
/// </para>
/// <list type="bullet">
/// <item><description>使用 ISafeExecutionService 包裹后台任务，符合 copilot-instructions.md 第一节第3条</description></item>
/// </list>
/// </remarks>
public sealed class SortingServicesInitHostedService : IHostedService
{
    private readonly ISortingOrchestrator _orchestrator;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<SortingServicesInitHostedService> _logger;

    public SortingServicesInitHostedService(
        ISortingOrchestrator orchestrator,
        ISafeExecutionService safeExecutor,
        ILogger<SortingServicesInitHostedService> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 启动时初始化分拣服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("========== 分拣服务初始化 ==========");
                
                try
                {
                    // 启动分拣编排服务（会自动启动传感器监听和连接上游）
                    await _orchestrator.StartAsync(cancellationToken);
                    
                    _logger.LogInformation("✅ 分拣服务初始化完成");
                    _logger.LogInformation("  - 传感器监听已启动并开始轮询");
                    _logger.LogInformation("  - 上游连接已建立或将在首次使用时建立");
                    _logger.LogInformation("  - 分拣编排服务已就绪");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 分拣服务初始化失败");
                    throw;
                }
                finally
                {
                    _logger.LogInformation("=======================================");
                }
            },
            operationName: "SortingServicesInitialization",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止分拣服务...");
        await _orchestrator.StopAsync();
        _logger.LogInformation("分拣服务已停止");
    }
}
