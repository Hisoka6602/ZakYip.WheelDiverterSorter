using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Utilities;

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
    private readonly ISafeExecutionService _safeExecutor;
    private Timer? _timer;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LogCleanupHostedService(
        ILogger<LogCleanupHostedService> logger,
        ILogCleanupPolicy cleanupPolicy,
        IOptions<LogCleanupOptions> options,
        ISafeExecutionService safeExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cleanupPolicy = cleanupPolicy ?? throw new ArgumentNullException(nameof(cleanupPolicy));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
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
            await _safeExecutor.ExecuteAsync(
                () => _cleanupPolicy.CleanupAsync(cancellationToken),
                "LogCleanup",
                cancellationToken);
        }, cancellationToken);

        // 设置定时器，定期执行清理
        var intervalMs = TimeSpan.FromHours(_options.CleanupIntervalHours).TotalMilliseconds;
        _timer = new Timer(
            callback: async _ =>
            {
                await _safeExecutor.ExecuteAsync(
                    () => _cleanupPolicy.CleanupAsync(CancellationToken.None),
                    "LogCleanup");
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
