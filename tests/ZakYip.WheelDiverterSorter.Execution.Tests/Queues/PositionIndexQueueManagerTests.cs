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
    private readonly Mock<IParcelLossDetectionConfigurationRepository> _mockConfigRepository;

    public PositionIndexQueueManagerTests()
    {
        _mockLogger = new Mock<ILogger<PositionIndexQueueManager>>();
        _mockClock = new Mock<ISystemClock>();
        _mockIntervalTracker = new Mock<IPositionIntervalTracker>();
        _mockConfigRepository = new Mock<IParcelLossDetectionConfigurationRepository>();
        
        // 使用固定时间确保测试可重复
        _mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2025, 1, 1, 12, 0, 0));
    }

    /// <summary>
    /// 测试：当 IsEnabled = false 时，入队时应清除丢失检测字段
    /// </summary>
    [Fact]
    public void EnqueueTask_WhenIsEnabledIsFalse_ShouldClearLostDetectionFields()
    {
        // Arrange
        var config = new ParcelLossDetectionConfiguration
        {
            IsEnabled = false  // 禁用检测
        };
        
        _mockConfigRepository
            .Setup(r => r.Get())
            .Returns(config);

        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object,
            _mockConfigRepository.Object);

        var task = new PositionQueueItem
        {
            ParcelId = 1001,
            DiverterId = 1,
            PositionIndex = 1,
            DiverterAction = DiverterDirection.Left,
            ExpectedArrivalTime = _mockClock.Object.LocalNow.AddSeconds(5),
            TimeoutThresholdMs = 2000,
            FallbackAction = DiverterDirection.Straight,
            CreatedAt = _mockClock.Object.LocalNow,
            // 初始状态有值
            LostDetectionTimeoutMs = 3000,
            LostDetectionDeadline = _mockClock.Object.LocalNow.AddSeconds(8)
        };

        // Act
        manager.EnqueueTask(1, task);
        var dequeuedTask = manager.DequeueTask(1);

        // Assert
        Assert.NotNull(dequeuedTask);
        Assert.Null(dequeuedTask.LostDetectionTimeoutMs);
        Assert.Null(dequeuedTask.LostDetectionDeadline);
    }

    /// <summary>
    /// 测试：当 IsEnabled = true 时，入队时应保留或计算丢失检测字段（基于配置）
    /// </summary>
    [Fact]
    public void EnqueueTask_WhenIsEnabledIsTrue_ShouldApplyLostDetectionFields()
    {
        // Arrange
        var config = new ParcelLossDetectionConfiguration
        {
            IsEnabled = true,  // 启用检测
            LostDetectionMultiplier = 1.5  // 此系数已不再用于计算，保留为向后兼容
        };
        
        _mockConfigRepository
            .Setup(r => r.Get())
            .Returns(config);
        
        // 注意：不再模拟中位数阈值计算，因为现在使用 TimeoutThresholdMs × 1.5
        // 中位数仅用于观测日志，不影响实际判断逻辑

        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object,
            _mockConfigRepository.Object);

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

        // Assert
        Assert.NotNull(dequeuedTask);
        Assert.NotNull(dequeuedTask.LostDetectionTimeoutMs);
        Assert.NotNull(dequeuedTask.LostDetectionDeadline);
        // 丢失检测阈值 = TimeoutThresholdMs × 1.5 = 2000 × 1.5 = 3000
        Assert.Equal(3000, dequeuedTask.LostDetectionTimeoutMs);
        Assert.Equal(expectedArrival.AddMilliseconds(3000), dequeuedTask.LostDetectionDeadline);
    }

    /// <summary>
    /// 测试：当配置仓储为 null 时，默认启用检测（安全策略）
    /// </summary>
    [Fact]
    public void EnqueueTask_WhenConfigRepositoryIsNull_ShouldDefaultToEnabled()
    {
        // Arrange
        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object,
            configRepository: null);  // 没有注入配置仓储

        var task = new PositionQueueItem
        {
            ParcelId = 1003,
            DiverterId = 1,
            PositionIndex = 1,
            DiverterAction = DiverterDirection.Straight,
            ExpectedArrivalTime = _mockClock.Object.LocalNow.AddSeconds(5),
            TimeoutThresholdMs = 2000,
            FallbackAction = DiverterDirection.Straight,
            CreatedAt = _mockClock.Object.LocalNow
        };

        // Act
        manager.EnqueueTask(1, task);
        var dequeuedTask = manager.DequeueTask(1);

        // Assert
        // 默认应该启用检测，所以应该应用静态回退阈值（TimeoutThresholdMs × 1.5）
        Assert.NotNull(dequeuedTask);
        Assert.NotNull(dequeuedTask.LostDetectionTimeoutMs);
        Assert.NotNull(dequeuedTask.LostDetectionDeadline);
        Assert.Equal(3000, dequeuedTask.LostDetectionTimeoutMs);  // 2000 × 1.5
    }

    /// <summary>
    /// 测试：当配置读取失败时，默认启用检测（安全策略）
    /// </summary>
    [Fact]
    public void EnqueueTask_WhenConfigReadFails_ShouldDefaultToEnabled()
    {
        // Arrange
        _mockConfigRepository
            .Setup(r => r.Get())
            .Throws(new InvalidOperationException("Database connection failed"));

        var manager = new PositionIndexQueueManager(
            _mockLogger.Object,
            _mockClock.Object,
            _mockIntervalTracker.Object,
            _mockConfigRepository.Object);

        var task = new PositionQueueItem
        {
            ParcelId = 1004,
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
        // 配置读取失败，应该回退到默认行为（启用检测）
        Assert.NotNull(dequeuedTask);
        Assert.NotNull(dequeuedTask.LostDetectionTimeoutMs);
        Assert.NotNull(dequeuedTask.LostDetectionDeadline);
    }
}
