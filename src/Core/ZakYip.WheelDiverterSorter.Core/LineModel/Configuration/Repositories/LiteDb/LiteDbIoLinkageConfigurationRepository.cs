using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;

/// <summary>
/// 基于 LiteDB 的 IO 联动配置仓储实现
/// </summary>
public class LiteDbIoLinkageConfigurationRepository : IIoLinkageConfigurationRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<IoLinkageConfiguration> _collection;
    private const string CollectionName = "IoLinkageConfiguration";
    private const string ConfigName = "io_linkage";

    /// <summary>
    /// 初始化 LiteDB IO 联动配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB 数据库文件路径</param>
    public LiteDbIoLinkageConfigurationRepository(string databasePath)
    {
        // 使用 Shared 模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<IoLinkageConfiguration>(CollectionName);
        
        // 为 ConfigName 字段创建唯一索引
        _collection.EnsureIndex(x => x.ConfigName, unique: true);
    }

    /// <summary>
    /// 获取 IO 联动配置
    /// </summary>
    /// <returns>IO 联动配置，如不存在则返回默认配置</returns>
    public IoLinkageConfiguration Get()
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

        return config ?? IoLinkageConfiguration.GetDefault();
    }

    /// <summary>
    /// 更新 IO 联动配置
    /// </summary>
    /// <param name="configuration">IO 联动配置</param>
    public void Update(IoLinkageConfiguration configuration)
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

        // 查找现有配置
        var existing = _collection
            .Query()
            .Where(x => x.ConfigName == ConfigName)
            .FirstOrDefault();

        if (existing != null)
        {
            // 更新现有配置，保持现有的 Id
            configuration = configuration with 
            { 
                ConfigName = ConfigName,
                Id = existing.Id 
            };
            _collection.Update(configuration);
        }
        else
        {
            // 插入新配置，ConfigName 需确保为 io_linkage
            configuration = configuration with { ConfigName = ConfigName };
            _collection.Insert(configuration);
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

        if (existing == null)
        {
            var defaultConfig = IoLinkageConfiguration.GetDefault();
            
            if (currentTime.HasValue)
            {
                defaultConfig = defaultConfig with 
                { 
                    CreatedAt = currentTime.Value,
                    UpdatedAt = currentTime.Value
                };
            }

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
