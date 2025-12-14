using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

/// <summary>
/// 基于LiteDB的包裹丢失检测配置仓储实现
/// </summary>
public class LiteDbParcelLossDetectionConfigurationRepository : IParcelLossDetectionConfigurationRepository
{
    private readonly string _connectionString;
    private const string CollectionName = "ParcelLossDetectionConfiguration";

    public LiteDbParcelLossDetectionConfigurationRepository(string databasePath)
    {
        _connectionString = $"Filename={databasePath};Connection=shared";
    }

    /// <summary>
    /// 获取包裹丢失检测配置
    /// </summary>
    public ParcelLossDetectionConfiguration Get()
    {
        using var db = new LiteDatabase(_connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection<ParcelLossDetectionConfiguration>(CollectionName);
        
        var config = collection.FindAll().FirstOrDefault();
        if (config == null)
        {
            // 如果没有配置，返回默认配置并保存
            config = ParcelLossDetectionConfiguration.GetDefault();
            collection.Insert(config);
        }

        return config;
    }

    /// <summary>
    /// 更新包裹丢失检测配置
    /// </summary>
    public void Update(ParcelLossDetectionConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // UpdatedAt 由调用者设置（通过 ISystemClock.LocalNow）
        configuration.Version++;

        using var db = new LiteDatabase(_connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection<ParcelLossDetectionConfiguration>(CollectionName);

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
        var collection = db.GetCollection<ParcelLossDetectionConfiguration>(CollectionName);

        // 只有在集合为空时才初始化
        if (!collection.Exists(_ => true))
        {
            var defaultConfig = ParcelLossDetectionConfiguration.GetDefault();
            collection.Insert(defaultConfig);
        }
    }
}
