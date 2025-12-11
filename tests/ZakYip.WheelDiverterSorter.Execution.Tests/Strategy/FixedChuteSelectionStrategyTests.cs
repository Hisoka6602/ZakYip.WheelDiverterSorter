using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Moq;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;
using ZakYip.WheelDiverterSorter.Execution.Strategy;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Strategy;

/// <summary>
/// 固定格口选择策略单元测试
/// </summary>
public class FixedChuteSelectionStrategyTests
{
    private readonly Mock<ILogger<FixedChuteSelectionStrategy>> _mockLogger;
    private readonly FixedChuteSelectionStrategy _strategy;

    public FixedChuteSelectionStrategyTests()
    {
        _mockLogger = new Mock<ILogger<FixedChuteSelectionStrategy>>();
        _strategy = new FixedChuteSelectionStrategy(_mockLogger.Object);
    }

    [Fact]
    public async Task SelectChuteAsync_WithValidFixedChute_ReturnsSuccess()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.FixedChute,
            ExceptionChuteId = 999,
            FixedChuteId = 5
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.TargetChuteId);
        Assert.False(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_WithOverloadForced_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.FixedChute,
            ExceptionChuteId = 999,
            FixedChuteId = 5,
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
    public async Task SelectChuteAsync_WithNullFixedChuteId_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.FixedChute,
            ExceptionChuteId = 999,
            FixedChuteId = null
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
        Assert.Contains("固定格口", result.ExceptionReason);
    }

    [Fact]
    public async Task SelectChuteAsync_WithZeroFixedChuteId_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.FixedChute,
            ExceptionChuteId = 999,
            FixedChuteId = 0
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_WithNegativeFixedChuteId_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.FixedChute,
            ExceptionChuteId = 999,
            FixedChuteId = -1
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
    }
}
