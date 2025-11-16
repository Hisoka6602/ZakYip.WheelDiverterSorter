using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 内存中的路由计划仓储实现（用于演示和测试）
/// </summary>
public class InMemoryRoutePlanRepository : IRoutePlanRepository
{
    private readonly ConcurrentDictionary<long, RoutePlan> _plans = new();

    public Task<RoutePlan?> GetByParcelIdAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        _plans.TryGetValue(parcelId, out var plan);
        return Task.FromResult(plan);
    }

    public Task SaveAsync(RoutePlan routePlan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routePlan);
        _plans[routePlan.ParcelId] = routePlan;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        _plans.TryRemove(parcelId, out _);
        return Task.CompletedTask;
    }
}
