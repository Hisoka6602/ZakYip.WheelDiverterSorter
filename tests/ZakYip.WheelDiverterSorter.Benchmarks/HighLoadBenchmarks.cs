using BenchmarkDotNet.Attributes;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

/// <summary>
/// 高负载场景性能基准测试 (500-1000包裹/分钟)
/// High-load scenario performance benchmarks (500-1000 parcels/minute)
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class HighLoadBenchmarks
{
    private ISwitchingPathGenerator _generator = null!;
    private ISwitchingPathExecutor _executor = null!;
    private IRouteConfigurationRepository _repository = null!;
    private const string TestDbPath = "highload_benchmark_test.db";
    private List<SwitchingPath> _preCachedPaths = null!;
    private readonly Random _random = new();

    [GlobalSetup]
    public void Setup()
    {
        // 清理旧的测试数据库
        if (File.Exists(TestDbPath))
        {
            File.Delete(TestDbPath);
        }

        // 创建测试配置仓储
        _repository = new LiteDbRouteConfigurationRepository(TestDbPath);

        // 添加多个路由配置以模拟真实场景
        for (int i = 1; i <= 10; i++)
        {
            var route = new ChuteRouteConfiguration
            {
                ChuteId = i,
                DiverterConfigurations = GenerateDiverterConfig(i),
                IsEnabled = true
            };
            _repository.Upsert(route);
        }

        _generator = new DefaultSwitchingPathGenerator(_repository);
        _executor = new MockSwitchingPathExecutor(new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock());

        // 预生成路径用于执行测试
        _preCachedPaths = new List<SwitchingPath>();
        for (int i = 1; i <= 10; i++)
        {
            var path = _generator.GeneratePath(i);
            if (path != null)
            {
                _preCachedPaths.Add(path);
            }
        }
    }

    private List<DiverterConfigurationEntry> GenerateDiverterConfig(int chuteId)
    {
        var config = new List<DiverterConfigurationEntry>();
        
        // 根据chuteId生成不同复杂度的路径
        int segmentCount = (chuteId % 3) + 1; // 1-3段
        
        for (int i = 1; i <= segmentCount; i++)
        {
            config.Add(new DiverterConfigurationEntry
            {
                DiverterId = i,
                TargetDirection = (DiverterDirection)(i % 3),
                SequenceNumber = i
            });
        }
        
        return config;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_repository is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (File.Exists(TestDbPath))
        {
            try
            {
                File.Delete(TestDbPath);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

    /// <summary>
    /// 模拟500包裹/分钟的负载 (每包裹120ms)
    /// Simulates 500 parcels/minute load (120ms per parcel)
    /// </summary>
    [Benchmark]
    public void Load_500ParcelsPerMinute()
    {
        // 8.33 parcels/second = 500 parcels/minute
        // 在benchmark循环中处理8个包裹来模拟1秒的负载
        for (int i = 0; i < 8; i++)
        {
            int chuteId = (i % 10) + 1;
            _generator.GeneratePath(chuteId);
        }
    }

    /// <summary>
    /// 模拟1000包裹/分钟的负载 (每包裹60ms)
    /// Simulates 1000 parcels/minute load (60ms per parcel)
    /// </summary>
    [Benchmark]
    public void Load_1000ParcelsPerMinute()
    {
        // 16.67 parcels/second = 1000 parcels/minute
        // 在benchmark循环中处理17个包裹来模拟1秒的负载
        for (int i = 0; i < 17; i++)
        {
            int chuteId = (i % 10) + 1;
            _generator.GeneratePath(chuteId);
        }
    }

    /// <summary>
    /// 模拟峰值负载 - 1500包裹/分钟
    /// Simulates peak load - 1500 parcels/minute
    /// </summary>
    [Benchmark]
    public void Load_PeakLoad_1500ParcelsPerMinute()
    {
        // 25 parcels/second = 1500 parcels/minute
        for (int i = 0; i < 25; i++)
        {
            int chuteId = (i % 10) + 1;
            _generator.GeneratePath(chuteId);
        }
    }

    /// <summary>
    /// 完整端到端性能测试 - 路径生成和执行
    /// Full end-to-end performance test - path generation and execution
    /// </summary>
    [Benchmark]
    public async Task EndToEnd_500ParcelsPerMinute()
    {
        var tasks = new List<Task>();
        
        for (int i = 0; i < 8; i++)
        {
            int chuteId = (i % 10) + 1;
            var path = _generator.GeneratePath(chuteId);
            
            if (path != null)
            {
                tasks.Add(_executor.ExecuteAsync(path));
            }
        }
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 并发路径执行测试 - 模拟高并发场景
    /// Concurrent path execution test - simulates high concurrency scenarios
    /// </summary>
    [Benchmark]
    public async Task ConcurrentExecution_HighLoad()
    {
        var tasks = new List<Task>();
        
        // 并发执行20个路径
        for (int i = 0; i < 20; i++)
        {
            var path = _preCachedPaths[i % _preCachedPaths.Count];
            tasks.Add(_executor.ExecuteAsync(path));
        }
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 批量路径生成测试 - 测试批量操作性能
    /// Batch path generation test - tests batch operation performance
    /// </summary>
    [Benchmark]
    public void BatchPathGeneration_100Paths()
    {
        for (int i = 0; i < 100; i++)
        {
            int chuteId = (i % 10) + 1;
            _generator.GeneratePath(chuteId);
        }
    }

    /// <summary>
    /// 批量路径生成测试 - 500个路径
    /// Batch path generation test - 500 paths
    /// </summary>
    [Benchmark]
    public void BatchPathGeneration_500Paths()
    {
        for (int i = 0; i < 500; i++)
        {
            int chuteId = (i % 10) + 1;
            _generator.GeneratePath(chuteId);
        }
    }

    /// <summary>
    /// 批量路径生成测试 - 1000个路径
    /// Batch path generation test - 1000 paths
    /// </summary>
    [Benchmark]
    public void BatchPathGeneration_1000Paths()
    {
        for (int i = 0; i < 1000; i++)
        {
            int chuteId = (i % 10) + 1;
            _generator.GeneratePath(chuteId);
        }
    }

    /// <summary>
    /// 混合负载测试 - 路径生成和执行混合
    /// Mixed load test - combination of path generation and execution
    /// </summary>
    [Benchmark]
    public async Task MixedLoad_GenerationAndExecution()
    {
        var tasks = new List<Task>();
        
        for (int i = 0; i < 50; i++)
        {
            int chuteId = (i % 10) + 1;
            var path = _generator.GeneratePath(chuteId);
            
            if (path != null && i % 3 == 0)
            {
                tasks.Add(_executor.ExecuteAsync(path));
            }
        }
        
        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// 压力测试 - 超高负载
    /// Stress test - extreme load
    /// </summary>
    [Benchmark]
    public void StressTest_ExtremeLoad()
    {
        // 模拟2000包裹/分钟的极端负载
        for (int i = 0; i < 33; i++)
        {
            int chuteId = _random.Next(1, 11);
            _generator.GeneratePath(chuteId);
        }
    }
}
