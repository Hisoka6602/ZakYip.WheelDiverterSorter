# PR-08: 拥堵检测、超载处置策略与产能估算使用指南

## 概述

PR-08 实现了拥堵检测、超载处置策略和产能估算功能，但**不实施上游节流控制**。系统只负责被动防守和策略处置，所有"节奏"只存在于仿真项目中。

## 核心设定

- **300ms 间隔** 只存在于仿真程序，用于构造压力场景
- **真实现场**：用户随时可以往皮带上丢包裹，系统无法阻止、无法规定"合适间隔"
- 能做的只有：
  - 尽量分拣成功
  - 分拣不了的包裹要有合理、可观测的异常策略（回流/异常口）
  - 给出"当前产能极限"的监控与建议

## 架构分层

### 1. Core 层（Sorting.Core）

#### 拥堵检测

```csharp
using ZakYip.Sorting.Core.Runtime;
using ZakYip.Sorting.Core.Policies;

// 创建拥堵检测器
var thresholds = new CongestionThresholds
{
    WarningInFlightParcels = 50,
    SevereInFlightParcels = 100,
    WarningAverageLatencyMs = 3000,
    SevereAverageLatencyMs = 5000,
    WarningFailureRatio = 0.1,
    SevereFailureRatio = 0.3
};

var detector = new ThresholdBasedCongestionDetector(thresholds);

// 检测拥堵
var snapshot = new CongestionSnapshot
{
    InFlightParcels = 60,
    AverageLatencyMs = 3500,
    MaxLatencyMs = 6000,
    FailureRatio = 0.15,
    TimeWindowSeconds = 60,
    TotalSampledParcels = 100
};

var level = detector.Detect(snapshot);
// level = CongestionLevel.Warning
```

#### 超载处置策略

```csharp
using ZakYip.Sorting.Core.Overload;

// 创建超载策略
var config = new OverloadPolicyConfiguration
{
    Enabled = true,
    ForceExceptionOnSevere = true,
    ForceExceptionOnTimeout = true,
    ForceExceptionOnWindowMiss = false,
    MaxInFlightParcels = 120,
    MinRequiredTtlMs = 500,
    MinArrivalWindowMs = 200
};

var policy = new DefaultOverloadHandlingPolicy(config);

// 评估包裹
var context = new OverloadContext
{
    ParcelId = "P001",
    TargetChuteId = 5,
    CurrentLineSpeed = 1000m,
    CurrentPosition = "Node-1",
    EstimatedArrivalWindowMs = 180,
    CurrentCongestionLevel = CongestionLevel.Warning,
    RemainingTtlMs = 450,
    InFlightParcels = 65
};

var decision = policy.Evaluate(context);
// decision.ShouldForceException = true
// decision.Reason = "剩余TTL不足：450ms < 500ms"
```

#### 产能估算

```csharp
using ZakYip.Sorting.Core.Policies;

// 创建产能估算器
var estimationThresholds = new CapacityEstimationThresholds
{
    MinSuccessRate = 0.95,
    MaxAcceptableLatencyMs = 3000,
    MaxExceptionRate = 0.05
};

var estimator = new SimpleCapacityEstimator(estimationThresholds);

// 估算产能
var history = new CapacityHistory
{
    TestResults = new[]
    {
        new CapacityTestResult
        {
            IntervalMs = 1000,
            ParcelCount = 100,
            SuccessRate = 0.98,
            AverageLatencyMs = 1200,
            MaxLatencyMs = 2500,
            ExceptionRate = 0.02,
            OverloadTriggerCount = 0
        },
        new CapacityTestResult
        {
            IntervalMs = 500,
            ParcelCount = 100,
            SuccessRate = 0.96,
            AverageLatencyMs = 1500,
            MaxLatencyMs = 3000,
            ExceptionRate = 0.04,
            OverloadTriggerCount = 0
        },
        new CapacityTestResult
        {
            IntervalMs = 300,
            ParcelCount = 100,
            SuccessRate = 0.85,
            AverageLatencyMs = 2500,
            MaxLatencyMs = 5000,
            ExceptionRate = 0.15,
            OverloadTriggerCount = 10
        }
    }
};

var result = estimator.Estimate(history);
// result.SafeMinParcelsPerMinute = 60 (1000ms interval)
// result.SafeMaxParcelsPerMinute = 120 (500ms interval)
// result.DangerousThresholdParcelsPerMinute = 200 (300ms interval)
```

### 2. Observability 层

#### Prometheus 指标

```csharp
using ZakYip.WheelDiverterSorter.Observability;

var metrics = new PrometheusMetrics();

// 设置拥堵等级
metrics.SetCongestionLevel((int)CongestionLevel.Warning); // 0=Normal, 1=Warning, 2=Severe

// 记录超载包裹
metrics.RecordOverloadParcel("Timeout");
metrics.RecordOverloadParcel("WindowMiss");
metrics.RecordOverloadParcel("CapacityExceeded");

// 设置推荐产能
metrics.SetRecommendedCapacity(120.0); // 每分钟120个包裹

// 设置平均延迟
metrics.SetAverageLatency(1500.0); // 1500毫秒

// 设置在途包裹数
metrics.SetInFlightParcels(65);
```

