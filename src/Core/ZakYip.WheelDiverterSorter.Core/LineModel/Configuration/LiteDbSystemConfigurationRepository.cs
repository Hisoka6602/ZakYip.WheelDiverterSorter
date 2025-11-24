using LiteDB;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 基于LiteDB的系统配置仓储实现
/// </summary>
public class LiteDbSystemConfigurationRepository : ISystemConfigurationRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<SystemConfiguration> _collection;
    private readonly ISystemClock _systemClock;
    private const string CollectionName = "SystemConfiguration";
    private const string SystemConfigName = "system";

    /// <summary>
    /// 初始化LiteDB系统配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    /// <param name="systemClock">系统时钟</param>
    public LiteDbSystemConfigurationRepository(string databasePath, ISystemClock systemClock)
    {
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<SystemConfiguration>(CollectionName);
        
        // 为ConfigName字段创建唯一索引
        _collection.EnsureIndex(x => x.ConfigName, unique: true);
    }

    /// <summary>
    /// 获取系统配置
    /// </summary>
    /// <returns>系统配置，如不存在则返回默认配置</returns>
    public SystemConfiguration Get()
    {
        var config = _collection
            .Query()
            .Where(x => x.ConfigName == SystemConfigName)
            .FirstOrDefault();

        if (config == null)
        {
            // 如果不存在，初始化默认配置并返回
            InitializeDefault();
            config = _collection
                .Query()
                .Where(x => x.ConfigName == SystemConfigName)
                .FirstOrDefault();
        }

        return config ?? SystemConfiguration.GetDefault();
    }

    /// <summary>
    /// 更新系统配置
    /// </summary>
    /// <param name="configuration">系统配置</param>
    public void Update(SystemConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // 验证配置
        var (isValid, errorMessage) = configuration.Validate();
        if (!isValid)
        {
            throw new ArgumentException(errorMessage, nameof(configuration));
        }

        // 确保ConfigName为system
        configuration.ConfigName = SystemConfigName;
        // UpdatedAt 由调用者设置（通过 ISystemClock.LocalNow）
        // configuration.UpdatedAt 应该在调用此方法前已由调用者设置

        // 查找现有配置
        var existing = _collection
            .Query()
            .Where(x => x.ConfigName == SystemConfigName)
            .FirstOrDefault();

        if (existing != null)
        {
            // 更新现有配置，保留原有ID和创建时间，增加版本号
            configuration.Id = existing.Id;
            configuration.CreatedAt = existing.CreatedAt;
            configuration.Version = existing.Version + 1;
            _collection.Update(configuration);
        }
        else
        {
            // 插入新配置
            configuration.Version = 1;
            _collection.Insert(configuration);
        }
    }

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    public void InitializeDefault(DateTime? currentTime = null)
    {
        // 检查是否已有配置
        var existing = _collection
            .Query()
            .Where(x => x.ConfigName == SystemConfigName)
            .FirstOrDefault();

        if (existing == null)
        {
            var defaultConfig = SystemConfiguration.GetDefault();
            // 如果提供了当前时间，则使用；否则使用系统时钟的本地时间作为持久化时间戳
            var now = currentTime ?? _systemClock.LocalNow;
            defaultConfig.CreatedAt = now;
            defaultConfig.UpdatedAt = now;
            _collection.Insert(defaultConfig);
        }
    }

    /// <summary>
    /// 释放数据库资源
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }
}
