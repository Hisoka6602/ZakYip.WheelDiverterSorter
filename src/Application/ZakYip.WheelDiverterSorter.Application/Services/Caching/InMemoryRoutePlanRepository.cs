using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Application.Services.Caching;

/// <summary>
/// 基于内存缓存的路由计划仓储实现
/// </summary>
/// <remarks>
/// <para><b>设计目标</b>：</para>
/// <list type="bullet">
///   <item>避免 LiteDB 中时间戳 ParcelId 导致的重复键/空键异常</item>
///   <item>3分钟滑动过期，防止缓存无限增长</item>
///   <item>线程安全，支持高并发访问</item>
///   <item>所有操作通过 ISafeExecutionService 隔离异常，不影响系统分拣</item>
/// </list>
///
/// <para><b>文件结构说明</b>：</para>
/// <list type="bullet">
///   <item>位置：Application/Services/Caching/InMemoryRoutePlanRepository.cs</item>
///   <item>依赖：IMemoryCache, ISafeExecutionService, ILogger</item>
///   <item>生命周期：Singleton（与 IMemoryCache 生命周期一致）</item>
///   <item>缓存策略：3分钟滑动过期，高优先级</item>
/// </list>
///
/// <para><b>异常处理</b>：</para>
/// <para>
/// 所有操作均通过 ISafeExecutionService 包裹，确保任何异常都被记录并隔离，
/// 不会导致系统分拣流程崩溃。失败时返回 null 或静默失败，上层代码应处理 null 情况。
/// </para>
/// </remarks>
public sealed class InMemoryRoutePlanRepository : IRoutePlanRepository
{
    /// <summary>
    /// 滑动过期时间：3分钟
    /// </summary>
    /// <remarks>
    /// 3分钟足够完成包裹的分拣和改口操作，同时避免长期驻留内存导致内存泄漏。
    /// </remarks>
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(3);

    private readonly IMemoryCache _memoryCache;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<InMemoryRoutePlanRepository> _logger;

    /// <summary>
    /// 初始化内存路由计划仓储
    /// </summary>
    /// <param name="memoryCache">内存缓存实例</param>
    /// <param name="safeExecutor">安全执行服务</param>
    /// <param name="logger">日志实例</param>
    public InMemoryRoutePlanRepository(
        IMemoryCache memoryCache,
        ISafeExecutionService safeExecutor,
        ILogger<InMemoryRoutePlanRepository> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<RoutePlan?> GetByParcelIdAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                if (parcelId <= 0)
                {
                    _logger.LogWarning("GetByParcelIdAsync: Invalid ParcelId={ParcelId}", parcelId);
                    return null;
                }

                var cacheKey = BuildCacheKey(parcelId);
                if (_memoryCache.TryGetValue(cacheKey, out RoutePlan? routePlan) && routePlan is not null)
                {
                    _logger.LogDebug("GetByParcelIdAsync: 缓存命中 ParcelId={ParcelId}", parcelId);
                    return routePlan;
                }

                _logger.LogDebug("GetByParcelIdAsync: 缓存未命中 ParcelId={ParcelId}", parcelId);
                return null;
            },
            operationName: "InMemoryRoutePlanRepository.GetByParcelIdAsync",
            cancellationToken: cancellationToken,
            defaultValue: null
        );
    }

    /// <inheritdoc/>
    public async Task SaveAsync(RoutePlan routePlan, CancellationToken cancellationToken = default)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(routePlan);

                if (routePlan.ParcelId <= 0)
                {
                    _logger.LogWarning(
                        "SaveAsync: Invalid RoutePlan.ParcelId={ParcelId}",
                        routePlan.ParcelId);
                    return;
                }

                var cacheKey = BuildCacheKey(routePlan.ParcelId);
                var options = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = SlidingExpiration,
                    Priority = CacheItemPriority.High,
                    Size = 1 // 每个路由计划占用大小为 1
                };

                _memoryCache.Set(cacheKey, routePlan, options);

                _logger.LogDebug(
                    "SaveAsync: 成功保存路由计划 ParcelId={ParcelId}, TargetChuteId={ChuteId}, Status={Status}",
                    routePlan.ParcelId,
                    routePlan.CurrentTargetChuteId,
                    routePlan.Status);

                await Task.CompletedTask;
            },
            operationName: "InMemoryRoutePlanRepository.SaveAsync",
            cancellationToken: cancellationToken
        );
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                if (parcelId <= 0)
                {
                    _logger.LogWarning("DeleteAsync: Invalid ParcelId={ParcelId}", parcelId);
                    return;
                }

                var cacheKey = BuildCacheKey(parcelId);
                _memoryCache.Remove(cacheKey);

                _logger.LogDebug("DeleteAsync: 已删除路由计划 ParcelId={ParcelId}", parcelId);

                await Task.CompletedTask;
            },
            operationName: "InMemoryRoutePlanRepository.DeleteAsync",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 构建缓存键
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <returns>缓存键字符串</returns>
    private static string BuildCacheKey(long parcelId)
    {
        return $"RoutePlan:{parcelId}";
    }
}
