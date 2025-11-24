using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ISystemClock _clock;

    public Worker(
        ILogger<Worker> logger,
        ISafeExecutionService safeExecutor,
        ISystemClock clock)
    {
        _logger = logger;
        _safeExecutor = safeExecutor;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Worker running at: {time}", _clock.LocalNow);
                    }
                    await Task.Delay(1000, stoppingToken);
                }
            },
            "WorkerLoop",
            stoppingToken);
    }
}
