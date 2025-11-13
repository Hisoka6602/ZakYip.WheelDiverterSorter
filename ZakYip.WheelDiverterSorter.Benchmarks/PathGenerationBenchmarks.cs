using BenchmarkDotNet.Attributes;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

/// <summary>
/// 路径生成性能基准测试
/// </summary>
[MemoryDiagnoser]
public class PathGenerationBenchmarks
{
    private ISwitchingPathGenerator _generator = null!;
    private IRouteConfigurationRepository _repository = null!;
    private const string TestDbPath = "benchmark_test.db";

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

        // 添加测试路由配置
        var routeA = new ChuteRouteConfiguration
        {
            ChuteId = "CHUTE_A",
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = "D1", TargetDirection = DiverterDirection.Right, SequenceNumber = 1 }
            },
            IsEnabled = true
        };

        var routeB = new ChuteRouteConfiguration
        {
            ChuteId = "CHUTE_B",
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = "D1", TargetDirection = DiverterDirection.Left, SequenceNumber = 1 }
            },
            IsEnabled = true
        };

        var routeC = new ChuteRouteConfiguration
        {
            ChuteId = "CHUTE_C",
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = "D1", TargetDirection = DiverterDirection.Straight, SequenceNumber = 1 },
                new() { DiverterId = "D2", TargetDirection = DiverterDirection.Right, SequenceNumber = 2 }
            },
            IsEnabled = true
        };

        _repository.Upsert(routeA);
        _repository.Upsert(routeB);
        _repository.Upsert(routeC);

        _generator = new DefaultSwitchingPathGenerator(_repository);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // 清理测试数据库
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

    [Benchmark]
    public SwitchingPath? GeneratePath_SingleSegment()
    {
        return _generator.GeneratePath("CHUTE_B");
    }

    [Benchmark]
    public SwitchingPath? GeneratePath_TwoSegments()
    {
        return _generator.GeneratePath("CHUTE_A");
    }

    [Benchmark]
    public SwitchingPath? GeneratePath_Unknown()
    {
        return _generator.GeneratePath("CHUTE_UNKNOWN");
    }

    [Benchmark]
    public void GeneratePath_Batch100()
    {
        for (int i = 0; i < 100; i++)
        {
            var chuteId = i % 3 == 0 ? "CHUTE_A" : (i % 3 == 1 ? "CHUTE_B" : "CHUTE_C");
            _generator.GeneratePath(chuteId);
        }
    }
}
