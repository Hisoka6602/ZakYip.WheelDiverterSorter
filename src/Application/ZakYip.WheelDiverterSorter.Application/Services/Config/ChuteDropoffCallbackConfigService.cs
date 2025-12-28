using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Observability.ConfigurationAudit;
using CoreConfig = ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 格口落格回调配置服务实现
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public sealed class ChuteDropoffCallbackConfigService : CoreConfig.IChuteDropoffCallbackConfigService
{
    private static readonly object CallbackConfigCacheKey = new();

    private readonly IChuteDropoffCallbackConfigurationRepository _repository;
    private readonly ISlidingConfigCache _configCache;
    private readonly ILogger<ChuteDropoffCallbackConfigService> _logger;
    private readonly IConfigurationAuditLogger _auditLogger;

    public ChuteDropoffCallbackConfigService(
        IChuteDropoffCallbackConfigurationRepository repository,
        ISlidingConfigCache configCache,
        ILogger<ChuteDropoffCallbackConfigService> logger,
        IConfigurationAuditLogger auditLogger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    /// <inheritdoc />
    public ChuteDropoffCallbackConfiguration GetCallbackConfiguration()
    {
        return _configCache.GetOrAdd(CallbackConfigCacheKey, () => _repository.Get());
    }

    /// <inheritdoc />
    public void UpdateCallbackConfiguration(ChuteDropoffCallbackConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // 获取修改前的配置
        var beforeConfig = _repository.Get();

        // 更新配置到持久化
        _repository.Update(config);

        // 热更新：立即刷新缓存
        var updatedConfig = _repository.Get();
        _configCache.Set(CallbackConfigCacheKey, updatedConfig);

        // 记录配置审计日志
        _auditLogger.LogConfigurationChange(
            configName: "ChuteDropoffCallbackConfiguration",
            operationType: "Update",
            beforeConfig: beforeConfig,
            afterConfig: updatedConfig);

        _logger.LogInformation(
            "格口落格回调配置已更新（热更新生效）: CallbackMode={CallbackMode}",
            updatedConfig.CallbackMode);
    }
}
