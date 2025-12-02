using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 日志配置服务实现
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public class LoggingConfigService : ILoggingConfigService
{
    private static readonly object LoggingConfigCacheKey = new();

    private readonly ILoggingConfigurationRepository _repository;
    private readonly ISlidingConfigCache _configCache;
    private readonly ILogger<LoggingConfigService> _logger;

    public LoggingConfigService(
        ILoggingConfigurationRepository repository,
        ISlidingConfigCache configCache,
        ILogger<LoggingConfigService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public LoggingConfiguration GetLoggingConfig()
    {
        return _configCache.GetOrAdd(LoggingConfigCacheKey, () => _repository.Get());
    }

    public LoggingConfiguration GetDefaultTemplate()
    {
        return LoggingConfiguration.GetDefault();
    }

    public async Task<LoggingConfigUpdateResult> UpdateLoggingConfigAsync(UpdateLoggingConfigCommand request)
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

            // 更新配置到持久化
            _repository.Update(config);

            // 热更新：立即刷新缓存
            var updatedConfig = _repository.Get();
            _configCache.Set(LoggingConfigCacheKey, updatedConfig);

            _logger.LogInformation(
                "日志配置已更新（热更新生效）: ParcelLifecycle={ParcelLifecycle}, ParcelTrace={ParcelTrace}, PathExecution={PathExecution}, Communication={Communication}, Driver={Driver}, Performance={Performance}, Alarm={Alarm}, Debug={Debug}, Version={Version}",
                updatedConfig.EnableParcelLifecycleLog,
                updatedConfig.EnableParcelTraceLog,
                updatedConfig.EnablePathExecutionLog,
                updatedConfig.EnableCommunicationLog,
                updatedConfig.EnableDriverLog,
                updatedConfig.EnablePerformanceLog,
                updatedConfig.EnableAlarmLog,
                updatedConfig.EnableDebugLog,
                updatedConfig.Version);

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

        // 热更新：立即刷新缓存
        var updatedConfig = _repository.Get();
        _configCache.Set(LoggingConfigCacheKey, updatedConfig);

        _logger.LogInformation("日志配置已重置为默认值（热更新生效）");

        return updatedConfig;
    }

    private static LoggingConfiguration MapToConfiguration(UpdateLoggingConfigCommand request)
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
