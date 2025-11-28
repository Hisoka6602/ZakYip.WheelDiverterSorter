using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;

/// <summary>
/// 基于LiteDB的摆轮配置仓储实现
/// </summary>
public class LiteDbWheelDiverterConfigurationRepository : IWheelDiverterConfigurationRepository
{
    private readonly string _connectionString;
    private const string CollectionName = "WheelDiverterConfiguration";

    public LiteDbWheelDiverterConfigurationRepository(string databasePath)
    {
        _connectionString = $"Filename={databasePath};Connection=shared";
    }

    /// <summary>
    /// 获取摆轮配置
    /// </summary>
    public WheelDiverterConfiguration Get()
    {
        using var db = new LiteDatabase(_connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection<WheelDiverterConfiguration>(CollectionName);
        
        var config = collection.FindAll().FirstOrDefault();
        if (config == null)
        {
            // 如果没有配置，返回默认配置并保存
            config = WheelDiverterConfiguration.GetDefault();
            collection.Insert(config);
        }

        return config;
    }

    /// <summary>
    /// 更新摆轮配置
    /// </summary>
    public void Update(WheelDiverterConfiguration configuration)
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
        configuration.Version++;

        using var db = new LiteDatabase(_connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection<WheelDiverterConfiguration>(CollectionName);

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
        var collection = db.GetCollection<WheelDiverterConfiguration>(CollectionName);

        // 只有在集合为空时才初始化
        if (!collection.Exists(_ => true))
        {
            var defaultConfig = WheelDiverterConfiguration.GetDefault();
            collection.Insert(defaultConfig);
        }
    }
}
