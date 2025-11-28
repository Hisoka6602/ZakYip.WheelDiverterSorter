using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;

namespace ZakYip.WheelDiverterSorter.Execution.SelfTest;

/// <summary>
/// 默认配置验证器实现
/// </summary>
public class DefaultConfigValidator : IConfigValidator
{
    private readonly ISystemConfigurationRepository _systemConfig;
    private readonly IRouteConfigurationRepository _routeConfig;
    private readonly ILogger<DefaultConfigValidator> _logger;

    public DefaultConfigValidator(
        ISystemConfigurationRepository systemConfig,
        IRouteConfigurationRepository routeConfig,
        ILogger<DefaultConfigValidator> logger)
    {
        _systemConfig = systemConfig ?? throw new ArgumentNullException(nameof(systemConfig));
        _routeConfig = routeConfig ?? throw new ArgumentNullException(nameof(routeConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ConfigHealthStatus> ValidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始验证系统配置...");

            // 验证系统配置
            var sysConfig = _systemConfig.Get();
            if (sysConfig == null)
            {
                _logger.LogError("系统配置为空");
                return new ConfigHealthStatus
                {
                    IsValid = false,
                    ErrorMessage = "系统配置未找到或为空"
                };
            }

            // 验证异常格口配置
            if (sysConfig.ExceptionChuteId <= 0)
            {
                _logger.LogError("异常格口ID配置无效: {ExceptionChuteId}", sysConfig.ExceptionChuteId);
                return new ConfigHealthStatus
                {
                    IsValid = false,
                    ErrorMessage = $"异常格口ID配置无效: {sysConfig.ExceptionChuteId}"
                };
            }

            // 验证路由配置
            var routes = _routeConfig.GetAllEnabled();
            if (routes == null || !routes.Any())
            {
                _logger.LogWarning("路由配置为空，可能未初始化");
                // 路由配置为空不视为致命错误，系统可以启动后再配置
            }

            _logger.LogInformation("系统配置验证成功");
            await Task.CompletedTask; // 使方法异步兼容
            return new ConfigHealthStatus
            {
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "配置验证失败");
            return new ConfigHealthStatus
            {
                IsValid = false,
                ErrorMessage = $"配置验证异常: {ex.Message}"
            };
        }
    }
}
