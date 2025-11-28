using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

/// <summary>
/// 性能瓶颈分析基准测试
/// Performance bottleneck analysis benchmarks
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class PerformanceBottleneckBenchmarks
{
    private ISwitchingPathGenerator _generator = null!;
    private ISwitchingPathExecutor _executor = null!;
    private IRouteConfigurationRepository _repository = null!;
    private const string TestDbPath = "bottleneck_benchmark_test.db";
    private SwitchingPath _simplePath = null!;
    private SwitchingPath _complexPath = null!;
    private readonly Consumer _consumer = new();

    [GlobalSetup]
    public void Setup()
    {
        if (File.Exists(TestDbPath))
        {
            File.Delete(TestDbPath);
        }

        _repository = new LiteDbRouteConfigurationRepository(TestDbPath);
        
        // 简单路径配置
        var simpleRoute = new ChuteRouteConfiguration
        {
            ChuteId = 1,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = 1, TargetDirection = DiverterDirection.Right, SequenceNumber = 1 }
            },
            IsEnabled = true
        };
        
        // 复杂路径配置 - 5段
        var complexRoute = new ChuteRouteConfiguration
        {
            ChuteId = 2,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = 1, TargetDirection = DiverterDirection.Right, SequenceNumber = 1 },
                new() { DiverterId = 2, TargetDirection = DiverterDirection.Left, SequenceNumber = 2 },
                new() { DiverterId = 3, TargetDirection = DiverterDirection.Straight, SequenceNumber = 3 },
                new() { DiverterId = 4, TargetDirection = DiverterDirection.Right, SequenceNumber = 4 },
                new() { DiverterId = 5, TargetDirection = DiverterDirection.Left, SequenceNumber = 5 }
            },
            IsEnabled = true
        };

        _repository.Upsert(simpleRoute);
        _repository.Upsert(complexRoute);

        _generator = new DefaultSwitchingPathGenerator(_repository);
        _executor = new MockSwitchingPathExecutor(new ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock());

        _simplePath = _generator.GeneratePath(1)!;
        _complexPath = _generator.GeneratePath(2)!;
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

    // ========================================
    // 瓶颈1: 数据库访问性能
    // Bottleneck 1: Database access performance
    // ========================================

    /// <summary>
    /// 测试单次数据库读取性能
    /// Tests single database read performance
    /// </summary>
    [Benchmark]
    public ChuteRouteConfiguration? DatabaseRead_Single()
    {
        return _repository.GetByChuteId(1);
    }

    /// <summary>
    /// 测试批量数据库读取性能
    /// Tests batch database read performance
    /// </summary>
    [Benchmark]
    public void DatabaseRead_Batch10()
    {
        for (int i = 1; i <= 10; i++)
        {
            _repository.GetByChuteId(i % 2 == 0 ? 1 : 2);
        }
    }

    /// <summary>
    /// 测试数据库写入性能
    /// Tests database write performance
    /// </summary>
    [Benchmark]
    public void DatabaseWrite_Single()
    {
        var route = new ChuteRouteConfiguration
        {
            ChuteId = 999,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = 1, TargetDirection = DiverterDirection.Right, SequenceNumber = 1 }
            },
            IsEnabled = true
        };
        _repository.Upsert(route);
    }

    // ========================================
    // 瓶颈2: 路径生成性能
    // Bottleneck 2: Path generation performance
    // ========================================

    /// <summary>
    /// 测试简单路径生成性能
    /// Tests simple path generation performance
    /// </summary>
    [Benchmark]
    public SwitchingPath? PathGeneration_Simple()
    {
        return _generator.GeneratePath(1);
    }

    /// <summary>
    /// 测试复杂路径生成性能
    /// Tests complex path generation performance
    /// </summary>
    [Benchmark]
    public SwitchingPath? PathGeneration_Complex()
    {
        return _generator.GeneratePath(2);
    }

    /// <summary>
    /// 测试连续路径生成性能（缓存效果）
    /// Tests consecutive path generation performance (caching effect)
    /// </summary>
    [Benchmark]
    public void PathGeneration_Consecutive100()
    {
        for (int i = 0; i < 100; i++)
        {
            _generator.GeneratePath(1);
        }
    }

    /// <summary>
    /// 测试交替路径生成性能
    /// Tests alternating path generation performance
    /// </summary>
    [Benchmark]
    public void PathGeneration_Alternating100()
    {
        for (int i = 0; i < 100; i++)
        {
            _generator.GeneratePath(i % 2 == 0 ? 1 : 2);
        }
    }

    // ========================================
    // 瓶颈3: 路径执行性能
    // Bottleneck 3: Path execution performance
    // ========================================

    /// <summary>
    /// 测试简单路径执行性能
    /// Tests simple path execution performance
    /// </summary>
    [Benchmark]
    public async Task<PathExecutionResult> PathExecution_Simple()
    {
        return await _executor.ExecuteAsync(_simplePath);
    }

    /// <summary>
    /// 测试复杂路径执行性能
    /// Tests complex path execution performance
    /// </summary>
    [Benchmark]
    public async Task<PathExecutionResult> PathExecution_Complex()
    {
        return await _executor.ExecuteAsync(_complexPath);
    }

    /// <summary>
    /// 测试并发路径执行性能
    /// Tests concurrent path execution performance
    /// </summary>
    [Benchmark]
    public async Task PathExecution_Concurrent10()
    {
        var tasks = new Task<PathExecutionResult>[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = _executor.ExecuteAsync(i % 2 == 0 ? _simplePath : _complexPath);
        }
        await Task.WhenAll(tasks);
    }

    // ========================================
    // 瓶颈4: 内存分配和GC压力
    // Bottleneck 4: Memory allocation and GC pressure
    // ========================================

    /// <summary>
    /// 测试路径对象创建的内存分配
    /// Tests memory allocation from path object creation
    /// </summary>
    [Benchmark]
    public void MemoryAllocation_PathCreation()
    {
        for (int i = 0; i < 100; i++)
        {
            var path = new SwitchingPath
            {
                TargetChuteId = 1,
                Segments = new List<SwitchingPathSegment>
                {
                    new()
                    {
                        SequenceNumber = 1,
                        DiverterId = 1,
                        TargetDirection = DiverterDirection.Right,
                        TtlMilliseconds = 5000
                    }
                }.AsReadOnly(),
                GeneratedAt = DateTimeOffset.Now,
                FallbackChuteId = WellKnownChuteIds.DefaultException
            };
            _consumer.Consume(path);
        }
    }

    /// <summary>
    /// 测试配置对象创建的内存分配
    /// Tests memory allocation from configuration object creation
    /// </summary>
    [Benchmark]
    public void MemoryAllocation_ConfigCreation()
    {
        for (int i = 0; i < 100; i++)
        {
            var config = new ChuteRouteConfiguration
            {
                ChuteId = i,
                DiverterConfigurations = new List<DiverterConfigurationEntry>
                {
                    new() { DiverterId = 1, TargetDirection = DiverterDirection.Right, SequenceNumber = 1 }
                },
                IsEnabled = true
            };
            _consumer.Consume(config);
        }
    }

    // ========================================
    // 瓶颈5: 端到端性能分析
    // Bottleneck 5: End-to-end performance analysis
    // ========================================

    /// <summary>
    /// 测试完整的端到端流程
    /// Tests complete end-to-end flow
    /// </summary>
    [Benchmark]
    public async Task<PathExecutionResult> EndToEnd_SimplePath()
    {
        var path = _generator.GeneratePath(1);
        if (path == null)
        {
            return new PathExecutionResult 
            { 
                IsSuccess = false, 
                ActualChuteId = WellKnownChuteIds.DefaultException,
                FailureReason = "Path generation failed"
            };
        }
        return await _executor.ExecuteAsync(path);
    }

    /// <summary>
    /// 测试端到端流程 - 复杂路径
    /// Tests end-to-end flow - complex path
    /// </summary>
    [Benchmark]
    public async Task<PathExecutionResult> EndToEnd_ComplexPath()
    {
        var path = _generator.GeneratePath(2);
        if (path == null)
        {
            return new PathExecutionResult 
            { 
                IsSuccess = false, 
                ActualChuteId = WellKnownChuteIds.DefaultException,
                FailureReason = "Path generation failed"
            };
        }
        return await _executor.ExecuteAsync(path);
    }

    /// <summary>
    /// 测试端到端批量处理
    /// Tests end-to-end batch processing
    /// </summary>
    [Benchmark]
    public async Task EndToEnd_Batch50()
    {
        var tasks = new List<Task<PathExecutionResult>>();
        
        for (int i = 0; i < 50; i++)
        {
            var chuteId = i % 2 == 0 ? 1 : 2;
            var path = _generator.GeneratePath(chuteId);
            
            if (path != null)
            {
                tasks.Add(_executor.ExecuteAsync(path));
            }
        }
        
        await Task.WhenAll(tasks);
    }

    // ========================================
    // 瓶颈6: 错误处理性能
    // Bottleneck 6: Error handling performance
    // ========================================

    /// <summary>
    /// 测试无效路径的处理性能
    /// Tests invalid path handling performance
    /// </summary>
    [Benchmark]
    public SwitchingPath? ErrorHandling_InvalidChute()
    {
        return _generator.GeneratePath(9999);
    }

    /// <summary>
    /// 测试批量错误处理
    /// Tests batch error handling
    /// </summary>
    [Benchmark]
    public void ErrorHandling_Batch100()
    {
        for (int i = 0; i < 100; i++)
        {
            _generator.GeneratePath(9999 + i);
        }
    }
}
