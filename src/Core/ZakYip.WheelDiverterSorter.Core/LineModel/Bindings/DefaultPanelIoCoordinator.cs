using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 默认面板 IO 协调器实现。
/// 根据系统状态和告警情况，协调三色灯显示和按钮操作权限。
/// </summary>
/// <remarks>
/// PR-FIX-SHADOW-ENUM: 统一使用 SystemState，删除 SystemOperatingState 影分身。
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
    public IEnumerable<SignalTowerState> DetermineSignalTowerStates(
        SystemState systemState,
        bool hasAlarms,
        bool upstreamConnected)
    {
        var states = new List<SignalTowerState>();

        switch (systemState)
        {
            case SystemState.Booting:
                // 启动中：黄灯闪烁
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 500));
                break;

            case SystemState.Ready:
                // 就绪：黄灯常亮
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Yellow));
                break;

            case SystemState.Running:
                // 运行中：绿灯常亮
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Green));
                
                // 如果上游未连接，同时亮黄灯提示
                if (!upstreamConnected)
                {
                    states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 1000));
                }
                break;

            case SystemState.Paused:
                // 暂停：绿灯和黄灯交替闪烁
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Green, 500));
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 500));
                break;

            case SystemState.Faulted:
                // 故障：红灯闪烁，蜂鸣器响
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Red, 500));
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Buzzer, 500));
                break;

            case SystemState.EmergencyStop:
                // 急停：红灯常亮，蜂鸣器持续响
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Red));
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Buzzer));
                break;

            default:
                // 未知状态：所有灯熄灭
                states.Add(SignalTowerState.CreateOff(SignalTowerChannel.Red));
                states.Add(SignalTowerState.CreateOff(SignalTowerChannel.Yellow));
                states.Add(SignalTowerState.CreateOff(SignalTowerChannel.Green));
                states.Add(SignalTowerState.CreateOff(SignalTowerChannel.Buzzer));
                break;
        }

        // 如果有告警且不是已经在故障或急停状态，添加红灯警告
        if (hasAlarms && systemState != SystemState.Faulted && 
            systemState != SystemState.EmergencyStop)
        {
            states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Red, 1000));
        }

        return states;
    }

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

            PanelButtonType.ModeAuto or PanelButtonType.ModeManual => 
                systemState is SystemState.Ready,

            _ => false
        };
    }
}
