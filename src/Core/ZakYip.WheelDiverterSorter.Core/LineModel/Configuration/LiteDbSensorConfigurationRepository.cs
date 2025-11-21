using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 基于LiteDB的传感器配置仓储实现
/// </summary>
public class LiteDbSensorConfigurationRepository : ISensorConfigurationRepository
{
    private readonly string _connectionString;
    private const string CollectionName = "SensorConfiguration";

    public LiteDbSensorConfigurationRepository(string databasePath)
    {
        _connectionString = $"Filename={databasePath};Connection=shared";
    }

    /// <summary>
    /// 获取传感器配置
    /// </summary>
    public SensorConfiguration Get()
    {
        using var db = new LiteDatabase(_connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection<SensorConfiguration>(CollectionName);
        
        var config = collection.FindAll().FirstOrDefault();
        if (config == null)
        {
            // 如果没有配置，返回默认配置并保存
            config = SensorConfiguration.GetDefault();
            collection.Insert(config);
        }

        return config;
    }

    /// <summary>
    /// 更新传感器配置
    /// </summary>
    public void Update(SensorConfiguration configuration)
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
        var collection = db.GetCollection<SensorConfiguration>(CollectionName);

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
        var collection = db.GetCollection<SensorConfiguration>(CollectionName);

        // 只有在集合为空时才初始化
        if (!collection.Exists(_ => true))
        {
            var defaultConfig = SensorConfiguration.GetDefault();
            collection.Insert(defaultConfig);
        }
    }
}
