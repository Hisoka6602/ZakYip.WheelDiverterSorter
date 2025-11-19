namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 包裹分拣后台工作服务
/// </summary>
/// <remarks>
/// 负责管理ParcelSortingOrchestrator的生命周期，
/// 在应用启动时自动启动分拣编排服务，在应用停止时自动停止
/// </remarks>
public class ParcelSortingWorker : BackgroundService
{
    private readonly ParcelSortingOrchestrator _orchestrator;
    private readonly ILogger<ParcelSortingWorker> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ParcelSortingWorker(
        ParcelSortingOrchestrator orchestrator,
        ILogger<ParcelSortingWorker> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行后台任务
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("包裹分拣后台服务正在启动...");

        try
        {
            // 启动编排服务
            await _orchestrator.StartAsync(stoppingToken);

            // 保持运行直到取消
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("包裹分拣后台服务正在停止...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "包裹分拣后台服务发生异常");
            throw;
        }
        finally
        {
            // 停止编排服务
            await _orchestrator.StopAsync();
        }
    }
}
