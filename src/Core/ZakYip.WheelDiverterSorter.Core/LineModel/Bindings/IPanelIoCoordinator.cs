using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 面板 IO 协调器接口。
/// 负责根据系统运行状态，协调面板按钮响应。
/// </summary>
/// <remarks>
/// 注意：信号塔功能已被IO联动机制替代，不再通过此接口管理。
/// </remarks>
public interface IPanelIoCoordinator
{
    /// <summary>
    /// 判断在当前系统状态下，指定按钮是否允许操作。
    /// </summary>
    /// <param name="buttonType">按钮类型</param>
    /// <param name="systemState">系统运行状态</param>
    /// <returns>如果允许操作返回 true，否则返回 false</returns>
    bool IsButtonOperationAllowed(PanelButtonType buttonType, SystemState systemState);
}
