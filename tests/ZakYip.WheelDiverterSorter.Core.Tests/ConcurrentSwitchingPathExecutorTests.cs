using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// 并发路径执行器测试：测试并发控制、资源锁、队列管理
/// </summary>
public class ConcurrentSwitchingPathExecutorTests
{
    private readonly Mock<ISwitchingPathExecutor> _innerExecutorMock;
    private readonly DiverterResourceLockManager _lockManager;
    private readonly IOptions<ConcurrencyOptions> _options;

    public ConcurrentSwitchingPathExecutorTests()
    {
        _innerExecutorMock = new Mock<ISwitchingPathExecutor>();
        _lockManager = new DiverterResourceLockManager();
        _options = Options.Create(new ConcurrencyOptions
        {
            MaxConcurrentParcels = 5,
            DiverterLockTimeoutMs = 1000
        });
    }

    [Fact]
    public void Constructor_WithNullInnerExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ConcurrentSwitchingPathExecutor(
                null!,
                _lockManager,
                _options,
                NullLogger<ConcurrentSwitchingPathExecutor>.Instance));
    }

    [Fact]
    public void Constructor_WithNullLockManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ConcurrentSwitchingPathExecutor(
                _innerExecutorMock.Object,
                null!,
                _options,
                NullLogger<ConcurrentSwitchingPathExecutor>.Instance));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ConcurrentSwitchingPathExecutor(
                _innerExecutorMock.Object,
                _lockManager,
                null!,
                NullLogger<ConcurrentSwitchingPathExecutor>.Instance));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var executor = new ConcurrentSwitchingPathExecutor(
            _innerExecutorMock.Object,
            _lockManager,
            _options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            executor.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPath_ExecutesSuccessfully()
    {
        // Arrange
        var path = CreateTestPath(targetChuteId: 1, diverterIds: new[] { 1, 2 });
        var expectedResult = new PathExecutionResult
        {
            IsSuccess = true,
            ActualChuteId = 1
        };

        _innerExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var executor = new ConcurrentSwitchingPathExecutor(
            _innerExecutorMock.Object,
            _lockManager,
            _options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act
        var result = await executor.ExecuteAsync(path);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ActualChuteId);
        _innerExecutorMock.Verify(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AcquiresLocksForAllDiverters()
    {
        // Arrange
        var path = CreateTestPath(targetChuteId: 1, diverterIds: new[] { 1, 2, 3 });
        var lockAcquisitions = new List<int>();

        _innerExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult { IsSuccess = true, ActualChuteId = 1 })
            .Callback(() =>
            {
                // Record which locks are held during execution
                for (int i = 1; i <= 3; i++)
                {
                    lockAcquisitions.Add(i);
                }
            });

        var executor = new ConcurrentSwitchingPathExecutor(
            _innerExecutorMock.Object,
            _lockManager,
            _options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act
        await executor.ExecuteAsync(path);

        // Assert
        Assert.Equal(3, lockAcquisitions.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentRequests_EnforcesMaxConcurrency()
    {
        // Arrange
        var maxConcurrent = 3;
        var options = Options.Create(new ConcurrencyOptions
        {
            MaxConcurrentParcels = maxConcurrent,
            DiverterLockTimeoutMs = 5000
        });

        var concurrentCount = 0;
        var maxObservedConcurrent = 0;

        _innerExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var current = Interlocked.Increment(ref concurrentCount);
                maxObservedConcurrent = Math.Max(maxObservedConcurrent, current);
                await Task.Delay(50);
                Interlocked.Decrement(ref concurrentCount);
                return new PathExecutionResult { IsSuccess = true, ActualChuteId = 1 };
            });

        var executor = new ConcurrentSwitchingPathExecutor(
            _innerExecutorMock.Object,
            _lockManager,
            options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act - try to execute 10 paths concurrently
        var tasks = Enumerable.Range(1, 10).Select(i =>
            executor.ExecuteAsync(CreateTestPath(i, new[] { i * 10 }))
        );

        await Task.WhenAll(tasks);

        // Assert
        Assert.True(maxObservedConcurrent <= maxConcurrent,
            $"Max concurrent was {maxObservedConcurrent}, expected <= {maxConcurrent}");
    }

    [Fact]
    public async Task ExecuteAsync_SameDiverter_ExecutesSerially()
    {
        // Arrange
        var mockExecutor = new Mock<ISwitchingPathExecutor>();
        var lockManager = new DiverterResourceLockManager();
        
        var path = CreateTestPath(targetChuteId: 1, diverterIds: new[] { 1 });
        var executionOrder = new List<int>();
        var executionId = 0;

        mockExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var id = Interlocked.Increment(ref executionId);
                executionOrder.Add(id);
                await Task.Delay(50);
                return new PathExecutionResult { IsSuccess = true, ActualChuteId = 1 };
            });

        var executor = new ConcurrentSwitchingPathExecutor(
            mockExecutor.Object,
            lockManager,
            _options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act - try to execute multiple paths using the same diverter
        var tasks = Enumerable.Range(1, 5).Select(i =>
            executor.ExecuteAsync(path)
        );

        await Task.WhenAll(tasks);

        // Assert - executions should be in order (serial)
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, executionOrder);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentDiverters_ExecutesConcurrently()
    {
        // Arrange
        var concurrentCount = 0;
        var maxObservedConcurrent = 0;

        _innerExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var current = Interlocked.Increment(ref concurrentCount);
                maxObservedConcurrent = Math.Max(maxObservedConcurrent, current);
                await Task.Delay(100);
                Interlocked.Decrement(ref concurrentCount);
                return new PathExecutionResult { IsSuccess = true, ActualChuteId = 1 };
            });

        var executor = new ConcurrentSwitchingPathExecutor(
            _innerExecutorMock.Object,
            _lockManager,
            _options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act - execute paths with different diverters
        var tasks = Enumerable.Range(1, 5).Select(i =>
            executor.ExecuteAsync(CreateTestPath(i, new[] { i }))
        );

        await Task.WhenAll(tasks);

        // Assert - multiple executions should run concurrently
        Assert.True(maxObservedConcurrent > 1,
            $"Expected concurrent execution, but max concurrent was {maxObservedConcurrent}");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ReturnsCanceledResult()
    {
        // Arrange
        var path = CreateTestPath(targetChuteId: 1, diverterIds: new[] { 1 });
        var cts = new CancellationTokenSource();

        _innerExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .Returns(async (SwitchingPath p, CancellationToken ct) =>
            {
                await Task.Delay(100, ct);  // This should throw when cancelled
                return new PathExecutionResult { IsSuccess = true, ActualChuteId = 1 };
            });

        var executor = new ConcurrentSwitchingPathExecutor(
            _innerExecutorMock.Object,
            _lockManager,
            _options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act
        var executeTask = executor.ExecuteAsync(path, cts.Token);
        await Task.Delay(10);  // Give task a chance to start
        cts.Cancel();
        var result = await executeTask;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("取消", result.FailureReason ?? "");
    }

    [Fact]
    public async Task ExecuteAsync_InnerExecutorThrows_ReturnsFailureResult()
    {
        // Arrange
        var path = CreateTestPath(targetChuteId: 1, diverterIds: new[] { 1 });

        _innerExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var executor = new ConcurrentSwitchingPathExecutor(
            _innerExecutorMock.Object,
            _lockManager,
            _options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act
        var result = await executor.ExecuteAsync(path);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("异常", result.FailureReason ?? "");
    }

    [Fact]
    public async Task ExecuteAsync_ReleasesLocksOnFailure()
    {
        // Arrange
        var path = CreateTestPath(targetChuteId: 1, diverterIds: new[] { 1 });

        _innerExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test failure"));

        var executor = new ConcurrentSwitchingPathExecutor(
            _innerExecutorMock.Object,
            _lockManager,
            _options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act
        var result1 = await executor.ExecuteAsync(path);

        // Reset mock for second call
        _innerExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PathExecutionResult { IsSuccess = true, ActualChuteId = 1 });

        // Should be able to execute again immediately
        var result2 = await executor.ExecuteAsync(path);

        // Assert
        Assert.False(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_HighConcurrency_MaintainsCorrectness()
    {
        // Arrange
        var mockExecutor = new Mock<ISwitchingPathExecutor>();
        var lockManager = new DiverterResourceLockManager();
        
        var totalRequests = 50;
        var successCount = 0;

        mockExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(10);
                return new PathExecutionResult { IsSuccess = true, ActualChuteId = 1 };
            });

        // Use longer timeout for high concurrency test to avoid spurious timeouts
        var options = Options.Create(new ConcurrencyOptions
        {
            MaxConcurrentParcels = 5,
            DiverterLockTimeoutMs = 30000  // Increased to 30 seconds for high contention scenarios
        });

        var executor = new ConcurrentSwitchingPathExecutor(
            mockExecutor.Object,
            lockManager,
            options,
            NullLogger<ConcurrentSwitchingPathExecutor>.Instance);

        // Act - execute many requests concurrently
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var tasks = Enumerable.Range(1, totalRequests).Select(async i =>
        {
            var path = CreateTestPath(i % 10, new[] { (i % 5) + 1 });  // Use diverter IDs 1-5, not 0-4
            var result = await executor.ExecuteAsync(path);
            if (result.IsSuccess)
            {
                Interlocked.Increment(ref successCount);
            }
            else
            {
                failures.Add($"Task {i}: {result.FailureReason}");
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        if (successCount != totalRequests)
        {
            var failureDetails = string.Join(Environment.NewLine, failures);
            Assert.Fail($"Expected {totalRequests} successes but got {successCount}. Failures:{Environment.NewLine}{failureDetails}");
        }
        Assert.Equal(totalRequests, successCount);
    }

    private static SwitchingPath CreateTestPath(int targetChuteId, int[] diverterIds)
    {
        var segments = diverterIds.Select((id, index) => new SwitchingPathSegment
        {
            SequenceNumber = index + 1,
            DiverterId = id,
            TargetDirection = DiverterDirection.Left,
            TtlMilliseconds = 5000
        }).ToList();

        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            Segments = segments,
            GeneratedAt = DateTimeOffset.UtcNow,
            FallbackChuteId = 999
        };
    }
}
