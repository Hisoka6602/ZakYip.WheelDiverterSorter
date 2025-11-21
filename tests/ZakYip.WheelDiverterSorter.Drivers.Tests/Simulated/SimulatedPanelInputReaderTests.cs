using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Simulated;

public class SimulatedPanelInputReaderTests
{
    [Fact]
    public async Task ReadButtonStateAsync_InitiallyNotPressed()
    {
        // Arrange
        var reader = new SimulatedPanelInputReader();

        // Act
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.Equal(PanelButtonType.Start, state.ButtonType);
        Assert.False(state.IsPressed);
        Assert.Equal(0, state.PressedDurationMs);
    }

    [Fact]
    public async Task SimulatePressButton_ShouldUpdateState()
    {
        // Arrange
        var reader = new SimulatedPanelInputReader();

        // Act
        reader.SimulatePressButton(PanelButtonType.Start);
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.True(state.IsPressed);
        Assert.Equal(PanelButtonType.Start, state.ButtonType);
    }

    [Fact]
    public async Task SimulateReleaseButton_ShouldUpdateStateAndRecordDuration()
    {
        // Arrange
        var reader = new SimulatedPanelInputReader();
        reader.SimulatePressButton(PanelButtonType.Start);

        // Act - Wait a bit then release
        await Task.Delay(50);
        reader.SimulateReleaseButton(PanelButtonType.Start);
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.False(state.IsPressed);
        Assert.True(state.PressedDurationMs > 0); // Should have some duration
    }

    [Fact]
    public async Task ReadAllButtonStatesAsync_ShouldReturnAllButtons()
    {
        // Arrange
        var reader = new SimulatedPanelInputReader();
        reader.SimulatePressButton(PanelButtonType.Start);
        reader.SimulatePressButton(PanelButtonType.Stop);

        // Act
        var allStates = await reader.ReadAllButtonStatesAsync();

        // Assert
        Assert.NotEmpty(allStates);
        Assert.Contains(allStates, kvp => kvp.Key == PanelButtonType.Start && kvp.Value.IsPressed);
        Assert.Contains(allStates, kvp => kvp.Key == PanelButtonType.Stop && kvp.Value.IsPressed);
        Assert.Contains(allStates, kvp => kvp.Key == PanelButtonType.Reset && !kvp.Value.IsPressed);
    }

    [Fact]
    public async Task ResetAllButtons_ShouldClearAllPressedStates()
    {
        // Arrange
        var reader = new SimulatedPanelInputReader();
        reader.SimulatePressButton(PanelButtonType.Start);
        reader.SimulatePressButton(PanelButtonType.Stop);
        reader.SimulatePressButton(PanelButtonType.Reset);

        // Act
        reader.ResetAllButtons();
        var allStates = await reader.ReadAllButtonStatesAsync();

        // Assert
        Assert.All(allStates, kvp => Assert.False(kvp.Value.IsPressed));
    }
}
