using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 系统配置服务实现
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public class SystemConfigService : ISystemConfigService
{
    private static readonly object SystemConfigCacheKey = new();

    private readonly ISystemConfigurationRepository _repository;
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly ISlidingConfigCache _configCache;
    private readonly ILogger<SystemConfigService> _logger;
    private readonly ISystemClock _systemClock;

    public SystemConfigService(
        ISystemConfigurationRepository repository,
        IRouteConfigurationRepository routeRepository,
        ISlidingConfigCache configCache,
        ILogger<SystemConfigService> logger,
        ISystemClock systemClock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    public SystemConfiguration GetSystemConfig()
    {
        return _configCache.GetOrAdd(SystemConfigCacheKey, () => _repository.Get());
    }

    public SystemConfiguration GetDefaultTemplate()
    {
        return SystemConfiguration.GetDefault();
    }

    public async Task<SystemConfigUpdateResult> UpdateSystemConfigAsync(UpdateSystemConfigCommand request)
    {
        await Task.Yield();
        try
        {
            var config = MapToConfiguration(request);

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                _logger.LogWarning("系统配置验证失败: {ErrorMessage}", errorMessage);
                return new SystemConfigUpdateResult(false, errorMessage, null);
            }

            // 验证异常格口是否存在于路由配置中
            var exceptionRoute = _routeRepository.GetByChuteId(config.ExceptionChuteId);
            if (exceptionRoute == null)
            {
                var error = $"异常格口 {config.ExceptionChuteId} 不存在于路由配置中，请先创建对应的路由配置";
                _logger.LogWarning("系统配置验证失败: {ErrorMessage}", error);
                return new SystemConfigUpdateResult(false, error, null);
            }

            if (!exceptionRoute.IsEnabled)
            {
                var error = $"异常格口 {config.ExceptionChuteId} 的路由配置未启用";
                _logger.LogWarning("系统配置验证失败: {ErrorMessage}", error);
                return new SystemConfigUpdateResult(false, error, null);
            }

            // 设置更新时间（通过 ISystemClock.LocalNow）
            config.UpdatedAt = _systemClock.LocalNow;

            // 更新配置到持久化
            _repository.Update(config);

            // 热更新：立即刷新缓存
            var updatedConfig = _repository.Get();
            _configCache.Set(SystemConfigCacheKey, updatedConfig);

            _logger.LogInformation(
                "系统配置已更新（热更新生效）: ExceptionChuteId={ExceptionChuteId}, Version={Version}",
                updatedConfig.ExceptionChuteId,
                updatedConfig.Version);

            return new SystemConfigUpdateResult(true, null, updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "系统配置验证失败");
            return new SystemConfigUpdateResult(false, ex.Message, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新系统配置失败");
            return new SystemConfigUpdateResult(false, "更新系统配置失败", null);
        }
    }

    public async Task<SystemConfiguration> ResetSystemConfigAsync()
    {
        await Task.Yield();
        var defaultConfig = SystemConfiguration.GetDefault();
        
        // 设置更新时间（通过 ISystemClock.LocalNow）
        defaultConfig.UpdatedAt = _systemClock.LocalNow;
        
        _repository.Update(defaultConfig);

        // 热更新：立即刷新缓存
        var updatedConfig = _repository.Get();
        _configCache.Set(SystemConfigCacheKey, updatedConfig);

        _logger.LogInformation("系统配置已重置为默认值（热更新生效）");

        return updatedConfig;
    }

    public SortingModeInfo GetSortingMode()
    {
        var config = GetSystemConfig();
        return new SortingModeInfo(
            config.SortingMode,
            config.FixedChuteId,
            config.AvailableChuteIds ?? new List<long>());
    }

    public async Task<SortingModeUpdateResult> UpdateSortingModeAsync(UpdateSortingModeCommand request)
    {
        try
        {
            await Task.Yield();
            // 验证分拣模式值
            if (!Enum.IsDefined(typeof(SortingMode), request.SortingMode))
            {
                var error = "分拣模式值无效，仅支持：Formal（正常）、FixedChute（指定落格）、RoundRobin（循环落格）";
                return new SortingModeUpdateResult(false, error, null);
            }

            // 获取当前配置
            var config = _repository.Get();

            // 更新分拣模式相关字段
            config.SortingMode = request.SortingMode;
            config.FixedChuteId = request.FixedChuteId;
            config.AvailableChuteIds = request.AvailableChuteIds ?? new List<long>();

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                _logger.LogWarning("分拣模式配置验证失败: {ErrorMessage}", errorMessage);
                return new SortingModeUpdateResult(false, errorMessage, null);
            }

            // 设置更新时间（通过 ISystemClock.LocalNow）
            config.UpdatedAt = _systemClock.LocalNow;

            // 更新配置到持久化
            _repository.Update(config);

            // 热更新：立即刷新缓存
            var updatedConfig = _repository.Get();
            _configCache.Set(SystemConfigCacheKey, updatedConfig);

            _logger.LogInformation(
                "分拣模式已更新（热更新生效）: SortingMode={SortingMode}, FixedChuteId={FixedChuteId}, AvailableChuteIds={AvailableChuteIds}",
                updatedConfig.SortingMode,
                updatedConfig.FixedChuteId,
                string.Join(",", updatedConfig.AvailableChuteIds ?? new List<long>()));

            var updatedMode = new SortingModeInfo(
                updatedConfig.SortingMode,
                updatedConfig.FixedChuteId,
                updatedConfig.AvailableChuteIds ?? new List<long>());

            return new SortingModeUpdateResult(true, null, updatedMode);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "分拣模式配置验证失败");
            return new SortingModeUpdateResult(false, ex.Message, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新分拣模式配置失败");
            return new SortingModeUpdateResult(false, "更新分拣模式配置失败", null);
        }
    }

    private static SystemConfiguration MapToConfiguration(UpdateSystemConfigCommand request)
    {
        return new SystemConfiguration
        {
            ExceptionChuteId = request.ExceptionChuteId,
            SortingMode = request.SortingMode,
            FixedChuteId = request.FixedChuteId,
            AvailableChuteIds = request.AvailableChuteIds ?? new List<long>()
            // Note: Communication-related fields are not set here.
            // They should be managed through /api/communication endpoints
        };
    }
}
