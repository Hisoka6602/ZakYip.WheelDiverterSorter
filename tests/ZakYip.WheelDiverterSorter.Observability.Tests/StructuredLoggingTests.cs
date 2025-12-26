using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Health;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// 测试结构化日志记录功能
/// Tests for structured logging functionality
/// </summary>
/// <remarks>
/// PR-SORT2：OptimizedSortingService 现在委托到 ISortingOrchestrator，
/// 只负责指标记录和异常包装。
/// </remarks>
public class StructuredLoggingTests : IDisposable
{
    private readonly Mock<ISortingOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger<OptimizedSortingService>> _mockLogger;
    private readonly SorterMetrics _metrics;
    private readonly OptimizedSortingService _service;
    private readonly ServiceProvider _serviceProvider;

    public StructuredLoggingTests()
    {
        _mockOrchestrator = new Mock<ISortingOrchestrator>();
        _mockLogger = new Mock<ILogger<OptimizedSortingService>>();

        var services = new ServiceCollection();
        services.AddMetrics();
        _serviceProvider = services.BuildServiceProvider();
        var meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
        _metrics = new SorterMetrics(meterFactory);

        _service = new OptimizedSortingService(
            _mockOrchestrator.Object,
            _metrics,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldReturnSuccessWhenOrchestratorSucceeds()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.ExecuteDebugSortAsync("PARCEL_001", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SortingResult(
                IsSuccess: true,
                ParcelId: "PARCEL_001",
                ActualChuteId: 42,
                TargetChuteId: 42,
                ExecutionTimeMs: 100));

        // Act
        var result = await _service.SortParcelAsync("PARCEL_001", 42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.ActualChuteId);
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldReturnFailureWhenOrchestratorFails()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.ExecuteDebugSortAsync("PARCEL_002", 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SortingResult(
                IsSuccess: false,
                ParcelId: "PARCEL_002",
                ActualChuteId: 0,
                TargetChuteId: 99,
                ExecutionTimeMs: 50,
                FailureReason: "路径生成失败"));

        // Act
        var result = await _service.SortParcelAsync("PARCEL_002", 99);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("路径生成失败", result.FailureReason);
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldLogErrorOnException()
    {
        // Arrange
        var testException = new InvalidOperationException("Test exception");
        _mockOrchestrator
            .Setup(o => o.ExecuteDebugSortAsync("PARCEL_003", 42, It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        // Act
        var result = await _service.SortParcelAsync("PARCEL_003", 42);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Test exception", result.FailureReason);

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("分拣异常")),
                It.Is<Exception>(ex => ex == testException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldDelegateToOrchestrator()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.ExecuteDebugSortAsync("PARCEL_004", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SortingResult(
                IsSuccess: true,
                ParcelId: "PARCEL_004",
                ActualChuteId: 5,
                TargetChuteId: 5,
                ExecutionTimeMs: 75));

        // Act
        await _service.SortParcelAsync("PARCEL_004", 5);

        // Assert - Verify delegation to orchestrator
        _mockOrchestrator.Verify(
            o => o.ExecuteDebugSortAsync("PARCEL_004", 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SortBatchAsync_ShouldProcessAllParcels()
    {
        // Arrange
        var parcels = new (string, long)[]
        {
            ("PARCEL_A", 1L),
            ("PARCEL_B", 2L),
            ("PARCEL_C", 3L)
        };

        _mockOrchestrator
            .Setup(o => o.ExecuteDebugSortAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string pid, long cid, CancellationToken ct) => new SortingResult(
                IsSuccess: true,
                ParcelId: pid,
                ActualChuteId: cid,
                TargetChuteId: cid,
                ExecutionTimeMs: 50));

        // Act
        var results = await _service.SortBatchAsync(parcels);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.IsSuccess));
    }
}
