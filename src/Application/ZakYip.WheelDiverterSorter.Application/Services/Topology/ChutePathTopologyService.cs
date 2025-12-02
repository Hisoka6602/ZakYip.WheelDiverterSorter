using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Application.Services.Topology;

/// <summary>
/// Application 层格口路径拓扑服务实现
/// </summary>
/// <remarks>
/// 委托 Core 层的仓储执行实际的数据访问操作，并提供业务验证逻辑。
/// 此服务作为 Host 层与 Core 层之间的桥梁。
/// 支持配置缓存与热更新：当拓扑更新时，自动清除相关路径缓存。
/// </remarks>
public class ChutePathTopologyService : IChutePathTopologyService
{
    private readonly IChutePathTopologyRepository _topologyRepository;
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISlidingConfigCache _configCache;
    private readonly IPathCacheManager? _pathCacheManager;

    private static readonly object TopologyCacheKey = new();

    /// <summary>
    /// 初始化格口路径拓扑服务
    /// </summary>
    /// <param name="topologyRepository">拓扑配置仓储</param>
    /// <param name="sensorRepository">传感器配置仓储</param>
    /// <param name="pathGenerator">摆轮路径生成器</param>
    /// <param name="configCache">统一滑动配置缓存</param>
    /// <param name="pathCacheManager">路径缓存管理器（可选，用于清除路径缓存）</param>
    public ChutePathTopologyService(
        IChutePathTopologyRepository topologyRepository,
        ISensorConfigurationRepository sensorRepository,
        ISwitchingPathGenerator pathGenerator,
        ISlidingConfigCache configCache,
        IPathCacheManager? pathCacheManager = null)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _sensorRepository = sensorRepository ?? throw new ArgumentNullException(nameof(sensorRepository));
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _pathCacheManager = pathCacheManager;
    }

    /// <inheritdoc />
    public ChutePathTopologyConfig GetTopology()
    {
        return _configCache.GetOrAdd(TopologyCacheKey, () => _topologyRepository.Get());
    }

    /// <inheritdoc />
    public void UpdateTopology(ChutePathTopologyConfig config)
    {
        // 验证配置
        var validationResult = ChutePathTopologyValidator.Validate(config);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"拓扑配置验证失败: {validationResult.ErrorMessage}");
        }

        // 获取旧配置中的所有格口ID，用于清除路径缓存
        var oldConfig = _topologyRepository.Get();
        var oldChuteIds = CollectAllChuteIds(oldConfig);

        // 更新配置到持久化
        _topologyRepository.Update(config);

        // 热更新：立即刷新拓扑缓存
        var updatedConfig = _topologyRepository.Get();
        _configCache.Set(TopologyCacheKey, updatedConfig);

        // 热更新：清除所有受影响的路径缓存
        var newChuteIds = CollectAllChuteIds(updatedConfig);
        var allAffectedChuteIds = oldChuteIds.Union(newChuteIds).ToList();

        if (_pathCacheManager != null && allAffectedChuteIds.Count > 0)
        {
            _pathCacheManager.InvalidateAllCache(allAffectedChuteIds);
        }
    }

    /// <summary>
    /// 收集拓扑配置中的所有格口ID
    /// </summary>
    private static HashSet<long> CollectAllChuteIds(ChutePathTopologyConfig config)
    {
        var chuteIds = new HashSet<long>();

        if (config.DiverterNodes != null)
        {
            foreach (var node in config.DiverterNodes)
            {
                if (node.LeftChuteIds != null)
                {
                    foreach (var id in node.LeftChuteIds)
                    {
                        chuteIds.Add(id);
                    }
                }
                if (node.RightChuteIds != null)
                {
                    foreach (var id in node.RightChuteIds)
                    {
                        chuteIds.Add(id);
                    }
                }
            }
        }

        return chuteIds;
    }

    /// <inheritdoc />
    public (bool IsValid, string? ErrorMessage) ValidateTopologyRequest(
        long entrySensorId,
        IReadOnlyList<DiverterPathNode> diverterNodes,
        long exceptionChuteId)
    {
        // 验证摆轮节点不能为空
        if (diverterNodes.Count == 0)
        {
            return (false, "至少需要配置一个摆轮节点 - At least one diverter node is required");
        }

        // 获取已配置的感应IO列表用于验证
        var sensorConfig = _sensorRepository.Get();
        var configuredSensorIds = sensorConfig.Sensors?.Select(s => s.SensorId).ToHashSet() ?? new HashSet<long>();

        // 验证入口传感器ID
        if (!configuredSensorIds.Contains(entrySensorId))
        {
            return (false, $"入口传感器ID ({entrySensorId}) 未配置，请先在感应IO配置中添加");
        }

        // 验证入口传感器类型必须是 ParcelCreation
        var entrySensor = sensorConfig.Sensors?.FirstOrDefault(s => s.SensorId == entrySensorId);
        if (entrySensor != null && entrySensor.IoType != SensorIoType.ParcelCreation)
        {
            return (false, $"入口传感器ID ({entrySensorId}) 类型必须是 ParcelCreation，当前类型为 {entrySensor.IoType}");
        }

        // 验证每个摆轮节点
        var allChuteIds = new HashSet<long>();
        var allDiverterIds = new HashSet<long>();
        var allSegmentIds = new HashSet<long>();

        foreach (var node in diverterNodes)
        {
            // 验证摆轮ID不重复
            if (!allDiverterIds.Add(node.DiverterId))
            {
                return (false, $"摆轮ID {node.DiverterId} 重复配置 - Duplicate diverter ID");
            }

            // 验证摆轮前感应IO（必须配置）
            if (!configuredSensorIds.Contains(node.FrontSensorId))
            {
                return (false, $"摆轮节点 {node.DiverterId} 的摆轮前感应IO ({node.FrontSensorId}) 未配置，请先在感应IO配置中添加");
            }

            // 验证类型必须是 WheelFront
            var frontSensor = sensorConfig.Sensors?.FirstOrDefault(s => s.SensorId == node.FrontSensorId);
            if (frontSensor != null && frontSensor.IoType != SensorIoType.WheelFront)
            {
                return (false, $"摆轮节点 {node.DiverterId} 的摆轮前感应IO ({node.FrontSensorId}) 类型必须是 WheelFront，当前类型为 {frontSensor.IoType}");
            }

            // 验证至少有一侧有格口
            var leftCount = node.LeftChuteIds?.Count ?? 0;
            var rightCount = node.RightChuteIds?.Count ?? 0;
            if (leftCount == 0 && rightCount == 0)
            {
                return (false, $"摆轮节点 {node.DiverterId} 必须至少配置一侧格口");
            }

            // 收集所有格口ID用于后续验证
            if (node.LeftChuteIds != null)
            {
                foreach (var chuteId in node.LeftChuteIds)
                {
                    if (!allChuteIds.Add(chuteId))
                    {
                        return (false, $"格口ID {chuteId} 在多个摆轮节点中重复配置 - Duplicate chute ID");
                    }
                }
            }
            if (node.RightChuteIds != null)
            {
                foreach (var chuteId in node.RightChuteIds)
                {
                    if (!allChuteIds.Add(chuteId))
                    {
                        return (false, $"格口ID {chuteId} 在多个摆轮节点中重复配置 - Duplicate chute ID");
                    }
                }
            }

            // 验证线体段ID不重复
            if (!allSegmentIds.Add(node.SegmentId))
            {
                return (false, $"线体段ID {node.SegmentId} 在多个摆轮节点中重复配置 - Duplicate segment ID");
            }
        }

        // 验证异常格口不能与普通格口重复
        if (allChuteIds.Contains(exceptionChuteId))
        {
            return (false, $"异常格口ID ({exceptionChuteId}) 不能与普通格口重复 - Exception chute ID cannot duplicate with normal chutes");
        }

        // 验证摆轮节点的位置索引不能重复
        var duplicatePositions = diverterNodes
            .GroupBy(n => n.PositionIndex)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatePositions.Any())
        {
            return (false, $"摆轮节点位置索引重复: {string.Join(", ", duplicatePositions)}");
        }

        // 验证位置索引是否连续（从1开始）
        var sortedPositions = diverterNodes.Select(n => n.PositionIndex).OrderBy(p => p).ToList();
        for (int i = 0; i < sortedPositions.Count; i++)
        {
            if (sortedPositions[i] != i + 1)
            {
                return (false, $"摆轮节点位置索引应从1开始连续递增，当前索引 {sortedPositions[i]} 不符合要求 - Position index should start from 1 and be consecutive");
            }
        }

        return (true, null);
    }

    /// <inheritdoc />
    public (bool IsValid, string? ErrorMessage) ValidateNDiverterTopology(
        IReadOnlyList<DiverterNodeConfig> diverters,
        long abnormalChuteId)
    {
        return ChutePathTopologyValidator.ValidateNDiverterTopology(diverters, abnormalChuteId);
    }

    /// <inheritdoc />
    public DiverterPathNode? FindNodeByChuteId(long chuteId)
    {
        var config = _topologyRepository.Get();
        return config.FindNodeByChuteId(chuteId);
    }

    /// <inheritdoc />
    public IReadOnlyList<DiverterPathNode>? GetPathToChute(long chuteId)
    {
        var config = _topologyRepository.Get();
        return config.GetPathToChute(chuteId);
    }

    /// <inheritdoc />
    public SwitchingPath? CreatePathForParcel(long targetChuteId)
    {
        return _pathGenerator.GeneratePath(targetChuteId);
    }
}
