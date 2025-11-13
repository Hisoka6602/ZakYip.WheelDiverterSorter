using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 基于LiteDB的驱动器配置仓储实现
/// </summary>
public class LiteDbDriverConfigurationRepository : IDriverConfigurationRepository
{
    private readonly string _connectionString;
    private const string CollectionName = "DriverConfiguration";

    public LiteDbDriverConfigurationRepository(string databasePath)
    {
        _connectionString = $"Filename={databasePath};Connection=shared";
    }

    /// <summary>
    /// 获取驱动器配置
    /// </summary>
    public DriverConfiguration Get()
    {
        using var db = new LiteDatabase(_connectionString);
        var collection = db.GetCollection<DriverConfiguration>(CollectionName);
        
        var config = collection.FindAll().FirstOrDefault();
        if (config == null)
        {
            // 如果没有配置，返回默认配置并保存
            config = DriverConfiguration.GetDefault();
            collection.Insert(config);
        }

        return config;
    }

    /// <summary>
    /// 更新驱动器配置
    /// </summary>
    public void Update(DriverConfiguration configuration)
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

        configuration.UpdatedAt = DateTime.UtcNow;
        configuration.Version++;

        using var db = new LiteDatabase(_connectionString);
        var collection = db.GetCollection<DriverConfiguration>(CollectionName);

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
        using var db = new LiteDatabase(_connectionString);
        var collection = db.GetCollection<DriverConfiguration>(CollectionName);

        // 只有在集合为空时才初始化
        if (!collection.Exists(_ => true))
        {
            var defaultConfig = DriverConfiguration.GetDefault();
            collection.Insert(defaultConfig);
        }
    }
}
