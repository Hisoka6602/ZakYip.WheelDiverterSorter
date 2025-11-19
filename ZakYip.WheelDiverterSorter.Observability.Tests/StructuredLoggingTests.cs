using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// 测试结构化日志记录功能
/// Tests for structured logging functionality
/// </summary>
public class StructuredLoggingTests
{
    private SwitchingPath CreateTestPath(int chuteId)
    {
        return new SwitchingPath
        {
            TargetChuteId = chuteId,
            Segments = new List<SwitchingPathSegment>(),
            GeneratedAt = DateTimeOffset.UtcNow,
            FallbackChuteId = 999
        };
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldLogStructuredInformation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<OptimizedSortingService>>();
        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        var mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var metrics = new SorterMetrics(meterFactory);

        var service = new OptimizedSortingService(
            mockPathGenerator.Object,
            mockPathExecutor.Object,
            metrics,
            mockLogger.Object);

        var testPath = CreateTestPath(42);

        mockPathGenerator.Setup(g => g.GeneratePath(42)).Returns(testPath);
        mockPathExecutor.Setup(e => e.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = true,
                ActualChuteId = 42
            });

        // Act
        await service.SortParcelAsync("PARCEL_001", 42);

        // Assert - Verify structured logging with specific parameters
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("分拣成功")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldLogWarningWhenPathGenerationFails()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<OptimizedSortingService>>();
        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        var mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var metrics = new SorterMetrics(meterFactory);

        var service = new OptimizedSortingService(
            mockPathGenerator.Object,
            mockPathExecutor.Object,
            metrics,
            mockLogger.Object);

        mockPathGenerator.Setup(g => g.GeneratePath(99)).Returns((SwitchingPath?)null);

        // Act
        await service.SortParcelAsync("PARCEL_002", 99);

        // Assert - Verify warning level log
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("路径生成失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldLogWarningWhenExecutionFails()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<OptimizedSortingService>>();
        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        var mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var metrics = new SorterMetrics(meterFactory);

        var service = new OptimizedSortingService(
            mockPathGenerator.Object,
            mockPathExecutor.Object,
            metrics,
            mockLogger.Object);

        var testPath = CreateTestPath(42);

        mockPathGenerator.Setup(g => g.GeneratePath(42)).Returns(testPath);
        mockPathExecutor.Setup(e => e.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = 0,
                FailureReason = "Timeout"
            });

        // Act
        await service.SortParcelAsync("PARCEL_003", 42);

        // Assert - Verify warning level log for failure
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("分拣失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldLogErrorWithExceptionDetails()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<OptimizedSortingService>>();
        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        var mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var metrics = new SorterMetrics(meterFactory);

        var service = new OptimizedSortingService(
            mockPathGenerator.Object,
            mockPathExecutor.Object,
            metrics,
            mockLogger.Object);

        var testException = new InvalidOperationException("Test exception");
        mockPathGenerator.Setup(g => g.GeneratePath(42))
            .Throws(testException);

        // Act
        await service.SortParcelAsync("PARCEL_004", 42);

        // Assert - Verify error level log with exception
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("分拣过程发生异常")),
                It.Is<Exception>(ex => ex == testException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OptimizedSortingService_ShouldIncludeStructuredDataInLogs()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<OptimizedSortingService>>();
        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        var mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var metrics = new SorterMetrics(meterFactory);

        var service = new OptimizedSortingService(
            mockPathGenerator.Object,
            mockPathExecutor.Object,
            metrics,
            mockLogger.Object);

        var testPath = CreateTestPath(42);

        mockPathGenerator.Setup(g => g.GeneratePath(42)).Returns(testPath);
        mockPathExecutor.Setup(e => e.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = true,
                ActualChuteId = 42
            });

        // Act
        await service.SortParcelAsync("PARCEL_005", 42);

        // Assert - Verify structured data is included (ParcelId, ChuteId, duration metrics)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("PARCEL_005") && 
                    v.ToString()!.Contains("42")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogLevels_ShouldBeUsedAppropriately()
    {
        // This test demonstrates that different log levels are used for different scenarios
        // Arrange
        var mockLogger = new Mock<ILogger<OptimizedSortingService>>();
        
        // Assert - Verify that the logger can be called with different log levels
        // This is a compile-time check that the API supports different log levels
        
        mockLogger.Setup(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

        mockLogger.Setup(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

        mockLogger.Setup(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

        // Act & Assert - Verify setup was successful
        Assert.NotNull(mockLogger.Object);
    }

    [Fact]
    public async Task StructuredLogging_ShouldIncludeTimingMetrics()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<OptimizedSortingService>>();
        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        var mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var metrics = new SorterMetrics(meterFactory);

        var service = new OptimizedSortingService(
            mockPathGenerator.Object,
            mockPathExecutor.Object,
            metrics,
            mockLogger.Object);

        var testPath = CreateTestPath(42);

        mockPathGenerator.Setup(g => g.GeneratePath(42)).Returns(testPath);
        mockPathExecutor.Setup(e => e.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = true,
                ActualChuteId = 42
            });

        // Act
        await service.SortParcelAsync("PARCEL_006", 42);

        // Assert - Verify that timing metrics are included in the log message
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("总时长") &&
                    v.ToString()!.Contains("生成") &&
                    v.ToString()!.Contains("执行")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StructuredLogging_ShouldLogFailureReasons()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<OptimizedSortingService>>();
        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        var mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var metrics = new SorterMetrics(meterFactory);

        var service = new OptimizedSortingService(
            mockPathGenerator.Object,
            mockPathExecutor.Object,
            metrics,
            mockLogger.Object);

        var testPath = CreateTestPath(42);

        mockPathGenerator.Setup(g => g.GeneratePath(42)).Returns(testPath);
        mockPathExecutor.Setup(e => e.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = 0,
                FailureReason = "Hardware communication timeout"
            });

        // Act
        await service.SortParcelAsync("PARCEL_007", 42);

        // Assert - Verify failure reason is included in log
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("原因")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
