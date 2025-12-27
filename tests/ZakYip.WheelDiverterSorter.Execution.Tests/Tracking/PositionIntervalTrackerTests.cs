using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution.Tracking;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Tracking;

/// <summary>
/// Position间隔追踪器测试
/// </summary>
public class PositionIntervalTrackerTests
{
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<ILogger<PositionIntervalTracker>> _mockLogger;
    private readonly PositionIntervalTrackerOptions _options;
    private readonly PositionIntervalTracker _tracker;

    public PositionIntervalTrackerTests()
    {
        _mockClock = new Mock<ISystemClock>();
        _mockLogger = new Mock<ILogger<PositionIntervalTracker>>();
        _options = new PositionIntervalTrackerOptions
        {
            WindowSize = 10,
            TimeoutMultiplier = 3.0,
            MinSamplesForThreshold = 3,
            MaxReasonableIntervalMs = 60000,
            LostDetectionMultiplier = 1.5
        };

        _tracker = new PositionIntervalTracker(
            _mockClock.Object,
            _mockLogger.Object,
            Options.Create(_options));
    }

    /// <summary>
    /// 测试：包裹从入口(position 0)到第一个摆轮(position 1)的间隔应该被记录
    /// </summary>
    [Fact]
    public void RecordParcelPosition_ShouldRecordIntervalFromEntryToPosition1()
    {
        // Arrange
        long parcelId = 1001;
        var entryTime = new DateTime(2025, 12, 14, 10, 0, 0);
        var position1Time = new DateTime(2025, 12, 14, 10, 0, 5); // 5 seconds later
        
        _mockClock.Setup(c => c.LocalNow).Returns(entryTime);

        // Act: 记录包裹在入口位置(position 0)
        _tracker.RecordParcelPosition(parcelId, 0, entryTime);
        
        // Act: 记录包裹到达第一个摆轮(position 1)
        _mockClock.Setup(c => c.LocalNow).Returns(position1Time);
        _tracker.RecordParcelPosition(parcelId, 1, position1Time);

        // Assert: position 1应该有间隔数据
        var stats = _tracker.GetStatistics(1);
        Assert.NotNull(stats);
        Assert.Equal(1, stats.Value.PositionIndex);
        Assert.Equal(5000.0, stats.Value.MedianIntervalMs); // 5秒 = 5000毫秒
        Assert.Equal(1, stats.Value.SampleCount);
    }

    /// <summary>
    /// 测试：包裹从position 1到position 2的间隔应该被记录
    /// </summary>
    [Fact]
    public void RecordParcelPosition_ShouldRecordIntervalFromPosition1ToPosition2()
    {
        // Arrange
        long parcelId = 1002;
        var entryTime = new DateTime(2025, 12, 14, 10, 0, 0);
        var position1Time = new DateTime(2025, 12, 14, 10, 0, 5);
        var position2Time = new DateTime(2025, 12, 14, 10, 0, 11); // 6 seconds after position 1

        // Act: 模拟包裹经过所有位置
        _tracker.RecordParcelPosition(parcelId, 0, entryTime);
        _tracker.RecordParcelPosition(parcelId, 1, position1Time);
        _tracker.RecordParcelPosition(parcelId, 2, position2Time);

        // Assert: position 2应该有间隔数据
        var stats = _tracker.GetStatistics(2);
        Assert.NotNull(stats);
        Assert.Equal(2, stats.Value.PositionIndex);
        Assert.Equal(6000.0, stats.Value.MedianIntervalMs); // 6秒 = 6000毫秒
        Assert.Equal(1, stats.Value.SampleCount);
    }

