using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.SelfTest;

/// <summary>
/// IO驱动器自检实现
/// IO driver self-test implementation
/// </summary>
public class IoDriverSelfTest : IDriverSelfTest
{
    private readonly IVendorConfigService _vendorConfigService;
    private readonly IEmcController? _emcController;
    private readonly ILogger<IoDriverSelfTest> _logger;
    private readonly ISystemClock _clock;

    public IoDriverSelfTest(
        IVendorConfigService vendorConfigService,
        ILogger<IoDriverSelfTest> logger,
        ISystemClock clock,
        IEmcController? emcController = null)
    {
        _vendorConfigService = vendorConfigService ?? throw new ArgumentNullException(nameof(vendorConfigService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _emcController = emcController;
    }

    /// <inheritdoc/>
    public string DriverName => "IO驱动器";

    /// <inheritdoc/>
    public Task<DriverHealthStatus> RunSelfTestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ioConfig = _vendorConfigService.GetDriverConfiguration();

            if (ioConfig == null)
            {
                return Task.FromResult(new DriverHealthStatus
                {
                    DriverName = "IO驱动器",
                    IsHealthy = false,
                    ErrorCode = "NOT_CONFIGURED",
                    ErrorMessage = "IO驱动器配置未初始化",
                    CheckedAt = _clock.LocalNowOffset
                });
            }

            // 获取厂商显示名称
            var vendorDisplayName = GetIoVendorDisplayName(ioConfig.VendorType);

            // 验证硬件模式下的配置
            if (ioConfig.VendorType == DriverVendorType.Leadshine && ioConfig.Leadshine == null)
            {
                return Task.FromResult(new DriverHealthStatus
                {
                    DriverName = $"IO驱动器 ({vendorDisplayName})",
                    IsHealthy = false,
                    ErrorCode = "NOT_CONFIGURED",
                    ErrorMessage = $"IO驱动器（{vendorDisplayName}）配置不完整，缺少雷赛控制卡参数",
                    CheckedAt = _clock.LocalNowOffset
                });
            }

            // 检查EMC控制器可用性
            var isConnected = false;
            var isHealthy = false;
            var errorMessage = string.Empty;

            if (_emcController != null)
            {
                isConnected = _emcController.IsAvailable();
                isHealthy = isConnected;

                errorMessage = isConnected
                    ? $"硬件模式已启用，厂商: {vendorDisplayName}"
                    : "EMC控制器未初始化或不可用";
            }
            else
            {
                // EMC控制器未注册
                isHealthy = false;
                errorMessage = "EMC控制器未注册到依赖注入容器";
            }

            return Task.FromResult(new DriverHealthStatus
            {
                DriverName = $"IO驱动器 ({vendorDisplayName})",
                IsHealthy = isHealthy,
                ErrorCode = isHealthy ? null : "NOT_CONNECTED",
                ErrorMessage = errorMessage,
                CheckedAt = _clock.LocalNowOffset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IO驱动器自检失败");
            return Task.FromResult(new DriverHealthStatus
            {
                DriverName = "IO驱动器",
                IsHealthy = false,
                ErrorCode = "SELF_TEST_ERROR",
                ErrorMessage = $"自检过程异常: {ex.Message}",
                CheckedAt = _clock.LocalNowOffset
            });
        }
    }

    /// <summary>
    /// 获取IO驱动厂商显示名称
    /// </summary>
    private static string GetIoVendorDisplayName(DriverVendorType vendorType)
    {
        return vendorType switch
        {
            DriverVendorType.Leadshine => "雷赛",
            DriverVendorType.Siemens => "西门子",
            DriverVendorType.Mitsubishi => "三菱",
            DriverVendorType.Omron => "欧姆龙",
            _ => vendorType.ToString()
        };
    }
}
