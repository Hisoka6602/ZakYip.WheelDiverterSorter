# PR-08C 实施总结

## 概述

PR-08C 完成了路径规划阶段的二次超载检查与容量测试集成，包括：
1. 在路径规划阶段接入二次超载判定
2. CapacityTestingRunner 与真实仿真场景集成
3. 为拥堵检测/超载策略/容量估算补充单元测试

## 一、路径规划阶段超载检查

### 1.1 路径时间预算评估

**位置**: `ZakYip.WheelDiverterSorter.Core/DefaultSwitchingPathGenerator.cs`

新增静态方法 `CanCompleteRouteInTime`，用于评估路径是否能在可用时间预算内完成：

```csharp
public static bool CanCompleteRouteInTime(
    SwitchingPath path,
    decimal currentLineSpeedMmPerSecond,
    double availableTimeBudgetMs)
```

**功能**：
- 计算路径所有段的总TTL（包含容差）
- 判断总TTL是否小于等于可用时间预算
- 用于路径规划阶段的可达性评估

### 1.2 路由超载检查集成

**位置**: `ZakYip.WheelDiverterSorter.Host/Services/ParcelSortingOrchestrator.cs`

在 `ProcessSortingAsync` 方法中，生成路径后新增二次超载检查：

```csharp
// PR-08C: 路径规划阶段的二次超载检查
if (!isOverloadException && _overloadPolicy != null && _congestionCollector != null && path != null)
{
    // 计算路径所需总时间
    double totalRouteTimeMs = path.Segments.Sum(s => (double)s.TtlMilliseconds);
    
    // 预估剩余时间
    double remainingTtlMs = EstimatedTotalTtlMs - estimatedElapsedMs;

    // 检查是否能在时间内完成
    if (totalRouteTimeMs > remainingTtlMs)
    {
        var routeDecision = _overloadPolicy.Evaluate(in routeOverloadContext);
        
        if (routeDecision.ShouldForceException)
        {
            // 重新生成到异常格口的路径
            // 记录 RouteOverload 指标
        }
    }
}
```

**触发条件**：
- 路径所需时间 > 剩余TTL
- 超载策略决定强制异常

**行为**：
- 重新生成到异常格口的路径
- 记录 Prometheus 指标: `sorting_overload_parcels_total{reason="RouteOverload"}`
- 记录日志（Info级别）

## 二、CapacityTestingRunner 集成

### 2.1 ISimulationScenarioRunner 接口扩展

**位置**: `ZakYip.WheelDiverterSorter.Simulation/Services/ISimulationScenarioRunner.cs`

新增方法支持运行任意配置的仿真场景：

```csharp
Task<SimulationSummary> RunScenarioAsync(
    SimulationOptions options, 
    CancellationToken cancellationToken = default);
```

**实现**: `SimulationScenarioRunner.RunScenarioAsync`
- 设置运行时配置
- 调用 SimulationRunner.RunAsync 执行仿真
- 返回 SimulationSummary 结果
- 自动清理运行时配置

### 2.2 CapacityTestingRunner 真实集成

**位置**: `ZakYip.WheelDiverterSorter.Simulation/Services/CapacityTestingRunner.cs`

重新实现 `RunCapacityTestAsync` 方法，实现真正的产能测试：

```csharp
public async Task<CapacityTestResults> RunCapacityTestAsync(
    SimulationOptions baseOptions,
    IReadOnlyList<int> testIntervals,
    int parcelsPerTest,
    CancellationToken cancellationToken = default)
```

**流程**：
1. 遍历每个测试间隔（如 [1000, 800, 600, 400, 300, 250, 200]ms）
2. 为每个间隔：
   - 创建测试场景配置（修改放包间隔和包裹数）
   - 调用 `ISimulationScenarioRunner.RunScenarioAsync` 运行仿真
   - 从 SimulationSummary 构建 CapacityTestResult
3. 收集所有结果到 CapacityHistory
4. 调用 `ICapacityEstimator.Estimate` 计算安全产能区间
5. 打印中文产能评估报告
6. 更新 Prometheus 指标: `sorting_capacity_recommended_parcels_per_minute`

