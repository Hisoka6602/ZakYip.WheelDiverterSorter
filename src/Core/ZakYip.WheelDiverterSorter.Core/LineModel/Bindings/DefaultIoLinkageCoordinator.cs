using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Validation;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 默认 IO 联动协调器实现。
/// 根据系统状态（运行中/停止/急停/异常等）控制中段皮带等设备的 IO 联动。
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

        // 根据系统状态选择对应的 IO 联动配置
        switch (systemState)
        {
            case SystemOperatingState.Running:
                // 运行中状态时，使用 RunningStateIos 配置
                points = options.RunningStateIos;
                break;
            
            case SystemOperatingState.Stopped:
            case SystemOperatingState.Standby:
            case SystemOperatingState.Stopping:
                // 停止/复位/待机状态时，使用 StoppedStateIos 配置
                points = options.StoppedStateIos;
                break;
            
            case SystemOperatingState.EmergencyStopped:
                // 急停状态时，优先使用 EmergencyStopStateIos，如果为空则使用 StoppedStateIos
                points = options.EmergencyStopStateIos.Count > 0
                    ? options.EmergencyStopStateIos
                    : options.StoppedStateIos;
                break;
            
            case SystemOperatingState.WaitingUpstream:
                // 等待上游状态时，使用 UpstreamConnectionExceptionStateIos 配置
                points = options.UpstreamConnectionExceptionStateIos;
                break;
            
            case SystemOperatingState.Faulted:
                // 故障状态时，优先使用 DiverterExceptionStateIos，如果为空则使用 StoppedStateIos
                points = options.DiverterExceptionStateIos.Count > 0
                    ? options.DiverterExceptionStateIos
                    : options.StoppedStateIos;
                break;
            
            default:
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
        // 在以下状态时激活 IO 联动
        return systemState is SystemOperatingState.Running
                           or SystemOperatingState.Stopped
                           or SystemOperatingState.Standby
                           or SystemOperatingState.Stopping
                           or SystemOperatingState.EmergencyStopped
                           or SystemOperatingState.WaitingUpstream
                           or SystemOperatingState.Faulted;
    }
}
