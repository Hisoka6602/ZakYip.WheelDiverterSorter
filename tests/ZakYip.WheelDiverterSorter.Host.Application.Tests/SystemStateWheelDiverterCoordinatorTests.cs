using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Host.Services.Workers;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// SystemStateWheelDiverterCoordinator 单元测试
/// </summary>
/// <remarks>
/// 测试系统状态与摆轮协调器的核心功能：
/// - 监听系统状态变化
/// - 当状态转换到 Running 时调用 PassThroughAllAsync
/// - 异常处理和日志记录
/// </remarks>
public class SystemStateWheelDiverterCoordinatorTests
{
    private readonly Mock<ISystemStateManager> _mockStateManager;
    private readonly Mock<IWheelDiverterConnectionService> _mockWheelDiverterService;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly Mock<ILogger<SystemStateWheelDiverterCoordinator>> _mockLogger;

    /// <summary>
    /// 状态变化检测延迟时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 测试中需要等待足够时间让后台服务检测到状态变化（轮询间隔200ms）。
    /// 使用1000ms确保有足够时间完成状态变化检测和处理。
    /// </remarks>
    private const int StateChangeDetectionDelayMs = 1000;

    public SystemStateWheelDiverterCoordinatorTests()
    {
        _mockStateManager = new Mock<ISystemStateManager>();
        _mockWheelDiverterService = new Mock<IWheelDiverterConnectionService>();
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
        _mockLogger = new Mock<ILogger<SystemStateWheelDiverterCoordinator>>();

        // 默认配置 SafeExecutionService 直接执行委托
        _mockSafeExecutor
            .Setup(s => s.ExecuteAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, string, CancellationToken>(
                async (operation, _, ct) => 
                {
                    await operation();
                    return true;
                });
    }

    /// <summary>
    /// 测试构造函数参数验证
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenParametersAreNull()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SystemStateWheelDiverterCoordinator(
                null!,
                _mockWheelDiverterService.Object,
                _mockSafeExecutor.Object,
                _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() =>
            new SystemStateWheelDiverterCoordinator(
                _mockStateManager.Object,
                null!,
                _mockSafeExecutor.Object,
                _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() =>
            new SystemStateWheelDiverterCoordinator(
                _mockStateManager.Object,
                _mockWheelDiverterService.Object,
                null!,
                _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() =>
            new SystemStateWheelDiverterCoordinator(
                _mockStateManager.Object,
                _mockWheelDiverterService.Object,
                _mockSafeExecutor.Object,
                null!));
    }

    /// <summary>
    /// 测试状态转换到 Running 时调用 PassThroughAllAsync
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldCallPassThroughAll_WhenStateChangesToRunning()
    {
        // Arrange
        var currentStateSequence = new Queue<SystemState>(new[]
        {
            SystemState.Booting,
            SystemState.Ready,
            SystemState.Running  // 第三次读取时状态变为Running
        });

        _mockStateManager
            .Setup(m => m.CurrentStatetate)
            .Returns(() => currentStateSequence.Count > 0 ? currentStateSequence.Dequeue() : SystemState.Running);

        _mockWheelDiverterService
            .Setup(s => s.PassThroughAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WheelDiverterOperationResult
            {
                IsSuccess = true,
                SuccessCount = 2,
                TotalCount = 2,
                FailedDriverIds = new List<string>()
            });

        var coordinator = new SystemStateWheelDiverterCoordinator(
            _mockStateManager.Object,
            _mockWheelDiverterService.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        // Act - 启动服务，等待一段时间让它检测到状态变化
        var executeTask = coordinator.StartAsync(cts.Token);
        await Task.Delay(StateChangeDetectionDelayMs);  // 等待足够时间让状态变化被检测
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // 预期的取消异常
        }

        // Assert - 验证 PassThroughAllAsync 被调用
        _mockWheelDiverterService.Verify(
            s => s.PassThroughAllAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce(),
            "当状态转换到 Running 时应调用 PassThroughAllAsync");
    }

    /// <summary>
    /// 测试 PassThroughAllAsync 成功时的日志记录
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldLogSuccess_WhenPassThroughAllSucceeds()
    {
        // Arrange
        var currentStateSequence = new Queue<SystemState>(new[]
        {
            SystemState.Ready,
            SystemState.Running
        });

        _mockStateManager
            .Setup(m => m.CurrentStatetate)
            .Returns(() => currentStateSequence.Count > 0 ? currentStateSequence.Dequeue() : SystemState.Running);

        _mockWheelDiverterService
            .Setup(s => s.PassThroughAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WheelDiverterOperationResult
            {
                IsSuccess = true,
                SuccessCount = 3,
                TotalCount = 3,
                FailedDriverIds = new List<string>()
            });

        var coordinator = new SystemStateWheelDiverterCoordinator(
            _mockStateManager.Object,
            _mockWheelDiverterService.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = coordinator.StartAsync(cts.Token);
        await Task.Delay(StateChangeDetectionDelayMs);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // 预期的取消异常
        }

        // Assert - 验证成功日志被记录
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("所有摆轮已成功设置为直行状态")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    /// <summary>
    /// 测试 PassThroughAllAsync 部分失败时的日志记录
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldLogWarning_WhenPassThroughAllPartiallyFails()
    {
        // Arrange
        var currentStateSequence = new Queue<SystemState>(new[]
        {
            SystemState.Ready,
            SystemState.Running
        });

        _mockStateManager
            .Setup(m => m.CurrentStatetate)
            .Returns(() => currentStateSequence.Count > 0 ? currentStateSequence.Dequeue() : SystemState.Running);

        _mockWheelDiverterService
            .Setup(s => s.PassThroughAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WheelDiverterOperationResult
            {
                IsSuccess = false,
                SuccessCount = 2,
                TotalCount = 3,
                FailedDriverIds = new List<string> { "Diverter-001" },
                ErrorMessage = "部分摆轮设置失败"
            });

        var coordinator = new SystemStateWheelDiverterCoordinator(
            _mockStateManager.Object,
            _mockWheelDiverterService.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = coordinator.StartAsync(cts.Token);
        await Task.Delay(StateChangeDetectionDelayMs);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // 预期的取消异常
        }

        // Assert - 验证警告日志被记录
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("部分摆轮设置为直行失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    /// <summary>
    /// 测试不在 Running 状态时不调用 PassThroughAllAsync
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldNotCallPassThroughAll_WhenStateDoesNotChangeToRunning()
    {
        // Arrange
        _mockStateManager
            .Setup(m => m.CurrentStatetate)
            .Returns(SystemState.Ready);  // 保持在 Ready 状态

        var coordinator = new SystemStateWheelDiverterCoordinator(
            _mockStateManager.Object,
            _mockWheelDiverterService.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = coordinator.StartAsync(cts.Token);
        await Task.Delay(500);  // 等待一段时间
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // 预期的取消异常
        }

        // Assert - 验证 PassThroughAllAsync 未被调用
        _mockWheelDiverterService.Verify(
            s => s.PassThroughAllAsync(It.IsAny<CancellationToken>()),
            Times.Never(),
            "状态未变为 Running 时不应调用 PassThroughAllAsync");
    }

    /// <summary>
    /// 测试从 Paused 恢复到 Running 也会触发 PassThroughAllAsync
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldCallPassThroughAll_WhenResumingFromPaused()
    {
        // Arrange
        var currentStateSequence = new Queue<SystemState>(new[]
        {
            SystemState.Paused,
            SystemState.Running  // 从暂停恢复到运行
        });

        _mockStateManager
            .Setup(m => m.CurrentStatetate)
            .Returns(() => currentStateSequence.Count > 0 ? currentStateSequence.Dequeue() : SystemState.Running);

        _mockWheelDiverterService
            .Setup(s => s.PassThroughAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WheelDiverterOperationResult
            {
                IsSuccess = true,
                SuccessCount = 2,
                TotalCount = 2,
                FailedDriverIds = new List<string>()
            });

        var coordinator = new SystemStateWheelDiverterCoordinator(
            _mockStateManager.Object,
            _mockWheelDiverterService.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = coordinator.StartAsync(cts.Token);
        await Task.Delay(StateChangeDetectionDelayMs);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // 预期的取消异常
        }

        // Assert - 验证 PassThroughAllAsync 被调用
        _mockWheelDiverterService.Verify(
            s => s.PassThroughAllAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce(),
            "从 Paused 恢复到 Running 时应调用 PassThroughAllAsync");
    }
}