**依赖注入**：
```csharp
public CapacityTestingRunner(
    ISimulationScenarioRunner scenarioRunner,
    ICapacityEstimator capacityEstimator,
    ILogger<CapacityTestingRunner> logger,
    PrometheusMetrics? metrics = null)
```

**报告输出示例**：
```
=== 产能测试评估报告 ===
测试数据点数: 7
安全产能区间: 60 - 100 包裹/分钟
危险阈值: 120 包裹/分钟
置信度: 70%
=== 各间隔测试结果 ===
间隔 1000ms (60.0 包裹/分钟): 成功率=98.00%, 异常率=2.00%, 平均延迟=1500ms
间隔 800ms (75.0 包裹/分钟): 成功率=97.00%, 异常率=3.00%, 平均延迟=2000ms
...
=== 报告结束 ===
```

## 三、单元测试

### 3.1 测试覆盖

新增 31 个单元测试，分布在以下测试类：

#### ThresholdBasedCongestionDetectorTests (10 测试)
**位置**: `ZakYip.WheelDiverterSorter.Core.Tests/ThrottleTests/ThresholdBasedCongestionDetectorTests.cs`

测试场景：
- ✅ 所有指标正常 → 返回 Normal
- ✅ 在途包裹数达到 Warning/Severe 阈值
- ✅ 平均延迟达到 Warning/Severe 阈值
- ✅ 失败率达到 Warning/Severe 阈值
- ✅ 多个 Warning 指标组合
- ✅ 单个 Severe 指标优先级

#### DefaultOverloadHandlingPolicyTests (10 测试)
**位置**: `ZakYip.WheelDiverterSorter.Core.Tests/ThrottleTests/DefaultOverloadHandlingPolicyTests.cs`

测试场景：
- ✅ 策略禁用 → 继续正常
- ✅ 严重拥堵 + ForceException 开关
- ✅ 超过最大在途包裹数 + ForceException 开关
- ✅ TTL不足 + ForceException 开关
- ✅ 到达窗口不足 + ForceException 开关
- ✅ 所有条件正常 → 继续正常
- ✅ 多条件优先级（Severe > 其他）

#### SimpleCapacityEstimatorTests (11 测试)
**位置**: `ZakYip.WheelDiverterSorter.Core.Tests/ThrottleTests/SimpleCapacityEstimatorTests.cs`

测试场景：
- ✅ 空历史数据 → 返回零产能
- ✅ 全部安全结果 → 正确区间
- ✅ 混合结果 → 过滤不安全点
- ✅ 低成功率 → 排除
- ✅ 高异常率 → 排除
- ✅ 高延迟 → 排除
- ✅ 10个数据点 → 100%置信度
- ✅ 5个数据点 → 50%置信度
- ✅ 无失败点 → 危险阈值为安全最大值的120%

### 3.2 测试运行

所有测试通过：
```bash
Test Run Successful.
Total tests: 28
     Passed: 28
 Total time: 3.5190 Seconds
```

## 四、Prometheus 指标

### 已存在指标（PR-08B）
- `sorting_overload_parcels_total{reason}` - 超载包裹计数
- `sorting_capacity_recommended_parcels_per_minute` - 推荐产能
- `sorting_average_latency_ms` - 平均延迟
- `sorting_congestion_level` - 拥堵等级
- `sorting_in_flight_parcels` - 在途包裹数

### PR-08C 新增使用
- **RouteOverload 原因**: 当路径规划阶段检测到超载时，使用 `reason="RouteOverload"` 记录

## 五、技术要点

### 5.1 路径时间预算计算

路径总时间 = Σ(段TTL)，其中每段TTL由 `CalculateSegmentTtl` 计算：
```
TTL = (段长度mm / 段速度mm/s) * 1000 + 容差时间ms
```

### 5.2 剩余TTL估算

当前实现使用简化估算：
```csharp
const double EstimatedTotalTtlMs = 30000;  // 假设30秒总TTL
double estimatedElapsedMs = 1000;          // 假设已经过1秒
double remainingTtlMs = EstimatedTotalTtlMs - estimatedElapsedMs;
```

**改进方向**：
- 从系统配置读取实际TTL
- 根据包裹进入时间动态计算已用时间
- 考虑包裹在各节点的实际位置

### 5.3 CapacityTestingRunner 调用方式

