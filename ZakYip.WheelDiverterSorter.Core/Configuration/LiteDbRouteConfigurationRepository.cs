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
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString);
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
    /// 这些配置基于直线拓扑结构：
    /// 入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(默认异常口)
    /// 每个摆轮左侧和右侧各有一个格口
    /// </remarks>
    public void InitializeDefaultData()
    {
        // 检查是否已有数据
        if (_collection.Count() > 0)
        {
            return;
        }

        // 插入默认配置，基于直线拓扑结构
        // 拓扑结构（从摆轮视角，上方为左侧，下方为右侧）：
        //       格口B     格口D     格口F
        //         ↑         ↑         ↑
        // 入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(默认异常口)
        //         ↓         ↓         ↓
        //      格口A      格口C     格口E
        var defaultConfigurations = new[]
        {
            // 格口A：摆轮D1右侧
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_A",
                ChuteName = "格口A（D1右侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetDirection = DiverterDirection.Right,
                        SequenceNumber = 1
                    }
                },
                BeltSpeedMeterPerSecond = 1.5,
                BeltLengthMeter = 5.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口B：摆轮D1左侧
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_B",
                ChuteName = "格口B（D1左侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetDirection = DiverterDirection.Left,
                        SequenceNumber = 1
                    }
                },
                BeltSpeedMeterPerSecond = 1.5,
                BeltLengthMeter = 5.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口C：摆轮D2右侧（需要D1直行通过）
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_C",
                ChuteName = "格口C（D2右侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D2",
                        TargetDirection = DiverterDirection.Right,
                        SequenceNumber = 2
                    }
                },
                BeltSpeedMeterPerSecond = 1.5,
                BeltLengthMeter = 10.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口D：摆轮D2左侧（需要D1直行通过）
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_D",
                ChuteName = "格口D（D2左侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D2",
                        TargetDirection = DiverterDirection.Left,
                        SequenceNumber = 2
                    }
                },
                BeltSpeedMeterPerSecond = 1.5,
                BeltLengthMeter = 10.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口E：摆轮D3右侧（需要D1、D2直行通过）
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_E",
                ChuteName = "格口E（D3右侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D2",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 2
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D3",
                        TargetDirection = DiverterDirection.Right,
                        SequenceNumber = 3
                    }
                },
                BeltSpeedMeterPerSecond = 1.5,
                BeltLengthMeter = 15.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口F：摆轮D3左侧（需要D1、D2直行通过）
            new ChuteRouteConfiguration
            {
                ChuteId = "CHUTE_F",
                ChuteName = "格口F（D3左侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D2",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 2
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = "D3",
                        TargetDirection = DiverterDirection.Left,
                        SequenceNumber = 3
                    }
                },
                BeltSpeedMeterPerSecond = 1.5,
                BeltLengthMeter = 15.0,
                ToleranceTimeMs = 2000,
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
