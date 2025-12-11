using LiteDB;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

/// <summary>
/// 基于LiteDB的路由计划仓储实现
/// </summary>
/// <remarks>
/// 用于持久化包裹的路由计划信息，支持改口功能。
/// 使用LiteDB存储以支持运行时数据的持久化。
/// </remarks>
public sealed class LiteDbRoutePlanRepository : IRoutePlanRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<RoutePlan> _collection;
    private const string CollectionName = "RoutePlans";
    private bool _disposed;

    /// <summary>
    /// 初始化LiteDB路由计划仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    public LiteDbRoutePlanRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be null or empty", nameof(databasePath));
        }

        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<RoutePlan>(CollectionName);
        
        // 为ParcelId字段创建唯一索引，确保每个包裹只有一个路由计划
        _collection.EnsureIndex(x => x.ParcelId, unique: true);
    }

    /// <inheritdoc/>
    public Task<RoutePlan?> GetByParcelIdAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LiteDbRoutePlanRepository));
        }

        RoutePlan? plan = _collection
            .Query()
            .Where(x => x.ParcelId == parcelId)
            .FirstOrDefault();

        return Task.FromResult(plan);
    }

    /// <inheritdoc/>
    public Task SaveAsync(RoutePlan routePlan, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LiteDbRoutePlanRepository));
        }

        ArgumentNullException.ThrowIfNull(routePlan);

        // 检查是否已存在该包裹的路由计划
        var existing = _collection
            .Query()
            .Where(x => x.ParcelId == routePlan.ParcelId)
            .FirstOrDefault();

        if (existing != null)
        {
            // 更新现有记录
            _collection.Update(routePlan);
        }
        else
        {
            // 插入新记录
            _collection.Insert(routePlan);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LiteDbRoutePlanRepository));
        }

        _collection.DeleteMany(x => x.ParcelId == parcelId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _database?.Dispose();
        _disposed = true;
    }
}