    /// <summary>
    /// 测试：多个包裹应该正确记录各个位置的间隔
    /// </summary>
    [Fact]
    public void RecordParcelPosition_MultipleParcelsShouldRecordAllPositionIntervals()
    {
        // Arrange: 模拟3个包裹经过2个摆轮(position 1和2)
        var baseTime = new DateTime(2025, 12, 14, 10, 0, 0);
        
        // Parcel 1: entry(0s) → pos1(5s) → pos2(11s)
        _tracker.RecordParcelPosition(1001, 0, baseTime);
        _tracker.RecordParcelPosition(1001, 1, baseTime.AddSeconds(5));
        _tracker.RecordParcelPosition(1001, 2, baseTime.AddSeconds(11));
        
        // Parcel 2: entry(6s) → pos1(11s) → pos2(17s)
        _tracker.RecordParcelPosition(1002, 0, baseTime.AddSeconds(6));
        _tracker.RecordParcelPosition(1002, 1, baseTime.AddSeconds(11));
        _tracker.RecordParcelPosition(1002, 2, baseTime.AddSeconds(17));
        
        // Parcel 3: entry(12s) → pos1(17s) → pos2(23s)
        _tracker.RecordParcelPosition(1003, 0, baseTime.AddSeconds(12));
        _tracker.RecordParcelPosition(1003, 1, baseTime.AddSeconds(17));
        _tracker.RecordParcelPosition(1003, 2, baseTime.AddSeconds(23));

        // Assert: position 1应该有3个样本
        var stats1 = _tracker.GetStatistics(1);
        Assert.NotNull(stats1);
        Assert.Equal(1, stats1.Value.PositionIndex);
        Assert.Equal(3, stats1.Value.SampleCount);
        Assert.Equal(5000.0, stats1.Value.MedianIntervalMs); // 中位数：5秒

        // Assert: position 2应该有3个样本
        var stats2 = _tracker.GetStatistics(2);
        Assert.NotNull(stats2);
        Assert.Equal(2, stats2.Value.PositionIndex);
        Assert.Equal(3, stats2.Value.SampleCount);
        Assert.Equal(6000.0, stats2.Value.MedianIntervalMs); // 中位数：6秒
    }

    /// <summary>
    /// 测试：GetAllStatistics应该返回所有position的数据（包括position 1）
    /// </summary>
    [Fact]
    public void GetAllStatistics_ShouldIncludePosition1()
    {
        // Arrange: 创建2个摆轮的场景
        var baseTime = new DateTime(2025, 12, 14, 10, 0, 0);
        
        // 一个包裹经过2个摆轮
        _tracker.RecordParcelPosition(1001, 0, baseTime);
        _tracker.RecordParcelPosition(1001, 1, baseTime.AddSeconds(5));
        _tracker.RecordParcelPosition(1001, 2, baseTime.AddSeconds(11));

        // Act: 获取所有统计数据
        var allStats = _tracker.GetAllStatistics();

        // Assert: 应该包含position 1和position 2的数据
        Assert.Equal(2, allStats.Count);
        Assert.Contains(allStats, s => s.PositionIndex == 1);
        Assert.Contains(allStats, s => s.PositionIndex == 2);
        
        // 验证position 1有数据
        var pos1 = allStats.First(s => s.PositionIndex == 1);
        Assert.NotNull(pos1.MedianIntervalMs);
        Assert.True(pos1.MedianIntervalMs > 0);
    }

    /// <summary>
    /// 测试：position 0（入口位置）不应该有间隔数据
    /// </summary>
    [Fact]
    public void RecordParcelPosition_Position0ShouldNotHaveIntervalData()
    {
        // Arrange
        var entryTime = new DateTime(2025, 12, 14, 10, 0, 0);
        
        // Act: 只记录入口位置
        _tracker.RecordParcelPosition(1001, 0, entryTime);

        // Assert: position 0不应该有间隔统计
        var stats = _tracker.GetStatistics(0);
        Assert.Null(stats);
        
        // GetAllStatistics也不应该包含position 0
        var allStats = _tracker.GetAllStatistics();
        Assert.DoesNotContain(allStats, s => s.PositionIndex == 0);
    }

