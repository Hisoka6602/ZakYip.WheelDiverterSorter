using ZakYip.WheelDiverterSorter.Simulation.Scenarios.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Simulation.Scenarios;

/// <summary>
/// PR-41: 混沌测试与长时间压力仿真场景
/// Chaos testing and long-term stress simulation scenarios
/// </summary>
public static class ChaosScenarioDefinitions
{
    /// <summary>
    /// 场景 CH-1：轻度混沌短期测试（5分钟）
    /// Mild chaos short-term test (5 minutes)
    /// </summary>
    /// <remarks>
    /// 用于验证系统在轻度混沌下的基本韧性
    /// - 轻度混沌注入（通讯延迟5%、驱动异常1%）
    /// - 持续5分钟
    /// - 中等流量（500包裹/分钟）
    /// </remarks>
    public static SimulationScenario CreateScenarioCH1()
    {
        return new SimulationScenario
        {
            ScenarioName = "CH-1_轻度混沌_5分钟",
            Options = new SimulationOptions
            {
                ParcelCount = 2500, // 5 minutes * 500/min
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(120), // ~500 parcels/min
                SortingMode = "RoundRobin",
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = false,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.9m,
                    MaxFactor = 1.1m,
                    IsDeterministic = false
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false,
                IsLongRunMode = true,
                LongRunDuration = TimeSpan.FromMinutes(5),
                MetricsPushIntervalSeconds = 30,
                IsEnableChaosTest = true,
                ChaosProfile = "Mild"
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 CH-2：中度混沌中期测试（30分钟）
    /// Moderate chaos medium-term test (30 minutes)
    /// </summary>
    /// <remarks>
    /// 用于验证系统在中度混沌下的持续韧性
    /// - 中度混沌注入（通讯延迟10%、驱动异常5%、IO掉点3%）
    /// - 持续30分钟
    /// - 高流量（800包裹/分钟）
    /// </remarks>
    public static SimulationScenario CreateScenarioCH2()
    {
        return new SimulationScenario
        {
            ScenarioName = "CH-2_中度混沌_30分钟",
            Options = new SimulationOptions
            {
                ParcelCount = 24000, // 30 minutes * 800/min
                LineSpeedMmps = 1200m,
                ParcelInterval = TimeSpan.FromMilliseconds(75), // ~800 parcels/min
                SortingMode = "RoundRobin",
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = true,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.85m,
                    MaxFactor = 1.15m,
                    IsDeterministic = false
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.01m
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false,
                IsLongRunMode = true,
                LongRunDuration = TimeSpan.FromMinutes(30),
                MetricsPushIntervalSeconds = 60,
                IsEnableChaosTest = true,
                ChaosProfile = "Moderate"
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 CH-3：重度混沌长期测试（2小时）
    /// Heavy chaos long-term test (2 hours)
    /// </summary>
    /// <remarks>
    /// 用于验证系统在重度混沌下的长期稳定性
    /// - 重度混沌注入（通讯延迟20%、驱动异常10%、IO掉点8%）
    /// - 持续2小时
    /// - 中等流量（600包裹/分钟）
    /// - 包含上游改口仿真
    /// </remarks>
    public static SimulationScenario CreateScenarioCH3()
    {
        return new SimulationScenario
        {
            ScenarioName = "CH-3_重度混沌_2小时",
            Options = new SimulationOptions
            {
                ParcelCount = 72000, // 2 hours * 60 min/hour * 600/min
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(100), // ~600 parcels/min
                SortingMode = "RoundRobin",
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = true,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.7m,
                    MaxFactor = 1.3m,
                    IsDeterministic = false
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.02m
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false,
                IsLongRunMode = true,
                LongRunDuration = TimeSpan.FromHours(2),
                MetricsPushIntervalSeconds = 120,
                IsEnableChaosTest = true,
                ChaosProfile = "Heavy",
                IsEnableUpstreamChuteChange = true,
                UpstreamChuteChangeProbability = 0.05m
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 CH-4：生产级负载压力测试（4小时）
    /// Production-level load stress test (4 hours)
    /// </summary>
    /// <remarks>
    /// 用于验证系统在生产级负载下的长期稳定性
    /// - 轻度混沌注入（模拟真实环境的偶发故障）
    /// - 持续4小时
    /// - 生产级流量（1000包裹/分钟）
    /// - 包含传感器故障仿真
    /// - 监控资源泄漏
    /// </remarks>
    public static SimulationScenario CreateScenarioCH4()
    {
        return new SimulationScenario
        {
            ScenarioName = "CH-4_生产级负载_4小时",
            Options = new SimulationOptions
            {
                ParcelCount = 240000, // 4 hours * 60 min/hour * 1000/min
                LineSpeedMmps = 1500m,
                ParcelInterval = TimeSpan.FromMilliseconds(60), // ~1000 parcels/min
                SortingMode = "RoundRobin",
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = true,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.95m,
                    MaxFactor = 1.05m,
                    IsDeterministic = false
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.005m
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false,
                IsLongRunMode = true,
                LongRunDuration = TimeSpan.FromHours(4),
                MetricsPushIntervalSeconds = 180,
                IsEnableChaosTest = true,
                ChaosProfile = "Mild",
                SensorFault = new SensorFaultOptions
                {
                    BehaviorMode = IoBehaviorMode.Chaos,
                    SensorLossProbability = 0.001m,
                    JitterProbability = 0.01m
                }
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 CH-5：极限韧性测试（30分钟）
    /// Extreme resilience test (30 minutes)
    /// </summary>
    /// <remarks>
    /// 用于测试系统的韧性极限
    /// - 极重度混沌注入（所有故障类型高概率）
    /// - 持续30分钟
    /// - 中等流量（避免因流量问题掩盖混沌影响）
    /// - 预期有较多异常但系统不崩溃
    /// </remarks>
    public static SimulationScenario CreateScenarioCH5()
    {
        return new SimulationScenario
        {
            ScenarioName = "CH-5_极限韧性_30分钟",
            Options = new SimulationOptions
            {
                ParcelCount = 15000, // 30 minutes * 500/min
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(120), // ~500 parcels/min
                SortingMode = "RoundRobin",
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = true,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.5m,
                    MaxFactor = 1.5m,
                    IsDeterministic = false
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.05m
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false,
                IsLongRunMode = true,
                LongRunDuration = TimeSpan.FromMinutes(30),
                MetricsPushIntervalSeconds = 60,
                IsEnableChaosTest = true,
                ChaosProfile = "Heavy",
                IsEnableUpstreamChuteChange = true,
                UpstreamChuteChangeProbability = 0.1m,
                SensorFault = new SensorFaultOptions
                {
                    BehaviorMode = IoBehaviorMode.Chaos,
                    SensorLossProbability = 0.005m,
                    JitterProbability = 0.03m
                }
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 获取所有混沌测试场景
    /// Get all chaos testing scenarios
    /// </summary>
    public static IEnumerable<SimulationScenario> GetAllChaosScenarios()
    {
        yield return CreateScenarioCH1();
        yield return CreateScenarioCH2();
        yield return CreateScenarioCH3();
        yield return CreateScenarioCH4();
        yield return CreateScenarioCH5();
    }
}
