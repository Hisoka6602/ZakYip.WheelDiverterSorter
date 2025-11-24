using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 默认面板 IO 协调器实现。
/// 根据系统状态和告警情况，协调三色灯显示和按钮操作权限。
/// </summary>
public class DefaultPanelIoCoordinator : IPanelIoCoordinator
{
    /// <inheritdoc/>
    public IEnumerable<SignalTowerState> DetermineSignalTowerStates(
        SystemOperatingState systemState,
        bool hasAlarms,
        bool upstreamConnected)
    {
        var states = new List<SignalTowerState>();

        switch (systemState)
        {
            case SystemOperatingState.Initializing:
                // 初始化中：黄灯闪烁
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 500));
                break;

            case SystemOperatingState.Standby:
                // 待机：黄灯常亮
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Yellow));
                break;

            case SystemOperatingState.Running:
                // 运行中：绿灯常亮
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Green));
                
                // 如果上游未连接，同时亮黄灯提示
                if (!upstreamConnected)
                {
                    states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 1000));
                }
                break;

            case SystemOperatingState.Paused:
                // 暂停：绿灯和黄灯交替闪烁
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Green, 500));
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 500));
                break;

            case SystemOperatingState.Stopping:
                // 停止中：黄灯快速闪烁
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 300));
                break;

            case SystemOperatingState.Stopped:
                // 已停止：黄灯常亮
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Yellow));
                break;

            case SystemOperatingState.Faulted:
                // 故障：红灯闪烁，蜂鸣器响
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Red, 500));
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Buzzer, 500));
                break;

            case SystemOperatingState.EmergencyStopped:
                // 急停：红灯常亮，蜂鸣器持续响
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Red));
                states.Add(SignalTowerState.CreateOn(SignalTowerChannel.Buzzer));
                break;

            case SystemOperatingState.WaitingUpstream:
                // 等待上游：黄灯慢速闪烁
                states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 1000));
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
        if (hasAlarms && systemState != SystemOperatingState.Faulted && 
            systemState != SystemOperatingState.EmergencyStopped)
        {
            states.Add(SignalTowerState.CreateBlinking(SignalTowerChannel.Red, 1000));
        }

        return states;
    }

    /// <inheritdoc/>
    public bool IsButtonOperationAllowed(PanelButtonType buttonType, SystemOperatingState systemState)
    {
        return buttonType switch
        {
            PanelButtonType.Start => systemState is SystemOperatingState.Standby 
                                                   or SystemOperatingState.Stopped 
                                                   or SystemOperatingState.Paused,

            PanelButtonType.Stop => systemState is SystemOperatingState.Running 
                                                  or SystemOperatingState.Paused,

            PanelButtonType.Reset => systemState is SystemOperatingState.Faulted 
                                                   or SystemOperatingState.EmergencyStopped 
                                                   or SystemOperatingState.Stopped,

            PanelButtonType.EmergencyStop => systemState != SystemOperatingState.EmergencyStopped,

            PanelButtonType.ModeAuto or PanelButtonType.ModeManual => 
                systemState is SystemOperatingState.Standby or SystemOperatingState.Stopped,

            _ => false
        };
    }
}
