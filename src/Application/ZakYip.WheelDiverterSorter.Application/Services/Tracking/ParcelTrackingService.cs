using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Sorting.Tracking;

namespace ZakYip.WheelDiverterSorter.Application.Services.Tracking;

/// <summary>
/// 包裹跟踪服务实现（内存存储）
/// </summary>
/// <remarks>
/// 使用 ConcurrentDictionary 实现线程安全的内存存储。
/// 
/// <para><b>线程安全</b>：</para>
/// 所有操作都通过 ConcurrentDictionary 保证线程安全，
/// 支持多线程同时访问（如 SortingOrchestrator 更新状态、
/// ParcelLifetimeMonitor 扫描检测超时/丢失）。
/// 
/// <para><b>内存管理</b>：</para>
/// 建议定期调用 CleanupExpiredRecordsAsync 清理已完成或已丢失的旧记录。
/// </remarks>
public sealed class ParcelTrackingService : IParcelTrackingService
{
    private readonly ConcurrentDictionary<long, ParcelTrackingRecord> _records = new();

    /// <inheritdoc />
    public Task<ParcelTrackingRecord> CreateTrackingRecordAsync(
        long parcelId,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken = default)
    {
        var record = ParcelTrackingRecord.CreateDetected(parcelId, detectedAt);
        _records[parcelId] = record;
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<ParcelTrackingRecord?> UpdateAssignedAsync(
        long parcelId,
        long targetChuteId,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken = default)
    {
        if (!_records.TryGetValue(parcelId, out var existing))
        {
            return Task.FromResult<ParcelTrackingRecord?>(null);
        }

        var updated = existing.WithAssigned(targetChuteId, assignedAt);
        _records[parcelId] = updated;
        return Task.FromResult<ParcelTrackingRecord?>(updated);
    }

    /// <inheritdoc />
    public Task<ParcelTrackingRecord?> UpdateRoutingAsync(
        long parcelId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        if (!_records.TryGetValue(parcelId, out var existing))
        {
            return Task.FromResult<ParcelTrackingRecord?>(null);
        }

        var updated = existing.WithRouting(lastSeenAt);
        _records[parcelId] = updated;
        return Task.FromResult<ParcelTrackingRecord?>(updated);
    }

    /// <inheritdoc />
    public Task<ParcelTrackingRecord?> UpdateSortedAsync(
        long parcelId,
        long actualChuteId,
        DateTimeOffset sortedAt,
        CancellationToken cancellationToken = default)
    {
        if (!_records.TryGetValue(parcelId, out var existing))
        {
            return Task.FromResult<ParcelTrackingRecord?>(null);
        }

        var updated = existing.WithSorted(actualChuteId, sortedAt);
        _records[parcelId] = updated;
        return Task.FromResult<ParcelTrackingRecord?>(updated);
    }

    /// <inheritdoc />
    public Task<ParcelTrackingRecord?> UpdateTimedOutAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        if (!_records.TryGetValue(parcelId, out var existing))
        {
            return Task.FromResult<ParcelTrackingRecord?>(null);
        }

        var updated = existing.WithTimedOut();
        _records[parcelId] = updated;
        return Task.FromResult<ParcelTrackingRecord?>(updated);
    }

    /// <inheritdoc />
    public Task<ParcelTrackingRecord?> UpdateLostAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        if (!_records.TryGetValue(parcelId, out var existing))
        {
            return Task.FromResult<ParcelTrackingRecord?>(null);
        }

        var updated = existing.WithLost();
        _records[parcelId] = updated;
        return Task.FromResult<ParcelTrackingRecord?>(updated);
    }

    /// <inheritdoc />
    public Task<ParcelTrackingRecord?> GetByParcelIdAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        _records.TryGetValue(parcelId, out var record);
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ParcelTrackingRecord>> GetActiveRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            ParcelLifecycleStatus.Detected,
            ParcelLifecycleStatus.Assigned,
            ParcelLifecycleStatus.Routing,
            ParcelLifecycleStatus.TimedOut
        };

        var activeRecords = _records.Values
            .Where(r => activeStatuses.Contains(r.Status))
            .ToList();

        return Task.FromResult<IReadOnlyList<ParcelTrackingRecord>>(activeRecords);
    }

    /// <inheritdoc />
    public Task<int> CleanupExpiredRecordsAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default)
    {
        var finalStatuses = new[]
        {
            ParcelLifecycleStatus.Sorted,
            ParcelLifecycleStatus.Lost
        };

        var toRemove = _records
            .Where(kvp =>
                finalStatuses.Contains(kvp.Value.Status) &&
                kvp.Value.DetectedAt < olderThan)
            .Select(kvp => kvp.Key)
            .ToList();

        var removedCount = 0;
        foreach (var key in toRemove)
        {
            if (_records.TryRemove(key, out _))
            {
                removedCount++;
            }
        }

        return Task.FromResult(removedCount);
    }
}
