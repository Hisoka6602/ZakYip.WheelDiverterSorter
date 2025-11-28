using Moq;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;
using ZakYip.WheelDiverterSorter.Execution.Strategy;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Strategy;

/// <summary>
/// 轮询格口选择策略单元测试
/// </summary>
public class RoundRobinChuteSelectionStrategyTests
{
    private readonly Mock<ILogger<RoundRobinChuteSelectionStrategy>> _mockLogger;
    private readonly RoundRobinChuteSelectionStrategy _strategy;

    public RoundRobinChuteSelectionStrategyTests()
    {
        _mockLogger = new Mock<ILogger<RoundRobinChuteSelectionStrategy>>();
        _strategy = new RoundRobinChuteSelectionStrategy(_mockLogger.Object);
    }

    [Fact]
    public async Task SelectChuteAsync_WithValidAvailableChutes_ReturnsFirstChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = new List<long> { 1, 2, 3 }
        };

        _strategy.ResetIndex();

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.TargetChuteId);
        Assert.False(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_MultipleCalls_CyclesThroughChutes()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = new List<long> { 1, 2, 3 }
        };

        _strategy.ResetIndex();

        // Act & Assert
        var result1 = await _strategy.SelectChuteAsync(context with { ParcelId = 1 }, CancellationToken.None);
        Assert.Equal(1, result1.TargetChuteId);

        var result2 = await _strategy.SelectChuteAsync(context with { ParcelId = 2 }, CancellationToken.None);
        Assert.Equal(2, result2.TargetChuteId);

        var result3 = await _strategy.SelectChuteAsync(context with { ParcelId = 3 }, CancellationToken.None);
        Assert.Equal(3, result3.TargetChuteId);

        // Should cycle back to first
        var result4 = await _strategy.SelectChuteAsync(context with { ParcelId = 4 }, CancellationToken.None);
        Assert.Equal(1, result4.TargetChuteId);
    }

    [Fact]
    public async Task SelectChuteAsync_WithOverloadForced_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = new List<long> { 1, 2, 3 },
            IsOverloadForced = true
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
        Assert.Contains("超载", result.ExceptionReason);
    }

    [Fact]
    public async Task SelectChuteAsync_WithEmptyAvailableChutes_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = new List<long>()
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
        Assert.Contains("没有可用格口", result.ExceptionReason);
    }

    [Fact]
    public async Task SelectChuteAsync_WithNullAvailableChutes_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = null!
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_IndexExceedsAvailableChutes_ResetsToZero()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = new List<long> { 1, 2, 3 }
        };

        // Simulate index being out of bounds by cycling beyond the list
        _strategy.ResetIndex();
        for (int i = 0; i < 5; i++)
        {
            await _strategy.SelectChuteAsync(context with { ParcelId = i }, CancellationToken.None);
        }

        // Act - This should be at index 2 (5 % 3 = 2)
        var result = await _strategy.SelectChuteAsync(context with { ParcelId = 100 }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsException);
        // Should be at a valid chute (1, 2, or 3)
        Assert.Contains(result.TargetChuteId, new[] { 1L, 2L, 3L });
    }

    [Fact]
    public void ResetIndex_ResetsToZero()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = new List<long> { 1, 2, 3 }
        };

        // Advance the index
        _strategy.SelectChuteAsync(context with { ParcelId = 1 }, CancellationToken.None);
        _strategy.SelectChuteAsync(context with { ParcelId = 2 }, CancellationToken.None);

        // Act
        _strategy.ResetIndex();

        // Assert - after reset, next selection should return first chute
        var result = _strategy.SelectChuteAsync(context with { ParcelId = 100 }, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(1, result.TargetChuteId);
    }
}
