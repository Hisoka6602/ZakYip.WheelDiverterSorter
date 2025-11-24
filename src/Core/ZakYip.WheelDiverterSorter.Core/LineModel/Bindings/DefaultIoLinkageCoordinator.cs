using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 默认 IO 联动协调器实现。
/// 根据系统状态（运行中/停止）控制中段皮带等设备的 IO 联动。
/// </summary>
public class DefaultIoLinkageCoordinator : IIoLinkageCoordinator
{
    private readonly ILogger<DefaultIoLinkageCoordinator> _logger;

    public DefaultIoLinkageCoordinator(ILogger<DefaultIoLinkageCoordinator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IoLinkagePoint> DetermineIoLinkagePoints(
        SystemOperatingState systemState,
        IoLinkageOptions options)
    {
        if (!options.Enabled)
        {
            return Array.Empty<IoLinkagePoint>();
        }

        List<IoLinkagePoint> points;

        // 运行中状态时，使用 RunningStateIos 配置
        if (systemState == SystemOperatingState.Running)
        {
            points = options.RunningStateIos;
        }
        // 停止/复位/待机状态时，使用 StoppedStateIos 配置
        else if (systemState is SystemOperatingState.Stopped 
                         or SystemOperatingState.Standby
                         or SystemOperatingState.Stopping)
        {
            points = options.StoppedStateIos;
        }
        else
        {
            // 其他状态不触发 IO 联动
            return Array.Empty<IoLinkagePoint>();
        }

        // 过滤无效端点
        var validPoints = IoEndpointValidator.FilterAndLogInvalidEndpoints(points, _logger).ToList();
        return validPoints.AsReadOnly();
    }

    /// <inheritdoc/>
    public bool ShouldActivateIoLinkage(SystemOperatingState systemState)
    {
        // 运行中或停止相关状态时激活 IO 联动
        return systemState is SystemOperatingState.Running
                           or SystemOperatingState.Stopped
                           or SystemOperatingState.Standby
                           or SystemOperatingState.Stopping;
    }
}
