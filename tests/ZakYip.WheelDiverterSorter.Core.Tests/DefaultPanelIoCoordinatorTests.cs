using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;


using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;namespace ZakYip.WheelDiverterSorter.Core.Tests;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

public class DefaultPanelIoCoordinatorTests
{
    private readonly DefaultPanelIoCoordinator _coordinator = new();

    [Fact]
    public void DetermineSignalTowerStates_WhenRunning_ShouldReturnGreenLight()
    {
        // Arrange
        var systemState = SystemState.Running;

        // Act
        var states = _coordinator.DetermineSignalTowerStates(systemState, hasAlarms: false, upstreamConnected: true);

        // Assert
        var statesList = states.ToList();
        Assert.Single(statesList);
        Assert.Contains(statesList, s => s.Channel == SignalTowerChannel.Green && s.IsActive);
    }

    [Fact]
    public void DetermineSignalTowerStates_WhenFaulted_ShouldReturnBlinkingRedLightAndBuzzer()
    {
        // Arrange
        var systemState = SystemState.Faulted;

        // Act
        var states = _coordinator.DetermineSignalTowerStates(systemState, hasAlarms: true, upstreamConnected: true);

        // Assert
        var statesList = states.ToList();
        Assert.Contains(statesList, s => s.Channel == SignalTowerChannel.Red && s.IsBlinking);
        Assert.Contains(statesList, s => s.Channel == SignalTowerChannel.Buzzer && s.IsBlinking);
    }

    [Fact]
    public void DetermineSignalTowerStates_WhenEmergencyStopped_ShouldReturnSolidRedLightAndBuzzer()
    {
        // Arrange
        var systemState = SystemState.EmergencyStop;

        // Act
        var states = _coordinator.DetermineSignalTowerStates(systemState, hasAlarms: true, upstreamConnected: true);

        // Assert
        var statesList = states.ToList();
        Assert.Contains(statesList, s => s.Channel == SignalTowerChannel.Red && s.IsActive && !s.IsBlinking);
        Assert.Contains(statesList, s => s.Channel == SignalTowerChannel.Buzzer && s.IsActive && !s.IsBlinking);
    }

    [Fact]
    public void DetermineSignalTowerStates_WhenStandby_ShouldReturnYellowLight()
    {
        // Arrange
        var systemState = SystemState.Ready;

        // Act
        var states = _coordinator.DetermineSignalTowerStates(systemState, hasAlarms: false, upstreamConnected: true);

        // Assert
        var statesList = states.ToList();
        Assert.Single(statesList);
        Assert.Contains(statesList, s => s.Channel == SignalTowerChannel.Yellow && s.IsActive && !s.IsBlinking);
    }

    [Fact]
    public void DetermineSignalTowerStates_WhenRunningWithoutUpstream_ShouldIncludeBlinkingYellow()
    {
        // Arrange
        var systemState = SystemState.Running;

        // Act
        var states = _coordinator.DetermineSignalTowerStates(systemState, hasAlarms: false, upstreamConnected: false);

        // Assert
        var statesList = states.ToList();
        Assert.Contains(statesList, s => s.Channel == SignalTowerChannel.Green && s.IsActive);
        Assert.Contains(statesList, s => s.Channel == SignalTowerChannel.Yellow && s.IsBlinking);
    }

    [Fact]
    public void IsButtonOperationAllowed_StartButton_InStandby_ShouldReturnTrue()
    {
        // Arrange
        var buttonType = PanelButtonType.Start;
        var systemState = SystemState.Ready;

        // Act
        var result = _coordinator.IsButtonOperationAllowed(buttonType, systemState);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsButtonOperationAllowed_StartButton_InRunning_ShouldReturnFalse()
    {
        // Arrange
        var buttonType = PanelButtonType.Start;
        var systemState = SystemState.Running;

        // Act
        var result = _coordinator.IsButtonOperationAllowed(buttonType, systemState);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsButtonOperationAllowed_StopButton_InRunning_ShouldReturnTrue()
    {
        // Arrange
        var buttonType = PanelButtonType.Stop;
        var systemState = SystemState.Running;

        // Act
        var result = _coordinator.IsButtonOperationAllowed(buttonType, systemState);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsButtonOperationAllowed_ResetButton_InFaulted_ShouldReturnTrue()
    {
        // Arrange
        var buttonType = PanelButtonType.Reset;
        var systemState = SystemState.Faulted;

        // Act
        var result = _coordinator.IsButtonOperationAllowed(buttonType, systemState);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsButtonOperationAllowed_EmergencyStop_AlwaysAllowed_ExceptInEmergencyStopped()
    {
        // Arrange
        var buttonType = PanelButtonType.EmergencyStop;

        // Act & Assert - Should be allowed in most states
        Assert.True(_coordinator.IsButtonOperationAllowed(buttonType, SystemState.Running));
        Assert.True(_coordinator.IsButtonOperationAllowed(buttonType, SystemState.Ready));
        Assert.True(_coordinator.IsButtonOperationAllowed(buttonType, SystemState.Faulted));

        // Should NOT be allowed if already emergency stopped
        Assert.False(_coordinator.IsButtonOperationAllowed(buttonType, SystemState.EmergencyStop));
    }
}
