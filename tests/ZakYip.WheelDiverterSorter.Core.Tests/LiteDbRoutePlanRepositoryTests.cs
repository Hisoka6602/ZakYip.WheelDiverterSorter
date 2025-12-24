using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// LiteDB RoutePlan 仓储测试
/// </summary>
public class LiteDbRoutePlanRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly LiteDbRoutePlanRepository _repository;

    public LiteDbRoutePlanRepositoryTests()
    {
        // 使用临时数据库文件
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_routeplans_{Guid.NewGuid()}.db");
        _repository = new LiteDbRoutePlanRepository(_tempDbPath);
    }

    public void Dispose()
    {
        _repository.Dispose();
        
        // 清理临时文件
        if (File.Exists(_tempDbPath))
        {
            File.Delete(_tempDbPath);
        }
    }

    [Fact]
    public async Task SaveAsync_ShouldInsertNewRoutePlan()
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
    public async Task SaveAsync_WithZeroParcelId_ShouldThrowArgumentException()
    {
        // Arrange - 创建一个 ParcelId 为 0 的 RoutePlan（这不应该发生，但我们要防御性编程）
        var routePlan = new RoutePlan
        {
            ParcelId = 0,
            InitialTargetChuteId = 10,
            CurrentTargetChuteId = 10,
            Status = RoutePlanStatus.Created,
            CreatedAt = DateTimeOffset.Now,
            LastModifiedAt = DateTimeOffset.Now
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.SaveAsync(routePlan));
        
        Assert.Contains("ParcelId must be a positive value", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_MultipleRoutePlans_ShouldNotCauseDuplicateKeyError()
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
    public async Task SaveAsync_ThenUpdate_ShouldNotCreateDuplicate()
    {
        // Arrange
        var parcelId = 1766567704191L; // 使用错误日志中的实际包裹 ID
        var initialChuteId = 2L;
        var updatedChuteId = 5L;
        var createdAt = DateTimeOffset.Now;

        var routePlan = new RoutePlan(parcelId, initialChuteId, createdAt);

        // Act - 第一次保存
        await _repository.SaveAsync(routePlan);

        // 模拟上游分配新格口
        routePlan.TryApplyChuteChange(updatedChuteId, DateTimeOffset.Now, out _);

        // 第二次保存（应该是更新操作，而不是插入）
        await _repository.SaveAsync(routePlan);

        // Assert - 应该只有一条记录
        var retrieved = await _repository.GetByParcelIdAsync(parcelId);
        Assert.NotNull(retrieved);
        Assert.Equal(parcelId, retrieved.ParcelId);
        Assert.Equal(updatedChuteId, retrieved.CurrentTargetChuteId);
        Assert.Equal(1, retrieved.ChuteChangeCount);
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
    public async Task RoutePlan_ShouldPreserveAllPropertiesAfterSerialization()
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
        
        // LiteDB 可能会丢失毫秒精度，所以我们只检查到秒级别
        var expectedDeadline = new DateTimeOffset(deadline.Year, deadline.Month, deadline.Day,
            deadline.Hour, deadline.Minute, deadline.Second, deadline.Offset);
        var actualDeadline = new DateTimeOffset(retrieved.LastReplanDeadline.Value.Year,
            retrieved.LastReplanDeadline.Value.Month, retrieved.LastReplanDeadline.Value.Day,
            retrieved.LastReplanDeadline.Value.Hour, retrieved.LastReplanDeadline.Value.Minute,
            retrieved.LastReplanDeadline.Value.Second, retrieved.LastReplanDeadline.Value.Offset);
        Assert.Equal(expectedDeadline, actualDeadline);
    }
}
