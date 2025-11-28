using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 面板操作端到端测试。
/// 验证面板按钮、系统状态机和三色灯的完整集成流程。
/// </summary>
public class PanelOperationsE2ETests
{
    [Fact]
    [SimulationScenario("PanelOps_BasicFlow_StartStopReset")]
    public async Task E2E_BasicOperationFlow_StartStopReset()
    {
        // Arrange - 创建完整的面板 IO 系统
        var panelReader = new SimulatedPanelInputReader();
        var signalTower = new SimulatedSignalTowerOutput();
        var coordinator = new DefaultPanelIoCoordinator();

        // 初始状态：系统待机
        var currentState = SystemOperatingState.Standby;

        // Act & Assert: 步骤 1 - 待机状态，显示黄灯
        await signalTower.TurnOffAllAsync();
        var states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: false, upstreamConnected: true);
        await signalTower.SetChannelStatesAsync(states);

        var towerStates = await signalTower.GetAllChannelStatesAsync();
        Assert.True(towerStates[SignalTowerChannel.Yellow].IsActive);
        Assert.False(towerStates[SignalTowerChannel.Green].IsActive);

        // 步骤 2 - 模拟按下 Start 按钮
        panelReader.SimulatePressButton(PanelButtonType.Start);
        var startButtonState = await panelReader.ReadButtonStateAsync(PanelButtonType.Start);
        Assert.True(startButtonState.IsPressed);

        // 验证在待机状态下允许 Start 操作
        Assert.True(coordinator.IsButtonOperationAllowed(PanelButtonType.Start, currentState));

