using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZakYip.WheelDiverterSorter.Observability.Tracing;

/// <summary>
/// 日志清理后台服务
/// </summary>
/// <remarks>
/// 定期执行日志清理任务，防止日志文件占满磁盘。
/// </remarks>
public class LogCleanupHostedService : IHostedService, IDisposable
{
    private readonly ILogger<LogCleanupHostedService> _logger;
    private readonly ILogCleanupPolicy _cleanupPolicy;
    private readonly LogCleanupOptions _options;
    private Timer? _timer;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LogCleanupHostedService(
        ILogger<LogCleanupHostedService> logger,
        ILogCleanupPolicy cleanupPolicy,
        IOptions<LogCleanupOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cleanupPolicy = cleanupPolicy ?? throw new ArgumentNullException(nameof(cleanupPolicy));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 启动服务
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("日志清理服务启动。清理间隔: {Interval} 小时", _options.CleanupIntervalHours);

        // 立即执行一次清理（但不等待）
        _ = Task.Run(async () =>
        {
            try
            {
                await _cleanupPolicy.CleanupAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "日志清理任务执行失败");
            }
        }, cancellationToken);

        // 设置定时器，定期执行清理
        var intervalMs = TimeSpan.FromHours(_options.CleanupIntervalHours).TotalMilliseconds;
        _timer = new Timer(
            callback: async _ =>
            {
                try
                {
                    await _cleanupPolicy.CleanupAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "定时日志清理任务执行失败");
                }
            },
            state: null,
            dueTime: (int)intervalMs,
            period: (int)intervalMs);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("日志清理服务停止");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _timer?.Dispose();
    }
}
