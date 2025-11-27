using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮设备 HAL适配器
/// ShuDiNiao Wheel Diverter Device HAL Adapter
/// </summary>
/// <remarks>
/// 此类将数递鸟 <see cref="IWheelDiverterDriver"/> 适配为统一的 
/// <see cref="IWheelDiverterDevice"/> HAL接口。
/// </remarks>
public sealed class ShuDiNiaoWheelDiverterDeviceAdapter : IWheelDiverterDevice
{
    private readonly IWheelDiverterDriver _driver;
    private readonly ILogger<ShuDiNiaoWheelDiverterDeviceAdapter>? _logger;
    private WheelDiverterState _lastKnownState = WheelDiverterState.Unknown;

    /// <inheritdoc/>
    public string DeviceId => _driver.DiverterId;

    /// <summary>
    /// 创建数递鸟摆轮HAL适配器
    /// </summary>
    /// <param name="driver">底层数递鸟驱动</param>
    /// <param name="logger">日志记录器</param>
    public ShuDiNiaoWheelDiverterDeviceAdapter(
        IWheelDiverterDriver driver,
        ILogger<ShuDiNiaoWheelDiverterDeviceAdapter>? logger = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OperationResult> ExecuteAsync(
        Core.Hardware.WheelCommand command,
        CancellationToken cancellationToken = default)
    {
        bool success;

        try
        {
            success = command.Direction switch
            {
                DiverterDirection.Left => await _driver.TurnLeftAsync(cancellationToken),
                DiverterDirection.Right => await _driver.TurnRightAsync(cancellationToken),
                DiverterDirection.Straight => await _driver.PassThroughAsync(cancellationToken),
                _ => false
            };

            if (success)
            {
                _lastKnownState = command.Direction switch
                {
                    DiverterDirection.Left => WheelDiverterState.AtLeft,
                    DiverterDirection.Right => WheelDiverterState.AtRight,
                    DiverterDirection.Straight => WheelDiverterState.AtStraight,
                    _ => WheelDiverterState.Unknown
                };

                _logger?.LogDebug(
                    "[摆轮-HAL适配器] 设备 {DeviceId} 命令执行成功 | 方向={Direction}",
                    DeviceId,
                    command.Direction);

                return OperationResult.Success();
            }
            else
            {
                _logger?.LogWarning(
                    "[摆轮-HAL适配器] 设备 {DeviceId} 命令执行失败 | 方向={Direction}",
                    DeviceId,
                    command.Direction);

                return OperationResult.Failure(
                    ErrorCodes.WheelCommandFailed,
                    $"摆轮 {DeviceId} 命令执行失败");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "[摆轮-HAL适配器] 设备 {DeviceId} 命令执行异常 | 方向={Direction}",
                DeviceId,
                command.Direction);

            return OperationResult.Failure(
                ErrorCodes.WheelCommunicationError,
                $"摆轮 {DeviceId} 通信错误: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _driver.StopAsync(cancellationToken);
            
            if (success)
            {
                _lastKnownState = WheelDiverterState.Idle;
                return OperationResult.Success();
            }
            else
            {
                return OperationResult.Failure(
                    ErrorCodes.WheelCommandFailed,
                    $"摆轮 {DeviceId} 停止命令执行失败");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[摆轮-HAL适配器] 设备 {DeviceId} 停止异常", DeviceId);
            return OperationResult.Failure(
                ErrorCodes.WheelCommunicationError,
                $"摆轮 {DeviceId} 通信错误: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<WheelDiverterState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var statusStr = await _driver.GetStatusAsync();
            
            // 解析状态字符串 - 这是一个简化的实现
            // 实际应根据数递鸟协议返回的状态进行解析
            if (statusStr.Contains("左转", StringComparison.OrdinalIgnoreCase))
                return WheelDiverterState.AtLeft;
            if (statusStr.Contains("右转", StringComparison.OrdinalIgnoreCase))
                return WheelDiverterState.AtRight;
            if (statusStr.Contains("直通", StringComparison.OrdinalIgnoreCase))
                return WheelDiverterState.AtStraight;
            if (statusStr.Contains("停止", StringComparison.OrdinalIgnoreCase))
                return WheelDiverterState.Idle;
            if (statusStr.Contains("故障", StringComparison.OrdinalIgnoreCase))
                return WheelDiverterState.Fault;
            
            return _lastKnownState;
        }
        catch
        {
            return _lastKnownState;
        }
    }
}
