using System.Collections.Concurrent;
using System.Timers;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Events.Queue;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Queues;

/// <summary>
/// 待执行包裹队列 - 存储已路由但等待WheelFront传感器触发的包裹
/// </summary>
/// <remarks>
/// 用于实现拓扑驱动的延迟执行机制：
/// 1. 包裹创建并获得路由后，加入待执行队列
/// 2. WheelFront传感器触发时，从队列取出包裹执行分拣
/// 3. 超时未触发的包裹自动路由到异常格口
/// </remarks>
public class PendingParcelQueue : IPendingParcelQueue, IDisposable
{
    private readonly ConcurrentDictionary<long, PendingParcelEntry> _pendingParcels = new();
    private readonly ConcurrentDictionary<long, System.Timers.Timer> _timers = new();
    private readonly ILogger<PendingParcelQueue> _logger;
    private readonly ISystemClock _clock;

    public PendingParcelQueue(
        ILogger<PendingParcelQueue> logger,
        ISystemClock clock)
    {
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// 包裹超时事件 - 当包裹在队列中等待超时时触发
    /// </summary>
    public event EventHandler<ParcelTimedOutEventArgs>? ParcelTimedOut;

    /// <summary>
    /// 添加包裹到待执行队列
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <param name="wheelDiverterId">摆轮ID（long类型，用于匹配）</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <param name="preGeneratedPath">预生成的分拣路径（TD-062: 路径预生成优化）</param>
    public void Enqueue(long parcelId, long targetChuteId, long wheelDiverterId, int timeoutSeconds, ZakYip.WheelDiverterSorter.Core.LineModel.Topology.SwitchingPath preGeneratedPath)
    {
        var entry = new PendingParcelEntry
        {
            ParcelId = parcelId,
            TargetChuteId = targetChuteId,
            WheelDiverterId = wheelDiverterId,
            EnqueuedAt = _clock.LocalNow,
            TimeoutAt = _clock.LocalNow.AddSeconds(timeoutSeconds),
            PreGeneratedPath = preGeneratedPath
        };

        if (_pendingParcels.TryAdd(parcelId, entry))
        {
            // 启动超时 Timer（事件驱动，替代轮询）
            // 防止溢出：timeoutSeconds * 1000 可能溢出 int，先转换为 long
            var timer = new System.Timers.Timer((double)((long)timeoutSeconds * 1000));
            timer.AutoReset = false;
            timer.Elapsed += (sender, e) =>
            {
                // 移除并释放 timer
                if (_timers.TryRemove(parcelId, out var t))
                {
                    t.Stop();
                    t.Dispose();
                }
                OnParcelTimedOut(entry);
            };
            _timers.TryAdd(parcelId, timer);
            timer.Start();

            _logger.LogDebug(
                "包裹 {ParcelId} 已加入待执行队列，目标格口: {ChuteId}, 摆轮ID: {WheelDiverterId}, 超时时间: {TimeoutSeconds}秒",
                parcelId, targetChuteId, wheelDiverterId, timeoutSeconds);
        }
        else
        {
            _logger.LogWarning(
                "包裹 {ParcelId} 已存在于待执行队列中，跳过重复加入",
                parcelId);
        }
    }

    /// <summary>
    /// 触发包裹超时事件
    /// </summary>
    private void OnParcelTimedOut(PendingParcelEntry entry)
    {
        var elapsedMs = (_clock.LocalNow - entry.EnqueuedAt).TotalMilliseconds;
        
        _logger.LogWarning(
            "包裹 {ParcelId} 等待超时，目标格口: {ChuteId}, 摆轮ID: {WheelDiverterId}, 等待时间: {ElapsedMs}ms",
            entry.ParcelId, entry.TargetChuteId, entry.WheelDiverterId, elapsedMs);
        
        ParcelTimedOut.SafeInvoke(this, new ParcelTimedOutEventArgs
        {
            ParcelId = entry.ParcelId,
            TargetChuteId = entry.TargetChuteId,
            WheelDiverterId = entry.WheelDiverterId,
            ElapsedMs = elapsedMs
        }, _logger, nameof(ParcelTimedOut));
    }

    /// <summary>
    /// 当WheelFront传感器触发时，取出对应包裹
    /// </summary>
    /// <param name="wheelDiverterId">摆轮ID（long类型）</param>
    public PendingParcelEntry? DequeueByWheelDiverterId(long wheelDiverterId)
    {
        // 查找第一个匹配该摆轮ID的包裹
        var matchingEntry = _pendingParcels
            .Where(kvp => kvp.Value.WheelDiverterId == wheelDiverterId)
            .OrderBy(kvp => kvp.Value.EnqueuedAt) // FIFO顺序
            .FirstOrDefault();

        if (matchingEntry.Value != null && 
            _pendingParcels.TryRemove(matchingEntry.Key, out var entry))
        {
            // 取消并清理定时器
            if (_timers.TryRemove(entry.ParcelId, out var timer))
            {
                timer.Stop();
                timer.Dispose();
            }

            var waitTime = (_clock.LocalNow - entry.EnqueuedAt).TotalMilliseconds;
            _logger.LogDebug(
                "包裹 {ParcelId} 已从待执行队列取出，摆轮ID: {WheelDiverterId}, 等待时间: {WaitTime}ms",
                entry.ParcelId, wheelDiverterId, waitTime);
            return entry;
        }

        return null;
    }

    /// <summary>
    /// 获取所有超时的包裹
    /// </summary>
    public List<PendingParcelEntry> GetTimedOutParcels()
    {
        var now = _clock.LocalNow;
        var timedOut = new List<PendingParcelEntry>();

        foreach (var kvp in _pendingParcels)
        {
            if (kvp.Value.TimeoutAt < now)
            {
                if (_pendingParcels.TryRemove(kvp.Key, out var entry))
                {
                    var waitTime = (now - entry.EnqueuedAt).TotalSeconds;
                    _logger.LogWarning(
                        "包裹 {ParcelId} 等待超时，目标格口: {ChuteId}, 摆轮ID: {WheelDiverterId}, 等待时间: {WaitTime}秒",
                        entry.ParcelId, entry.TargetChuteId, entry.WheelDiverterId, waitTime);
                    timedOut.Add(entry);
                }
            }
        }

        return timedOut;
    }

    /// <summary>
    /// 获取队列中的包裹数量
    /// </summary>
    public int Count => _pendingParcels.Count;

    /// <summary>
    /// 获取所有待执行包裹（用于监控）
    /// </summary>
    public IReadOnlyCollection<PendingParcelEntry> GetAll()
    {
        return _pendingParcels.Values.ToList();
    }

    /// <summary>
    /// 释放资源，清理所有定时器
    /// </summary>
    public void Dispose()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Stop();
            timer.Dispose();
        }
        _timers.Clear();
        _pendingParcels.Clear();
    }
}

