using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 厂商配置服务实现
/// </summary>
/// <remarks>
/// 封装所有厂商配置的 CRUD 操作，为 Host 层提供统一的配置访问门面。
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public sealed class VendorConfigService : IVendorConfigService
{
    private static readonly object DriverConfigCacheKey = new();
    private static readonly object SensorConfigCacheKey = new();
    private static readonly object WheelDiverterConfigCacheKey = new();

    private readonly IDriverConfigurationRepository _driverRepository;
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly IWheelDiverterConfigurationRepository _wheelRepository;
    private readonly ISlidingConfigCache _configCache;
    private readonly ILogger<VendorConfigService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public VendorConfigService(
        IDriverConfigurationRepository driverRepository,
        ISensorConfigurationRepository sensorRepository,
        IWheelDiverterConfigurationRepository wheelRepository,
        ISlidingConfigCache configCache,
        ILogger<VendorConfigService> logger)
    {
        _driverRepository = driverRepository ?? throw new ArgumentNullException(nameof(driverRepository));
        _sensorRepository = sensorRepository ?? throw new ArgumentNullException(nameof(sensorRepository));
        _wheelRepository = wheelRepository ?? throw new ArgumentNullException(nameof(wheelRepository));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region IO驱动器配置

    /// <inheritdoc/>
    public DriverConfiguration GetDriverConfiguration()
    {
        return _configCache.GetOrAdd(DriverConfigCacheKey, () => _driverRepository.Get());
    }

    /// <inheritdoc/>
    public void UpdateDriverConfiguration(DriverConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var (isValid, errorMessage) = config.Validate();
        if (!isValid)
        {
            throw new ArgumentException(
                errorMessage ?? "IO驱动器配置验证失败：请检查配置参数是否完整和有效");
        }

        _driverRepository.Update(config);

        // 热更新：立即刷新缓存
        var updatedConfig = _driverRepository.Get();
        _configCache.Set(DriverConfigCacheKey, updatedConfig);

        _logger.LogInformation(
            "IO驱动器配置已更新（热更新生效）: VendorType={VendorType}, UseHardware={UseHardware}, Version={Version}",
            updatedConfig.VendorType,
            updatedConfig.UseHardwareDriver,
            updatedConfig.Version);
    }

    /// <inheritdoc/>
    public DriverConfiguration ResetDriverConfiguration()
    {
        var defaultConfig = DriverConfiguration.GetDefault();
        _driverRepository.Update(defaultConfig);

        // 热更新：立即刷新缓存
        var updatedConfig = _driverRepository.Get();
        _configCache.Set(DriverConfigCacheKey, updatedConfig);

        _logger.LogInformation("IO驱动器配置已重置为默认值（热更新生效）");

        return updatedConfig;
    }

    #endregion

    #region 感应IO配置

    /// <inheritdoc/>
    public SensorConfiguration GetSensorConfiguration()
    {
        return _configCache.GetOrAdd(SensorConfigCacheKey, () => _sensorRepository.Get());
    }

    /// <inheritdoc/>
    public void UpdateSensorConfiguration(SensorConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var (isValid, errorMessage) = config.Validate();
        if (!isValid)
        {
            throw new ArgumentException(
                errorMessage ?? "感应IO配置验证失败：请检查传感器配置参数是否完整和有效");
        }

        _sensorRepository.Update(config);

        // 热更新：立即刷新缓存
        var updatedConfig = _sensorRepository.Get();
        _configCache.Set(SensorConfigCacheKey, updatedConfig);

        _logger.LogInformation(
            "感应IO配置已更新（热更新生效）: SensorCount={SensorCount}, Version={Version}",
            updatedConfig.Sensors?.Count ?? 0,
            updatedConfig.Version);
    }

    /// <inheritdoc/>
    public SensorConfiguration ResetSensorConfiguration()
    {
        var defaultConfig = SensorConfiguration.GetDefault();
        _sensorRepository.Update(defaultConfig);

        // 热更新：立即刷新缓存
        var updatedConfig = _sensorRepository.Get();
        _configCache.Set(SensorConfigCacheKey, updatedConfig);

        _logger.LogInformation("感应IO配置已重置为默认值（热更新生效）");

        return updatedConfig;
    }

    #endregion

    #region 摆轮配置

    /// <inheritdoc/>
    public WheelDiverterConfiguration GetWheelDiverterConfiguration()
    {
        return _configCache.GetOrAdd(WheelDiverterConfigCacheKey, () => _wheelRepository.Get());
    }

    /// <inheritdoc/>
    public void UpdateWheelDiverterConfiguration(WheelDiverterConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var (isValid, errorMessage) = config.Validate();
        if (!isValid)
        {
            throw new ArgumentException(
                errorMessage ?? "摆轮配置验证失败：请检查摆轮设备配置参数是否完整和有效");
        }

        _wheelRepository.Update(config);

        // 热更新：立即刷新缓存
        var updatedConfig = _wheelRepository.Get();
        _configCache.Set(WheelDiverterConfigCacheKey, updatedConfig);

        _logger.LogInformation(
            "摆轮配置已更新（热更新生效）: VendorType={VendorType}, Version={Version}",
            updatedConfig.VendorType,
            updatedConfig.Version);
    }

    /// <inheritdoc/>
    public ShuDiNiaoWheelDiverterConfig? GetShuDiNiaoConfiguration()
    {
        return GetWheelDiverterConfiguration().ShuDiNiao;
    }

    /// <inheritdoc/>
    public void UpdateShuDiNiaoConfiguration(ShuDiNiaoWheelDiverterConfig shuDiNiaoConfig)
    {
        ArgumentNullException.ThrowIfNull(shuDiNiaoConfig);

        var config = _wheelRepository.Get();
        config.ShuDiNiao = shuDiNiaoConfig;

        if (shuDiNiaoConfig.Devices.Any())
        {
            config.VendorType = WheelDiverterVendorType.ShuDiNiao;
        }

        var (isValid, errorMessage) = config.Validate();
        if (!isValid)
        {
            throw new ArgumentException(errorMessage ?? "配置验证失败");
        }

        _wheelRepository.Update(config);

        // 热更新：立即刷新缓存
        var updatedConfig = _wheelRepository.Get();
        _configCache.Set(WheelDiverterConfigCacheKey, updatedConfig);

        _logger.LogInformation(
            "数递鸟摆轮配置已更新（热更新生效）: 设备数量={DeviceCount}",
            shuDiNiaoConfig.Devices.Count);
    }

    #endregion
}
