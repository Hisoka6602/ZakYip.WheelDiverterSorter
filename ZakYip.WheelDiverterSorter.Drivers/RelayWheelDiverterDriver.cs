using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 基于继电器的摆轮驱动器实现
/// </summary>
/// <remarks>
/// 内部维护继电器通道映射，将语义化操作转换为具体的继电器控制。
/// 隐藏Y点编号、继电器通道等硬件细节，不暴露给上层。
/// </remarks>
public class RelayWheelDiverterDriver : IWheelDiverterDriver
{
    private readonly ILogger<RelayWheelDiverterDriver> _logger;
    private readonly IDiverterController _diverterController;
    private readonly RelayChannelMapping _mapping;
    private string _currentStatus = "未知";

    /// <summary>
    /// 继电器通道映射配置
    /// </summary>
    /// <remarks>
    /// 封装具体的硬件映射细节，包括左转、右转、直通对应的继电器角度。
    /// </remarks>
    public class RelayChannelMapping
    {
        /// <summary>左转对应的摆轮角度</summary>
        public int LeftTurnAngle { get; init; } = 45;

        /// <summary>右转对应的摆轮角度</summary>
        public int RightTurnAngle { get; init; } = -45;

        /// <summary>直通对应的摆轮角度</summary>
        public int PassThroughAngle { get; init; } = 0;

        /// <summary>停止对应的摆轮角度</summary>
        public int StopAngle { get; init; } = 0;
    }

    /// <inheritdoc/>
    public string DiverterId => _diverterController.DiverterId;

    /// <summary>
    /// 初始化基于继电器的摆轮驱动器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="diverterController">底层摆轮控制器</param>
    /// <param name="mapping">继电器通道映射配置（可选，使用默认值）</param>
    public RelayWheelDiverterDriver(
        ILogger<RelayWheelDiverterDriver> logger,
        IDiverterController diverterController,
        RelayChannelMapping? mapping = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _diverterController = diverterController ?? throw new ArgumentNullException(nameof(diverterController));
        _mapping = mapping ?? new RelayChannelMapping();

        _logger.LogInformation(
            "已初始化继电器摆轮驱动器 {DiverterId}，左转={LeftAngle}°，右转={RightAngle}°，直通={PassAngle}°",
            DiverterId, _mapping.LeftTurnAngle, _mapping.RightTurnAngle, _mapping.PassThroughAngle);
    }

    /// <inheritdoc/>
    public async Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行左转", DiverterId);
            var result = await _diverterController.SetAngleAsync(_mapping.LeftTurnAngle, cancellationToken);

            if (result)
            {
                _currentStatus = "左转";
                _logger.LogInformation("摆轮 {DiverterId} 左转成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 左转失败", DiverterId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 左转异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TurnRightAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行右转", DiverterId);
            var result = await _diverterController.SetAngleAsync(_mapping.RightTurnAngle, cancellationToken);

            if (result)
            {
                _currentStatus = "右转";
                _logger.LogInformation("摆轮 {DiverterId} 右转成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 右转失败", DiverterId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 右转异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> PassThroughAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行直通", DiverterId);
            var result = await _diverterController.SetAngleAsync(_mapping.PassThroughAngle, cancellationToken);

            if (result)
            {
                _currentStatus = "直通";
                _logger.LogInformation("摆轮 {DiverterId} 直通成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 直通失败", DiverterId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 直通异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("摆轮 {DiverterId} 执行停止", DiverterId);
            var result = await _diverterController.SetAngleAsync(_mapping.StopAngle, cancellationToken);

            if (result)
            {
                _currentStatus = "已停止";
                _logger.LogInformation("摆轮 {DiverterId} 停止成功", DiverterId);
            }
            else
            {
                _logger.LogWarning("摆轮 {DiverterId} 停止失败", DiverterId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摆轮 {DiverterId} 停止异常", DiverterId);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<string> GetStatusAsync()
    {
        return Task.FromResult(_currentStatus);
    }
}