#### Grafana 查询示例

```promql
# 拥堵等级
sorting_congestion_level

# 超载包裹总数（按原因分组）
rate(sorting_overload_parcels_total[5m])

# 推荐产能 vs 实际吞吐量
sorting_capacity_recommended_parcels_per_minute
vs
rate(sorter_parcel_throughput_total[1m]) * 60

# 平均延迟
sorting_average_latency_ms

# 在途包裹数
sorting_inflight_parcels
```

### 3. Simulation 层

#### 产能测试场景

```csharp
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Simulation.Services;

// 创建基础场景
var baseScenario = ScenarioDefinitions.CreateCapacityTestBaseScenario(parcelsPerTest: 100);

// 定义测试间隔（毫秒）
var testIntervals = new[] { 1000, 800, 600, 400, 300, 250, 200, 150 };

// 运行产能测试
var capacityRunner = new CapacityTestingRunner(simulationRunner, logger);
var results = await capacityRunner.RunCapacityTestAsync(
    baseScenario,
    testIntervals,
    parcelsPerTest: 100
);

// 分析结果
foreach (var result in results.TestResults)
{
    Console.WriteLine($"间隔: {result.IntervalMs}ms");
    Console.WriteLine($"  成功率: {result.SuccessRate:P2}");
    Console.WriteLine($"  平均延迟: {result.AverageLatencyMs:F2}ms");
    Console.WriteLine($"  异常率: {result.ExceptionRate:P2}");
}

// 使用产能估算器
var estimator = new SimpleCapacityEstimator(estimationThresholds);
var capacity = estimator.Estimate(new CapacityHistory
{
    TestResults = results.TestResults
});

Console.WriteLine($"安全产能区间: {capacity.SafeMinParcelsPerMinute:F0} - {capacity.SafeMaxParcelsPerMinute:F0} 包裹/分钟");
Console.WriteLine($"危险阈值: {capacity.DangerousThresholdParcelsPerMinute:F0} 包裹/分钟");
```

## 验收标准

### 1. 仿真压力测试

在仿真中将放包间隔调得很小（例如 150ms）时：

- ✓ 拥堵等级会从 Normal → Warning → Severe
- ✓ 超载处置策略会明显被触发
- ✓ `sorting_overload_parcels_total` 指标上升
- ✓ 成功落格比例下降，异常口比例上升

### 2. 真实系统行为

即便用户连续疯狂放包裹，系统也不会阻止入口光电创建包裹：

- ✓ 只能根据策略将来不及分拣的包裹送异常口/回流
- ✓ 提供清晰的监控数据
- ✓ OverloadPolicy 的启用/禁用和参数调整可以通过配置管理

### 3. 监控与建议

系统提供：

- ✓ 当前拥堵等级（Normal/Warning/Severe）
- ✓ 当前在途包裹数
- ✓ 平均分拣延迟
- ✓ 超载包裹数量（按原因分类）
- ✓ 推荐安全产能区间（仅供参考，不强制执行）

## 配置示例

```json
{
  "CongestionDetection": {
    "Thresholds": {
      "WarningInFlightParcels": 50,
      "SevereInFlightParcels": 100,
      "WarningAverageLatencyMs": 3000,
      "SevereAverageLatencyMs": 5000,
      "WarningFailureRatio": 0.1,
      "SevereFailureRatio": 0.3
    }
  },
  "OverloadPolicy": {
    "Enabled": true,
    "ForceExceptionOnSevere": true,
    "ForceExceptionOnOverCapacity": false,
    "ForceExceptionOnTimeout": true,
    "ForceExceptionOnWindowMiss": false,
    "MaxInFlightParcels": null,
    "MinRequiredTtlMs": 500,
    "MinArrivalWindowMs": 200
  },
  "CapacityEstimation": {
    "Thresholds": {
      "MinSuccessRate": 0.95,
      "MaxAcceptableLatencyMs": 3000,
      "MaxExceptionRate": 0.05
    }
  }
}
```

## 注意事项

1. **不做上游节流**：系统不会阻止用户放包，只能被动处理
2. **产能建议**：产能估算结果仅供参考，不会反向控制放包行为
3. **策略配置**：超载策略可配置，不同现场可以根据实际情况调整
4. **监控为主**：重点是提供清晰的监控数据，帮助运维人员了解系统状态
5. **优雅降级**：当系统超载时，通过策略将包裹引导到异常口，而不是崩溃或错分

## 后续扩展

- [ ] 集成到 Host 层，提供 API 接口管理超载策略配置
- [ ] 在 Execution 层实际应用超载策略
- [ ] 完善 Grafana 仪表盘展示
- [ ] 添加更多产能测试场景
- [ ] 支持回流策略（如果拓扑支持）
