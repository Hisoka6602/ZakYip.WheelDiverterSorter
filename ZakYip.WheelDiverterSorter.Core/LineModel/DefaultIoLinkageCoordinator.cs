using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 默认 IO 联动协调器实现。
/// 根据系统状态（运行中/停止）控制中段皮带等设备的 IO 联动。
/// </summary>
public class DefaultIoLinkageCoordinator : IIoLinkageCoordinator
{
    /// <inheritdoc/>
    public IReadOnlyList<IoLinkagePoint> DetermineIoLinkagePoints(
        SystemOperatingState systemState,
        IoLinkageOptions options)
    {
        if (!options.Enabled)
        {
            return Array.Empty<IoLinkagePoint>();
        }

        // 运行中状态时，使用 RunningStateIos 配置
        if (systemState == SystemOperatingState.Running)
        {
            return options.RunningStateIos.AsReadOnly();
        }

        // 停止/复位/待机状态时，使用 StoppedStateIos 配置
        if (systemState is SystemOperatingState.Stopped 
                        or SystemOperatingState.Standby
                        or SystemOperatingState.Stopping)
        {
            return options.StoppedStateIos.AsReadOnly();
        }

        // 其他状态不触发 IO 联动
        return Array.Empty<IoLinkagePoint>();
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
