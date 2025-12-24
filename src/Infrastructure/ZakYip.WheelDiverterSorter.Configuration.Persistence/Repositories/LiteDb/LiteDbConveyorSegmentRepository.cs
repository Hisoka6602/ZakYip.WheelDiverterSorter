using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

/// <summary>
/// 基于LiteDB的输送线段配置仓储实现
/// </summary>
public class LiteDbConveyorSegmentRepository : IConveyorSegmentRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<ConveyorSegmentConfiguration> _collection;
    private readonly ISystemClock _systemClock;
    private readonly ILogger<LiteDbConveyorSegmentRepository>? _logger;
    private const string CollectionName = "ConveyorSegmentConfiguration";

    /// <summary>
    /// 初始化LiteDB输送线段配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    /// <param name="systemClock">系统时钟</param>
    /// <param name="logger">日志记录器（可选）</param>
    public LiteDbConveyorSegmentRepository(
        string databasePath, 
        ISystemClock systemClock,
        ILogger<LiteDbConveyorSegmentRepository>? logger = null)
    {
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _logger = logger;
        
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<ConveyorSegmentConfiguration>(CollectionName);
        
        // 为SegmentId字段创建唯一索引（业务唯一键，用于业务逻辑查询）
        _collection.EnsureIndex(x => x.SegmentId, unique: true);
    }

    /// <summary>
    /// 获取所有线段配置
    /// </summary>
    /// <returns>线段配置列表</returns>
    public IEnumerable<ConveyorSegmentConfiguration> GetAll()
    {
        return _collection
            .Query()
            .OrderBy(x => x.SegmentId)
            .ToList();
    }

    /// <summary>
    /// 根据线段ID获取配置
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>线段配置，如不存在则返回null</returns>
    public ConveyorSegmentConfiguration? GetById(long segmentId)
    {
        return _collection
            .Query()
            .Where(x => x.SegmentId == segmentId)
            .FirstOrDefault();
    }

    /// <summary>
    /// 插入新的线段配置
    /// </summary>
    /// <param name="config">线段配置</param>
    public void Insert(ConveyorSegmentConfiguration config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        // 检查ID是否已存在
        if (Exists(config.SegmentId))
        {
            throw new InvalidOperationException($"输送线段ID {config.SegmentId} 已存在");
        }

        // 设置创建和更新时间
        var now = _systemClock.LocalNow;
        var configWithTimestamps = config with 
        { 
            CreatedAt = now, 
            UpdatedAt = now 
        };

        _collection.Insert(configWithTimestamps);
    }

    /// <summary>
    /// 更新线段配置
    /// </summary>
    /// <param name="config">线段配置</param>
    /// <returns>是否更新成功</returns>
    public bool Update(ConveyorSegmentConfiguration config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var existing = GetById(config.SegmentId);
        if (existing == null)
        {
            return false;
        }

        // 保留创建时间，更新修改时间
        var configWithTimestamps = config with 
        { 
            CreatedAt = existing.CreatedAt, 
            UpdatedAt = _systemClock.LocalNow 
        };

        return _collection.Update(configWithTimestamps);
    }

    /// <summary>
    /// 删除线段配置
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>是否删除成功</returns>
    public bool Delete(long segmentId)
    {
        // 先通过 SegmentId 查询记录
        var config = GetById(segmentId);
        if (config == null)
        {
            return false;
        }
        
        // 使用内部 Id 删除记录
        return _collection.Delete(new BsonValue(config.Id));
    }

    /// <summary>
    /// 检查线段ID是否存在
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>是否存在</returns>
    public bool Exists(long segmentId)
    {
        return _collection
            .Query()
            .Where(x => x.SegmentId == segmentId)
            .Exists();
    }

    /// <summary>
    /// 批量插入线段配置
    /// </summary>
    /// <param name="configs">线段配置列表</param>
    public void InsertBatch(IEnumerable<ConveyorSegmentConfiguration> configs)
    {
        if (configs == null)
        {
            throw new ArgumentNullException(nameof(configs));
        }

        var now = _systemClock.LocalNow;
        var configsWithTimestamps = configs.Select(c => c with 
        { 
            CreatedAt = now, 
            UpdatedAt = now 
        }).ToList();

        _collection.InsertBulk(configsWithTimestamps);
    }

    /// <summary>
    /// 批量异步插入线段配置
    /// </summary>
    /// <param name="configs">线段配置列表</param>
    /// <returns>插入的数量</returns>
    public async Task<int> BulkInsertAsync(IEnumerable<ConveyorSegmentConfiguration> configs)
    {
        if (configs == null)
        {
            throw new ArgumentNullException(nameof(configs));
        }

        var configList = configs.ToList();
        if (configList.Count == 0)
        {
            return 0;
        }

        return await Task.Run(() =>
        {
            var now = _systemClock.LocalNow;
            var configsWithTimestamps = configList.Select(c => c with 
            { 
                CreatedAt = now, 
                UpdatedAt = now 
            }).ToList();

            var count = _collection.InsertBulk(configsWithTimestamps);
            return count;
        });
    }

    /// <summary>
    /// 批量异步更新线段配置
    /// </summary>
    /// <param name="configs">线段配置列表</param>
    /// <returns>更新的数量</returns>
    public async Task<int> BulkUpdateAsync(IEnumerable<ConveyorSegmentConfiguration> configs)
    {
        if (configs == null)
        {
            throw new ArgumentNullException(nameof(configs));
        }

        var configList = configs.ToList();
        if (configList.Count == 0)
        {
            return 0;
        }

        return await Task.Run(() =>
        {
            var count = 0;
            var now = _systemClock.LocalNow;
            
            foreach (var config in configList)
            {
                var existing = GetById(config.SegmentId);
                if (existing != null)
                {
                    var configWithTimestamps = config with 
                    { 
                        CreatedAt = existing.CreatedAt, 
                        UpdatedAt = now 
                    };
                    
                    if (_collection.Update(configWithTimestamps))
                    {
                        count++;
                    }
                }
            }
            
            return count;
        });
    }

    /// <summary>
    /// 批量异步获取线段配置
    /// </summary>
    /// <param name="segmentIds">线段ID集合</param>
    /// <returns>线段配置集合</returns>
    public async Task<IEnumerable<ConveyorSegmentConfiguration>> BulkGetAsync(IEnumerable<long> segmentIds)
    {
        if (segmentIds == null)
        {
            throw new ArgumentNullException(nameof(segmentIds));
        }

        var idList = segmentIds.ToList();
        if (idList.Count == 0)
        {
            return Enumerable.Empty<ConveyorSegmentConfiguration>();
        }

        return await Task.Run(() =>
        {
            return _collection
                .Query()
                .Where(x => idList.Contains(x.SegmentId))
                .ToList();
        });
    }

    /// <summary>
    /// 释放数据库资源
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }
}