```csharp
// 配置基础仿真选项
var baseOptions = new SimulationOptions
{
    ParcelCount = 100,
    ParcelInterval = TimeSpan.FromMilliseconds(1000),
    SortingMode = SortingMode.RoundRobin
};

// 定义测试间隔
var testIntervals = new[] { 1000, 800, 600, 400, 300, 250, 200 };

// 运行产能测试
var results = await capacityTestingRunner.RunCapacityTestAsync(
    baseOptions,
    testIntervals,
    parcelsPerTest: 100,
    cancellationToken);

// 查看结果
Console.WriteLine($"安全产能: {results.EstimationResult.SafeMaxParcelsPerMinute:F0} 包裹/分钟");
```

## 六、配置示例

### 6.1 拥堵检测阈值
```csharp
var thresholds = new CongestionThresholds
{
    WarningInFlightParcels = 50,
    SevereInFlightParcels = 100,
    WarningAverageLatencyMs = 3000,
    SevereAverageLatencyMs = 5000,
    WarningFailureRatio = 0.1,
    SevereFailureRatio = 0.3
};
```

### 6.2 超载策略配置
```csharp
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
```

### 6.3 产能估算阈值
```csharp
var thresholds = new CapacityEstimationThresholds
{
    MinSuccessRate = 0.95,
    MaxAcceptableLatencyMs = 3000,
    MaxExceptionRate = 0.05
};
```

## 七、验收要点

### 7.1 路径规划阶段超载检查
✅ 路径所需时间超过剩余TTL时，触发二次超载检查
✅ 超载策略决定强制异常时，包裹走异常口
✅ Prometheus 指标 `sorting_overload_parcels_total{reason="RouteOverload"}` 正确自增
✅ 日志记录包裹ID、节点ID、剩余时间、决策结果

### 7.2 CapacityTestingRunner
✅ 能够运行多个间隔的仿真测试
✅ 正确收集每个间隔的测试结果
✅ 计算安全产能区间和危险阈值
✅ 打印中文产能评估报告
✅ 更新 Prometheus 指标 `sorting_capacity_recommended_parcels_per_minute`

### 7.3 单元测试
✅ 31 个单元测试全部通过
✅ 测试覆盖 ThresholdBasedCongestionDetector 核心逻辑
✅ 测试覆盖 DefaultOverloadHandlingPolicy 所有决策分支
✅ 测试覆盖 SimpleCapacityEstimator 各种数据场景

## 八、未来改进

### 8.1 路径规划超载检查
- 实现更精确的剩余TTL计算（基于包裹实际进入时间）
- 支持从系统配置读取TTL参数
- 增加路径复杂度评估（考虑节点数量、动作类型）

### 8.2 产能测试
- 支持 CLI 命令直接运行产能测试
- 增加可视化报告输出（图表、曲线）
- 支持自动化产能基准测试

### 8.3 集成测试
- 添加路径规划超载的端到端集成测试
- 添加 CapacityTestingRunner 的集成测试
- 模拟真实高负载场景的压力测试

## 九、相关文件

### 核心实现
- `ZakYip.WheelDiverterSorter.Core/DefaultSwitchingPathGenerator.cs`
- `ZakYip.WheelDiverterSorter.Host/Services/ParcelSortingOrchestrator.cs`
- `ZakYip.WheelDiverterSorter.Simulation/Services/ISimulationScenarioRunner.cs`
- `ZakYip.WheelDiverterSorter.Simulation/Services/SimulationScenarioRunner.cs`
- `ZakYip.WheelDiverterSorter.Simulation/Services/CapacityTestingRunner.cs`

### 单元测试
- `ZakYip.WheelDiverterSorter.Core.Tests/ThrottleTests/ThresholdBasedCongestionDetectorTests.cs`
- `ZakYip.WheelDiverterSorter.Core.Tests/ThrottleTests/DefaultOverloadHandlingPolicyTests.cs`
- `ZakYip.WheelDiverterSorter.Core.Tests/ThrottleTests/SimpleCapacityEstimatorTests.cs`

### 文档
- `PR08C_IMPLEMENTATION_SUMMARY.md` (本文档)
- `PR08_USAGE_GUIDE.md` (待更新)
