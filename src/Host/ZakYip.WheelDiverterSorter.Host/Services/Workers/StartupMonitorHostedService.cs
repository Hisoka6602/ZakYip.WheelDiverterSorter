using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 启动监控后台服务
/// </summary>
/// <remarks>
/// 该服务用于监控应用程序启动过程，确保启动不会超时。
/// 如果启动过程耗时过长（超过配置的阈值），会记录警告日志。
/// 这对于诊断 Windows Service 1053 错误（服务未及时响应）特别有用。
/// </remarks>
public sealed class StartupMonitorHostedService : IHostedService
{
    /// <summary>
    /// 启动超时阈值（毫秒）- Windows Service 默认超时为 30 秒
    /// </summary>
    private const int StartupWarningThresholdMs = 25000; // 25 秒警告阈值
    
    private readonly ILogger<StartupMonitorHostedService> _logger;
    private readonly System.Diagnostics.Stopwatch _stopwatch;

    public StartupMonitorHostedService(ILogger<StartupMonitorHostedService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    }

    /// <summary>
    /// 启动时记录启动监控信息
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stopwatch.Stop();
        var elapsedMs = _stopwatch.ElapsedMilliseconds;
        
        _logger.LogInformation("========== 启动监控报告 ==========");
        _logger.LogInformation($"应用程序启动耗时: {elapsedMs} ms ({elapsedMs / 1000.0:F2} 秒)");
        
        if (elapsedMs > StartupWarningThresholdMs)
        {
            _logger.LogWarning(
                "⚠️ 应用程序启动耗时过长: {ElapsedMs} ms，超过警告阈值 {ThresholdMs} ms",
                elapsedMs, StartupWarningThresholdMs);
            _logger.LogWarning(
                "这可能导致 Windows Service 启动超时（错误 1053）。" +
                "请检查以下可能的原因：");
            _logger.LogWarning("  1. 硬件设备初始化耗时过长（IO 板卡、PLC 等）");
            _logger.LogWarning("  2. 数据库连接或查询耗时过长");
            _logger.LogWarning("  3. 网络连接检查耗时过长（上游系统、设备等）");
            _logger.LogWarning("  4. 配置文件加载或验证耗时过长");
            _logger.LogWarning("  5. 大量后台服务同时启动");
            _logger.LogWarning("建议: 考虑异步初始化非关键组件，或增加 Windows Service 启动超时时间。");
        }
        else
        {
            _logger.LogInformation("✅ 应用程序启动速度正常");
        }
        
        _logger.LogInformation("====================================");
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务（无操作）
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartupMonitor service停止");
        return Task.CompletedTask;
    }
}
