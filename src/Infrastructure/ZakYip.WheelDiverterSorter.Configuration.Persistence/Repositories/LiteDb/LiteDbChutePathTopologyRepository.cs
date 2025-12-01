using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

/// <summary>
/// 基于LiteDB的格口路径拓扑配置仓储实现
/// </summary>
public class LiteDbChutePathTopologyRepository : IChutePathTopologyRepository, IDisposable
{
    /// <summary>
    /// 默认拓扑配置ID常量
    /// </summary>
    public const string DefaultTopologyId = "default";
    
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<ChutePathConfigStorageEntity> _collection;
    private readonly ISystemClock _systemClock;
    private const string CollectionName = "ChutePathTopologyConfiguration";

    /// <summary>
    /// 初始化LiteDB格口路径拓扑配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    /// <param name="systemClock">系统时钟</param>
    public LiteDbChutePathTopologyRepository(string databasePath, ISystemClock systemClock)
    {
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<ChutePathConfigStorageEntity>(CollectionName);
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        
        // 为TopologyId字段创建唯一索引
        _collection.EnsureIndex(x => x.TopologyId, unique: true);
    }

    /// <summary>
    /// 获取格口路径拓扑配置
    /// </summary>
    /// <returns>格口路径拓扑配置，如不存在则返回默认配置</returns>
    public ChutePathTopologyConfig Get()
    {
        var entity = _collection
            .Query()
            .Where(x => x.TopologyId == DefaultTopologyId)
            .FirstOrDefault();

        if (entity == null)
        {
            // 如果不存在，初始化默认配置并返回
            InitializeDefault();
            entity = _collection
                .Query()
                .Where(x => x.TopologyId == DefaultTopologyId)
                .FirstOrDefault();
        }

        return entity != null ? MapToConfig(entity) : GetDefaultConfig();
    }

    /// <summary>
    /// 更新格口路径拓扑配置
    /// </summary>
    /// <param name="configuration">格口路径拓扑配置</param>
    public void Update(ChutePathTopologyConfig configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var entity = MapToEntity(configuration);
        // UpdatedAt 由调用者设置（通过 ISystemClock.LocalNow）

        // 查找现有配置
        var existing = _collection
            .Query()
            .Where(x => x.TopologyId == DefaultTopologyId)
            .FirstOrDefault();

        if (existing != null)
        {
            // 更新现有配置，保留Id和CreatedAt
            entity.Id = existing.Id;
            entity.CreatedAt = existing.CreatedAt;
            _collection.Update(entity);
        }
        else
        {
            // 插入新配置
            _collection.Insert(entity);
        }
    }

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    public void InitializeDefault(DateTime? currentTime = null)
    {
        var existing = _collection
            .Query()
            .Where(x => x.TopologyId == DefaultTopologyId)
            .FirstOrDefault();

        if (existing == null)
        {
            var now = currentTime ?? _systemClock.LocalNow;
            var defaultConfig = GetDefaultConfig();
            var entity = MapToEntity(defaultConfig);
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            _collection.Insert(entity);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }

    private ChutePathTopologyConfig GetDefaultConfig()
    {
        var now = _systemClock.LocalNow;
        return new ChutePathTopologyConfig
        {
            TopologyId = DefaultTopologyId,
            TopologyName = "默认格口路径拓扑",
            Description = "系统默认的格口路径拓扑配置",
            EntrySensorId = 1, // 引用默认的创建包裹感应IO
            DiverterNodes = Array.Empty<DiverterPathNode>(),
            ExceptionChuteId = 999, // 默认异常格口ID
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static ChutePathTopologyConfig MapToConfig(ChutePathConfigStorageEntity entity)
    {
        return new ChutePathTopologyConfig
        {
            TopologyId = entity.TopologyId,
            TopologyName = entity.TopologyName,
            Description = entity.Description,
            EntrySensorId = entity.EntrySensorId,
            DiverterNodes = entity.DiverterNodes,
            ExceptionChuteId = entity.ExceptionChuteId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static ChutePathConfigStorageEntity MapToEntity(ChutePathTopologyConfig config)
    {
        return new ChutePathConfigStorageEntity
        {
            TopologyId = config.TopologyId,
            TopologyName = config.TopologyName,
            Description = config.Description,
            EntrySensorId = config.EntrySensorId,
            DiverterNodes = config.DiverterNodes.ToList(),
            ExceptionChuteId = config.ExceptionChuteId,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    /// <summary>
    /// LiteDB存储实体
    /// </summary>
    private class ChutePathConfigStorageEntity
    {
        [BsonId]
        public int Id { get; set; }

        public required string TopologyId { get; set; }
        public required string TopologyName { get; set; }
        public string? Description { get; set; }
        public required long EntrySensorId { get; set; }
        public required List<DiverterPathNode> DiverterNodes { get; set; }
        public required long ExceptionChuteId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
