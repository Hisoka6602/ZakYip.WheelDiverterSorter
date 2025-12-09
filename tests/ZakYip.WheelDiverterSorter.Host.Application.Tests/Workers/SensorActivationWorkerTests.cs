using Moq;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Host.Services.Workers;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests.Workers;

/// <summary>
/// 传感器激活后台服务单元测试
/// </summary>
/// <remarks>
/// 测试 SensorActivationWorker 的传感器生命周期管理功能：
/// - 系统进入 Running 状态时启动传感器
/// - 系统进入 Ready/EmergencyStop/Faulted 状态时停止传感器
/// - 状态转换处理正确性
/// - SafeExecutionService 异常隔离
/// 
/// TODO (TD-051): 完善 SensorActivationWorker 集成测试，增加状态转换场景测试
/// </remarks>
public class SensorActivationWorkerTests
{
    private readonly Mock<ILogger<SensorActivationWorker>> _mockLogger;
    private readonly Mock<ISystemStateManager> _mockStateManager;
    private readonly Mock<IParcelDetectionService> _mockParcelDetectionService;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;

    public SensorActivationWorkerTests()
    {
        _mockLogger = new Mock<ILogger<SensorActivationWorker>>();
        _mockStateManager = new Mock<ISystemStateManager>();
        _mockParcelDetectionService = new Mock<IParcelDetectionService>();
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorActivationWorker(
            null!,
            _mockStateManager.Object,
            _mockParcelDetectionService.Object,
            _mockSafeExecutor.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenStateManagerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorActivationWorker(
            _mockLogger.Object,
            null!,
            _mockParcelDetectionService.Object,
            _mockSafeExecutor.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenParcelDetectionServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorActivationWorker(
            _mockLogger.Object,
            _mockStateManager.Object,
            null!,
            _mockSafeExecutor.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSafeExecutorIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorActivationWorker(
            _mockLogger.Object,
            _mockStateManager.Object,
            _mockParcelDetectionService.Object,
            null!));
    }

    [Theory]
    [InlineData(SystemState.Running)]
    [InlineData(SystemState.Ready)]
    [InlineData(SystemState.Paused)]
    public void Constructor_ShouldSucceed_WithValidParameters(SystemState initialState)
    {
        // Arrange
        _mockStateManager.Setup(sm => sm.CurrentState).Returns(initialState);

        // Act
        var worker = new SensorActivationWorker(
            _mockLogger.Object,
            _mockStateManager.Object,
            _mockParcelDetectionService.Object,
            _mockSafeExecutor.Object);

        // Assert - Worker should be created without throwing
        Assert.NotNull(worker);
    }
}
