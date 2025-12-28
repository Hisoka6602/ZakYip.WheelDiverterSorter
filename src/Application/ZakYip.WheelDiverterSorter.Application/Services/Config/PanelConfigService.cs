using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 面板配置服务实现
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public class PanelConfigService : IPanelConfigService
{
    private static readonly object PanelConfigCacheKey = new();

    private readonly IPanelConfigurationRepository _repository;
    private readonly ISlidingConfigCache _configCache;
    private readonly ILogger<PanelConfigService> _logger;

    public PanelConfigService(
        IPanelConfigurationRepository repository,
        ISlidingConfigCache configCache,
        ILogger<PanelConfigService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public PanelConfiguration GetPanelConfig()
    {
        return _configCache.GetOrAdd(PanelConfigCacheKey, () => _repository.Get());
    }

    public void UpdatePanelConfig(PanelConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger.LogInformation("更新面板配置");

        // 先持久化
        _repository.Update(configuration);

        // 立即刷新缓存（热更新）
        var updatedConfig = _repository.Get();
        _configCache.Set(PanelConfigCacheKey, updatedConfig);

        _logger.LogInformation("面板配置已更新并刷新缓存");
    }

    public PanelConfiguration RefreshCacheFromRepository()
    {
        var updatedConfig = _repository.Get();
        _configCache.Set(PanelConfigCacheKey, updatedConfig);
        _logger.LogDebug("面板配置缓存已刷新");
        return updatedConfig;
    }
}
