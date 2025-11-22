using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 基于LiteDB的摆轮硬件绑定配置仓储实现
/// </summary>
public class LiteDbWheelBindingsRepository : IWheelBindingsRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<WheelBindingsConfig> _collection;
    private const string CollectionName = "WheelBindingsConfiguration";
    private const string DefaultConfigName = "wheel-bindings";

    /// <summary>
    /// 初始化LiteDB摆轮硬件绑定配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    public LiteDbWheelBindingsRepository(string databasePath)
    {
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<WheelBindingsConfig>(CollectionName);
        
        // 为ConfigName字段创建唯一索引
        _collection.EnsureIndex(x => x.ConfigName, unique: true);
    }

    /// <summary>
    /// 获取摆轮硬件绑定配置
    /// </summary>
    public WheelBindingsConfig Get()
    {
        var config = _collection
            .Query()
            .Where(x => x.ConfigName == DefaultConfigName)
            .FirstOrDefault();

        if (config == null)
        {
            // 如果不存在，初始化默认配置并返回
            InitializeDefault();
            config = _collection
                .Query()
                .Where(x => x.ConfigName == DefaultConfigName)
                .FirstOrDefault();
        }

        return config ?? GetDefaultConfig();
    }

    /// <summary>
    /// 更新摆轮硬件绑定配置
    /// </summary>
    public void Update(WheelBindingsConfig configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration.ConfigName = DefaultConfigName;
        // UpdatedAt 由调用者设置（通过 ISystemClock.LocalNow）

        // 查找现有配置
        var existing = _collection
            .Query()
            .Where(x => x.ConfigName == DefaultConfigName)
            .FirstOrDefault();

        if (existing != null)
        {
            // 更新现有配置，保留Id和CreatedAt
            configuration.Id = existing.Id;
            configuration.CreatedAt = existing.CreatedAt;
            _collection.Update(configuration);
        }
        else
        {
            // 插入新配置
            _collection.Insert(configuration);
        }
    }

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    public void InitializeDefault(DateTime? currentTime = null)
    {
        var existing = _collection
            .Query()
            .Where(x => x.ConfigName == DefaultConfigName)
            .FirstOrDefault();

        if (existing == null)
        {
            var now = currentTime ?? DateTime.UtcNow;
            var defaultConfig = GetDefaultConfig();
            defaultConfig.CreatedAt = now;
            defaultConfig.UpdatedAt = now;
            _collection.Insert(defaultConfig);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }

    private static WheelBindingsConfig GetDefaultConfig()
    {
        return new WheelBindingsConfig
        {
            ConfigName = DefaultConfigName,
            Bindings = new List<WheelHardwareBinding>(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }
}
