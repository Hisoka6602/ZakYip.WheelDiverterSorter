using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Simulated;

public class SimulatedPanelInputReaderTests
{
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly DateTimeOffset _testTime;

    public SimulatedPanelInputReaderTests()
    {
        _systemClockMock = new Mock<ISystemClock>();
        _testTime = new DateTimeOffset(2025, 12, 8, 12, 0, 0, TimeSpan.Zero);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(_testTime);
    }

    [Fact]
    public async Task ReadButtonStateAsync_InitiallyNotPressed()
    {
        // Arrange
        var reader = new SimulatedPanelInputReader(_systemClockMock.Object);

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
        var reader = new SimulatedPanelInputReader(_systemClockMock.Object);

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
        var reader = new SimulatedPanelInputReader(_systemClockMock.Object);
        reader.SimulatePressButton(PanelButtonType.Start);

        // Act - 模拟时间推进
        var pressTime = _testTime;
        var releaseTime = _testTime.AddMilliseconds(50);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(releaseTime);
        
        reader.SimulateReleaseButton(PanelButtonType.Start);
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.False(state.IsPressed);
        Assert.Equal(50, state.PressedDurationMs); // 应该记录 50ms 的按压时长
    }

    [Fact]
    public async Task ReadAllButtonStatesAsync_ShouldReturnAllButtons()
    {
        // Arrange
        var reader = new SimulatedPanelInputReader(_systemClockMock.Object);
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
        var reader = new SimulatedPanelInputReader(_systemClockMock.Object);
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
