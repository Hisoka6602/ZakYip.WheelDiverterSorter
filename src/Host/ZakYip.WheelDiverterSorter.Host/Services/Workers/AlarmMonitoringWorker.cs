using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 告警监控后台服务 / Alarm Monitoring Background Service
/// Periodically checks alarm conditions and maintains alarm state
/// </summary>
public class AlarmMonitoringWorker : BackgroundService
{
    private readonly ILogger<AlarmMonitoringWorker> _logger;
    private readonly AlarmService _alarmService;
    private readonly ISafeExecutionService _safeExecutor;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    public AlarmMonitoringWorker(
        ILogger<AlarmMonitoringWorker> logger,
        AlarmService alarmService,
        ISafeExecutionService safeExecutor)
    {
        _logger = logger;
        _alarmService = alarmService;
        _safeExecutor = safeExecutor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("告警监控服务启动 / Alarm monitoring service started");

        // Report system restart alarm
        await _safeExecutor.ExecuteAsync(
            () =>
            {
                _alarmService.ReportSystemRestart();
                return Task.CompletedTask;
            },
            "ReportSystemRestart",
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _safeExecutor.ExecuteAsync(
                async () =>
                {
                    // Check RuleEngine disconnection duration
                    _alarmService.CheckRuleEngineDisconnection();

                    // Log active alarms count
                    var activeAlarms = _alarmService.GetActiveAlarms();
                    if (activeAlarms.Count > 0)
                    {
                        _logger.LogWarning(
                            "当前活跃告警数量 / Active alarms count: {Count}",
                            activeAlarms.Count);
                    }

                    await Task.Delay(CheckInterval, stoppingToken);
                },
                "AlarmMonitoringCheck",
                stoppingToken);
        }

        _logger.LogInformation("告警监控服务停止 / Alarm monitoring service stopped");
    }
}
