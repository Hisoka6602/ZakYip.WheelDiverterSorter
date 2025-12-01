using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

/// <summary>
/// 基于LiteDB的通信配置仓储实现
/// </summary>
public class LiteDbCommunicationConfigurationRepository : ICommunicationConfigurationRepository
{
    private readonly string _connectionString;
    private const string CollectionName = "CommunicationConfiguration";

    public LiteDbCommunicationConfigurationRepository(string databasePath)
    {
        _connectionString = $"Filename={databasePath};Connection=shared";
    }

    /// <summary>
    /// 获取通信配置
    /// </summary>
    public CommunicationConfiguration Get()
    {
        using var db = new LiteDatabase(_connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection<CommunicationConfiguration>(CollectionName);
        
        var config = collection.FindAll().FirstOrDefault();
        if (config == null)
        {
            // 如果没有配置，返回默认配置并保存
            config = CommunicationConfiguration.GetDefault();
            collection.Insert(config);
        }

        return config;
    }

    /// <summary>
    /// 更新通信配置
    /// </summary>
    public void Update(CommunicationConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var (isValid, errorMessage) = configuration.Validate();
        if (!isValid)
        {
            throw new ArgumentException(errorMessage);
        }

        // UpdatedAt 由调用者设置（通过 ISystemClock.LocalNow）
        // configuration.UpdatedAt 应该在调用此方法前已由调用者设置
        configuration.Version++;

        using var db = new LiteDatabase(_connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection<CommunicationConfiguration>(CollectionName);

        if (configuration.Id == 0)
        {
            // 新配置，删除旧的并插入新的
            collection.DeleteAll();
            collection.Insert(configuration);
        }
        else
        {
            // 更新现有配置
            collection.Update(configuration);
        }
    }

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    public void InitializeDefault()
    {
        using var db = new LiteDatabase(_connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection<CommunicationConfiguration>(CollectionName);

        // 只有在集合为空时才初始化
        if (!collection.Exists(_ => true))
        {
            var defaultConfig = CommunicationConfiguration.GetDefault();
            collection.Insert(defaultConfig);
        }
    }
}
