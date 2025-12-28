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

    #region ReplaceTasksInPlace 测试（PR: 修复上游乱序响应导致的包裹张冠李戴问题）

    /// <summary>
    /// 测试1：基本替换场景 - 保持队列顺序
    /// </summary>
    [Fact]
    public void ReplaceTasksInPlace_BasicReplacement_ShouldPreserveQueueOrder()
    {
        // Arrange
        var manager = new PositionIndexQueueManager(
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
    /// 测试2：上游乱序响应场景 - P2响应先到，P1后到
    /// </summary>
    [Fact]
    public void ReplaceTasksInPlace_OutOfOrderUpstreamResponse_ShouldMaintainCorrectOrder()
    {
        // Arrange
        var manager = new PositionIndexQueueManager(
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
    /// 测试3：幽灵任务清理 - 新路径比旧路径短
    /// </summary>
    [Fact]
    public void ReplaceTasksInPlace_ShorterPath_ShouldCleanupGhostTasks()
    {
        // Arrange
        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        // 初始路径：Position 0→5（异常口999，6个Position）
        var parcelId = 2001L;
        for (int pos = 0; pos <= 5; pos++)
        {
            var task = CreateTask(parcelId, pos, DiverterDirection.Straight);
            manager.EnqueueTask(pos, task);
        }

        // Act: 上游返回新格口，新路径只到Position 2（Position 0→2，3个Position）
        var newTasks = new List<PositionQueueItem>
        {
            CreateTask(parcelId, 0, DiverterDirection.Straight),
            CreateTask(parcelId, 1, DiverterDirection.Straight),
            CreateTask(parcelId, 2, DiverterDirection.Left) // Position 2左转落格
        };

        var result = manager.ReplaceTasksInPlace(parcelId, newTasks);

        // Assert: 验证替换和清理结果
        Assert.True(result.IsFullySuccessful);
        Assert.Equal(3, result.ReplacedCount); // 替换了Position 0、1、2
        Assert.Equal(3, result.RemovedCount); // 清理了Position 3、4、5的幽灵任务
        Assert.Equal(new List<int> { 0, 1, 2 }, result.ReplacedPositions);
        Assert.Equal(new List<int> { 3, 4, 5 }, result.RemovedPositions);

        // 验证Position 3、4、5的队列中没有该包裹的任务（幽灵任务已清理）
        Assert.Null(manager.PeekTask(3)); // Position 3队列为空
        Assert.Null(manager.PeekTask(4)); // Position 4队列为空
        Assert.Null(manager.PeekTask(5)); // Position 5队列为空
    }

    /// <summary>
    /// 测试4：部分成功场景 - 部分任务已出队执行
    /// </summary>
    [Fact]
    public void ReplaceTasksInPlace_PartialSuccess_SomeTasksAlreadyDequeued()
    {
        // Arrange
        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        var parcelId = 3001L;

        // 在Position 1、2、3入队任务
        manager.EnqueueTask(1, CreateTask(parcelId, 1, DiverterDirection.Straight));
        manager.EnqueueTask(2, CreateTask(parcelId, 2, DiverterDirection.Straight));
        manager.EnqueueTask(3, CreateTask(parcelId, 3, DiverterDirection.Straight));

        // 模拟Position 1的任务已经被出队执行了
        var dequeuedTask = manager.DequeueTask(1);
        Assert.NotNull(dequeuedTask);

        // Act: 尝试替换所有3个Position的任务
        var newTasks = new List<PositionQueueItem>
        {
            CreateTask(parcelId, 1, DiverterDirection.Left),
            CreateTask(parcelId, 2, DiverterDirection.Right),
            CreateTask(parcelId, 3, DiverterDirection.Straight)
        };

        var result = manager.ReplaceTasksInPlace(parcelId, newTasks);

        // Assert: 部分成功
        Assert.False(result.IsFullySuccessful);
        Assert.True(result.IsPartiallySuccessful);
        Assert.Equal(2, result.ReplacedCount); // 只替换了Position 2、3
        Assert.Single(result.NotFoundPositions); // Position 1找不到
        Assert.Contains(1, result.NotFoundPositions);
        Assert.Equal(new List<int> { 2, 3 }, result.ReplacedPositions);
    }

    /// <summary>
    /// 测试5：并发替换 - 多个包裹同时被替换
    /// </summary>
    [Fact]
    public async Task ReplaceTasksInPlace_ConcurrentReplacement_ShouldHandleCorrectly()
    {
        // Arrange
        var manager = new PositionIndexQueueManager(
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

        // 验证队列中的任务都已正确替换（顺序可能因并发而变化，但所有任务都应存在）
        var task1 = manager.DequeueTask(1);
        var task2 = manager.DequeueTask(1);
        var task3 = manager.DequeueTask(1);

        var parcelIds = new[] { task1!.ParcelId, task2!.ParcelId, task3!.ParcelId };
        Assert.Contains(4001L, parcelIds);
        Assert.Contains(4002L, parcelIds);
        Assert.Contains(4003L, parcelIds);
    }

    /// <summary>
    /// 测试6：幽灵任务清理不影响其他包裹
    /// </summary>
    [Fact]
    public void ReplaceTasksInPlace_GhostTaskCleanup_ShouldNotAffectOtherParcels()
    {
        // Arrange
        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object);

        // Position 3队列中有2个包裹的任务：P5001、P5002
        manager.EnqueueTask(3, CreateTask(5001, 3, DiverterDirection.Straight));
        manager.EnqueueTask(3, CreateTask(5002, 3, DiverterDirection.Right));

        // Act: 替换P5001，新路径不包含Position 3（触发幽灵任务清理）
        var newTasks = new List<PositionQueueItem>
        {
            CreateTask(5001, 1, DiverterDirection.Left),
            CreateTask(5001, 2, DiverterDirection.Straight)
            // Position 3不在新路径中
        };

        var result = manager.ReplaceTasksInPlace(5001, newTasks);

        // Assert: P5001的Position 3任务被清理
        Assert.Equal(1, result.RemovedCount);
        Assert.Contains(3, result.RemovedPositions);

        // 验证P5002的任务仍在Position 3队列中（不受影响）
        var remainingTask = manager.PeekTask(3);
        Assert.NotNull(remainingTask);
        Assert.Equal(5002L, remainingTask.ParcelId);
        Assert.Equal(DiverterDirection.Right, remainingTask.DiverterAction);
    }

    #endregion

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
