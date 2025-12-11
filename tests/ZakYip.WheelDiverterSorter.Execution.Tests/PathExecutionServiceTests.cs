using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Execution.PathExecution;

namespace ZakYip.WheelDiverterSorter.Execution.Tests;

/// <summary>
/// PathExecutionService 单元测试
/// </summary>
public class PathExecutionServiceTests
{
    private readonly Mock<ISwitchingPathExecutor> _mockPathExecutor;
    private readonly Mock<IPathFailureHandler> _mockPathFailureHandler;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<ILogger<PathExecutionService>> _mockLogger;
    private readonly Mock<PrometheusMetrics> _mockMetrics;
    private readonly PathExecutionService _service;

    public PathExecutionServiceTests()
    {
        _mockPathExecutor = new Mock<ISwitchingPathExecutor>();
        _mockPathFailureHandler = new Mock<IPathFailureHandler>();
        _mockClock = new Mock<ISystemClock>();
        _mockLogger = new Mock<ILogger<PathExecutionService>>();
        _mockMetrics = new Mock<PrometheusMetrics>(_mockClock.Object);

        _mockClock.Setup(c => c.LocalNowOffset).Returns(DateTimeOffset.UtcNow);

        _service = new PathExecutionService(
            _mockPathExecutor.Object,
            _mockPathFailureHandler.Object,
            _mockClock.Object,
            _mockLogger.Object,
            _mockMetrics.Object);
    }

    private static SwitchingPath CreateTestPath(long targetChuteId = 1, long fallbackChuteId = 999)
    {
        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            FallbackChuteId = fallbackChuteId,
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = 101,
                    TargetDirection = DiverterDirection.Left,
                    TtlMilliseconds = 5000
                },
                new SwitchingPathSegment
                {
                    SequenceNumber = 2,
                    DiverterId = 102,
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                }
            }
        };
    }

    [Fact]
    public async Task ExecutePathAsync_WhenSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        var path = CreateTestPath();
        var parcelId = 12345L;

        _mockPathExecutor.Setup(e => e.ExecuteAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = true,
                ActualChuteId = path.TargetChuteId
            });

        // Act
        var result = await _service.ExecutePathAsync(parcelId, path);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ActualChuteId.Should().Be(path.TargetChuteId);
        result.FailureReason.Should().BeNull();

        // 验证执行器被调用
        _mockPathExecutor.Verify(e => e.ExecuteAsync(path, It.IsAny<CancellationToken>()), Times.Once);

        // 验证失败处理器没有被调用
        _mockPathFailureHandler.Verify(
            h => h.HandlePathFailure(It.IsAny<long>(), It.IsAny<SwitchingPath>(), It.IsAny<string>(), It.IsAny<SwitchingPathSegment?>()),
            Times.Never);
        _mockPathFailureHandler.Verify(
            h => h.HandleSegmentFailure(It.IsAny<long>(), It.IsAny<SwitchingPath>(), It.IsAny<SwitchingPathSegment>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecutePathAsync_WhenFailed_ReturnsFailureResultAndCallsPathFailureHandler()
    {
        // Arrange
        var path = CreateTestPath();
        var parcelId = 12345L;
        var failedSegment = path.Segments[0];
        var failureReason = "TTL超时";

        _mockPathExecutor.Setup(e => e.ExecuteAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = failureReason,
                FailedSegment = failedSegment,
                FailureTime = DateTimeOffset.UtcNow
            });

        // Act
        var result = await _service.ExecutePathAsync(parcelId, path);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ActualChuteId.Should().Be(path.FallbackChuteId);
        result.FailureReason.Should().Be(failureReason);

        // 验证失败处理器被调用（HandleSegmentFailure）
        _mockPathFailureHandler.Verify(
            h => h.HandleSegmentFailure(parcelId, path, failedSegment, failureReason),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePathAsync_WhenFailedWithoutSegment_CallsHandlePathFailure()
    {
        // Arrange
        var path = CreateTestPath();
        var parcelId = 12345L;
        var failureReason = "设备通信失败";

        _mockPathExecutor.Setup(e => e.ExecuteAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = failureReason,
                FailedSegment = null, // 没有指定失败的段
                FailureTime = DateTimeOffset.UtcNow
            });

        // Act
        var result = await _service.ExecutePathAsync(parcelId, path);

        // Assert
        result.IsSuccess.Should().BeFalse();

        // 验证 HandlePathFailure 被调用（而非 HandleSegmentFailure）
        _mockPathFailureHandler.Verify(
            h => h.HandlePathFailure(parcelId, path, failureReason, null),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePathAsync_WhenCancelled_ReturnsFailureResultAndCallsPathFailureHandler()
    {
        // Arrange
        var path = CreateTestPath();
        var parcelId = 12345L;

        _mockPathExecutor.Setup(e => e.ExecuteAsync(path, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _service.ExecutePathAsync(parcelId, path, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ActualChuteId.Should().Be(path.FallbackChuteId);
        result.FailureReason.Should().Contain("取消");

        // 验证失败处理器被调用
        _mockPathFailureHandler.Verify(
            h => h.HandlePathFailure(parcelId, path, It.IsAny<string>(), null),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePathAsync_WhenExceptionThrown_ReturnsFailureResultAndCallsPathFailureHandler()
    {
        // Arrange
        var path = CreateTestPath();
        var parcelId = 12345L;
        var exceptionMessage = "网络连接断开";

        _mockPathExecutor.Setup(e => e.ExecuteAsync(path, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        // Act
        var result = await _service.ExecutePathAsync(parcelId, path);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ActualChuteId.Should().Be(path.FallbackChuteId);
        result.FailureReason.Should().Contain(exceptionMessage);

        // 验证失败处理器被调用
        _mockPathFailureHandler.Verify(
            h => h.HandlePathFailure(parcelId, path, It.Is<string>(r => r.Contains(exceptionMessage)), null),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePathAsync_RecordsMetrics()
    {
        // Arrange
        var path = CreateTestPath();
        var parcelId = 12345L;

        _mockPathExecutor.Setup(e => e.ExecuteAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = true,
                ActualChuteId = path.TargetChuteId
            });

        // Act - 使用真实的 metrics 对象（不是 mock）来验证不会抛出异常
        var realClock = new LocalSystemClock();
        var realMetrics = new PrometheusMetrics(realClock);
        var serviceWithRealMetrics = new PathExecutionService(
            _mockPathExecutor.Object,
            _mockPathFailureHandler.Object,
            _mockClock.Object,
            _mockLogger.Object,
            realMetrics);

        var result = await serviceWithRealMetrics.ExecutePathAsync(parcelId, path);

        // Assert - 确保执行成功且不抛出异常
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecutePathAsync_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var parcelId = 12345L;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ExecutePathAsync(parcelId, null!));
    }

    [Fact]
    public async Task ExecutePathAsync_WithNullMetrics_WorksCorrectly()
    {
        // Arrange - 创建没有 metrics 的服务
        var serviceWithoutMetrics = new PathExecutionService(
            _mockPathExecutor.Object,
            _mockPathFailureHandler.Object,
            _mockClock.Object,
            _mockLogger.Object,
            metrics: null);

        var path = CreateTestPath();
        var parcelId = 12345L;

        _mockPathExecutor.Setup(e => e.ExecuteAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult
            {
                IsSuccess = true,
                ActualChuteId = path.TargetChuteId
            });

        // Act
        var result = await serviceWithoutMetrics.ExecutePathAsync(parcelId, path);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
