using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 带缓存的摆轮路径生成器，减少数据库访问和重复计算
/// Cached switching path generator that reduces database access and repeated calculations
/// </summary>
public class CachedSwitchingPathGenerator : ISwitchingPathGenerator
{
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly IRouteTemplateCache _cache;
    private readonly ISwitchingPathGenerator _innerGenerator;

    /// <summary>
    /// 初始化带缓存的路径生成器
    /// Initializes cached path generator
    /// </summary>
    /// <param name="routeRepository">路由配置仓储</param>
    /// <param name="cache">路由模板缓存</param>
    public CachedSwitchingPathGenerator(
        IRouteConfigurationRepository routeRepository,
        IRouteTemplateCache cache)
    {
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        
        // 使用内部生成器进行实际的路径生成
        _innerGenerator = new DefaultSwitchingPathGenerator(_routeRepository);
    }

    /// <inheritdoc/>
    public SwitchingPath? GeneratePath(int targetChuteId)
    {
        if (targetChuteId <= 0)
        {
            return null;
        }

        // 尝试从缓存获取路由配置
        if (_cache.TryGet(targetChuteId, out var cachedConfig) && cachedConfig != null)
        {
            // 使用缓存的配置生成路径
            return GeneratePathFromConfig(cachedConfig);
        }

        // 缓存未命中，从数据库查询
        var routeConfig = _routeRepository.GetByChuteId(targetChuteId);
        if (routeConfig == null || routeConfig.DiverterConfigurations.Count == 0)
        {
            return null;
        }

        // 将配置添加到缓存
        _cache.Add(targetChuteId, routeConfig);

        // 生成路径
        return GeneratePathFromConfig(routeConfig);
    }

    /// <summary>
    /// 从配置生成路径
    /// Generates path from configuration
    /// </summary>
    private SwitchingPath GeneratePathFromConfig(ChuteRouteConfiguration routeConfig)
    {
        // 生成有序的摆轮段列表
        var segments = routeConfig.DiverterConfigurations
            .OrderBy(config => config.SequenceNumber)
            .Select((config, index) => new SwitchingPathSegment
            {
                SequenceNumber = index + 1,
                DiverterId = config.DiverterId,
                TargetDirection = config.TargetDirection,
                TtlMilliseconds = CalculateSegmentTtl(config)
            })
            .ToList();

        return new SwitchingPath
        {
            TargetChuteId = routeConfig.ChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = DateTimeOffset.UtcNow,
            FallbackChuteId = WellKnownChuteIds.DefaultException
        };
    }

    /// <summary>
    /// 计算段的TTL（生存时间）
    /// Calculates segment TTL
    /// </summary>
    private int CalculateSegmentTtl(DiverterConfigurationEntry segmentConfig)
    {
        // 计算理论通过时间（毫秒）
        var theoreticalTimeMs = (segmentConfig.SegmentLengthMm / segmentConfig.SegmentSpeedMmPerSecond) * 1000;
        
        // 加上容差时间
        var calculatedTtl = (int)Math.Ceiling(theoreticalTimeMs) + segmentConfig.SegmentToleranceTimeMs;
        
        // 确保TTL不小于最小值（1秒）
        const int MinTtlMs = 1000;
        return Math.Max(calculatedTtl, MinTtlMs);
    }

    /// <summary>
    /// 使缓存失效（在配置更新时调用）
    /// Invalidates cache (call when configuration is updated)
    /// </summary>
    /// <param name="chuteId">格口ID，如果为null则清空整个缓存</param>
    public void InvalidateCache(int? chuteId = null)
    {
        if (chuteId.HasValue)
        {
            _cache.Remove(chuteId.Value);
        }
        else
        {
            _cache.Clear();
        }
    }
}
