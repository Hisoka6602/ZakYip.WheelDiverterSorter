using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Execution.Tracking;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Queues;

/// <summary>
/// Position-Index 队列管理器测试
/// </summary>
public class PositionIndexQueueManagerTests
{
    private readonly Mock<ILogger<PositionIndexQueueManager>> _mockLogger;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<IPositionIntervalTracker> _mockIntervalTracker;

    public PositionIndexQueueManagerTests()
    {
        _mockLogger = new Mock<ILogger<PositionIndexQueueManager>>();
        _mockClock = new Mock<ISystemClock>();
        _mockIntervalTracker = new Mock<IPositionIntervalTracker>();
        
        // 使用固定时间确保测试可重复
        _mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2025, 1, 1, 12, 0, 0));
    }

    /// <summary>
    /// 测试：入队和出队基本功能（不包含丢失检测字段，已废弃）
    /// </summary>
    [Fact]
    public void EnqueueTask_BasicEnqueueDequeue_ShouldWork()
    {
        // Arrange
        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        var task = new PositionQueueItem
        {
            ParcelId = 1001,
            DiverterId = 1,
            PositionIndex = 1,
            DiverterAction = DiverterDirection.Left,
            ExpectedArrivalTime = _mockClock.Object.LocalNow.AddSeconds(5),
            TimeoutThresholdMs = 2000,
            FallbackAction = DiverterDirection.Straight,
            CreatedAt = _mockClock.Object.LocalNow
        };

        // Act
        manager.EnqueueTask(1, task);
        var dequeuedTask = manager.DequeueTask(1);

        // Assert
        Assert.NotNull(dequeuedTask);
        Assert.Equal(1001, dequeuedTask.ParcelId);
        Assert.Equal(DiverterDirection.Left, dequeuedTask.DiverterAction);
    }

    /// <summary>
    /// 测试：包裹丢失检测功能已废弃（旧测试保留作为文档）
    /// </summary>
    [Fact]
    public void EnqueueTask_LostDetectionFeature_Deprecated()
    {
        // 注意：包裹丢失检测功能已从系统中移除
        // 现在使用 IO 触发时的同步检测（基于下一个包裹的 EarliestDequeueTime）
        // 不再有后台丢失监控服务和相关配置
        
        // Arrange
        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);
        
        var expectedArrival = _mockClock.Object.LocalNow.AddSeconds(5);
        var task = new PositionQueueItem
        {
            ParcelId = 1002,
            DiverterId = 1,
            PositionIndex = 1,
            DiverterAction = DiverterDirection.Right,
            ExpectedArrivalTime = expectedArrival,
            TimeoutThresholdMs = 2000,  // 来自输送线配置
            FallbackAction = DiverterDirection.Straight,
            CreatedAt = _mockClock.Object.LocalNow
        };

        // Act
        manager.EnqueueTask(1, task);
        var dequeuedTask = manager.DequeueTask(1);

        // Assert - 功能已废弃，测试保留作为文档
        // 注意：包裹丢失检测功能已从系统中移除
        // 现在使用 IO 触发时的同步检测（基于下一个包裹的 EarliestDequeueTime）
        // 不再有后台丢失监控服务和相关配置
        Assert.NotNull(dequeuedTask);
        // LostDetection fields 已废弃，不再设置
    }
}
