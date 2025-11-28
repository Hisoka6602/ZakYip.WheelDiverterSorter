using BenchmarkDotNet.Attributes;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Benchmarks;

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
            ChuteId = 1,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = 1, TargetDirection = DiverterDirection.Right, SequenceNumber = 1 }
            },
            IsEnabled = true
        };

        var routeB = new ChuteRouteConfiguration
        {
            ChuteId = 2,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = 1, TargetDirection = DiverterDirection.Left, SequenceNumber = 1 }
            },
            IsEnabled = true
        };

        var routeC = new ChuteRouteConfiguration
        {
            ChuteId = 3,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new() { DiverterId = 1, TargetDirection = DiverterDirection.Straight, SequenceNumber = 1 },
                new() { DiverterId = 2, TargetDirection = DiverterDirection.Right, SequenceNumber = 2 }
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
        return _generator.GeneratePath(2);
    }

    [Benchmark]
    public SwitchingPath? GeneratePath_TwoSegments()
    {
        return _generator.GeneratePath(1);
    }

    [Benchmark]
    public SwitchingPath? GeneratePath_Unknown()
    {
        return _generator.GeneratePath(999);
    }

    [Benchmark]
    public void GeneratePath_Batch100()
    {
        for (int i = 0; i < 100; i++)
        {
            var chuteId = i % 3 == 0 ? 1 : (i % 3 == 1 ? 2 : 3);
            _generator.GeneratePath(chuteId);
        }
    }
}
