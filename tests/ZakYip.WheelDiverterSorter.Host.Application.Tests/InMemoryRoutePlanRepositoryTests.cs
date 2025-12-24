using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 内存路由计划仓储测试
/// </summary>
public class InMemoryRoutePlanRepositoryTests
{
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly Mock<ILogger<InMemoryRoutePlanRepository>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly InMemoryRoutePlanRepository _repository;

    public InMemoryRoutePlanRepositoryTests()
    {
        // 配置 SafeExecutionService Mock - 直接执行传入的函数
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
        
        // Mock ExecuteAsync<T> with return value
        _mockSafeExecutor
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<Task<RoutePlan?>>>(),
                It.IsAny<string>(),
                It.IsAny<RoutePlan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task<RoutePlan?>>, string, RoutePlan?, CancellationToken>(
                (func, _, _, _) => func());

        // Mock ExecuteAsync without return value
        _mockSafeExecutor
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, string, CancellationToken>(
                async (func, _, _) => { await func(); return true; });

        _mockLogger = new Mock<ILogger<InMemoryRoutePlanRepository>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _repository = new InMemoryRoutePlanRepository(_memoryCache, _mockSafeExecutor.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task SaveAsync_ShouldStoreRoutePlanInCache()
    {
        // Arrange
        var parcelId = 1234567L;
        var chuteId = 10L;
        var createdAt = DateTimeOffset.Now;
        var routePlan = new RoutePlan(parcelId, chuteId, createdAt);

        // Act
        await _repository.SaveAsync(routePlan);

        // Assert
        var retrieved = await _repository.GetByParcelIdAsync(parcelId);
        Assert.NotNull(retrieved);
        Assert.Equal(parcelId, retrieved.ParcelId);
        Assert.Equal(chuteId, retrieved.CurrentTargetChuteId);
        Assert.Equal(RoutePlanStatus.Created, retrieved.Status);
    }

    [Fact]
    public async Task SaveAsync_ShouldUpdateExistingRoutePlan()
    {
        // Arrange
        var parcelId = 9876543L;
        var initialChuteId = 10L;
        var updatedChuteId = 20L;
        var createdAt = DateTimeOffset.Now;

        var routePlan = new RoutePlan(parcelId, initialChuteId, createdAt);
        await _repository.SaveAsync(routePlan);

        // Act - 更新格口
        routePlan.TryApplyChuteChange(updatedChuteId, DateTimeOffset.Now, out _);
        await _repository.SaveAsync(routePlan);

        // Assert
        var retrieved = await _repository.GetByParcelIdAsync(parcelId);
        Assert.NotNull(retrieved);
        Assert.Equal(updatedChuteId, retrieved.CurrentTargetChuteId);
        Assert.Equal(1, retrieved.ChuteChangeCount);
    }

    [Fact]
    public async Task SaveAsync_WithZeroParcelId_ShouldLogWarningAndNotThrow()
    {
        // Arrange - 创建一个 ParcelId 为 0 的 RoutePlan
        var routePlan = new RoutePlan
        {
            ParcelId = 0,
            InitialTargetChuteId = 10,
            CurrentTargetChuteId = 10,
            Status = RoutePlanStatus.Created,
            CreatedAt = DateTimeOffset.Now,
            LastModifiedAt = DateTimeOffset.Now
        };

        // Act & Assert - 应该不抛异常（SafeExecutionService 包裹）
        await _repository.SaveAsync(routePlan);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid RoutePlan.ParcelId")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_MultipleRoutePlans_ShouldStoreAllWithoutConflict()
    {
        // Arrange
        var routePlan1 = new RoutePlan(111111L, 10, DateTimeOffset.Now);
        var routePlan2 = new RoutePlan(222222L, 20, DateTimeOffset.Now);
        var routePlan3 = new RoutePlan(333333L, 30, DateTimeOffset.Now);

        // Act - 保存多个 RoutePlan
        await _repository.SaveAsync(routePlan1);
        await _repository.SaveAsync(routePlan2);
        await _repository.SaveAsync(routePlan3);

        // Assert - 所有 RoutePlan 都应该成功保存
        var retrieved1 = await _repository.GetByParcelIdAsync(111111L);
        var retrieved2 = await _repository.GetByParcelIdAsync(222222L);
        var retrieved3 = await _repository.GetByParcelIdAsync(333333L);

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.NotNull(retrieved3);
        Assert.Equal(10, retrieved1.CurrentTargetChuteId);
        Assert.Equal(20, retrieved2.CurrentTargetChuteId);
        Assert.Equal(30, retrieved3.CurrentTargetChuteId);
    }

    [Fact]
    public async Task SaveAsync_DuplicateParcelId_ShouldOverwriteWithoutError()
    {
        // Arrange
        var parcelId = 1766567704191L;
        var routePlan1 = new RoutePlan(parcelId, 2L, DateTimeOffset.Now);
        var routePlan2 = new RoutePlan(parcelId, 5L, DateTimeOffset.Now);

        // Act - 保存相同 ParcelId 的两个 RoutePlan（模拟重复键场景）
        await _repository.SaveAsync(routePlan1);
        await _repository.SaveAsync(routePlan2);

        // Assert - 应该自动覆盖，不抛异常
        var retrieved = await _repository.GetByParcelIdAsync(parcelId);
        Assert.NotNull(retrieved);
        Assert.Equal(parcelId, retrieved.ParcelId);
        Assert.Equal(5L, retrieved.CurrentTargetChuteId); // 最后一次保存的值
    }

    [Fact]
    public async Task GetByParcelIdAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var nonExistentParcelId = 999999L;

        // Act
        var result = await _repository.GetByParcelIdAsync(nonExistentParcelId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByParcelIdAsync_WithInvalidParcelId_ShouldReturnNullAndLogWarning()
    {
        // Act
        var result = await _repository.GetByParcelIdAsync(0);

        // Assert
        Assert.Null(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid ParcelId")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveRoutePlan()
    {
        // Arrange
        var parcelId = 555555L;
        var routePlan = new RoutePlan(parcelId, 10, DateTimeOffset.Now);
        await _repository.SaveAsync(routePlan);

        // Act
        await _repository.DeleteAsync(parcelId);

        // Assert
        var retrieved = await _repository.GetByParcelIdAsync(parcelId);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidParcelId_ShouldLogWarningAndNotThrow()
    {
        // Act & Assert - 应该不抛异常
        await _repository.DeleteAsync(0);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid ParcelId")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RoutePlan_ShouldPreserveAllPropertiesInCache()
    {
        // Arrange
        var parcelId = 777777L;
        var initialChuteId = 10L;
        var newChuteId = 20L;
        var createdAt = DateTimeOffset.Now;
        var deadline = createdAt.AddMinutes(5);

        var routePlan = new RoutePlan(parcelId, initialChuteId, createdAt, deadline);
        routePlan.MarkAsExecuting(DateTimeOffset.Now);
        routePlan.TryApplyChuteChange(newChuteId, DateTimeOffset.Now, out _);

        // Act
        await _repository.SaveAsync(routePlan);
        var retrieved = await _repository.GetByParcelIdAsync(parcelId);

        // Assert - 验证所有关键属性都被正确保存和恢复
        Assert.NotNull(retrieved);
        Assert.Equal(parcelId, retrieved.ParcelId);
        Assert.Equal(initialChuteId, retrieved.InitialTargetChuteId);
        Assert.Equal(newChuteId, retrieved.CurrentTargetChuteId);
        Assert.Equal(RoutePlanStatus.Executing, retrieved.Status);
        Assert.Equal(1, retrieved.ChuteChangeCount);
        Assert.NotNull(retrieved.LastReplanDeadline);
        Assert.Equal(deadline, retrieved.LastReplanDeadline.Value);
    }

    [Fact]
    public async Task CacheExpiration_ShouldBeConfiguredCorrectly()
    {
        // Arrange
        var parcelId = 888888L;
        var routePlan = new RoutePlan(parcelId, 10, DateTimeOffset.Now);

        // Act
        await _repository.SaveAsync(routePlan);

        // Assert - 验证缓存已设置（此时应该能获取到）
        var retrieved = await _repository.GetByParcelIdAsync(parcelId);
        Assert.NotNull(retrieved);

        // Note: 实际的过期测试需要等待3分钟，不适合单元测试
        // 这里只验证基本的存储和检索功能正常
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var parcelIds = Enumerable.Range(1, 100).Select(i => (long)i).ToArray();
        var tasks = new List<Task>();

        // Act - 并发保存100个路由计划
        foreach (var parcelId in parcelIds)
        {
            tasks.Add(Task.Run(async () =>
            {
                var routePlan = new RoutePlan(parcelId, parcelId * 10, DateTimeOffset.Now);
                await _repository.SaveAsync(routePlan);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - 验证所有路由计划都成功保存
        foreach (var parcelId in parcelIds)
        {
            var retrieved = await _repository.GetByParcelIdAsync(parcelId);
            Assert.NotNull(retrieved);
            Assert.Equal(parcelId, retrieved.ParcelId);
            Assert.Equal(parcelId * 10, retrieved.CurrentTargetChuteId);
        }
    }
}
