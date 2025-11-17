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

    /// <summary>
    /// 场景 HD-1：轻微高密度 + 默认异常口策略
    /// </summary>
    /// <remarks>
    /// - ParcelInterval 稍小于最小安全头距（制造临界情况）
    /// - DenseParcelStrategy = RouteToException
    /// - 期望：部分包裹被判为高密度 → 路由到异常格口；所有 SortedToTargetChute 的包裹头距都不小于安全阈值；SortedToWrongChuteCount == 0
    /// </remarks>
    public static SimulationScenario CreateScenarioHD1(string sortingMode, int parcelCount = 50)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景HD-1-轻微高密度-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m, // 1 m/s = 1000 mm/s
                ParcelInterval = TimeSpan.FromMilliseconds(400), // 间隔时间约400ms
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
                // 最小安全头距配置：约500mm（0.5秒 * 1000mm/s）
                MinSafeHeadwayMm = 500m,
                MinSafeHeadwayTime = TimeSpan.FromMilliseconds(500),
                DenseParcelStrategy = DenseParcelStrategy.RouteToException,
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 HD-2：极端高密度（几乎重叠）
    /// </summary>
    /// <remarks>
    /// - ParcelInterval 远小于最小安全头距
    /// - DenseParcelStrategy = RouteToException
    /// - 期望：大部分包裹被标记为高密度并进入异常口；非异常包裹依然满足头距 >= 安全阈值；SortedToWrongChuteCount == 0
    /// </remarks>
    public static SimulationScenario CreateScenarioHD2(string sortingMode, int parcelCount = 50)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景HD-2-极端高密度-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(150), // 间隔时间仅150ms
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
                // 最小安全头距配置：约600mm（0.6秒 * 1000mm/s）
                MinSafeHeadwayMm = 600m,
                MinSafeHeadwayTime = TimeSpan.FromMilliseconds(600),
                DenseParcelStrategy = DenseParcelStrategy.RouteToException,
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 HD-3A：策略变体 - MarkAsTimeout
    /// </summary>
    /// <remarks>
    /// - 高密度包裹标记为 Timeout 状态
    /// - 期望：高密度包裹不再被视为 SortedToTargetChute；Timeout 计数随高密度比例增加；SortedToWrongChuteCount == 0
    /// </remarks>
    public static SimulationScenario CreateScenarioHD3A(string sortingMode, int parcelCount = 50)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景HD-3A-策略MarkAsTimeout-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(300),
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
                MinSafeHeadwayMm = 500m,
                MinSafeHeadwayTime = TimeSpan.FromMilliseconds(500),
                DenseParcelStrategy = DenseParcelStrategy.MarkAsTimeout,
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 HD-3B：策略变体 - MarkAsDropped
    /// </summary>
    /// <remarks>
    /// - 高密度包裹标记为 Dropped 状态
    /// - 期望：高密度包裹不再被视为 SortedToTargetChute；Dropped 计数随高密度比例增加；SortedToWrongChuteCount == 0
    /// </remarks>
    public static SimulationScenario CreateScenarioHD3B(string sortingMode, int parcelCount = 50)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景HD-3B-策略MarkAsDropped-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(300),
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
                MinSafeHeadwayMm = 500m,
                MinSafeHeadwayTime = TimeSpan.FromMilliseconds(500),
                DenseParcelStrategy = DenseParcelStrategy.MarkAsDropped,
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 E：高摩擦 + 中等掉包率
    /// </summary>
    /// <remarks>
    /// - FrictionModel.MinFactor = 0.7, MaxFactor = 1.3
    /// - DropoutProbabilityPerSegment ≈ 0.1
    /// - 期望：部分包裹出现 Timeout / Dropped；SortedToWrongChute == 0
    /// - 此场景用于验证系统在高摩擦和掉包同时存在时的鲁棒性
    /// </remarks>
    public static SimulationScenario CreateScenarioE(string sortingMode, int parcelCount = 20)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景E-高摩擦有丢失-{sortingMode}",
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
                    MinFactor = 0.7m,
                    MaxFactor = 1.3m,
                    IsDeterministic = true,
                    Seed = 42
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.1m,
                    Seed = 42
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null // 全局验证即可
        };
    }

    /// <summary>
    /// 场景 SF-1：摆轮前传感器故障（100% 确定性故障）
    /// </summary>
    /// <remarks>
    /// - 摆轮前传感器持续不触发（100% 确定性故障，无随机概率）
    /// - 总包裹数：999
    /// - 所有包裹的时间线标记为 IsSensorFault = true
    /// - 不触发摆轮前传感器事件
    /// - 期望：所有包裹被路由到异常口，状态标记为 SensorFault
    /// </remarks>
    public static SimulationScenario CreateScenarioSF1(string sortingMode, int parcelCount = 999)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景SF-1-摆轮前传感器故障-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(500),
                SortingMode = sortingMode,
                FixedChuteIds = sortingMode == "FixedChute" ? new[] { 1L, 2L, 3L } : null,
                ExceptionChuteId = 999,
                IsEnableRandomFriction = false,
                IsEnableRandomDropout = false,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 1.0m,
                    MaxFactor = 1.0m,
                    IsDeterministic = true,
                    Seed = 42
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.0m,
                    Seed = 42
                },
                SensorFault = new SensorFaultOptions
                {
                    IsPreDiverterSensorFault = true, // 启用摆轮前传感器故障
                    FaultStartOffset = null, // 从开始就故障
                    FaultDuration = null // 持续到结束
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 SJ-1：传感器抖动（高频抖动但不是所有包裹）
    /// </summary>
    /// <remarks>
    /// - 传感器短时间内多次触发
    /// - 使用固定概率（约40%）或固定索引模式（每3个包裹抖一次）
    /// - 发生抖动的包裹：在时间线上重复发送传感器事件，标记为 IsSensorFault = true，FailureReason = "传感器抖动产生重复检测"
    /// - 未抖动的包裹保持正常轨迹
    /// - 期望：至少部分包裹发生抖动，抖动的必须异常，正常的可以正常分拣
    /// </remarks>
    public static SimulationScenario CreateScenarioSJ1(string sortingMode, int parcelCount = 30)
    {
        return new SimulationScenario
        {
            ScenarioName = $"场景SJ-1-传感器抖动-{sortingMode}",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m,
                ParcelInterval = TimeSpan.FromMilliseconds(500),
                SortingMode = sortingMode,
                FixedChuteIds = sortingMode == "FixedChute" ? new[] { 1L, 2L, 3L } : null,
                ExceptionChuteId = 999,
                IsEnableRandomFriction = false,
                IsEnableRandomDropout = false,
                FrictionModel = new FrictionModelOptions
                {
                    MinFactor = 1.0m,
                    MaxFactor = 1.0m,
                    IsDeterministic = true,
                    Seed = 42
                },
                DropoutModel = new DropoutModelOptions
                {
                    DropoutProbabilityPerSegment = 0.0m,
                    Seed = 42
                },
                SensorFault = new SensorFaultOptions
                {
                    IsEnableSensorJitter = true, // 启用传感器抖动
                    JitterTriggerCount = 3, // 每次抖动触发3次
                    JitterIntervalMs = 50, // 50ms内触发
                    JitterProbability = 0.4m // 40%概率抖动（不是所有包裹）
                },
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 LongRunDenseFlow：长时间高密度分拣仿真场景
    /// </summary>
    /// <remarks>
    /// - 10 摆轮 / 21 格口（1-20 正常，21 异常口）
    /// - 1000 个包裹，每 300ms 创建一个
    /// - 主线不停，包裹连续上车，支持并发处理
    /// - 间隔过近的包裹路由到异常口 (ChuteId=21)
    /// - 从入口到异常口的物理路径约 2 分钟
    /// - 理论同时在线包裹数约 400 个
    /// </remarks>
    public static SimulationScenario CreateLongRunDenseFlow(int parcelCount = 1000)
    {
        return new SimulationScenario
        {
            ScenarioName = "LongRunDenseFlow-长时间高密度分拣",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m, // 1 m/s = 1000 mm/s
                ParcelInterval = TimeSpan.FromMilliseconds(300), // 每300ms创建一个包裹
                SortingMode = "RoundRobin", // 轮询模式，目标格口在1-20之间
                FixedChuteIds = null,
                ExceptionChuteId = 21, // 异常口为21号格口
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = false, // 不启用掉包，专注于高密度场景
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
                // 最小安全间隔配置：300ms 时间间隔作为阈值
                MinSafeHeadwayMm = 300m, // 300mm 空间间隔
                MinSafeHeadwayTime = TimeSpan.FromMilliseconds(300), // 300ms 时间间隔
                DenseParcelStrategy = DenseParcelStrategy.RouteToException,
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null
        };
    }

    /// <summary>
    /// 场景 E 长跑仿真：高密度持续分拣与 Observability 验收场景
    /// </summary>
    /// <remarks>
    /// PR-05 需求：
    /// - 10 台摆轮，中间长度不一致（已在 InMemoryRouteConfigurationRepository 中配置）
    /// - 异常口在末端（ChuteId=11）
    /// - 每 300ms 创建包裹，总数 1000 个
    /// - 包裹目标格口随机分布（1-10），异常口为固定 Id
    /// - 单包裹从入口到异常口约 2 分钟
    /// - 启用长跑模式，暴露 Prometheus metrics 端点
    /// - 不会因为上一包未到达摆轮就暂停创建下一包
    /// - 模拟高密度流量 + 可能无法在当前节点分拣的压力
    /// </remarks>
    public static SimulationScenario CreateScenarioE_LongRunSimulation(int parcelCount = 1000)
    {
        return new SimulationScenario
        {
            ScenarioName = "场景E-长跑仿真-高密度持续分拣",
            Options = new SimulationOptions
            {
                ParcelCount = parcelCount,
                LineSpeedMmps = 1000m, // 1 m/s = 1000 mm/s
                ParcelInterval = TimeSpan.FromMilliseconds(300), // 每300ms创建一个包裹
                SortingMode = "RoundRobin", // 轮询模式，目标格口在1-10之间
                FixedChuteIds = new[] { 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L, 10L }, // 10个正常格口
                ExceptionChuteId = 11, // 异常口在末端，ChuteId=11
                IsEnableRandomFriction = true,
                IsEnableRandomDropout = false, // 不启用随机掉包
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
                // 最小安全间隔配置：300ms 时间间隔作为阈值
                // 这确保了高密度流量场景
                MinSafeHeadwayMm = 300m, // 300mm 空间间隔
                MinSafeHeadwayTime = TimeSpan.FromMilliseconds(300), // 300ms 时间间隔
                DenseParcelStrategy = DenseParcelStrategy.RouteToException,
                
                // 启用长跑模式以暴露 Prometheus metrics
                IsLongRunMode = true,
                MetricsPushIntervalSeconds = 30, // 每30秒输出一次统计
                FailFastOnMisSort = false,
                
                IsEnableVerboseLogging = false,
                IsPauseAtEnd = false
            },
            Expectations = null
        };
    }
}
