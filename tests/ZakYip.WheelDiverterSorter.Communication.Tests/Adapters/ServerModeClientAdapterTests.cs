using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Adapters;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Adapters;

/// <summary>
/// ServerModeClientAdapter 测试
/// </summary>
/// <remarks>
/// 验证服务端模式适配器正确处理各种上游消息类型，特别是 PanelButtonPressedMessage
/// </remarks>
public class ServerModeClientAdapterTests : IDisposable
{
    private readonly UpstreamServerBackgroundService _serverService;
    private readonly Mock<ILogger<ServerModeClientAdapter>> _mockLogger;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly ServerModeClientAdapter _adapter;

    public ServerModeClientAdapterTests()
    {
        // 创建真实的依赖项（但不启动服务器）
        var serverLogger = new Mock<ILogger<UpstreamServerBackgroundService>>().Object;
        _mockClock = new Mock<ISystemClock>();
        _mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2024, 1, 15, 10, 30, 0));
        
        var safeExecutor = new Mock<ISafeExecutionService>().Object;
        var options = new UpstreamConnectionOptions
        {
            ConnectionMode = ConnectionMode.Client // 使用Client模式以避免启动服务器
        };
        
        // 创建一个空的 RuleEngineServerFactory（不需要实际功能）
        var loggerFactory = new Mock<Microsoft.Extensions.Logging.ILoggerFactory>().Object;
        var serverFactory = new RuleEngineServerFactory(
            loggerFactory,
            _mockClock.Object);
        
        _serverService = new UpstreamServerBackgroundService(
            serverLogger,
            _mockClock.Object,
            safeExecutor,
            options,
            serverFactory);
        
        _mockLogger = new Mock<ILogger<ServerModeClientAdapter>>();
        
        _adapter = new ServerModeClientAdapter(
            _serverService,
            _mockLogger.Object,
            _mockClock.Object,
            safeExecutor);  // 添加 safeExecutor 参数
    }

    public void Dispose()
    {
        _adapter?.Dispose();
        _serverService?.Dispose();
    }

    [Fact(DisplayName = "SendAsync 应成功处理 PanelButtonPressedMessage")]
    public async Task SendAsync_WithPanelButtonPressedMessage_ShouldReturnTrue()
    {
        // Arrange
        var message = new PanelButtonPressedMessage
        {
            ButtonType = PanelButtonType.Start,
            PressedAt = DateTimeOffset.Now,
            SystemStateBefore = SystemState.Ready,
            SystemStateAfter = SystemState.Running
        };

        // Act
        var result = await _adapter.SendAsync(message);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "SendAsync 处理 PanelButtonPressedMessage 时应记录日志")]
    public async Task SendAsync_WithPanelButtonPressedMessage_ShouldLogInformation()
    {
        // Arrange
        var message = new PanelButtonPressedMessage
        {
            ButtonType = PanelButtonType.Stop,
            PressedAt = DateTimeOffset.Now,
            SystemStateBefore = SystemState.Running,
            SystemStateAfter = SystemState.Paused
        };

        // Act
        await _adapter.SendAsync(message);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("面板按钮按下通知")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory(DisplayName = "SendAsync 应处理所有面板按钮类型")]
    [InlineData(PanelButtonType.Start, SystemState.Ready, SystemState.Running)]
    [InlineData(PanelButtonType.Stop, SystemState.Running, SystemState.Paused)]
    [InlineData(PanelButtonType.EmergencyStop, SystemState.Running, SystemState.EmergencyStop)]
    [InlineData(PanelButtonType.Reset, SystemState.EmergencyStop, SystemState.Ready)]
    public async Task SendAsync_WithVariousPanelButtonTypes_ShouldReturnTrue(
        PanelButtonType buttonType,
        SystemState stateBefore,
        SystemState stateAfter)
    {
        // Arrange
        var message = new PanelButtonPressedMessage
        {
            ButtonType = buttonType,
            PressedAt = DateTimeOffset.Now,
            SystemStateBefore = stateBefore,
            SystemStateAfter = stateAfter
        };

        // Act
        var result = await _adapter.SendAsync(message);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "SendAsync 处理 PanelButtonPressedMessage 应使用 ISystemClock")]
    public async Task SendAsync_WithPanelButtonPressedMessage_ShouldUseSystemClock()
    {
        // Arrange
        var expectedTime = new DateTime(2024, 12, 15, 14, 30, 0);
        _mockClock.Setup(c => c.LocalNow).Returns(expectedTime);
        
        var message = new PanelButtonPressedMessage
        {
            ButtonType = PanelButtonType.Start,
            PressedAt = DateTimeOffset.Now,
            SystemStateBefore = SystemState.Ready,
            SystemStateAfter = SystemState.Running
        };

        // Act
        await _adapter.SendAsync(message);

        // Assert
        _mockClock.Verify(c => c.LocalNow, Times.Once);
    }
}
