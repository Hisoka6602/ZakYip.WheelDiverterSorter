using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Application.Services;

/// <summary>
/// 系统配置服务实现
/// </summary>
public class SystemConfigService : ISystemConfigService
{
    private readonly ISystemConfigurationRepository _repository;
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly ILogger<SystemConfigService> _logger;

    public SystemConfigService(
        ISystemConfigurationRepository repository,
        IRouteConfigurationRepository routeRepository,
        ILogger<SystemConfigService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SystemConfiguration GetSystemConfig()
    {
        return _repository.Get();
    }

    public SystemConfiguration GetDefaultTemplate()
    {
        return SystemConfiguration.GetDefault();
    }

    public async Task<SystemConfigUpdateResult> UpdateSystemConfigAsync(SystemConfigRequest request)
    {
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

            // 更新配置
            _repository.Update(config);

            _logger.LogInformation(
                "系统配置已更新: ExceptionChuteId={ExceptionChuteId}, Version={Version}",
                config.ExceptionChuteId,
                config.Version);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
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
        var defaultConfig = SystemConfiguration.GetDefault();
        _repository.Update(defaultConfig);

        _logger.LogInformation("系统配置已重置为默认值");

        return _repository.Get();
    }

    public SortingModeInfo GetSortingMode()
    {
        var config = _repository.Get();
        return new SortingModeInfo(
            config.SortingMode,
            config.FixedChuteId,
            config.AvailableChuteIds ?? new List<long>());
    }

    public async Task<SortingModeUpdateResult> UpdateSortingModeAsync(SortingModeRequest request)
    {
        try
        {
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

            // 更新配置
            _repository.Update(config);

            _logger.LogInformation(
                "分拣模式已更新: SortingMode={SortingMode}, FixedChuteId={FixedChuteId}, AvailableChuteIds={AvailableChuteIds}",
                config.SortingMode,
                config.FixedChuteId,
                string.Join(",", config.AvailableChuteIds));

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
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

    private static SystemConfiguration MapToConfiguration(SystemConfigRequest request)
    {
        return new SystemConfiguration
        {
            ExceptionChuteId = request.ExceptionChuteId,
            MqttDefaultPort = request.MqttDefaultPort,
            TcpDefaultPort = request.TcpDefaultPort,
#pragma warning disable CS0618 // 向后兼容
            ChuteAssignmentTimeoutMs = request.ChuteAssignmentTimeoutMs,
#pragma warning restore CS0618
            RequestTimeoutMs = request.RequestTimeoutMs,
            RetryCount = request.RetryCount,
            RetryDelayMs = request.RetryDelayMs,
            EnableAutoReconnect = request.EnableAutoReconnect,
            SortingMode = request.SortingMode,
            FixedChuteId = request.FixedChuteId,
            AvailableChuteIds = request.AvailableChuteIds ?? new List<long>()
        };
    }
}
