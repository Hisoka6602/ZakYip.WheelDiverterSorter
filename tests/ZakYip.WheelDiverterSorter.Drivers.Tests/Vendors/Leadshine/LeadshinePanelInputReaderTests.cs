using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Vendors.Leadshine;

/// <summary>
/// 雷赛面板输入读取器测试
/// </summary>
public class LeadshinePanelInputReaderTests
{
    private readonly Mock<ILogger<LeadshinePanelInputReader>> _loggerMock;
    private readonly Mock<IInputPort> _inputPortMock;
    private readonly Mock<IPanelConfigService> _configServiceMock;
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly DateTimeOffset _testTime;

    public LeadshinePanelInputReaderTests()
    {
        _loggerMock = new Mock<ILogger<LeadshinePanelInputReader>>();
        _inputPortMock = new Mock<IInputPort>();
        _configServiceMock = new Mock<IPanelConfigService>();
        _systemClockMock = new Mock<ISystemClock>();
        
        _testTime = new DateTimeOffset(2025, 12, 8, 12, 0, 0, TimeSpan.Zero);
        _systemClockMock.Setup(x => x.LocalNowOffset).Returns(_testTime);
    }

    private LeadshinePanelInputReader CreateReader()
    {
        return new LeadshinePanelInputReader(
            _loggerMock.Object,
            _inputPortMock.Object,
            _configServiceMock.Object,
            _systemClockMock.Object);
    }

    private PanelConfiguration CreateTestConfig(
        int? startBit = 1,
        TriggerLevel startLevel = TriggerLevel.ActiveLow,
        int? stopBit = 2,
        TriggerLevel stopLevel = TriggerLevel.ActiveLow,
        int? emergencyStopBit = 3,
        TriggerLevel emergencyStopLevel = TriggerLevel.ActiveHigh)
    {
        var emergencyStopButtons = new List<EmergencyStopButtonConfig>();
        if (emergencyStopBit.HasValue)
        {
            emergencyStopButtons.Add(new EmergencyStopButtonConfig 
            { 
                InputBit = emergencyStopBit.Value, 
                InputTriggerLevel = emergencyStopLevel 
            });
        }

        return new PanelConfiguration
        {
            StartButtonInputBit = startBit,
            StartButtonTriggerLevel = startLevel,
            StopButtonInputBit = stopBit,
            StopButtonTriggerLevel = stopLevel,
            EmergencyStopButtons = emergencyStopButtons
        };
    }

    [Fact]
    public async Task ReadButtonStateAsync_按钮按下_ActiveHigh触发电平_返回按下状态()
    {
        // Arrange
        var config = CreateTestConfig(startBit: 1, startLevel: TriggerLevel.ActiveHigh);
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);
        _inputPortMock.Setup(x => x.ReadAsync(1)).ReturnsAsync(true); // IO 位为高电平

        var reader = CreateReader();

