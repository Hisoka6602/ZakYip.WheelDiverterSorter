using LiteDB;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

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
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<ChuteRouteConfiguration>(CollectionName);
        
        // 为ChuteId字段创建索引以提高查询性能
        _collection.EnsureIndex(x => x.ChuteId, unique: true);
        _collection.EnsureIndex(x => x.IsEnabled);
    }

    /// <summary>
    /// 根据格口ID获取路由配置
    /// </summary>
    public ChuteRouteConfiguration? GetByChuteId(int chuteId)
    {
        if (chuteId <= 0)
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

        // UpdatedAt 由调用者设置（通过 ISystemClock.LocalNow）
        // configuration.UpdatedAt 应该在调用此方法前已由调用者设置

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
    public bool Delete(int chuteId)
    {
        if (chuteId <= 0)
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
        //       格口2     格口4     格口6
        //         ↑         ↑         ↑
        // 入口 → 摆轮1 → 摆轮2 → 摆轮3 → 末端(默认异常口999)
        //         ↓         ↓         ↓
        //      格口1      格口3     格口5
        var defaultConfigurations = new[]
        {
            // 格口1：摆轮1右侧
            new ChuteRouteConfiguration
            {
                ChuteId = 1,
                ChuteName = "格口1（摆轮1右侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 1,
                        DiverterName = "摆轮1",
                        TargetDirection = DiverterDirection.Right,
                        SequenceNumber = 1,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    }
                },
                BeltSpeedMmPerSecond = 1500.0,
                BeltLengthMm = 5000.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口2：摆轮1左侧
            new ChuteRouteConfiguration
            {
                ChuteId = 2,
                ChuteName = "格口2（摆轮1左侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 1,
                        DiverterName = "摆轮1",
                        TargetDirection = DiverterDirection.Left,
                        SequenceNumber = 1,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    }
                },
                BeltSpeedMmPerSecond = 1500.0,
                BeltLengthMm = 5000.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口3：摆轮2右侧（需要摆轮1直行通过）
            new ChuteRouteConfiguration
            {
                ChuteId = 3,
                ChuteName = "格口3（摆轮2右侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 1,
                        DiverterName = "摆轮1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 2,
                        DiverterName = "摆轮2",
                        TargetDirection = DiverterDirection.Right,
                        SequenceNumber = 2,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    }
                },
                BeltSpeedMmPerSecond = 1500.0,
                BeltLengthMm = 10000.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口4：摆轮2左侧（需要摆轮1直行通过）
            new ChuteRouteConfiguration
            {
                ChuteId = 4,
                ChuteName = "格口4（摆轮2左侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 1,
                        DiverterName = "摆轮1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 2,
                        DiverterName = "摆轮2",
                        TargetDirection = DiverterDirection.Left,
                        SequenceNumber = 2,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    }
                },
                BeltSpeedMmPerSecond = 1500.0,
                BeltLengthMm = 10000.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口5：摆轮3右侧（需要摆轮1、2直行通过）
            new ChuteRouteConfiguration
            {
                ChuteId = 5,
                ChuteName = "格口5（摆轮3右侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 1,
                        DiverterName = "摆轮1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 2,
                        DiverterName = "摆轮2",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 2,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 3,
                        DiverterName = "摆轮3",
                        TargetDirection = DiverterDirection.Right,
                        SequenceNumber = 3,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    }
                },
                BeltSpeedMmPerSecond = 1500.0,
                BeltLengthMm = 15000.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 格口6：摆轮3左侧（需要摆轮1、2直行通过）
            new ChuteRouteConfiguration
            {
                ChuteId = 6,
                ChuteName = "格口6（摆轮3左侧）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 1,
                        DiverterName = "摆轮1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 2,
                        DiverterName = "摆轮2",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 2,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 3,
                        DiverterName = "摆轮3",
                        TargetDirection = DiverterDirection.Left,
                        SequenceNumber = 3,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    }
                },
                BeltSpeedMmPerSecond = 1500.0,
                BeltLengthMm = 15000.0,
                ToleranceTimeMs = 2000,
                IsEnabled = true
            },
            // 异常格口999：所有摆轮直行通过到末端
            new ChuteRouteConfiguration
            {
                ChuteId = 999,
                ChuteName = "异常格口（末端）",
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 1,
                        DiverterName = "摆轮1",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 1,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 2,
                        DiverterName = "摆轮2",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 2,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    },
                    new DiverterConfigurationEntry
                    {
                        DiverterId = 3,
                        DiverterName = "摆轮3",
                        TargetDirection = DiverterDirection.Straight,
                        SequenceNumber = 3,
                        SegmentLengthMm = 5000.0,
                        SegmentSpeedMmPerSecond = 1500.0,
                        SegmentToleranceTimeMs = 2000
                    }
                },
                BeltSpeedMmPerSecond = 1500.0,
                BeltLengthMm = 20000.0,
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