        // 步骤 3 - 系统切换到运行状态
        currentState = SystemOperatingState.Running;
        await signalTower.TurnOffAllAsync();
        states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: false, upstreamConnected: true);
        await signalTower.SetChannelStatesAsync(states);

        towerStates = await signalTower.GetAllChannelStatesAsync();
        Assert.True(towerStates[SignalTowerChannel.Green].IsActive);
        Assert.False(towerStates[SignalTowerChannel.Red].IsActive);

        // 步骤 4 - 释放 Start 按钮
        panelReader.SimulateReleaseButton(PanelButtonType.Start);

        // 步骤 5 - 模拟按下 Stop 按钮
        panelReader.SimulatePressButton(PanelButtonType.Stop);
        Assert.True(coordinator.IsButtonOperationAllowed(PanelButtonType.Stop, currentState));

        // 步骤 6 - 系统切换到停止中状态
        currentState = SystemOperatingState.Stopping;
        await signalTower.TurnOffAllAsync();
        states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: false, upstreamConnected: true);
        await signalTower.SetChannelStatesAsync(states);

        towerStates = await signalTower.GetAllChannelStatesAsync();
        Assert.True(towerStates[SignalTowerChannel.Yellow].IsBlinking);

        // 步骤 7 - 系统完全停止
        currentState = SystemOperatingState.Stopped;
        await signalTower.TurnOffAllAsync();
        states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: false, upstreamConnected: true);
        await signalTower.SetChannelStatesAsync(states);

        panelReader.SimulateReleaseButton(PanelButtonType.Stop);

        // 验证历史记录
        var history = signalTower.GetStateChangeHistory();
        Assert.NotEmpty(history);
    }

    [Fact]
    [SimulationScenario("PanelOps_FaultScenario_RedLightAndBuzzer")]
    public async Task E2E_FaultScenario_RedLightAndBuzzer()
    {
        // Arrange
        var panelReader = new SimulatedPanelInputReader();
        var signalTower = new SimulatedSignalTowerOutput();
        var coordinator = new DefaultPanelIoCoordinator();

        // Act - 模拟系统故障
        var currentState = SystemOperatingState.Faulted;
        await signalTower.TurnOffAllAsync();
        var states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: true, upstreamConnected: true);
        await signalTower.SetChannelStatesAsync(states);

        // Assert - 验证红灯闪烁和蜂鸣器激活
        var towerStates = await signalTower.GetAllChannelStatesAsync();
        Assert.True(towerStates[SignalTowerChannel.Red].IsActive);
        Assert.True(towerStates[SignalTowerChannel.Red].IsBlinking);
        Assert.True(towerStates[SignalTowerChannel.Buzzer].IsActive);

        // 验证在故障状态下不允许 Start，但允许 Reset
        Assert.False(coordinator.IsButtonOperationAllowed(PanelButtonType.Start, currentState));
        Assert.True(coordinator.IsButtonOperationAllowed(PanelButtonType.Reset, currentState));

        // Act - 模拟按下 Reset 按钮
        panelReader.SimulatePressButton(PanelButtonType.Reset);
        var resetState = await panelReader.ReadButtonStateAsync(PanelButtonType.Reset);
        Assert.True(resetState.IsPressed);

        // 系统恢复到待机状态 - 先关闭所有灯，再设置新状态
        currentState = SystemOperatingState.Standby;
        await signalTower.TurnOffAllAsync();
        states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: false, upstreamConnected: true);
        await signalTower.SetChannelStatesAsync(states);

        panelReader.SimulateReleaseButton(PanelButtonType.Reset);

        // Assert - 验证恢复后的状态
        towerStates = await signalTower.GetAllChannelStatesAsync();
        Assert.True(towerStates[SignalTowerChannel.Yellow].IsActive);
        Assert.False(towerStates[SignalTowerChannel.Red].IsActive);
    }

    [Fact]
    [SimulationScenario("PanelOps_EmergencyStop_Scenario")]
    public async Task E2E_EmergencyStopScenario()
    {
        // Arrange
        var panelReader = new SimulatedPanelInputReader();
        var signalTower = new SimulatedSignalTowerOutput();
        var coordinator = new DefaultPanelIoCoordinator();

        // 系统运行中
        var currentState = SystemOperatingState.Running;
        await signalTower.TurnOffAllAsync();
        var states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: false, upstreamConnected: true);
        await signalTower.SetChannelStatesAsync(states);

        // Act - 触发急停
        panelReader.SimulatePressButton(PanelButtonType.EmergencyStop);
        Assert.True(coordinator.IsButtonOperationAllowed(PanelButtonType.EmergencyStop, currentState));

        // 系统进入急停状态
        currentState = SystemOperatingState.EmergencyStopped;
        await signalTower.TurnOffAllAsync();
        states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: true, upstreamConnected: true);
        await signalTower.SetChannelStatesAsync(states);

        // Assert - 验证急停状态
        var towerStates = await signalTower.GetAllChannelStatesAsync();
        Assert.True(towerStates[SignalTowerChannel.Red].IsActive);
        Assert.False(towerStates[SignalTowerChannel.Red].IsBlinking); // 急停时红灯常亮，不闪烁
        Assert.True(towerStates[SignalTowerChannel.Buzzer].IsActive);

        // 验证在急停状态下不允许 Start 和再次 EmergencyStop
        Assert.False(coordinator.IsButtonOperationAllowed(PanelButtonType.Start, currentState));
        Assert.False(coordinator.IsButtonOperationAllowed(PanelButtonType.EmergencyStop, currentState));

        // 但允许 Reset
        Assert.True(coordinator.IsButtonOperationAllowed(PanelButtonType.Reset, currentState));
    }

    [Fact]
    [SimulationScenario("PanelOps_UpstreamDisconnected_Warning")]
    public async Task E2E_UpstreamDisconnectedWarning()
    {
        // Arrange
        var signalTower = new SimulatedSignalTowerOutput();
        var coordinator = new DefaultPanelIoCoordinator();

        // Act - 系统运行中，但上游断开
        var currentState = SystemOperatingState.Running;
        await signalTower.TurnOffAllAsync();
        var states = coordinator.DetermineSignalTowerStates(currentState, hasAlarms: false, upstreamConnected: false);
        await signalTower.SetChannelStatesAsync(states);

        // Assert - 验证同时显示绿灯（运行）和闪烁黄灯（上游断开警告）
        var towerStates = await signalTower.GetAllChannelStatesAsync();
        Assert.True(towerStates[SignalTowerChannel.Green].IsActive);
        Assert.True(towerStates[SignalTowerChannel.Yellow].IsActive);
        Assert.True(towerStates[SignalTowerChannel.Yellow].IsBlinking);
    }

    [Fact]
    [SimulationScenario("PanelOps_CompleteWorkflow_StateHistory")]
    public async Task E2E_CompleteWorkflowWithStateHistory()
    {
        // Arrange
        var panelReader = new SimulatedPanelInputReader();
        var signalTower = new SimulatedSignalTowerOutput();
        var coordinator = new DefaultPanelIoCoordinator();

        signalTower.ClearHistory();

        // Act - 执行完整的工作流程
        var workflow = new[]
        {
            SystemOperatingState.Initializing,
            SystemOperatingState.Standby,
            SystemOperatingState.Running,
            SystemOperatingState.Stopping,
            SystemOperatingState.Stopped
        };

        foreach (var state in workflow)
        {
            await signalTower.TurnOffAllAsync();
            var states = coordinator.DetermineSignalTowerStates(state, hasAlarms: false, upstreamConnected: true);
            await signalTower.SetChannelStatesAsync(states);
        }

        // Assert - 验证状态历史记录
        var history = signalTower.GetStateChangeHistory();
        Assert.NotEmpty(history);

        // 验证历史记录包含了不同状态的灯光变化
        var uniqueChannels = history.Select(h => h.State.Channel).Distinct().ToList();
        Assert.Contains(SignalTowerChannel.Yellow, uniqueChannels);
        Assert.Contains(SignalTowerChannel.Green, uniqueChannels);
    }
}
