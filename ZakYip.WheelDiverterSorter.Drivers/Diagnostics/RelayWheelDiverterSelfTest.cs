using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.Diagnostics;

/// <summary>
/// 继电器摆轮驱动器自检实现
/// </summary>
public class RelayWheelDiverterSelfTest : IDriverSelfTest
{
    private readonly IWheelDiverterDriver _driver;
    private readonly ILogger<RelayWheelDiverterSelfTest> _logger;

    public RelayWheelDiverterSelfTest(
        IWheelDiverterDriver driver,
        ILogger<RelayWheelDiverterSelfTest> logger)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string DriverName => $"RelayWheelDiverter-{_driver.DiverterId}";

    /// <inheritdoc/>
    public async Task<DriverHealthStatus> RunSelfTestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始自检摆轮驱动器: {DriverName}", DriverName);

            // 执行安全读取操作，验证驱动器是否可访问
            // 这里我们尝试获取摆轮的当前状态，这是一个读操作，不会触发实际动作
            var status = await _driver.GetStatusAsync();
            
            _logger.LogInformation("摆轮驱动器自检成功: {DriverName}, 状态: {Status}", DriverName, status);

            return new DriverHealthStatus
            {
                DriverName = DriverName,
                IsHealthy = true,
                CheckedAt = DateTimeOffset.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("摆轮驱动器自检被取消: {DriverName}", DriverName);
            return new DriverHealthStatus
            {
                DriverName = DriverName,
                IsHealthy = false,
                ErrorCode = "CANCELLED",
                ErrorMessage = "自检操作被取消",
                CheckedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮驱动器自检失败: {DriverName}", DriverName);
            return new DriverHealthStatus
            {
                DriverName = DriverName,
                IsHealthy = false,
                ErrorCode = "DRIVER_ERROR",
                ErrorMessage = $"驱动器访问失败: {ex.Message}",
                CheckedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
