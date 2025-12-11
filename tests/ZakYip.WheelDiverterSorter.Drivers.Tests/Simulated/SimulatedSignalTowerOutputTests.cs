using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Simulated;

public class SimulatedSignalTowerOutputTests
{
    [Fact]
    public async Task SetChannelStateAsync_ShouldUpdateChannelState()
    {
        // Arrange
        var output = new SimulatedSignalTowerOutput();
        var state = SignalTowerState.CreateOn(SignalTowerChannel.Green);

        // Act
        await output.SetChannelStateAsync(state);
        var allStates = await output.GetAllChannelStatesAsync();

        // Assert
        Assert.True(allStates[SignalTowerChannel.Green].IsActive);
        Assert.False(allStates[SignalTowerChannel.Green].IsBlinking);
    }

    [Fact]
    public async Task SetChannelStatesAsync_ShouldUpdateMultipleChannels()
    {
        // Arrange
        var output = new SimulatedSignalTowerOutput();
        var states = new[]
        {
            SignalTowerState.CreateOn(SignalTowerChannel.Red),
            SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 500)
        };

        // Act
        await output.SetChannelStatesAsync(states);
        var allStates = await output.GetAllChannelStatesAsync();

        // Assert
        Assert.True(allStates[SignalTowerChannel.Red].IsActive);
        Assert.False(allStates[SignalTowerChannel.Red].IsBlinking);
        Assert.True(allStates[SignalTowerChannel.Yellow].IsActive);
        Assert.True(allStates[SignalTowerChannel.Yellow].IsBlinking);
    }

    [Fact]
    public async Task TurnOffAllAsync_ShouldDeactivateAllChannels()
    {
        // Arrange
        var output = new SimulatedSignalTowerOutput();
        await output.SetChannelStateAsync(SignalTowerState.CreateOn(SignalTowerChannel.Red));
        await output.SetChannelStateAsync(SignalTowerState.CreateOn(SignalTowerChannel.Green));
        await output.SetChannelStateAsync(SignalTowerState.CreateOn(SignalTowerChannel.Buzzer));

        // Act
        await output.TurnOffAllAsync();
        var allStates = await output.GetAllChannelStatesAsync();

        // Assert
        Assert.All(allStates, kvp => Assert.False(kvp.Value.IsActive));
    }

    [Fact]
    public async Task GetStateChangeHistory_ShouldRecordChanges()
    {
        // Arrange
        var output = new SimulatedSignalTowerOutput();

        // Act
        await output.SetChannelStateAsync(SignalTowerState.CreateOn(SignalTowerChannel.Green));
        await output.SetChannelStateAsync(SignalTowerState.CreateBlinking(SignalTowerChannel.Yellow, 500));
        await output.SetChannelStateAsync(SignalTowerState.CreateOff(SignalTowerChannel.Green));

        var history = output.GetStateChangeHistory();

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal(SignalTowerChannel.Green, history[0].State.Channel);
        Assert.True(history[0].State.IsActive);
        Assert.Equal(SignalTowerChannel.Yellow, history[1].State.Channel);
        Assert.True(history[1].State.IsBlinking);
        Assert.Equal(SignalTowerChannel.Green, history[2].State.Channel);
        Assert.False(history[2].State.IsActive);
    }

    [Fact]
    public void ClearHistory_ShouldRemoveAllHistoryEntries()
    {
        // Arrange
        var output = new SimulatedSignalTowerOutput();
        output.SetChannelStateAsync(SignalTowerState.CreateOn(SignalTowerChannel.Red)).Wait();
        output.SetChannelStateAsync(SignalTowerState.CreateOn(SignalTowerChannel.Yellow)).Wait();

        // Act
        output.ClearHistory();
        var history = output.GetStateChangeHistory();

        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public async Task InitialState_AllChannelsShouldBeOff()
    {
        // Arrange & Act
        var output = new SimulatedSignalTowerOutput();
        var allStates = await output.GetAllChannelStatesAsync();

        // Assert
        Assert.All(allStates, kvp => Assert.False(kvp.Value.IsActive));
    }
}
