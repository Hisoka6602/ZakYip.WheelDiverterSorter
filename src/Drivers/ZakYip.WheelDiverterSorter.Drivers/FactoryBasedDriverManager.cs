using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 基于工厂创建的驱动器列表的驱动管理器适配器
/// </summary>
/// <remarks>
/// <para>此类将工厂创建的驱动器列表适配为 <see cref="IWheelDiverterDriverManager"/> 接口。</para>
/// <para>用于向后兼容现有的驱动器工厂模式，同时支持新的统一命令执行器。</para>
/// <para>注意：此适配器不支持热更新配置，热更新场景请使用具体厂商的管理器实现
/// （如 <see cref="Vendors.ShuDiNiao.ShuDiNiaoWheelDiverterDriverManager"/>）。</para>
/// </remarks>
internal sealed class FactoryBasedDriverManager : IWheelDiverterDriverManager
{
    private readonly IReadOnlyDictionary<string, IWheelDiverterDriver> _drivers;
    private readonly ILogger<FactoryBasedDriverManager> _logger;

    /// <summary>
    /// 初始化基于工厂的驱动管理器
    /// </summary>
    /// <param name="drivers">工厂创建的驱动器列表</param>
    /// <param name="loggerFactory">日志工厂</param>
    public FactoryBasedDriverManager(
        IReadOnlyList<IWheelDiverterDriver> drivers,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        
        _logger = loggerFactory.CreateLogger<FactoryBasedDriverManager>();
        _drivers = drivers.ToDictionary(d => d.DiverterId, d => d);
        
        _logger.LogInformation(
            "已初始化基于工厂的驱动管理器，管理 {Count} 个摆轮",
            _drivers.Count);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IWheelDiverterDriver> GetActiveDrivers()
    {
        return _drivers;
    }

    /// <inheritdoc/>
    public IWheelDiverterDriver? GetDriver(string diverterId)
    {
        return _drivers.TryGetValue(diverterId, out var driver) ? driver : null;
    }

    /// <inheritdoc/>
    public Task<WheelDiverterConfigApplyResult> ApplyConfigurationAsync(
        WheelDiverterConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        // 此适配器不支持热更新配置
        _logger.LogWarning(
            "FactoryBasedDriverManager 不支持热更新配置，请使用具体厂商的管理器实现");
        
        return Task.FromResult(new WheelDiverterConfigApplyResult
        {
            IsSuccess = false,
            ConnectedCount = _drivers.Count,
            TotalCount = _drivers.Count,
            FailedDriverIds = Array.Empty<string>(),
            ErrorMessage = "此驱动管理器不支持热更新配置"
        });
    }

    /// <inheritdoc/>
    public Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("断开所有驱动器连接（工厂模式）");
        
        // 尝试停止所有驱动器
        var tasks = _drivers.Values.Select(async driver =>
        {
            try
            {
                await driver.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "停止驱动器 {DiverterId} 时出现异常", driver.DiverterId);
            }
        });
        
        return Task.WhenAll(tasks);
    }

    /// <inheritdoc/>
    public Task<WheelDiverterReconnectResult> ReconnectAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("触发驱动器重连（工厂模式）");
        
        // 工厂模式下的重连是惰性的，在下次发送命令时自动重连
        return Task.FromResult(new WheelDiverterReconnectResult
        {
            IsSuccess = true,
            ReconnectedCount = _drivers.Count,
            TotalCount = _drivers.Count,
            FailedDriverIds = Array.Empty<string>()
        });
    }
}
