using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 厂商配置服务实现
/// </summary>
/// <remarks>
/// 封装所有厂商配置的 CRUD 操作，为 Host 层提供统一的配置访问门面。
/// </remarks>
public sealed class VendorConfigService : IVendorConfigService
{
    private readonly IDriverConfigurationRepository _driverRepository;
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly IWheelDiverterConfigurationRepository _wheelRepository;
    private readonly ILogger<VendorConfigService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public VendorConfigService(
        IDriverConfigurationRepository driverRepository,
        ISensorConfigurationRepository sensorRepository,
        IWheelDiverterConfigurationRepository wheelRepository,
        ILogger<VendorConfigService> logger)
    {
        _driverRepository = driverRepository ?? throw new ArgumentNullException(nameof(driverRepository));
        _sensorRepository = sensorRepository ?? throw new ArgumentNullException(nameof(sensorRepository));
        _wheelRepository = wheelRepository ?? throw new ArgumentNullException(nameof(wheelRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region IO驱动器配置

    /// <inheritdoc/>
    public DriverConfiguration GetDriverConfiguration()
    {
        return _driverRepository.Get();
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

        _logger.LogInformation(
            "IO驱动器配置已更新: VendorType={VendorType}, UseHardware={UseHardware}, Version={Version}",
            config.VendorType,
            config.UseHardwareDriver,
            config.Version);
    }

    /// <inheritdoc/>
    public DriverConfiguration ResetDriverConfiguration()
    {
        var defaultConfig = DriverConfiguration.GetDefault();
        _driverRepository.Update(defaultConfig);

        _logger.LogInformation("IO驱动器配置已重置为默认值");

        return _driverRepository.Get();
    }

    #endregion

    #region 感应IO配置

    /// <inheritdoc/>
    public SensorConfiguration GetSensorConfiguration()
    {
        return _sensorRepository.Get();
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

        _logger.LogInformation(
            "感应IO配置已更新: SensorCount={SensorCount}, Version={Version}",
            config.Sensors?.Count ?? 0,
            config.Version);
    }

    /// <inheritdoc/>
    public SensorConfiguration ResetSensorConfiguration()
    {
        var defaultConfig = SensorConfiguration.GetDefault();
        _sensorRepository.Update(defaultConfig);

        _logger.LogInformation("感应IO配置已重置为默认值");

        return _sensorRepository.Get();
    }

    #endregion

    #region 摆轮配置

    /// <inheritdoc/>
    public WheelDiverterConfiguration GetWheelDiverterConfiguration()
    {
        return _wheelRepository.Get();
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

        _logger.LogInformation(
            "摆轮配置已更新: VendorType={VendorType}, Version={Version}",
            config.VendorType,
            config.Version);
    }

    /// <inheritdoc/>
    public ShuDiNiaoWheelDiverterConfig? GetShuDiNiaoConfiguration()
    {
        return _wheelRepository.Get().ShuDiNiao;
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

        _logger.LogInformation(
            "数递鸟摆轮配置已更新: 设备数量={DeviceCount}",
            shuDiNiaoConfig.Devices.Count);
    }

    #endregion
}
