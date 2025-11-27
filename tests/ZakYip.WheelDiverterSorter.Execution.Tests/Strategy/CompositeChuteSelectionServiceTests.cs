using Moq;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Execution.Strategy;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Strategy;

/// <summary>
/// 组合格口选择服务单元测试
/// </summary>
public class CompositeChuteSelectionServiceTests : IDisposable
{
    private readonly Mock<ILogger<FixedChuteSelectionStrategy>> _mockFixedLogger;
    private readonly Mock<ILogger<RoundRobinChuteSelectionStrategy>> _mockRoundRobinLogger;
    private readonly Mock<ILogger<FormalChuteSelectionStrategy>> _mockFormalLogger;
    private readonly Mock<ILogger<CompositeChuteSelectionService>> _mockServiceLogger;
    private readonly Mock<IUpstreamRoutingClient> _mockUpstreamClient;
    private readonly Mock<ISystemClock> _mockClock;
    
    private readonly FixedChuteSelectionStrategy _fixedStrategy;
    private readonly RoundRobinChuteSelectionStrategy _roundRobinStrategy;
    private readonly FormalChuteSelectionStrategy _formalStrategy;
    private readonly CompositeChuteSelectionService _service;

    public CompositeChuteSelectionServiceTests()
    {
        _mockFixedLogger = new Mock<ILogger<FixedChuteSelectionStrategy>>();
        _mockRoundRobinLogger = new Mock<ILogger<RoundRobinChuteSelectionStrategy>>();
        _mockFormalLogger = new Mock<ILogger<FormalChuteSelectionStrategy>>();
        _mockServiceLogger = new Mock<ILogger<CompositeChuteSelectionService>>();
        _mockUpstreamClient = new Mock<IUpstreamRoutingClient>();
        _mockClock = new Mock<ISystemClock>();

        var testTime = new DateTimeOffset(2025, 11, 22, 12, 0, 0, TimeSpan.Zero);
        _mockClock.Setup(c => c.LocalNow).Returns(testTime.LocalDateTime);
        _mockUpstreamClient.Setup(c => c.IsConnected).Returns(true);

        _fixedStrategy = new FixedChuteSelectionStrategy(_mockFixedLogger.Object);
        _roundRobinStrategy = new RoundRobinChuteSelectionStrategy(_mockRoundRobinLogger.Object);
        _formalStrategy = new FormalChuteSelectionStrategy(
            _mockUpstreamClient.Object,
            _mockClock.Object,
            _mockFormalLogger.Object,
            subscribeToEvents: true);

        _service = new CompositeChuteSelectionService(
            _fixedStrategy,
            _roundRobinStrategy,
            _formalStrategy,
            _mockServiceLogger.Object);
    }

    public void Dispose()
    {
        _formalStrategy.Dispose();
    }

    [Fact]
    public async Task SelectChuteAsync_WithFixedChuteMode_UsesFixedStrategy()
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
        var result = await _service.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.TargetChuteId);
        Assert.False(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_WithRoundRobinMode_UsesRoundRobinStrategy()
    {
        // Arrange
        _roundRobinStrategy.ResetIndex();
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = new List<long> { 1, 2, 3 }
        };

        // Act
        var result = await _service.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.TargetChuteId); // First in round robin
        Assert.False(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_WithFormalModeAndDisconnected_ReturnsExceptionChute()
    {
        // Arrange
        _mockUpstreamClient.Setup(c => c.IsConnected).Returns(false);

        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999
        };

        // Act
        var result = await _service.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_WithOverloadForced_AllModesReturnExceptionChute()
    {
        // Arrange
        var modes = new[] { SortingMode.Formal, SortingMode.FixedChute, SortingMode.RoundRobin };

        foreach (var mode in modes)
        {
            var context = new SortingContext
            {
                ParcelId = 1001,
                SortingMode = mode,
                ExceptionChuteId = 999,
                FixedChuteId = 5,
                AvailableChuteIds = new List<long> { 1, 2, 3 },
                IsOverloadForced = true
            };

            // Act
            var result = await _service.SelectChuteAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(999, result.TargetChuteId);
            Assert.True(result.IsException);
        }
    }

    [Fact]
    public async Task SelectChuteAsync_WithInvalidFixedChuteConfig_FallsBackToExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.FixedChute,
            ExceptionChuteId = 999,
            FixedChuteId = null // Invalid config
        };

        // Act
        var result = await _service.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_WithInvalidRoundRobinConfig_FallsBackToExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.RoundRobin,
            ExceptionChuteId = 999,
            AvailableChuteIds = new List<long>() // Empty config
        };

        // Act
        var result = await _service.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_WithUnknownMode_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = (SortingMode)999, // Invalid mode
            ExceptionChuteId = 999
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.SelectChuteAsync(context, CancellationToken.None));
    }
}
