using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.SelfTest;

/// <summary>
/// 摆轮驱动器自检实现
/// Wheel diverter driver self-test implementation
/// </summary>
public class WheelDiverterSelfTest : IDriverSelfTest
{
    private readonly IVendorConfigService _vendorConfigService;
    private readonly IWheelDiverterDriverManager? _wheelDiverterDriverManager;
    private readonly INodeHealthRegistry? _nodeHealthRegistry;
    private readonly ILogger<WheelDiverterSelfTest> _logger;
    private readonly ISystemClock _clock;

    public WheelDiverterSelfTest(
        IVendorConfigService vendorConfigService,
        ILogger<WheelDiverterSelfTest> logger,
        ISystemClock clock,
        IWheelDiverterDriverManager? wheelDiverterDriverManager = null,
        INodeHealthRegistry? nodeHealthRegistry = null)
    {
        _vendorConfigService = vendorConfigService ?? throw new ArgumentNullException(nameof(vendorConfigService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _wheelDiverterDriverManager = wheelDiverterDriverManager;
        _nodeHealthRegistry = nodeHealthRegistry;
    }

    /// <inheritdoc/>
    public string DriverName => "摆轮驱动器";

    /// <inheritdoc/>
    public Task<DriverHealthStatus> RunSelfTestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var wheelConfig = _vendorConfigService.GetWheelDiverterConfiguration();

            if (wheelConfig == null)
            {
                return Task.FromResult(new DriverHealthStatus
                {
                    DriverName = "摆轮驱动器",
                    IsHealthy = false,
                    ErrorCode = "NOT_CONFIGURED",
                    ErrorMessage = "摆轮驱动器配置未初始化",
                    CheckedAt = _clock.LocalNowOffset
                });
            }

            // 获取厂商显示名称
            var vendorDisplayName = GetWheelDiverterVendorDisplayName(wheelConfig.VendorType);

            // 检查实际连接状态（如果有驱动管理器）
            if (_wheelDiverterDriverManager != null)
            {
                var activeDrivers = _wheelDiverterDriverManager.GetActiveDrivers();
                var configuredDeviceCount = GetConfiguredWheelDiverterCount(wheelConfig);
                var connectedCount = activeDrivers.Count;

                if (connectedCount == 0 && configuredDeviceCount > 0)
                {
                    return Task.FromResult(new DriverHealthStatus
                    {
                        DriverName = $"摆轮驱动器 ({vendorDisplayName})",
                        IsHealthy = false,
                        ErrorCode = "NOT_CONNECTED",
                        ErrorMessage = $"摆轮驱动器未连接：厂商 {vendorDisplayName}，已配置 {configuredDeviceCount} 台设备，但均未连接",
                        CheckedAt = _clock.LocalNowOffset
                    });
                }

                if (connectedCount < configuredDeviceCount)
                {
                    return Task.FromResult(new DriverHealthStatus
                    {
                        DriverName = $"摆轮驱动器 ({vendorDisplayName})",
                        IsHealthy = false,
                        ErrorCode = "PARTIAL_CONNECTED",
                        ErrorMessage = $"摆轮驱动器部分连接：厂商 {vendorDisplayName}，已配置 {configuredDeviceCount} 台设备，已连接 {connectedCount} 台",
                        CheckedAt = _clock.LocalNowOffset
                    });
                }

                return Task.FromResult(new DriverHealthStatus
                {
                    DriverName = $"摆轮驱动器 ({vendorDisplayName})",
                    IsHealthy = true,
                    ErrorCode = null,
                    ErrorMessage = $"摆轮驱动器全部连接：厂商 {vendorDisplayName}，已连接 {connectedCount} 台设备",
                    CheckedAt = _clock.LocalNowOffset
                });
            }

            // 没有驱动管理器时，只验证配置
            return Task.FromResult(new DriverHealthStatus
            {
                DriverName = $"摆轮驱动器 ({vendorDisplayName})",
                IsHealthy = true,
                ErrorCode = null,
                ErrorMessage = $"摆轮驱动器已配置，厂商: {vendorDisplayName}，硬件模式已启用",
                CheckedAt = _clock.LocalNowOffset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮驱动器自检失败");
            return Task.FromResult(new DriverHealthStatus
            {
                DriverName = "摆轮驱动器",
                IsHealthy = false,
                ErrorCode = "SELF_TEST_ERROR",
                ErrorMessage = $"自检过程异常: {ex.Message}",
                CheckedAt = _clock.LocalNowOffset
            });
        }
    }

    /// <summary>
    /// 获取摆轮驱动厂商显示名称
    /// </summary>
    private static string GetWheelDiverterVendorDisplayName(WheelDiverterVendorType vendorType)
    {
        return vendorType switch
        {
            WheelDiverterVendorType.ShuDiNiao => "数递鸟",
            _ => vendorType.ToString()
        };
    }

    /// <summary>
    /// 获取已配置的摆轮设备数量
    /// </summary>
    private static int GetConfiguredWheelDiverterCount(WheelDiverterConfiguration config)
    {
        return config.VendorType switch
        {
            WheelDiverterVendorType.ShuDiNiao => config.ShuDiNiao?.Devices.Count(d => d.IsEnabled) ?? 0,
            _ => 0
        };
    }
}
