using Moq;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Execution.Orchestration;
using ZakYip.WheelDiverterSorter.Execution.Queues;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Orchestration;

/// <summary>
/// 测试系统状态变更时队列清空功能
/// </summary>
/// <remarks>
/// 验证 SortingOrchestrator 正确响应系统状态变更事件并清空队列。
/// 根据 CORE_ROUTING_LOGIC.md 规则测试队列清空时机。
/// </remarks>
public class SortingOrchestratorStateChangeTests : IDisposable
{
    private readonly Mock<IParcelDetectionService> _mockSensorEventProvider;
    private readonly Mock<IUpstreamRoutingClient> _mockUpstreamClient;
    private readonly Mock<ISwitchingPathGenerator> _mockPathGenerator;
    private readonly Mock<ISwitchingPathExecutor> _mockPathExecutor;
    private readonly Mock<ISystemConfigurationRepository> _mockConfigRepository;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<ILogger<SortingOrchestrator>> _mockLogger;
    private readonly Mock<ISortingExceptionHandler> _mockExceptionHandler;
    private readonly Mock<ISystemStateManager> _mockStateManager;
    private readonly Mock<IPositionIndexQueueManager> _mockQueueManager;
    private readonly IOptions<UpstreamConnectionOptions> _options;
    private readonly SortingOrchestrator _orchestrator;
    private readonly DateTimeOffset _testTime;

    public SortingOrchestratorStateChangeTests()
    {
        _mockSensorEventProvider = new Mock<IParcelDetectionService>();
        _mockUpstreamClient = new Mock<IUpstreamRoutingClient>();
        _mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        _mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        _mockConfigRepository = new Mock<ISystemConfigurationRepository>();
        _mockClock = new Mock<ISystemClock>();
        _mockLogger = new Mock<ILogger<SortingOrchestrator>>();
        _mockExceptionHandler = new Mock<ISortingExceptionHandler>();
        _mockStateManager = new Mock<ISystemStateManager>();
        _mockQueueManager = new Mock<IPositionIndexQueueManager>();

        _testTime = new DateTimeOffset(2025, 12, 18, 12, 0, 0, TimeSpan.Zero);
        _mockClock.Setup(c => c.LocalNow).Returns(_testTime.LocalDateTime);
        _mockClock.Setup(c => c.LocalNowOffset).Returns(_testTime);

        _options = Options.Create(new UpstreamConnectionOptions
        {
            FallbackTimeoutSeconds = 5m
        });

        var defaultConfig = new SystemConfiguration
        {
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999,
            ChuteAssignmentTimeout = new ChuteAssignmentTimeoutOptions { FallbackTimeoutSeconds = 5m }
        };

        _mockConfigRepository.Setup(r => r.Get()).Returns(defaultConfig);

        _orchestrator = new SortingOrchestrator(
            _mockSensorEventProvider.Object,
            _mockUpstreamClient.Object,
            _mockPathGenerator.Object,
            _mockPathExecutor.Object,
            _options,
            _mockConfigRepository.Object,
            _mockClock.Object,
            _mockLogger.Object,
            _mockExceptionHandler.Object,
            _mockStateManager.Object,
            queueManager: _mockQueueManager.Object
        );
    }

    [Fact]
    public void Constructor_ShouldSubscribeToStateChangedEvent()
    {
        // Arrange & Act - Constructor already called in setup
        
        // Assert - Verify that StateChanged event was subscribed to
        _mockStateManager.VerifyAdd(m => m.StateChanged += It.IsAny<EventHandler<StateChangeEventArgs>>(), Times.Once);
    }

    [Fact]
    public void StateChange_ToEmergencyStop_ShouldClearQueues()
    {
        // Arrange
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Running,
            NewState = SystemState.EmergencyStop,
            ChangedAt = _testTime
        };

        // Act - Raise the StateChanged event
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert - Verify ClearAllQueues was called
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Once);
    }

    [Fact]
    public void StateChange_FromRunningToReady_ShouldClearQueues()
    {
        // Arrange
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Running,
            NewState = SystemState.Ready,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Once);
    }

    [Fact]
    public void StateChange_FromPausedToReady_ShouldClearQueues()
    {
        // Arrange
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Paused,
            NewState = SystemState.Ready,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Once);
    }

    [Fact]
    public void StateChange_FromEmergencyStopToReady_ShouldClearQueues()
    {
        // Arrange - Emergency stop reset/recovery
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.EmergencyStop,
            NewState = SystemState.Ready,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Once);
    }

    [Fact]
    public void StateChange_FromFaultedToReady_ShouldClearQueues()
    {
        // Arrange - Fault recovery/reset
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Faulted,
            NewState = SystemState.Ready,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Once);
    }

    [Fact]
    public void StateChange_ToFaulted_ShouldClearQueues()
    {
        // Arrange
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Running,
            NewState = SystemState.Faulted,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Once);
    }

    [Fact]
    public void StateChange_FromReadyToRunning_ShouldNotClearQueues()
    {
        // Arrange - Normal start, queues should NOT be cleared
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Ready,
            NewState = SystemState.Running,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Never);
    }

    [Fact]
    public void StateChange_FromRunningToPaused_ShouldNotClearQueues()
    {
        // Arrange - Pause, queues should NOT be cleared
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Running,
            NewState = SystemState.Paused,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Never);
    }

    [Fact]
    public void StateChange_FromPausedToRunning_ShouldNotClearQueues()
    {
        // Arrange - Resume, queues should NOT be cleared
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Paused,
            NewState = SystemState.Running,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Never);
    }

    [Fact]
    public void StateChange_FromBootingToReady_ShouldNotClearQueues()
    {
        // Arrange - Boot complete, queues should NOT be cleared
        var eventArgs = new StateChangeEventArgs
        {
            OldState = SystemState.Booting,
            NewState = SystemState.Ready,
            ChangedAt = _testTime
        };

        // Act
        _mockStateManager.Raise(m => m.StateChanged += null, _mockStateManager.Object, eventArgs);

        // Assert
        _mockQueueManager.Verify(q => q.ClearAllQueues(), Times.Never);
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromStateChangedEvent()
    {
        // Arrange - Orchestrator already created and subscribed
        
        // Act
        _orchestrator.Dispose();

        // Assert - Verify that StateChanged event was unsubscribed
        _mockStateManager.VerifyRemove(m => m.StateChanged -= It.IsAny<EventHandler<StateChangeEventArgs>>(), Times.Once);
    }

    public void Dispose()
    {
        _orchestrator?.Dispose();
    }
}
