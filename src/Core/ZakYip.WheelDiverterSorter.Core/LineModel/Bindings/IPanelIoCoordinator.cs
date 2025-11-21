using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 面板 IO 协调器接口。
/// 负责根据系统运行状态，协调面板按钮响应和三色灯显示。
/// </summary>
public interface IPanelIoCoordinator
{
    /// <summary>
    /// 根据系统当前状态，计算并返回应当激活的信号塔通道状态。
    /// </summary>
    /// <param name="systemState">系统运行状态</param>
    /// <param name="hasAlarms">是否存在告警</param>
    /// <param name="upstreamConnected">上游是否连接</param>
    /// <returns>应当激活的信号塔通道状态集合</returns>
    IEnumerable<SignalTowerState> DetermineSignalTowerStates(
        SystemOperatingState systemState,
        bool hasAlarms,
        bool upstreamConnected);

    /// <summary>
    /// 判断在当前系统状态下，指定按钮是否允许操作。
    /// </summary>
    /// <param name="buttonType">按钮类型</param>
    /// <param name="systemState">系统运行状态</param>
    /// <returns>如果允许操作返回 true，否则返回 false</returns>
    bool IsButtonOperationAllowed(PanelButtonType buttonType, SystemOperatingState systemState);
}
