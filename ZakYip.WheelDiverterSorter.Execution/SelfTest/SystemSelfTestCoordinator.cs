using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Execution.SelfTest;

/// <summary>
/// 系统自检协调器实现
/// </summary>
public class SystemSelfTestCoordinator : ISelfTestCoordinator
{
    private readonly IEnumerable<IDriverSelfTest> _driverTests;
    private readonly IEnumerable<IUpstreamHealthChecker> _upstreamCheckers;
    private readonly IConfigValidator _configValidator;
    private readonly ILogger<SystemSelfTestCoordinator> _logger;

    public SystemSelfTestCoordinator(
        IEnumerable<IDriverSelfTest> driverTests,
        IEnumerable<IUpstreamHealthChecker> upstreamCheckers,
        IConfigValidator configValidator,
        ILogger<SystemSelfTestCoordinator> logger)
    {
        _driverTests = driverTests ?? throw new ArgumentNullException(nameof(driverTests));
        _upstreamCheckers = upstreamCheckers ?? throw new ArgumentNullException(nameof(upstreamCheckers));
        _configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<SystemSelfTestReport> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始系统自检...");
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // 并行执行驱动器自检
            var driverTestTasks = _driverTests
                .Select(test => test.RunSelfTestAsync(cancellationToken))
                .ToList();

            // 并行执行上游系统检查
            var upstreamCheckTasks = _upstreamCheckers
                .Select(checker => checker.CheckAsync(cancellationToken))
                .ToList();

            // 执行配置验证
            var configValidationTask = _configValidator.ValidateAsync(cancellationToken);

            // 等待所有任务完成
            await Task.WhenAll(
                Task.WhenAll(driverTestTasks),
                Task.WhenAll(upstreamCheckTasks),
                configValidationTask);

            var driverResults = driverTestTasks.Select(t => t.Result).ToList();
            var upstreamResults = upstreamCheckTasks.Select(t => t.Result).ToList();
            var configResult = await configValidationTask;

            // 判断自检是否成功
            var isSuccess = driverResults.All(d => d.IsHealthy) &&
                           upstreamResults.All(u => u.IsHealthy) &&
                           configResult.IsValid;

            var report = new SystemSelfTestReport
            {
                IsSuccess = isSuccess,
                Drivers = driverResults.AsReadOnly(),
                Upstreams = upstreamResults.AsReadOnly(),
                Config = configResult,
                PerformedAt = startTime
            };

            if (isSuccess)
            {
                _logger.LogInformation("系统自检成功完成");
            }
            else
            {
                _logger.LogWarning("系统自检失败，存在不健康的组件");
                LogFailedComponents(driverResults, upstreamResults, configResult);
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "系统自检过程中发生异常");
            
            // 返回失败报告
            return new SystemSelfTestReport
            {
                IsSuccess = false,
                Drivers = new List<DriverHealthStatus>().AsReadOnly(),
                Upstreams = new List<UpstreamHealthStatus>().AsReadOnly(),
                Config = new ConfigHealthStatus
                {
                    IsValid = false,
                    ErrorMessage = $"自检过程异常: {ex.Message}"
                },
                PerformedAt = startTime
            };
        }
    }

    private void LogFailedComponents(
        List<DriverHealthStatus> drivers,
        List<UpstreamHealthStatus> upstreams,
        ConfigHealthStatus config)
    {
        foreach (var driver in drivers.Where(d => !d.IsHealthy))
        {
            _logger.LogError("驱动器不健康: {DriverName}, 错误: {ErrorMessage}", 
                driver.DriverName, driver.ErrorMessage);
        }

        foreach (var upstream in upstreams.Where(u => !u.IsHealthy))
        {
            _logger.LogError("上游系统不健康: {EndpointName}, 错误: {ErrorMessage}", 
                upstream.EndpointName, upstream.ErrorMessage);
        }

        if (!config.IsValid)
        {
            _logger.LogError("配置无效: {ErrorMessage}", config.ErrorMessage);
        }
    }
}
