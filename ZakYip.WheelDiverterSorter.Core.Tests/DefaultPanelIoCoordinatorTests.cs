using Xunit;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class DefaultPanelIoCoordinatorTests
{
    private readonly DefaultPanelIoCoordinator _coordinator = new();

    [Fact]
    public void DetermineSignalTowerStates_WhenRunning_ShouldReturnGreenLight()
    {
        // Arrange
        var systemState = SystemOperatingState.Running;

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
        var systemState = SystemOperatingState.Faulted;

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
        var systemState = SystemOperatingState.EmergencyStopped;

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
        var systemState = SystemOperatingState.Standby;

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
        var systemState = SystemOperatingState.Running;

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
        var systemState = SystemOperatingState.Standby;

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
        var systemState = SystemOperatingState.Running;

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
        var systemState = SystemOperatingState.Running;

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
        var systemState = SystemOperatingState.Faulted;

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
        Assert.True(_coordinator.IsButtonOperationAllowed(buttonType, SystemOperatingState.Running));
        Assert.True(_coordinator.IsButtonOperationAllowed(buttonType, SystemOperatingState.Standby));
        Assert.True(_coordinator.IsButtonOperationAllowed(buttonType, SystemOperatingState.Faulted));

        // Should NOT be allowed if already emergency stopped
        Assert.False(_coordinator.IsButtonOperationAllowed(buttonType, SystemOperatingState.EmergencyStopped));
    }
}
