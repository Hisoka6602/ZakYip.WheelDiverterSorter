using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution.Queues;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

/// <summary>
/// 队列性能基准测试
/// </summary>
/// <remarks>
/// 测试场景：
/// - 每秒300个包裹（18000包裹/分钟）
/// - 6个摆轮12个格口
/// - 同时进行入队和出队操作
/// - 持续1小时（共1,080,000个包裹）
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class QueuePerformanceBenchmarks
{
    private IPositionIndexQueueManager _queueManager = null!;
    private ISystemClock _clock = null!;
    private List<PositionQueueItem> _testItems = null!;
    private Random _random = null!;
    
    // 测试配置：6个摆轮
    private const int DiverterCount = 6;
    private const int ChuteCount = 12;
    
    [GlobalSetup]
    public void Setup()
    {
        _clock = new LocalSystemClock();
        _queueManager = new PositionIndexQueueManager(
            NullLogger<PositionIndexQueueManager>.Instance,
            _clock);
        _random = new Random(42); // 固定种子确保可重复性
        
        // 预生成测试数据
        _testItems = GenerateTestItems(10000);
    }

    /// <summary>
    /// 生成测试用的队列任务项
    /// </summary>
    private List<PositionQueueItem> GenerateTestItems(int count)
    {
        var items = new List<PositionQueueItem>(count);
        var baseTime = _clock.LocalNow;
        
        for (int i = 0; i < count; i++)
        {
            items.Add(new PositionQueueItem
            {
                ParcelId = i + 1,
                DiverterId = _random.Next(1, DiverterCount + 1),
                DiverterAction = (DiverterDirection)_random.Next(0, 3),
                ExpectedArrivalTime = baseTime.AddMilliseconds(i * 10),
                TimeoutThresholdMs = 2000,
                FallbackAction = DiverterDirection.Straight,
                CreatedAt = baseTime.AddMilliseconds(i * 10),
                PositionIndex = _random.Next(1, DiverterCount + 1)
            });
        }
        
        return items;
    }

    /// <summary>
    /// 基准测试：单次入队操作性能
    /// </summary>
    [Benchmark]
    public void SingleEnqueue()
    {
        var item = _testItems[0];
        _queueManager.EnqueueTask(item.PositionIndex, item);
    }

    /// <summary>
    /// 基准测试：单次出队操作性能
    /// </summary>
    [Benchmark]
    public PositionQueueItem? SingleDequeue()
    {
        // 先入队一个项
        var item = _testItems[0];
        _queueManager.EnqueueTask(item.PositionIndex, item);
        
        // 测量出队性能
        return _queueManager.DequeueTask(item.PositionIndex);
    }

    /// <summary>
    /// 基准测试：优先入队操作性能（需要重建队列）
    /// </summary>
    [Benchmark]
    public void PriorityEnqueue_WithExistingQueue()
    {
        // 先构建一个有10个任务的队列
        var positionIndex = 1;
        for (int i = 0; i < 10; i++)
        {
            _queueManager.EnqueueTask(positionIndex, _testItems[i]);
        }
        
        // 测量优先入队性能
        var priorityItem = _testItems[100];
        _queueManager.EnqueuePriorityTask(positionIndex, priorityItem);
        
        // 清理
        _queueManager.ClearAllQueues();
    }

    /// <summary>
    /// 基准测试：300包裹/秒的入队性能（1秒内入队300个）
    /// </summary>
    [Benchmark]
    public void EnqueueBurst_300ParcelsPerSecond()
    {
        // 模拟1秒内300个包裹的入队操作
        for (int i = 0; i < 300; i++)
        {
            var item = _testItems[i % _testItems.Count];
            _queueManager.EnqueueTask(item.PositionIndex, item);
        }
        
        // 清理
        _queueManager.ClearAllQueues();
    }

    /// <summary>
    /// 基准测试：同时进行入队和出队操作（300包裹/秒）
    /// </summary>
    [Benchmark]
    public void ConcurrentEnqueueDequeue_300ParcelsPerSecond()
    {
        // 先入队一批
        for (int i = 0; i < 150; i++)
        {
            var item = _testItems[i % _testItems.Count];
            _queueManager.EnqueueTask(item.PositionIndex, item);
        }
        
        // 同时进行入队和出队
        var tasks = new List<Task>();
        
        // 并发入队150个
        tasks.Add(Task.Run(() =>
        {
            for (int i = 150; i < 300; i++)
            {
                var item = _testItems[i % _testItems.Count];
                _queueManager.EnqueueTask(item.PositionIndex, item);
            }
        }));
        
        // 并发出队150个
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < 150; i++)
            {
                var positionIndex = (i % DiverterCount) + 1;
                _queueManager.DequeueTask(positionIndex);
            }
        }));
        
        Task.WaitAll(tasks.ToArray());
        
        // 清理
        _queueManager.ClearAllQueues();
    }

    /// <summary>
    /// 基准测试：6个Position并发入队出队（模拟6个摆轮）
    /// </summary>
    [Benchmark]
    public void SixPositions_ConcurrentOperations()
    {
        var tasks = new List<Task>();
        
        // 6个Position并发操作
        for (int position = 1; position <= DiverterCount; position++)
        {
            var pos = position;
            tasks.Add(Task.Run(() =>
            {
                // 每个Position入队50个
                for (int i = 0; i < 50; i++)
                {
                    var item = _testItems[(pos * 100 + i) % _testItems.Count] with { PositionIndex = pos };
                    _queueManager.EnqueueTask(pos, item);
                }
                
                // 每个Position出队25个
                for (int i = 0; i < 25; i++)
                {
                    _queueManager.DequeueTask(pos);
                }
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        // 清理
        _queueManager.ClearAllQueues();
    }

    /// <summary>
    /// 基准测试：批量修改队列（UpdateAffectedParcelsToStraight）
    /// </summary>
    [Benchmark]
    public void UpdateAffectedParcels_MultiplePositions()
    {
        // 先构建6个Position的队列，每个50个任务
        for (int position = 1; position <= DiverterCount; position++)
        {
            for (int i = 0; i < 50; i++)
            {
                var item = _testItems[(position * 100 + i) % _testItems.Count] with { PositionIndex = position };
                _queueManager.EnqueueTask(position, item);
            }
        }
        
        // 测量批量修改性能
        var lostParcelCreatedAt = _clock.LocalNow.AddSeconds(-10);
        var detectionTime = _clock.LocalNow;
        _queueManager.UpdateAffectedParcelsToStraight(lostParcelCreatedAt, detectionTime);
        
        // 清理
        _queueManager.ClearAllQueues();
    }

    /// <summary>
    /// 基准测试：移除指定包裹的所有任务（RemoveAllTasksForParcel）
    /// </summary>
    [Benchmark]
    public void RemoveAllTasksForParcel_MultiplePositions()
    {
        // 先构建6个Position的队列，每个50个任务
        var targetParcelId = 42L;
        for (int position = 1; position <= DiverterCount; position++)
        {
            for (int i = 0; i < 50; i++)
            {
                var parcelId = i == 25 ? targetParcelId : (long)(position * 100 + i);
                var item = _testItems[i % _testItems.Count] with 
                { 
                    ParcelId = parcelId,
                    PositionIndex = position 
                };
                _queueManager.EnqueueTask(position, item);
            }
        }
        
        // 测量移除性能
        _queueManager.RemoveAllTasksForParcel(targetParcelId);
        
        // 清理
        _queueManager.ClearAllQueues();
    }

    /// <summary>
    /// 基准测试：ISystemClock.LocalNow的性能
    /// </summary>
    [Benchmark]
    public DateTime ClockAccess_LocalNow()
    {
        return _clock.LocalNow;
    }

    /// <summary>
    /// 基准测试：DateTime.Now的性能（对比）
    /// </summary>
    [Benchmark]
    public DateTime DateTimeNow_Direct()
    {
        return DateTime.Now;
    }

    /// <summary>
    /// 基准测试：300包裹/秒，每个包裹访问3次时钟（入队+出队+时间判断）
    /// </summary>
    [Benchmark]
    public void ClockAccess_300ParcelsPerSecond_ThreeTimesEach()
    {
        for (int i = 0; i < 900; i++) // 300 * 3
        {
            var _ = _clock.LocalNow;
        }
    }

    /// <summary>
    /// 压力测试：持续运行10秒（模拟3000个包裹）
    /// </summary>
    [Benchmark]
    public void StressTest_10Seconds_3000Parcels()
    {
        var totalParcels = 3000; // 10秒 * 300包裹/秒
        var tasks = new List<Task>();
        
        // 模拟持续入队和出队
        for (int position = 1; position <= DiverterCount; position++)
        {
            var pos = position;
            tasks.Add(Task.Run(() =>
            {
                var parcelsPerPosition = totalParcels / DiverterCount;
                
                // 入队
                for (int i = 0; i < parcelsPerPosition; i++)
                {
                    var item = _testItems[i % _testItems.Count] with { PositionIndex = pos };
                    _queueManager.EnqueueTask(pos, item);
                    
                    // 模拟出队（每2个入队，出1个）
                    if (i % 2 == 0)
                    {
                        _queueManager.DequeueTask(pos);
                    }
                }
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        // 清理
        _queueManager.ClearAllQueues();
    }

    /// <summary>
    /// 压力测试：模拟1分钟运行（18,000个包裹）
    /// </summary>
    [Benchmark]
    public void StressTest_1Minute_18000Parcels()
    {
        var totalParcels = 18000; // 60秒 * 300包裹/秒
        var tasks = new List<Task>();
        
        // 模拟持续入队和出队
        for (int position = 1; position <= DiverterCount; position++)
        {
            var pos = position;
            tasks.Add(Task.Run(() =>
            {
                var parcelsPerPosition = totalParcels / DiverterCount;
                
                // 入队
                for (int i = 0; i < parcelsPerPosition; i++)
                {
                    var item = _testItems[i % _testItems.Count] with { PositionIndex = pos };
                    _queueManager.EnqueueTask(pos, item);
                    
                    // 模拟出队（每2个入队，出1个）
                    if (i % 2 == 0)
                    {
                        _queueManager.DequeueTask(pos);
                    }
                }
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        // 清理
        _queueManager.ClearAllQueues();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _queueManager?.ClearAllQueues();
    }
}
