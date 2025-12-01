using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

/// <summary>
/// 基于LiteDB的面板配置仓储实现
/// </summary>
public class LiteDbPanelConfigurationRepository : IPanelConfigurationRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<PanelConfiguration> _collection;
    private const string CollectionName = "PanelConfiguration";
    private const string PanelConfigName = "panel";

    /// <summary>
    /// 初始化LiteDB面板配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    public LiteDbPanelConfigurationRepository(string databasePath)
    {
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<PanelConfiguration>(CollectionName);
        
        // 为ConfigName字段创建唯一索引
        _collection.EnsureIndex(x => x.ConfigName, unique: true);
    }

    /// <summary>
    /// 获取面板配置
    /// </summary>
    /// <returns>面板配置，如不存在则返回默认配置</returns>
    public PanelConfiguration Get()
    {
        var config = _collection
            .Query()
            .Where(x => x.ConfigName == PanelConfigName)
            .FirstOrDefault();

        if (config == null)
        {
            // 如果不存在，初始化默认配置并返回
            InitializeDefault();
            config = _collection
                .Query()
                .Where(x => x.ConfigName == PanelConfigName)
                .FirstOrDefault();
        }

        return config ?? PanelConfiguration.GetDefault();
    }

    /// <summary>
    /// 更新面板配置
    /// </summary>
    /// <param name="configuration">面板配置</param>
    public void Update(PanelConfiguration configuration)
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
            .Where(x => x.ConfigName == PanelConfigName)
            .FirstOrDefault();

        if (existing != null)
        {
            // 更新现有配置，保持现有的Id
            configuration = configuration with 
            { 
                ConfigName = PanelConfigName,
                Id = existing.Id 
            };
            _collection.Update(configuration);
        }
        else
        {
            // 插入新配置，ConfigName需确保为panel
            configuration = configuration with { ConfigName = PanelConfigName };
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
            .Where(x => x.ConfigName == PanelConfigName)
            .FirstOrDefault();

        if (existing == null)
        {
            var defaultConfig = PanelConfiguration.GetDefault();
            
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
