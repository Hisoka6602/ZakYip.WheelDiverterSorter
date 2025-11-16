using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;

namespace ZakYip.WheelDiverterSorter.Simulation.Scenarios;

/// <summary>
/// 预定义的仿真场景集合
/// </summary>
/// <remarks>
/// 提供标准的测试场景，用于验证系统在不同条件下的行为
/// </remarks>
public static class ScenarioDefinitions
{
    /// <summary>
    /// 场景 A：低摩擦差异、无掉包（基线场景）
    /// </summary>
    /// <remarks>
    /// - FrictionModel.MinFactor = 0.95, MaxFactor = 1.05
    /// - DropoutProbabilityPerSegment = 0
    /// - 期望：所有包裹 Status = SortedToTargetChute，且 SortedToWrongChute 计数为 0
    /// </remarks>
    public static SimulationScenario CreateScenarioA(string sortingMode, int parcelCount = 20)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景A-基线场景-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(500),
                SortingMode = sortingMode,
                FixedChuteIds = sortingMode == "FixedChute" ? new[] { 1L, 2L, 3L } : null,
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = false,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.95m,
                    MaxFactor = 1.05m,
                    IsDeterministic = true,
                    Seed = 42
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.0m,
                    Seed = 42
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null // 全局验证即可
        };
    }

    /// <summary>
    /// 场景 B：大摩擦差异、无掉包
    /// </summary>
    /// <remarks>
    /// - FrictionModel.MinFactor = 0.7, MaxFactor = 1.3
    /// - 允许个别包裹 Timeout
    /// - 期望：可能出现 Timeout，但所有非 Timeout 且有目标格口的包裹，必须 FinalChuteId == TargetChuteId；SortedToWrongChute == 0
    /// </remarks>
    public static SimulationScenario CreateScenarioB(string sortingMode, int parcelCount = 20)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景B-大摩擦差异-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(500),
                SortingMode = sortingMode,
                FixedChuteIds = sortingMode == "FixedChute" ? new[] { 1L, 2L, 3L } : null,
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = false,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.7m,
                    MaxFactor = 1.3m,
                    IsDeterministic = true,
                    Seed = 42
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.0m,
                    Seed = 42
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null // 全局验证即可
        };
    }

    /// <summary>
    /// 场景 C：中等摩擦差异 + 小概率掉包
    /// </summary>
    /// <remarks>
    /// - FrictionModel.MinFactor = 0.9, MaxFactor = 1.1
    /// - DropoutProbabilityPerSegment ≈ 0.05
    /// - 期望：部分包裹 Status = Dropped；任意 Status = SortedToTargetChute 的包裹，其传感器事件必须完整、在 TTL 内，且 FinalChuteId == TargetChuteId；SortedToWrongChute == 0
    /// </remarks>
    public static SimulationScenario CreateScenarioC(string sortingMode, int parcelCount = 20)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景C-中等摩擦+小概率掉包-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(500),
                SortingMode = sortingMode,
                FixedChuteIds = sortingMode == "FixedChute" ? new[] { 1L, 2L, 3L } : null,
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = true,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.9m,
                    MaxFactor = 1.1m,
                    IsDeterministic = true,
                    Seed = 42
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.05m,
                    Seed = 42
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null // 全局验证即可
        };
    }

    /// <summary>
    /// 场景 D：极端摩擦 + 高掉包率（压力场景）
    /// </summary>
    /// <remarks>
    /// - FrictionModel.MinFactor = 0.6, MaxFactor = 1.4
    /// - DropoutProbabilityPerSegment ≈ 0.2
    /// - 期望：可接受较多 Timeout / Dropped；仍然必须保证 SortedToWrongChute == 0
    /// </remarks>
    public static SimulationScenario CreateScenarioD(string sortingMode, int parcelCount = 20)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景D-极端压力-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(500),
                SortingMode = sortingMode,
                FixedChuteIds = sortingMode == "FixedChute" ? new[] { 1L, 2L, 3L } : null,
                ExceptionChuteId = 999,
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = true,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 0.6m,
                    MaxFactor = 1.4m,
                    IsDeterministic = true,
                    Seed = 42
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.2m,
                    Seed = 42
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null // 全局验证即可
        };
    }
}
