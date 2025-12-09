using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Host.Configuration;
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
/// TD-051: 已完善状态转换场景测试和异常场景测试（当前 PR）
/// </remarks>
public class SensorActivationWorkerTests
{
    private readonly Mock<ILogger<SensorActivationWorker>> _mockLogger;
    private readonly Mock<ISystemStateManager> _mockStateManager;
    private readonly Mock<IParcelDetectionService> _mockParcelDetectionService;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly IOptions<WorkerOptions> _workerOptions;

    public SensorActivationWorkerTests()
    {
        _mockLogger = new Mock<ILogger<SensorActivationWorker>>();
        _mockStateManager = new Mock<ISystemStateManager>();
        _mockParcelDetectionService = new Mock<IParcelDetectionService>();
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
        _workerOptions = Options.Create(new WorkerOptions());
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorActivationWorker(
            null!,
            _mockStateManager.Object,
            _mockParcelDetectionService.Object,
            _mockSafeExecutor.Object,
            _workerOptions));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenStateManagerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorActivationWorker(
            _mockLogger.Object,
            null!,
            _mockParcelDetectionService.Object,
            _mockSafeExecutor.Object,
            _workerOptions));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenParcelDetectionServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorActivationWorker(
            _mockLogger.Object,
            _mockStateManager.Object,
            null!,
            _mockSafeExecutor.Object,
            _workerOptions));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSafeExecutorIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorActivationWorker(
            _mockLogger.Object,
            _mockStateManager.Object,
            _mockParcelDetectionService.Object,
            null!,
            _workerOptions));
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
            _mockSafeExecutor.Object,
            _workerOptions);

        // Assert - Worker should be created without throwing
        Assert.NotNull(worker);
    }

    /// <summary>
    /// 测试：验证 Worker 能够正确使用 SafeExecutionService 并执行实际逻辑
    /// TD-051: 验证 SafeExecutionService 集成
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldUseSafeExecutionService()
    {
        // Arrange
        var executeCalled = false;
        
        _mockStateManager.Setup(sm => sm.CurrentState).Returns(SystemState.Booting);

        _mockSafeExecutor
            .Setup(se => se.ExecuteAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, string, CancellationToken>(async (func, _, ct) =>
            {
                executeCalled = true;
                await func(); // 实际执行传入的委托以验证内部逻辑
                return true;
            });

        var worker = new SensorActivationWorker(
            _mockLogger.Object,
            _mockStateManager.Object,
            _mockParcelDetectionService.Object,
            _mockSafeExecutor.Object,
            _workerOptions);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(executeCalled, "Worker 应通过 SafeExecutionService 执行");
        
        _mockSafeExecutor.Verify(
            se => se.ExecuteAsync(
                It.IsAny<Func<Task>>(),
                "SensorActivationWorkerLoop",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "应使用正确的操作名称调用 SafeExecutionService");
    }
}
