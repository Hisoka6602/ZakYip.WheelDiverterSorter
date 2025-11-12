using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 基于LiteDB的路由配置仓储实现
/// </summary>
public class LiteDbRouteConfigurationRepository : IRouteConfigurationRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<ChuteRouteConfiguration> _collection;
    private const string CollectionName = "ChuteRoutes";

    /// <summary>
    /// 初始化LiteDB路由配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    public LiteDbRouteConfigurationRepository(string databasePath)
    {
        _database = new LiteDatabase(databasePath);
        _collection = _database.GetCollection<ChuteRouteConfiguration>(CollectionName);
        
        // 为ChuteId字段创建索引以提高查询性能
        _collection.EnsureIndex(x => x.ChuteId, unique: true);
        _collection.EnsureIndex(x => x.IsEnabled);
    }

    /// <summary>
    /// 根据格口ID获取路由配置
    /// </summary>
    public ChuteRouteConfiguration? GetByChuteId(string chuteId)
    {
        if (string.IsNullOrWhiteSpace(chuteId))
        {
            return null;
        }

        return _collection
            .Query()
            .Where(x => x.ChuteId == chuteId && x.IsEnabled)
            .FirstOrDefault();
    }

    /// <summary>
    /// 获取所有启用的路由配置
    /// </summary>
    public IEnumerable<ChuteRouteConfiguration> GetAllEnabled()
    {
        return _collection
            .Query()
            .Where(x => x.IsEnabled)
            .ToList();
    }

    /// <summary>
    /// 添加或更新路由配置
    /// </summary>
    public void Upsert(ChuteRouteConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration.UpdatedAt = DateTime.UtcNow;

        // 查找现有配置
        var existing = _collection
            .Query()
            .Where(x => x.ChuteId == configuration.ChuteId)
            .FirstOrDefault();

        if (existing != null)
        {
            // 更新现有配置，保留原有ID和创建时间
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
    /// 删除指定格口的路由配置
    /// </summary>
    public bool Delete(string chuteId)
    {
        if (string.IsNullOrWhiteSpace(chuteId))
        {
            return false;
        }

        var config = _collection
            .Query()
            .Where(x => x.ChuteId == chuteId)
            .FirstOrDefault();

        if (config != null)
        {
            return _collection.Delete(config.Id);
        }

        return false;
    }

    /// <summary>
    /// 初始化默认配置数据
    /// </summary>
    /// <remarks>
    /// 如果数据库为空，则插入默认的示例配置。
    /// 这些配置与原始硬编码映射保持一致，确保向后兼容性。
    /// </remarks>
    public void InitializeDefaultData()
    {
        // 检查是否已有数据
        if (_collection.Count() > 0)
        {
            return;
        }

        // 插入默认配置，与原始硬编码映射保持一致
        var defaultConfigurations = new[]
        {
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_A",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetAngle = DiverterAngle.Angle30,
                        SequenceNumber = 1
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D2",
                        TargetAngle = DiverterAngle.Angle45,
                        SequenceNumber = 2
                    }
                },
                IsEnabled = true
            },
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_B",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetAngle = DiverterAngle.Angle0,
                        SequenceNumber = 1
                    }
                },
                IsEnabled = true
            },
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_C",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetAngle = DiverterAngle.Angle90,
                        SequenceNumber = 1
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D3",
                        TargetAngle = DiverterAngle.Angle30,
                        SequenceNumber = 2
                    }
                },
                IsEnabled = true
            }
        };

        _collection.InsertBulk(defaultConfigurations);
    }

    /// <summary>
    /// 释放数据库资源
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }
}
