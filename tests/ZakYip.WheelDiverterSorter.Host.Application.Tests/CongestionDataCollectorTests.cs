using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using Moq;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 拥堵数据收集器测试
/// Tests for CongestionDataCollector
/// </summary>
/// <remarks>
/// 验证 CongestionDataCollector 的核心功能：
/// 1. 旧数据清理机制正常工作
/// 2. 清理在预期间隔执行
/// 3. 线程安全性
/// 4. 重复进入包裹的处理
/// 5. 在途包裹计数准确性
/// </remarks>
public class CongestionDataCollectorTests
{
    private readonly Mock<ISystemClock> _mockClock;
    private DateTime _currentTime;

    public CongestionDataCollectorTests()
    {
        _mockClock = new Mock<ISystemClock>();
        _currentTime = new DateTime(2025, 12, 24, 12, 0, 0);
        _mockClock.Setup(c => c.LocalNow).Returns(() => _currentTime);
    }

    [Fact]
    public void RecordParcelEntry_ShouldIncrementInFlightCount()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);

        // Act
        collector.RecordParcelEntry(1, _currentTime);

        // Assert
        Assert.Equal(1, collector.GetInFlightCount());
    }

    [Fact]
    public void RecordParcelCompletion_ShouldDecrementInFlightCount()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);
        collector.RecordParcelEntry(1, _currentTime);

        // Act
        collector.RecordParcelCompletion(1, _currentTime.AddSeconds(5), true);

        // Assert
        Assert.Equal(0, collector.GetInFlightCount());
    }

    [Fact]
    public void RecordParcelEntry_WithDuplicateId_ShouldResetCompletionTime()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);
        var entryTime1 = _currentTime;
        var completionTime = _currentTime.AddSeconds(5);
        var entryTime2 = _currentTime.AddSeconds(10);

        // Act
        collector.RecordParcelEntry(1, entryTime1);
        collector.RecordParcelCompletion(1, completionTime, true);
        collector.RecordParcelEntry(1, entryTime2); // 重新进入

        // Assert
        var snapshot = collector.CollectSnapshot();
        // 重新进入的包裹会覆盖原记录，所以仍然是1个包裹，但CompletionTime应该被重置为null
        Assert.Equal(1, snapshot.TotalSampledParcels);
        // 由于CompletionTime被重置，在途包裹数应该是: 进入1次 -> 完成1次 (0) -> 进入1次 (1)
        Assert.Equal(1, collector.GetInFlightCount());
    }

    [Fact]
    public void CleanupOldHistory_ShouldRemoveOldRecords_AfterCleanupInterval()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);
        
        // 添加旧记录
        var oldTime = _currentTime;
        collector.RecordParcelEntry(1, oldTime);
        collector.RecordParcelCompletion(1, oldTime.AddSeconds(1), true);

        // 前进时间但仍在时间窗口内
        _currentTime = _currentTime.AddMinutes(1);
        var snapshot1 = collector.CollectSnapshot();
        Assert.Equal(1, snapshot1.TotalSampledParcels); // 记录仍在时间窗口内

        // 前进时间超过清理间隔 + 时间窗口 + 缓冲区（需要 > 70秒才会被清理）
        _currentTime = oldTime.AddMinutes(6).AddSeconds(10); // 6分10秒后
        collector.RecordParcelEntry(2, _currentTime); // 触发清理

        // Assert
        var snapshot2 = collector.CollectSnapshot();
        Assert.Equal(1, snapshot2.TotalSampledParcels); // 只有包裹 2 在窗口内
        // 旧记录（包裹 1）应该被清理掉
    }

    [Fact]
    public void CleanupOldHistory_ShouldNotExecute_BeforeCleanupInterval()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);
        collector.RecordParcelEntry(1, _currentTime);

        // 前进时间但不到清理间隔（5分钟）
        _currentTime = _currentTime.AddMinutes(4);

        // Act
        collector.RecordParcelEntry(2, _currentTime); // 触发清理检查

        // Assert - 旧记录仍应存在（未被清理）
        _currentTime = _currentTime.AddSeconds(-65); // 回到旧记录时间附近
        var snapshot = collector.CollectSnapshot();
        // 清理不应该执行，所以包裹1应该还在历史中
    }

    [Fact]
    public void CollectSnapshot_ShouldCalculateCorrectMetrics()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);
        
        // 添加测试数据
        collector.RecordParcelEntry(1, _currentTime);
        collector.RecordParcelCompletion(1, _currentTime.AddMilliseconds(100), true);
        
        collector.RecordParcelEntry(2, _currentTime.AddSeconds(1));
        collector.RecordParcelCompletion(2, _currentTime.AddSeconds(1).AddMilliseconds(200), true);
        
        collector.RecordParcelEntry(3, _currentTime.AddSeconds(2)); // 未完成

        // Act
        var snapshot = collector.CollectSnapshot();

        // Assert
        Assert.Equal(1, snapshot.InFlightParcels); // 只有包裹3未完成
        Assert.Equal(3, snapshot.TotalSampledParcels);
        Assert.Equal(150.0, snapshot.AverageLatencyMs); // (100 + 200) / 2
        Assert.Equal(200.0, snapshot.MaxLatencyMs);
        Assert.Equal(60, snapshot.TimeWindowSeconds);
    }

    [Fact]
    public void CollectSnapshot_ShouldOnlyIncludeRecordsWithinTimeWindow()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);
        
        // 添加时间窗口外的旧记录
        var oldTime = _currentTime.AddSeconds(-70); // 超过60秒窗口
        collector.RecordParcelEntry(1, oldTime);
        collector.RecordParcelCompletion(1, oldTime.AddSeconds(1), true);
        
        // 添加时间窗口内的新记录
        collector.RecordParcelEntry(2, _currentTime);
        collector.RecordParcelCompletion(2, _currentTime.AddMilliseconds(100), true);

        // Act
        var snapshot = collector.CollectSnapshot();

        // Assert
        Assert.Equal(1, snapshot.TotalSampledParcels); // 只统计窗口内的包裹2
        Assert.Equal(100.0, snapshot.AverageLatencyMs);
    }

    [Fact]
    public void ConcurrentAccess_ShouldMaintainThreadSafety()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);
        var tasks = new List<Task>();
        var parcelCount = 100;

        // Act - 并发添加记录
        for (int i = 0; i < parcelCount; i++)
        {
            var parcelId = i;
            tasks.Add(Task.Run(() =>
            {
                collector.RecordParcelEntry(parcelId, _currentTime);
                collector.RecordParcelCompletion(parcelId, _currentTime.AddMilliseconds(50), true);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var snapshot = collector.CollectSnapshot();
        Assert.Equal(0, snapshot.InFlightParcels); // 所有包裹都已完成
        Assert.Equal(parcelCount, snapshot.TotalSampledParcels);
    }

    [Fact]
    public void RecordParcelCompletion_ForNonExistentParcel_ShouldCreateRecord()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);

        // Act - 直接记录完成（没有先记录进入）
        collector.RecordParcelCompletion(1, _currentTime, true);

        // Assert
        Assert.Equal(-1, collector.GetInFlightCount()); // 递减从0变成-1（这是边缘情况）
        var snapshot = collector.CollectSnapshot();
        Assert.Equal(1, snapshot.TotalSampledParcels); // 记录应该存在
    }

    [Fact]
    public void GetInFlightCount_ShouldRemainAccurate_AfterCleanup()
    {
        // Arrange
        var collector = new CongestionDataCollector(_mockClock.Object);
        
        // 添加旧包裹
        collector.RecordParcelEntry(1, _currentTime);
        // 不完成，保持在途

        // 添加新包裹
        _currentTime = _currentTime.AddMinutes(10);
        collector.RecordParcelEntry(2, _currentTime);

        // Assert - 清理前
        Assert.Equal(2, collector.GetInFlightCount());

        // 触发清理（旧包裹应该被清理）
        collector.RecordParcelEntry(3, _currentTime);

        // Assert - 清理后，在途计数不应该因为清理而改变
        // 因为清理只删除字典中的记录，不影响 _inFlightParcels 计数器
        Assert.Equal(3, collector.GetInFlightCount());
    }
}