/// <summary>
/// 待执行包裹条目
/// </summary>
public sealed record PendingParcelEntry
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 绑定的摆轮ID（用于匹配WheelFront传感器触发）
    /// </summary>
    /// <remarks>
    /// 使用 long 类型的 DiverterId 进行匹配，符合项目ID匹配规范。
    /// </remarks>
    public required long WheelDiverterId { get; init; }

    /// <summary>
    /// 加入队列时间
    /// </summary>
    public required DateTime EnqueuedAt { get; init; }

    /// <summary>
    /// 超时时间
    /// </summary>
    public required DateTime TimeoutAt { get; init; }

    /// <summary>
    /// 预生成的分拣路径（TD-062: 路径预生成优化）
    /// </summary>
    /// <remarks>
    /// 在包裹加入队列时即生成路径，WheelFront传感器触发时直接执行，
    /// 避免实时生成路径的延迟。路径包含完整的摆轮转向指令。
    /// </remarks>
    public required ZakYip.WheelDiverterSorter.Core.LineModel.Topology.SwitchingPath PreGeneratedPath { get; init; }
}

/// <summary>
/// 待执行包裹队列接口
/// </summary>
public interface IPendingParcelQueue
{
    /// <summary>
    /// 包裹超时事件 - 当包裹在队列中等待超时时触发
    /// </summary>
    event EventHandler<ParcelTimedOutEventArgs>? ParcelTimedOut;

    /// <summary>
    /// 添加包裹到待执行队列
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <param name="wheelDiverterId">摆轮ID（long类型，用于匹配）</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <param name="preGeneratedPath">预生成的分拣路径（TD-062: 路径预生成优化）</param>
    void Enqueue(long parcelId, long targetChuteId, long wheelDiverterId, int timeoutSeconds, ZakYip.WheelDiverterSorter.Core.LineModel.Topology.SwitchingPath preGeneratedPath);

    /// <summary>
    /// 当WheelFront传感器触发时，取出对应包裹
    /// </summary>
    /// <param name="wheelDiverterId">摆轮ID（long类型）</param>
    PendingParcelEntry? DequeueByWheelDiverterId(long wheelDiverterId);

    /// <summary>
    /// 获取所有超时的包裹
    /// </summary>
    List<PendingParcelEntry> GetTimedOutParcels();

    /// <summary>
    /// 获取队列中的包裹数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 获取所有待执行包裹（用于监控）
    /// </summary>
    IReadOnlyCollection<PendingParcelEntry> GetAll();
}
