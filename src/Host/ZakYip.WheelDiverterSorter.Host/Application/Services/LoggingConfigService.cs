using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Application.Services;

/// <summary>
/// 日志配置服务实现
/// </summary>
public class LoggingConfigService : ILoggingConfigService
{
    private readonly ILoggingConfigurationRepository _repository;
    private readonly ILogger<LoggingConfigService> _logger;

    public LoggingConfigService(
        ILoggingConfigurationRepository repository,
        ILogger<LoggingConfigService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public LoggingConfiguration GetLoggingConfig()
    {
        return _repository.Get();
    }

    public LoggingConfiguration GetDefaultTemplate()
    {
        return LoggingConfiguration.GetDefault();
    }

    public async Task<LoggingConfigUpdateResult> UpdateLoggingConfigAsync(LoggingConfigRequest request)
    {
        await Task.Yield();
        try
        {
            var config = MapToConfiguration(request);

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                _logger.LogWarning("日志配置验证失败: {ErrorMessage}", errorMessage);
                return new LoggingConfigUpdateResult(false, errorMessage, null);
            }

            // 更新配置
            _repository.Update(config);

            _logger.LogInformation(
                "日志配置已更新: ParcelLifecycle={ParcelLifecycle}, ParcelTrace={ParcelTrace}, PathExecution={PathExecution}, Communication={Communication}, Driver={Driver}, Performance={Performance}, Alarm={Alarm}, Debug={Debug}, Version={Version}",
                config.EnableParcelLifecycleLog,
                config.EnableParcelTraceLog,
                config.EnablePathExecutionLog,
                config.EnableCommunicationLog,
                config.EnableDriverLog,
                config.EnablePerformanceLog,
                config.EnableAlarmLog,
                config.EnableDebugLog,
                config.Version);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return new LoggingConfigUpdateResult(true, null, updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "日志配置验证失败");
            return new LoggingConfigUpdateResult(false, ex.Message, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新日志配置失败");
            return new LoggingConfigUpdateResult(false, "更新日志配置失败", null);
        }
    }

    public async Task<LoggingConfiguration> ResetLoggingConfigAsync()
    {
        await Task.Yield();
        var defaultConfig = LoggingConfiguration.GetDefault();
        _repository.Update(defaultConfig);

        _logger.LogInformation("日志配置已重置为默认值");

        return _repository.Get();
    }

    private static LoggingConfiguration MapToConfiguration(LoggingConfigRequest request)
    {
        return new LoggingConfiguration
        {
            EnableParcelLifecycleLog = request.EnableParcelLifecycleLog,
            EnableParcelTraceLog = request.EnableParcelTraceLog,
            EnablePathExecutionLog = request.EnablePathExecutionLog,
            EnableCommunicationLog = request.EnableCommunicationLog,
            EnableDriverLog = request.EnableDriverLog,
            EnablePerformanceLog = request.EnablePerformanceLog,
            EnableAlarmLog = request.EnableAlarmLog,
            EnableDebugLog = request.EnableDebugLog
        };
    }
}
