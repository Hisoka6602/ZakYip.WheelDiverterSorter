using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Execution.Tracking;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Queues;

/// <summary>
/// Channel-based Position-Index 队列管理器测试
/// </summary>
public class ChannelBasedPositionIndexQueueManagerTests
{
    private readonly Mock<ILogger<ChannelBasedPositionIndexQueueManager>> _mockLogger;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<IPositionIntervalTracker> _mockIntervalTracker;

    public ChannelBasedPositionIndexQueueManagerTests()
    {
        _mockLogger = new Mock<ILogger<ChannelBasedPositionIndexQueueManager>>();
        _mockClock = new Mock<ISystemClock>();
        _mockIntervalTracker = new Mock<IPositionIntervalTracker>();
        
        // 使用固定时间确保测试可重复
        _mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2025, 1, 1, 12, 0, 0));
    }

    /// <summary>
    /// 测试：入队和出队基本功能
    /// </summary>
    [Fact]
    public void EnqueueTask_BasicEnqueueDequeue_ShouldWork()
    {
        // Arrange
        var manager = new ChannelBasedPositionIndexQueueManager(
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
    /// 测试：FIFO 顺序保证
    /// </summary>
    [Fact]
    public void EnqueueTask_MultipleTasks_ShouldMaintainFIFOOrder()
    {
        // Arrange
        var manager = new ChannelBasedPositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        // Act: 入队3个包裹
        manager.EnqueueTask(1, CreateTask(1001, 1, DiverterDirection.Left));
        manager.EnqueueTask(1, CreateTask(1002, 1, DiverterDirection.Right));
        manager.EnqueueTask(1, CreateTask(1003, 1, DiverterDirection.Straight));

        // Assert: 按FIFO顺序出队
        var task1 = manager.DequeueTask(1);
        Assert.Equal(1001L, task1!.ParcelId);

        var task2 = manager.DequeueTask(1);
        Assert.Equal(1002L, task2!.ParcelId);

        var task3 = manager.DequeueTask(1);
        Assert.Equal(1003L, task3!.ParcelId);
    }

    /// <summary>
    /// 测试：同一包裹多次入队应该只保留一个（更新数据）
    /// </summary>
    [Fact]
    public void EnqueueTask_SameParcelTwice_ShouldUpdateData()
    {
        // Arrange
        var manager = new ChannelBasedPositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        // Act: 同一包裹入队两次（模拟任务更新）
        manager.EnqueueTask(1, CreateTask(2001, 1, DiverterDirection.Left));
        manager.EnqueueTask(1, CreateTask(2001, 1, DiverterDirection.Right)); // 更新动作

        // Assert: 只有一个任务，使用最新的数据
        var task = manager.DequeueTask(1);
        Assert.NotNull(task);
        Assert.Equal(2001L, task.ParcelId);
        Assert.Equal(DiverterDirection.Right, task.DiverterAction); // ✅ 已更新

        // 队列应该为空
        var task2 = manager.DequeueTask(1);
        Assert.Null(task2);
    }

    /// <summary>
    /// 测试：基本替换场景 - 保持队列顺序
    /// </summary>
    [Fact]
    public void ReplaceTasksInPlace_BasicReplacement_ShouldPreserveQueueOrder()
    {
        // Arrange
        var manager = new ChannelBasedPositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        // 创建3个包裹的任务，按顺序入队：P1, P2, P3
        var p1Task = CreateTask(parcelId: 1001, positionIndex: 1, action: DiverterDirection.Straight);
        var p2Task = CreateTask(parcelId: 1002, positionIndex: 1, action: DiverterDirection.Straight);
        var p3Task = CreateTask(parcelId: 1003, positionIndex: 1, action: DiverterDirection.Straight);

        manager.EnqueueTask(1, p1Task);
        manager.EnqueueTask(1, p2Task);
        manager.EnqueueTask(1, p3Task);

        // Act: 替换P2的任务（从Straight改为Left）
        var newP2Task = p2Task with { DiverterAction = DiverterDirection.Left };
        var result = manager.ReplaceTasksInPlace(1002, new List<PositionQueueItem> { newP2Task });

        // Assert: 验证替换成功
        Assert.True(result.IsFullySuccessful);
        Assert.Equal(1, result.ReplacedCount);
        Assert.Contains(1, result.ReplacedPositions);

        // 验证队列顺序保持不变：P1, P2(Left), P3
        var task1 = manager.DequeueTask(1);
        Assert.Equal(1001L, task1!.ParcelId);
        Assert.Equal(DiverterDirection.Straight, task1.DiverterAction);

        var task2 = manager.DequeueTask(1);
        Assert.Equal(1002L, task2!.ParcelId);
        Assert.Equal(DiverterDirection.Left, task2.DiverterAction); // ✅ 已替换

        var task3 = manager.DequeueTask(1);
        Assert.Equal(1003L, task3!.ParcelId);
        Assert.Equal(DiverterDirection.Straight, task3.DiverterAction);
    }

    /// <summary>
    /// 测试：上游乱序响应场景 - P2响应先到，P1后到
    /// </summary>
    [Fact]
    public void ReplaceTasksInPlace_OutOfOrderUpstreamResponse_ShouldMaintainCorrectOrder()
    {
        // Arrange
        var manager = new ChannelBasedPositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        // 初始状态：3个包裹按顺序创建，都用异常格口（Straight）
        var p1Task = CreateTask(1001, 1, DiverterDirection.Straight);
        var p2Task = CreateTask(1002, 1, DiverterDirection.Straight);
        var p3Task = CreateTask(1003, 1, DiverterDirection.Straight);

        manager.EnqueueTask(1, p1Task);
        manager.EnqueueTask(1, p2Task);
        manager.EnqueueTask(1, p3Task);

        // Act: 模拟上游乱序响应
        // T1: P2的响应先到（格口6，需要Right）
        var newP2Task = p2Task with { DiverterAction = DiverterDirection.Right };
        var result2 = manager.ReplaceTasksInPlace(1002, new List<PositionQueueItem> { newP2Task });

        // T2: P1的响应后到（格口3，需要Left）
        var newP1Task = p1Task with { DiverterAction = DiverterDirection.Left };
        var result1 = manager.ReplaceTasksInPlace(1001, new List<PositionQueueItem> { newP1Task });

        // Assert: 两次替换都成功
        Assert.True(result2.IsFullySuccessful);
        Assert.True(result1.IsFullySuccessful);

        // 验证队列顺序仍然正确：P1(Left), P2(Right), P3(Straight)
        var task1 = manager.DequeueTask(1);
        Assert.Equal(1001L, task1!.ParcelId);
        Assert.Equal(DiverterDirection.Left, task1.DiverterAction); // ✅ P1保持在第一位

        var task2 = manager.DequeueTask(1);
        Assert.Equal(1002L, task2!.ParcelId);
        Assert.Equal(DiverterDirection.Right, task2.DiverterAction); // ✅ P2保持在第二位

        var task3 = manager.DequeueTask(1);
        Assert.Equal(1003L, task3!.ParcelId);
        Assert.Equal(DiverterDirection.Straight, task3.DiverterAction); // ✅ P3保持在第三位
    }

    /// <summary>
    /// 测试：并发替换 - 多个包裹同时被替换
    /// </summary>
    [Fact]
    public async Task ReplaceTasksInPlace_ConcurrentReplacement_ShouldHandleCorrectly()
    {
        // Arrange
        var manager = new ChannelBasedPositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        // 在Position 1入队3个包裹
        manager.EnqueueTask(1, CreateTask(4001, 1, DiverterDirection.Straight));
        manager.EnqueueTask(1, CreateTask(4002, 1, DiverterDirection.Straight));
        manager.EnqueueTask(1, CreateTask(4003, 1, DiverterDirection.Straight));

        // Act: 并发替换3个包裹
        var tasks = new[]
        {
            Task.Run(() => manager.ReplaceTasksInPlace(4001, new List<PositionQueueItem>
            {
                CreateTask(4001, 1, DiverterDirection.Left)
            })),
            Task.Run(() => manager.ReplaceTasksInPlace(4002, new List<PositionQueueItem>
            {
                CreateTask(4002, 1, DiverterDirection.Right)
            })),
            Task.Run(() => manager.ReplaceTasksInPlace(4003, new List<PositionQueueItem>
            {
                CreateTask(4003, 1, DiverterDirection.Left)
            }))
        };

        var results = await Task.WhenAll(tasks);

        // Assert: 所有替换都成功
        Assert.All(results, r => Assert.True(r.IsFullySuccessful));

        // 验证队列中的任务都已正确替换（顺序保持）
        var task1 = manager.DequeueTask(1);
        var task2 = manager.DequeueTask(1);
        var task3 = manager.DequeueTask(1);

        Assert.Equal(4001L, task1!.ParcelId);
        Assert.Equal(DiverterDirection.Left, task1.DiverterAction);
        
        Assert.Equal(4002L, task2!.ParcelId);
        Assert.Equal(DiverterDirection.Right, task2.DiverterAction);
        
        Assert.Equal(4003L, task3!.ParcelId);
        Assert.Equal(DiverterDirection.Left, task3.DiverterAction);
    }

    /// <summary>
    /// 辅助方法：创建测试任务
    /// </summary>
    private PositionQueueItem CreateTask(long parcelId, int positionIndex, DiverterDirection action)
    {
        return new PositionQueueItem
        {
            ParcelId = parcelId,
            DiverterId = positionIndex,
            PositionIndex = positionIndex,
            DiverterAction = action,
            ExpectedArrivalTime = _mockClock.Object.LocalNow.AddSeconds(5),
            TimeoutThresholdMs = 2000,
            FallbackAction = DiverterDirection.Straight,
            CreatedAt = _mockClock.Object.LocalNow,
            EarliestDequeueTime = _mockClock.Object.LocalNow
        };
    }
}