    /// <summary>
    /// 测试：缺少前一个位置时间的包裹应该跳过间隔计算
    /// </summary>
    [Fact]
    public void RecordParcelPosition_WithoutPreviousPosition_ShouldSkipIntervalCalculation()
    {
        // Arrange: 直接记录position 1，没有先记录position 0
        var position1Time = new DateTime(2025, 12, 14, 10, 0, 5);

        // Act: 直接记录position 1（缺少position 0的时间）
        _tracker.RecordParcelPosition(1001, 1, position1Time);

        // Assert: position 1不应该有间隔数据（因为缺少position 0）
        var stats = _tracker.GetStatistics(1);
        Assert.Null(stats); // 没有任何间隔数据
    }

    /// <summary>
    /// 测试：无效的 ParcelId（小于等于0）应该被拒绝
    /// </summary>
    [Fact]
    public void RecordParcelPosition_WithInvalidParcelId_ShouldBeRejected()
    {
        // Arrange
        var now = new DateTime(2025, 12, 27, 10, 0, 0);
        _mockClock.Setup(c => c.LocalNow).Returns(now);

        // Act & Assert: 尝试记录 ParcelId = 0
        _tracker.RecordParcelPosition(0, 0, now);
        _tracker.RecordParcelPosition(0, 1, now.AddSeconds(5));

        // 验证 ParcelId = 0 没有被追踪
        var stats = _tracker.GetStatistics(1);
        Assert.Null(stats); // 不应该有任何间隔数据

        // Act & Assert: 尝试记录 ParcelId = -1
        _tracker.RecordParcelPosition(-1, 0, now);
        _tracker.RecordParcelPosition(-1, 1, now.AddSeconds(5));

        // 验证 ParcelId = -1 没有被追踪
        stats = _tracker.GetStatistics(1);
        Assert.Null(stats); // 不应该有任何间隔数据

        // 验证警告日志被记录（ParcelId <= 0）
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("拒绝记录无效的包裹ID")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(4)); // 预期有4次警告（对 ParcelId=0 和 ParcelId=-1 各调用2次）
    }

    /// <summary>
    /// 测试：包裹记录不应该被自动清理，即使有大量包裹被追踪
    /// </summary>
    /// <remarks>
    /// 修复问题：自动清理会移除仍在运输中的包裹记录，导致后续间隔计算失败
    /// </remarks>
    [Fact]
    public void RecordParcelPosition_WithManyParcels_ShouldNotAutoCleanupInTransitParcels()
    {
        // Arrange: 模拟大量包裹（超过旧的清理阈值）
        var baseTime = new DateTime(2025, 12, 27, 10, 0, 0);
        
        // 创建 1500 个包裹的记录（超过旧的 1000 阈值）
        for (int i = 1; i <= 1500; i++)
        {
            long parcelId = 1000000 + i;
            var entryTime = baseTime.AddSeconds(i * 0.1);
            
            // 每个包裹只记录入口位置（position 0），还未到达下一位置
            _tracker.RecordParcelPosition(parcelId, 0, entryTime);
        }
        
        // Act: 现在第一个包裹到达 position 1
        // 第一个包裹在 baseTime + 0.1s 进入系统（position 0）
        // 现在在 baseTime + 5.1s 到达 position 1
        // 间隔应该是 5秒 = 5000ms
        var firstParcelArrivalTime = baseTime.AddSeconds(5.1);
        _tracker.RecordParcelPosition(1000001, 1, firstParcelArrivalTime);
        
        // Assert: 第一个包裹的间隔应该被正确计算（5秒 = 5000ms）
        // 如果自动清理误删了 position 0 的记录，这里会失败
        var stats = _tracker.GetStatistics(1);
        Assert.NotNull(stats);
        Assert.Equal(1, stats.Value.PositionIndex);
        Assert.Equal(5000.0, stats.Value.MedianIntervalMs!.Value, precision: 1); // 允许1ms误差
        Assert.Equal(1, stats.Value.SampleCount);
        
        // 验证没有清理警告日志（自动清理已禁用）
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("清理了")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never()); // 不应该有清理日志
    }
}
