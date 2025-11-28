using BenchmarkDotNet.Attributes;
using ZakYip.WheelDiverterSorter.Core.Sorting.Overload;
using ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

/// <summary>
/// 超载策略评估性能基准测试
/// Overload policy evaluation performance benchmarks
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class OverloadPolicyBenchmarks
{
    private IOverloadHandlingPolicy _policy = null!;
    private OverloadContext _normalContext;
    private OverloadContext _moderateContext;
    private OverloadContext _severeContext;
    private OverloadContext _overCapacityContext;
    private OverloadContext _timeoutContext;

    [GlobalSetup]
    public void Setup()
    {
        // 创建策略配置
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            ForceExceptionOnSevere = true,
            ForceExceptionOnOverCapacity = false,
            ForceExceptionOnTimeout = true,
            ForceExceptionOnWindowMiss = false,
            MaxInFlightParcels = 100,
            MinRequiredTtlMs = 500,
            MinArrivalWindowMs = 200
        };

        _policy = new DefaultOverloadHandlingPolicy(config);

        // 正常上下文
        _normalContext = new OverloadContext
        {
            InFlightParcels = 50,
            CurrentCongestionLevel = CongestionLevel.Normal,
            RemainingTtlMs = 2000,
            EstimatedArrivalWindowMs = 500
        };

        // 预警拥堵上下文
        _moderateContext = new OverloadContext
        {
            InFlightParcels = 75,
            CurrentCongestionLevel = CongestionLevel.Warning,
            RemainingTtlMs = 1500,
            EstimatedArrivalWindowMs = 300
        };

        // 严重拥堵上下文
        _severeContext = new OverloadContext
        {
            InFlightParcels = 95,
            CurrentCongestionLevel = CongestionLevel.Severe,
            RemainingTtlMs = 800,
            EstimatedArrivalWindowMs = 150
        };

        // 超容量上下文
        _overCapacityContext = new OverloadContext
        {
            InFlightParcels = 120,
            CurrentCongestionLevel = CongestionLevel.Warning,
            RemainingTtlMs = 1000,
            EstimatedArrivalWindowMs = 300
        };

        // 超时上下文
        _timeoutContext = new OverloadContext
        {
            InFlightParcels = 60,
            CurrentCongestionLevel = CongestionLevel.Normal,
            RemainingTtlMs = 400,
            EstimatedArrivalWindowMs = 250
        };
    }

    /// <summary>
    /// 测试正常场景的评估性能
    /// Tests evaluation performance for normal scenario
    /// </summary>
    [Benchmark(Baseline = true)]
    public OverloadDecision Evaluate_Normal()
    {
        return _policy.Evaluate(in _normalContext);
    }

    /// <summary>
    /// 测试中度拥堵场景的评估性能
    /// Tests evaluation performance for moderate congestion scenario
    /// </summary>
    [Benchmark]
    public OverloadDecision Evaluate_Moderate()
    {
        return _policy.Evaluate(in _moderateContext);
    }

    /// <summary>
    /// 测试严重拥堵场景的评估性能
    /// Tests evaluation performance for severe congestion scenario
    /// </summary>
    [Benchmark]
    public OverloadDecision Evaluate_Severe()
    {
        return _policy.Evaluate(in _severeContext);
    }

    /// <summary>
    /// 测试超容量场景的评估性能
    /// Tests evaluation performance for over-capacity scenario
    /// </summary>
    [Benchmark]
    public OverloadDecision Evaluate_OverCapacity()
    {
        return _policy.Evaluate(in _overCapacityContext);
    }

    /// <summary>
    /// 测试超时场景的评估性能
    /// Tests evaluation performance for timeout scenario
    /// </summary>
    [Benchmark]
    public OverloadDecision Evaluate_Timeout()
    {
        return _policy.Evaluate(in _timeoutContext);
    }

    /// <summary>
    /// 测试批量评估性能 - 100次
    /// Tests batch evaluation performance - 100 iterations
    /// </summary>
    [Benchmark]
    public void Evaluate_Batch100()
    {
        for (int i = 0; i < 100; i++)
        {
            switch (i % 5)
            {
                case 0:
                    _policy.Evaluate(in _normalContext);
                    break;
                case 1:
                    _policy.Evaluate(in _moderateContext);
                    break;
                case 2:
                    _policy.Evaluate(in _severeContext);
                    break;
                case 3:
                    _policy.Evaluate(in _overCapacityContext);
                    break;
                default:
                    _policy.Evaluate(in _timeoutContext);
                    break;
            }
        }
    }

    /// <summary>
    /// 测试批量评估性能 - 1000次（模拟高频场景）
    /// Tests batch evaluation performance - 1000 iterations (simulates high-frequency scenario)
    /// </summary>
    [Benchmark]
    public void Evaluate_Batch1000()
    {
        for (int i = 0; i < 1000; i++)
        {
            switch (i % 5)
            {
                case 0:
                    _policy.Evaluate(in _normalContext);
                    break;
                case 1:
                    _policy.Evaluate(in _moderateContext);
                    break;
                case 2:
                    _policy.Evaluate(in _severeContext);
                    break;
                case 3:
                    _policy.Evaluate(in _overCapacityContext);
                    break;
                default:
                    _policy.Evaluate(in _timeoutContext);
                    break;
            }
        }
    }

    /// <summary>
    /// 测试策略创建开销
    /// Tests policy creation overhead
    /// </summary>
    [Benchmark]
    public IOverloadHandlingPolicy CreatePolicy()
    {
        var config = new OverloadPolicyConfiguration
        {
            Enabled = true,
            MaxInFlightParcels = 100,
            MinRequiredTtlMs = 500,
            MinArrivalWindowMs = 200
        };
        return new DefaultOverloadHandlingPolicy(config);
    }
}
