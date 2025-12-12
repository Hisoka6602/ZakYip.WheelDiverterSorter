using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 默认面板 IO 协调器实现。
/// 根据系统状态协调按钮操作权限。
/// </summary>
/// <remarks>
/// PR-FIX-SHADOW-ENUM: 统一使用 SystemState，删除 SystemOperatingState 影分身。
/// 注意：信号塔功能已被IO联动机制替代，原DetermineSignalTowerStates方法已移除。
/// SystemState 映射说明：
/// - Booting → 启动中（对应原 Initializing）
/// - Ready → 就绪（对应原 Standby/Stopped）
/// - Running → 运行中
/// - Paused → 暂停
/// - Faulted → 故障
/// - EmergencyStop → 急停（对应原 EmergencyStopped）
/// </remarks>
public class DefaultPanelIoCoordinator : IPanelIoCoordinator
{
    /// <inheritdoc/>
    public bool IsButtonOperationAllowed(PanelButtonType buttonType, SystemState systemState)
    {
        return buttonType switch
        {
            PanelButtonType.Start => systemState is SystemState.Ready 
                                                   or SystemState.Paused,

            PanelButtonType.Stop => systemState is SystemState.Running 
                                                  or SystemState.Paused,

            PanelButtonType.Reset => systemState is SystemState.Faulted 
                                                   or SystemState.EmergencyStop
                                                   or SystemState.Ready,

            PanelButtonType.EmergencyStop => systemState != SystemState.EmergencyStop,

            _ => false
        };
    }
}
