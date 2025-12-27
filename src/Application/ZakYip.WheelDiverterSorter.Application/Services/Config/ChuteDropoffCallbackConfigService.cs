using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
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

    public ChuteDropoffCallbackConfigService(
        IChuteDropoffCallbackConfigurationRepository repository,
        ISlidingConfigCache configCache,
        ILogger<ChuteDropoffCallbackConfigService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ChuteDropoffCallbackConfiguration GetCallbackConfiguration()
    {
        return _configCache.GetOrAdd(CallbackConfigCacheKey, () => _repository.Get());
    }
}
