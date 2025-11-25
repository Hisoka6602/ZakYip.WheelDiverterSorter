using LiteDB;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 基于LiteDB的线体拓扑配置仓储实现
/// </summary>
public class LiteDbLineTopologyRepository : ILineTopologyRepository, IDisposable
{
    /// <summary>
    /// 默认拓扑配置ID常量
    /// </summary>
    public const string DefaultTopologyId = "default";
    
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<LineTopologyConfigEntity> _collection;
    private readonly ISystemClock _systemClock;
    private const string CollectionName = "LineTopologyConfiguration";

    /// <summary>
    /// 初始化LiteDB线体拓扑配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    /// <param name="systemClock">系统时钟</param>
    public LiteDbLineTopologyRepository(string databasePath, ISystemClock systemClock)
    {
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<LineTopologyConfigEntity>(CollectionName);
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        
        // 为TopologyId字段创建唯一索引
        _collection.EnsureIndex(x => x.TopologyId, unique: true);
    }

    /// <summary>
    /// 获取线体拓扑配置
    /// </summary>
    /// <returns>线体拓扑配置，如不存在则返回默认配置</returns>
    public LineTopologyConfig Get()
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
    /// 更新线体拓扑配置
    /// </summary>
    /// <param name="configuration">线体拓扑配置</param>
    public void Update(LineTopologyConfig configuration)
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

    private LineTopologyConfig GetDefaultConfig()
    {
        var now = _systemClock.LocalNow;
        return new LineTopologyConfig
        {
            TopologyId = DefaultTopologyId,
            TopologyName = "默认线体拓扑",
            Description = "系统默认的线体拓扑配置",
            WheelNodes = Array.Empty<WheelNodeConfig>(),
            Chutes = Array.Empty<ChuteConfig>(),
            LineSegments = Array.Empty<LineSegmentConfig>(),
            DefaultLineSpeedMmps = 500m,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static LineTopologyConfig MapToConfig(LineTopologyConfigEntity entity)
    {
        return new LineTopologyConfig
        {
            TopologyId = entity.TopologyId,
            TopologyName = entity.TopologyName,
            Description = entity.Description,
            WheelNodes = entity.WheelNodes,
            Chutes = entity.Chutes,
            LineSegments = entity.LineSegments,
            DefaultLineSpeedMmps = entity.DefaultLineSpeedMmps,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static LineTopologyConfigEntity MapToEntity(LineTopologyConfig config)
    {
        return new LineTopologyConfigEntity
        {
            TopologyId = config.TopologyId,
            TopologyName = config.TopologyName,
            Description = config.Description,
            WheelNodes = config.WheelNodes.ToList(),
            Chutes = config.Chutes.ToList(),
            LineSegments = config.LineSegments.ToList(),
            DefaultLineSpeedMmps = config.DefaultLineSpeedMmps,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    /// <summary>
    /// LiteDB存储实体
    /// </summary>
    private class LineTopologyConfigEntity
    {
        [BsonId]
        public int Id { get; set; }

        public required string TopologyId { get; set; }
        public required string TopologyName { get; set; }
        public string? Description { get; set; }
        public required List<WheelNodeConfig> WheelNodes { get; set; }
        public required List<ChuteConfig> Chutes { get; set; }
        public required List<LineSegmentConfig> LineSegments { get; set; }
        public decimal DefaultLineSpeedMmps { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