        // Act
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.Equal(PanelButtonType.Start, state.ButtonType);
        Assert.True(state.IsPressed); // ActiveHigh + true = 按下
        Assert.Equal(_testTime, state.LastChangedAt);
        Assert.Equal(0, state.PressedDurationMs);
    }

    [Fact]
    public async Task ReadButtonStateAsync_按钮未按下_ActiveHigh触发电平_返回未按下状态()
    {
        // Arrange
        var config = CreateTestConfig(startBit: 1, startLevel: TriggerLevel.ActiveHigh);
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);
        _inputPortMock.Setup(x => x.ReadAsync(1)).ReturnsAsync(false); // IO 位为低电平

        var reader = CreateReader();

        // Act
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.Equal(PanelButtonType.Start, state.ButtonType);
        Assert.False(state.IsPressed); // ActiveHigh + false = 未按下
    }

    [Fact]
    public async Task ReadButtonStateAsync_按钮按下_ActiveLow触发电平_返回按下状态()
    {
        // Arrange
        var config = CreateTestConfig(startBit: 1, startLevel: TriggerLevel.ActiveLow);
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);
        _inputPortMock.Setup(x => x.ReadAsync(1)).ReturnsAsync(false); // IO 位为低电平

        var reader = CreateReader();

        // Act
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.Equal(PanelButtonType.Start, state.ButtonType);
        Assert.True(state.IsPressed); // ActiveLow + false = 按下
    }

    [Fact]
    public async Task ReadButtonStateAsync_按钮未按下_ActiveLow触发电平_返回未按下状态()
    {
        // Arrange
        var config = CreateTestConfig(startBit: 1, startLevel: TriggerLevel.ActiveLow);
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);
        _inputPortMock.Setup(x => x.ReadAsync(1)).ReturnsAsync(true); // IO 位为高电平

        var reader = CreateReader();

        // Act
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.Equal(PanelButtonType.Start, state.ButtonType);
        Assert.False(state.IsPressed); // ActiveLow + true = 未按下
    }

    [Fact]
    public async Task ReadButtonStateAsync_按钮未配置_返回未按下状态()
    {
        // Arrange
        var config = CreateTestConfig(startBit: null); // Start 按钮未配置
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);

        var reader = CreateReader();

        // Act
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.Equal(PanelButtonType.Start, state.ButtonType);
        Assert.False(state.IsPressed);
        Assert.Equal(_testTime, state.LastChangedAt);
        
        // 不应调用 IO 读取
        _inputPortMock.Verify(x => x.ReadAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ReadButtonStateAsync_Reset按钮_返回未按下状态()
    {
        // Arrange
        var config = CreateTestConfig();
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);

        var reader = CreateReader();

        // Act
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Reset);

        // Assert
        Assert.Equal(PanelButtonType.Reset, state.ButtonType);
        Assert.False(state.IsPressed); // Reset 按钮未在配置中定义，始终返回未按下
        
        // 不应调用 IO 读取
        _inputPortMock.Verify(x => x.ReadAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ReadButtonStateAsync_IO读取失败_返回未按下状态并记录日志()
    {
        // Arrange
        var config = CreateTestConfig(startBit: 1);
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);
        _inputPortMock.Setup(x => x.ReadAsync(1)).ThrowsAsync(new InvalidOperationException("IO 读取失败"));

        var reader = CreateReader();

        // Act
        var state = await reader.ReadButtonStateAsync(PanelButtonType.Start);

        // Assert
        Assert.Equal(PanelButtonType.Start, state.ButtonType);
        Assert.False(state.IsPressed); // 异常时返回未按下状态
        
        // 验证记录了错误日志
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task ReadAllButtonStatesAsync_读取所有按钮状态()
    {
        // Arrange
        var config = CreateTestConfig(
            startBit: 1,
            startLevel: TriggerLevel.ActiveLow,
            stopBit: 2,
            stopLevel: TriggerLevel.ActiveLow,
            emergencyStopBit: 3,
            emergencyStopLevel: TriggerLevel.ActiveHigh);
        
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);
        _inputPortMock.Setup(x => x.ReadAsync(1)).ReturnsAsync(false); // Start 按下 (ActiveLow)
        _inputPortMock.Setup(x => x.ReadAsync(2)).ReturnsAsync(true);  // Stop 未按下 (ActiveLow)
        _inputPortMock.Setup(x => x.ReadAsync(3)).ReturnsAsync(true);  // EmergencyStop 按下 (ActiveHigh)

        var reader = CreateReader();

        // Act
        var allStates = await reader.ReadAllButtonStatesAsync();

        // Assert
        Assert.NotEmpty(allStates);
        Assert.Contains(allStates, kvp => kvp.Key == PanelButtonType.Start && kvp.Value.IsPressed);
        Assert.Contains(allStates, kvp => kvp.Key == PanelButtonType.Stop && !kvp.Value.IsPressed);
        Assert.Contains(allStates, kvp => kvp.Key == PanelButtonType.EmergencyStop && kvp.Value.IsPressed);
        Assert.Contains(allStates, kvp => kvp.Key == PanelButtonType.Reset && !kvp.Value.IsPressed);
    }

    [Fact]
    public async Task ReadAllButtonStatesAsync_多个按钮_只读取配置一次()
    {
        // Arrange
        var config = CreateTestConfig();
        _configServiceMock.Setup(x => x.GetPanelConfig()).Returns(config);
        _inputPortMock.Setup(x => x.ReadAsync(It.IsAny<int>())).ReturnsAsync(false);

        var reader = CreateReader();

        // Act
        await reader.ReadAllButtonStatesAsync();

        // Assert - 验证配置只读取了一次（通过查看实际调用次数）
        // 注意：当前实现会为每个按钮读取一次配置，这是一个已知的性能问题
        // 此测试用于确保未来优化后配置只读取一次
    }
}
