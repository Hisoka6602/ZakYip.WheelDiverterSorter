using BenchmarkDotNet.Attributes;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

/// <summary>
/// 路径执行性能基准测试
/// </summary>
[MemoryDiagnoser]
public class PathExecutionBenchmarks
{
    private ISwitchingPathExecutor _executor = null!;
    private SwitchingPath _singleSegmentPath = null!;
    private SwitchingPath _twoSegmentPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _executor = new MockSwitchingPathExecutor();

        _singleSegmentPath = new SwitchingPath
        {
            TargetChuteId = "CHUTE_B",
            Segments = new List<SwitchingPathSegment>
            {
                new()
                {
                    SequenceNumber = 1,
                    DiverterId = "D1",
                    TargetDirection = DiverterDirection.Left,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly(),
            GeneratedAt = DateTimeOffset.UtcNow,
            FallbackChuteId = "CHUTE_EXCEPTION"
        };

        _twoSegmentPath = new SwitchingPath
        {
            TargetChuteId = "CHUTE_A",
            Segments = new List<SwitchingPathSegment>
            {
                new()
                {
                    SequenceNumber = 1,
                    DiverterId = "D1",
                    TargetDirection = DiverterDirection.Right,
                    TtlMilliseconds = 5000
                },
                new()
                {
                    SequenceNumber = 2,
                    DiverterId = "D2",
                    TargetDirection = DiverterDirection.Left,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly(),
            GeneratedAt = DateTimeOffset.UtcNow,
            FallbackChuteId = "CHUTE_EXCEPTION"
        };
    }

    [Benchmark]
    public async Task<PathExecutionResult> ExecutePath_SingleSegment()
    {
        return await _executor.ExecuteAsync(_singleSegmentPath);
    }

    [Benchmark]
    public async Task<PathExecutionResult> ExecutePath_TwoSegments()
    {
        return await _executor.ExecuteAsync(_twoSegmentPath);
    }

    [Benchmark]
    public async Task ExecutePath_Batch10()
    {
        var tasks = new List<Task<PathExecutionResult>>();
        for (int i = 0; i < 10; i++)
        {
            var path = i % 2 == 0 ? _singleSegmentPath : _twoSegmentPath;
            tasks.Add(_executor.ExecuteAsync(path));
        }
        await Task.WhenAll(tasks);
    }
}
