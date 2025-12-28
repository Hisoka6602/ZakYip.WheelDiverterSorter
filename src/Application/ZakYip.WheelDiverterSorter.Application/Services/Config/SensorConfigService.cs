using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 传感器配置服务实现
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public class SensorConfigService : ISensorConfigService
{
    private static readonly object SensorConfigCacheKey = new();

    private readonly ISensorConfigurationRepository _repository;
    private readonly ISlidingConfigCache _configCache;
    private readonly ILogger<SensorConfigService> _logger;

    public SensorConfigService(
        ISensorConfigurationRepository repository,
        ISlidingConfigCache configCache,
        ILogger<SensorConfigService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SensorConfiguration GetSensorConfig()
    {
        return _configCache.GetOrAdd(SensorConfigCacheKey, () => _repository.Get());
    }

    public void UpdateSensorConfig(SensorConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger.LogInformation("更新传感器配置: 传感器数={SensorCount}", configuration.Sensors.Count);

        // 先持久化
        _repository.Update(configuration);

        // 立即刷新缓存（热更新）
        var updatedConfig = _repository.Get();
        _configCache.Set(SensorConfigCacheKey, updatedConfig);

        _logger.LogInformation("传感器配置已更新并刷新缓存");
    }

    public SensorConfiguration RefreshCacheFromRepository()
    {
        var updatedConfig = _repository.Get();
        _configCache.Set(SensorConfigCacheKey, updatedConfig);
        _logger.LogDebug("传感器配置缓存已刷新");
        return updatedConfig;
    }
}
