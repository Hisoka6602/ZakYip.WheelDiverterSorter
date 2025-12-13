using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

/// <summary>
/// 基于LiteDB的落格回调配置仓储实现
/// </summary>
public class LiteDbChuteDropoffCallbackConfigurationRepository : IChuteDropoffCallbackConfigurationRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<ChuteDropoffCallbackConfiguration> _collection;
    private readonly ISystemClock _systemClock;
    private const string CollectionName = "ChuteDropoffCallbackConfiguration";
    private const string ConfigName = "chute_dropoff_callback";

    /// <summary>
    /// 初始化LiteDB落格回调配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    /// <param name="systemClock">系统时钟</param>
    public LiteDbChuteDropoffCallbackConfigurationRepository(string databasePath, ISystemClock systemClock)
    {
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<ChuteDropoffCallbackConfiguration>(CollectionName);
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        
        // 为ConfigName字段创建唯一索引
        _collection.EnsureIndex(x => x.ConfigName, unique: true);
    }

    /// <summary>
    /// 获取落格回调配置
    /// </summary>
    /// <returns>落格回调配置，如不存在则返回默认配置</returns>
    public ChuteDropoffCallbackConfiguration Get()
    {
        var config = _collection
            .Query()
            .Where(x => x.ConfigName == ConfigName)
            .FirstOrDefault();

        if (config == null)
        {
            // 如果不存在，初始化默认配置并返回
            InitializeDefault();
            config = _collection
                .Query()
                .Where(x => x.ConfigName == ConfigName)
                .FirstOrDefault();
        }

        return config ?? ChuteDropoffCallbackConfiguration.GetDefault();
    }

    /// <summary>
    /// 更新落格回调配置
    /// </summary>
    /// <param name="configuration">落格回调配置</param>
    public void Update(ChuteDropoffCallbackConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var now = _systemClock.LocalNow;

        // 查找现有配置
        var existing = _collection
            .Query()
            .Where(x => x.ConfigName == ConfigName)
            .FirstOrDefault();

        ChuteDropoffCallbackConfiguration updatedConfig;
        
        if (existing != null)
        {
            // 更新现有配置，保留原有ID和创建时间
            updatedConfig = configuration with
            {
                Id = existing.Id,
                ConfigName = ConfigName,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = now
            };
            _collection.Update(updatedConfig);
        }
        else
        {
            // 插入新配置
            updatedConfig = configuration with
            {
                ConfigName = ConfigName,
                CreatedAt = now,
                UpdatedAt = now
            };
            _collection.Insert(updatedConfig);
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
            .Where(x => x.ConfigName == ConfigName)
            .FirstOrDefault();

        if (existing != null)
        {
            return; // 已存在配置，无需初始化
        }

        var now = currentTime ?? _systemClock.LocalNow;
        var defaultConfig = ChuteDropoffCallbackConfiguration.GetDefault() with
        {
            ConfigName = ConfigName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _collection.Insert(defaultConfig);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }
}
