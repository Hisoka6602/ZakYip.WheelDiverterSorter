# PR-08 实现总结：拥堵检测、超载处置策略与产能估算（无节流控制）

## 实施时间
2025-11-18

## 核心理念

PR-08 实现了拥堵检测、超载处置策略与产能估算功能，**不实施上游节流控制**。核心理念是：

- **真实现场约束**：用户随时可以往皮带上丢包裹，系统无法阻止、无法规定"合适间隔"
- **被动防守**：不主动节流，只负责检测、策略处置和监控建议
- **优雅降级**：分拣不了的包裹有合理、可观测的异常策略（回流/异常口）
- **监控为主**：给出"当前产能极限"的监控与建议，供运维参考

## 已完成的工作

### 1. Core 层（ZakYip.Sorting.Core）

#### Runtime 命名空间

新增拥堵检测和产能估算的核心抽象：

**接口**：
- `ICongestionDetector`：拥堵检测器接口
- `ICapacityEstimator`：产能估算器接口

**数据结构**：
- `CongestionLevel` 枚举：Normal(0) / Warning(1) / Severe(2)
- `CongestionSnapshot` 记录：拥堵检测快照（在途数、延迟、失败率等）
- `CapacityHistory` 和 `CapacityTestResult` 记录：产能历史数据
- `CapacityEstimationResult` 记录：产能估算结果（安全区间、危险阈值）

**实现**（Policies 命名空间）：
- `ThresholdBasedCongestionDetector`：基于阈值的拥堵检测器
- `CongestionThresholds`：可配置的拥堵阈值（在途数、延迟、失败率）
- `SimpleCapacityEstimator`：简单产能估算器
- `CapacityEstimationThresholds`：可配置的估算阈值

#### Overload 命名空间

新增超载处置策略：

**接口**：
- `IOverloadHandlingPolicy`：超载处置策略接口

**数据结构**：
- `OverloadContext` 记录：超载上下文（包裹信息、线速、TTL、拥堵等级等）
- `OverloadDecision` 记录：超载决策（是否异常口、是否打标记、是否回流）

**实现**：
- `DefaultOverloadHandlingPolicy`：默认超载策略
- `OverloadPolicyConfiguration`：可配置的策略参数
  - 严重拥堵时是否强制异常
  - 超载时是否强制异常
  - TTL不足时是否强制异常
  - 窗口不足时是否强制异常
  - 最大在途包裹数限制
  - 最小所需TTL和到达窗口

### 2. Observability 层

#### 新增 Prometheus 指标

- `sorting_overload_parcels_total{reason}`：超载包裹计数器
  - 支持的 reason 标签：`Timeout`, `WindowMiss`, `CapacityExceeded`
- `sorting_capacity_recommended_parcels_per_minute`：推荐产能（仅供参考）
- `sorting_average_latency_ms`：平均分拣延迟

#### 新增方法

- `RecordOverloadParcel(string reason)`：记录超载包裹
- `SetRecommendedCapacity(double parcelsPerMinute)`：设置推荐产能
- `SetAverageLatency(double latencyMs)`：设置平均延迟
- `SetCongestionLevel(int level)`：已存在，无需新增

### 3. Simulation 层

#### 新增服务

- `CapacityTestingRunner`：产能测试运行器
  - 支持使用不同放包间隔运行多次仿真
  - 收集成功率、延迟、异常率等数据
  - 生成 `CapacityTestResults` 结果集

#### 新增场景

- `CreateCapacityTestBaseScenario(int parcelCount)`：产能测试基础场景
  - 默认配置：100个包裹，1 m/s线速，轮询模式
  - 10个正常格口（1-10），1个异常口（11）
  - 支持通过不同间隔测试系统产能

### 4. 文档

- **PR08_USAGE_GUIDE.md**：完整使用指南
  - 核心设定说明
  - 各层代码示例
  - Prometheus 查询示例
  - 配置示例
  - 验收标准

- **PR08_OVERLOAD_IMPLEMENTATION_SUMMARY.md**：本文档

## 设计原则

### 1. 被动防守，不主动节流

| 做的事情 ✅ | 不做的事情 ❌ |
|------------|--------------|
| 检测拥堵状态 | 阻止用户放包 |
| 根据策略处理超载包裹 | 控制放包间隔 |
| 提供监控数据和建议 | 实施上游节流 |
| 引导包裹到异常口/回流 | 强制执行"最佳实践" |

### 2. 策略可配置

超载策略支持灵活配置：

```csharp
var config = new OverloadPolicyConfiguration
{
    Enabled = true,                        // 是否启用
    ForceExceptionOnSevere = true,         // 严重拥堵→异常口
    ForceExceptionOnTimeout = true,        // TTL不足→异常口
    ForceExceptionOnWindowMiss = false,    // 窗口不足→仅打标记
    MaxInFlightParcels = 120,              // 在途数上限
    MinRequiredTtlMs = 500,                // 最小TTL
    MinArrivalWindowMs = 200               // 最小到达窗口
};
```

### 3. 监控为主

重点提供清晰的监控数据：

| 指标 | 说明 | 用途 |
|------|------|------|
| `sorting_congestion_level` | 拥堵等级 (0/1/2) | 实时状态监控 |
| `sorting_inflight_parcels` | 在途包裹数 | 负载监控 |
| `sorting_average_latency_ms` | 平均延迟 | 性能监控 |
| `sorting_overload_parcels_total` | 超载包裹数 | 异常分析 |
| `sorting_capacity_recommended_parcels_per_minute` | 推荐产能 | 容量规划 |

### 4. 优雅降级

当系统超载时：
- ✅ 不崩溃
- ✅ 不错分（SortedToWrongChute 始终为 0）
- ✅ 按策略引导到异常口或回流
- ✅ 打标记供上游重试

## 架构分层

```
┌─────────────────────────────────────────┐
│ Host/Application Layer                  │  ⏳ 待实现：配置API
├─────────────────────────────────────────┤
│ Observability Layer                     │  ✅ 指标定义和记录
├─────────────────────────────────────────┤
│ Simulation Layer                        │  ✅ 产能测试框架
├─────────────────────────────────────────┤
│ Execution Layer                         │  ⏳ 待实现：应用策略
├─────────────────────────────────────────┤
│ Core Layer (Sorting.Core)               │  ✅ 完整抽象和实现
│  - Runtime: 拥堵检测、产能估算          │
│  - Overload: 超载策略                   │
│  - Policies: 具体实现                   │
└─────────────────────────────────────────┘
```

## 验收标准

### ✅ 已实现

1. **Core层抽象完整**
   - 所有接口和数据结构定义清晰
   - 有默认实现可直接使用
   - 支持灵活配置

2. **Observability层指标齐全**
   - 5个核心指标覆盖拥堵、超载、产能
   - 方法签名清晰，易于集成
   - 支持按原因分类统计

3. **Simulation层框架完备**
   - `CapacityTestingRunner` 支持批量测试
   - 场景定义支持参数化间隔
   - 结果数据结构完整

4. **文档详细**
   - 使用指南包含代码示例
   - 配置示例完整
   - 验收标准明确

### ⏳ 待完成

1. **Execution层集成**

   需要在以下位置应用策略：

   a) **包裹创建时**（入口光电触发）：
   ```csharp
   // 伪代码
   var snapshot = CollectCongestionSnapshot();
   var level = _congestionDetector.Detect(snapshot);
   
   var context = new OverloadContext
   {
       ParcelId = parcelId,
       CurrentCongestionLevel = level,
       InFlightParcels = snapshot.InFlightParcels,
       // ... 其他信息
   };
   
   var decision = _overloadPolicy.Evaluate(context);
   if (decision.ShouldForceException)
   {
       // 创建异常口分拣计划
       CreateExceptionPlan(parcelId, decision.Reason);
       _metrics.RecordOverloadParcel(decision.Reason);
   }
   else
   {
       // 正常分拣流程
       CreateNormalPlan(parcelId);
   }
   ```

   b) **路径规划阶段**（EjectPlanner）：
   ```csharp
   // 伪代码
   var estimatedTTL = CalculateRemainingTTL(parcel);
   var arrivalWindow = CalculateArrivalWindow(parcel, targetChute);
   
   if (estimatedTTL < _config.MinRequiredTtlMs || 
       arrivalWindow < _config.MinArrivalWindowMs)
   {
       var context = new OverloadContext { /* ... */ };
       var decision = _overloadPolicy.Evaluate(context);
       // 根据决策调整计划
   }
   ```

   c) **拥堵数据采集**：
   ```csharp
   // 伪代码
   public class CongestionDataCollector
   {
       private readonly CircularBuffer<ParcelLatency> _recentLatencies;
       
       public CongestionSnapshot CollectSnapshot()
       {
           return new CongestionSnapshot
           {
               InFlightParcels = GetInFlightCount(),
               AverageLatencyMs = _recentLatencies.Average(),
               MaxLatencyMs = _recentLatencies.Max(),
               FailureRatio = CalculateFailureRatio(),
               TimeWindowSeconds = 60,
               TotalSampledParcels = _recentLatencies.Count
           };
       }
   }
   ```

2. **Host/Application层API**

   需要实现配置管理接口：

   a) **配置控制器**：
   ```csharp
   [ApiController]
   [Route("api/config")]
   public class OverloadPolicyController : ControllerBase
   {
       [HttpGet("overload-policy")]
       public ActionResult<OverloadPolicyConfiguration> GetConfig()
       {
           // 从配置存储读取
       }
       
       [HttpPut("overload-policy")]
       public ActionResult UpdateConfig([FromBody] OverloadPolicyConfiguration config)
       {
           // 验证并保存配置
           // 通知相关服务重新加载
       }
   }
   ```

   b) **产能查询接口**：
   ```csharp
   [HttpGet("capacity-estimation")]
   public ActionResult<CapacityEstimationResult> GetCapacity()
   {
       // 返回最新的产能估算结果
   }
   
   [HttpPost("capacity-test")]
   public async Task<ActionResult> StartCapacityTest(
       [FromBody] CapacityTestRequest request)
   {
       // 触发产能测试任务
   }
   ```

3. **实际产能测试集成**

   `CapacityTestingRunner` 需要与仿真运行器集成：
   ```csharp
   // 需要实现的工厂模式或服务注入
   public class CapacityTestService
   {
       public async Task<CapacityTestResults> RunTestAsync(
           int[] intervals, int parcelsPerTest)
       {
           var results = new List<CapacityTestResult>();
           
           foreach (var interval in intervals)
           {
               // 重新初始化仿真环境
               var runner = CreateSimulationRunner(interval);
               var summary = await runner.RunAsync();
               
               // 转换为测试结果
               results.Add(ConvertToTestResult(interval, summary));
           }
           
           return new CapacityTestResults
           {
               TestResults = results,
               // ...
           };
       }
   }
   ```

4. **Grafana仪表盘**

   创建专门的监控面板：
   
   a) **拥堵等级**：
   ```promql
   sorting_congestion_level
   ```
   
   b) **吞吐量 vs 推荐产能**：
   ```promql
   # 实际吞吐（包裹/分钟）
   rate(sorter_parcel_throughput_total[1m]) * 60
   
   # vs
   
   # 推荐产能
   sorting_capacity_recommended_parcels_per_minute
   ```
   
   c) **超载包裹堆叠图**：
   ```promql
   rate(sorting_overload_parcels_total[5m]) by (reason)
   ```
   
   d) **性能指标**：
   ```promql
   # 在途包裹数
   sorting_inflight_parcels
   
   # 平均延迟
   sorting_average_latency_ms
   ```

## 使用示例

### 基本使用

```csharp
// 1. 创建拥堵检测器
var detector = new ThresholdBasedCongestionDetector(new CongestionThresholds
{
    WarningInFlightParcels = 50,
    SevereInFlightParcels = 100,
    // ...
});

// 2. 创建超载策略
var policy = new DefaultOverloadHandlingPolicy(new OverloadPolicyConfiguration
{
    Enabled = true,
    ForceExceptionOnSevere = true,
    // ...
});

// 3. 在分拣流程中使用
var snapshot = CollectCongestionData();
var level = detector.Detect(snapshot);

var context = new OverloadContext
{
    ParcelId = "P001",
    CurrentCongestionLevel = level,
    // ...
};

var decision = policy.Evaluate(context);
if (decision.ShouldForceException)
{
    RouteToException(decision.Reason);
    metrics.RecordOverloadParcel(decision.Reason);
}
```

### 产能测试

```csharp
// 创建测试运行器
var capacityRunner = new CapacityTestingRunner(simulationRunner, logger);

// 定义测试间隔
var intervals = new[] { 1000, 800, 600, 400, 300, 250, 200, 150 };

// 运行测试
var results = await capacityRunner.RunCapacityTestAsync(
    baseScenario,
    intervals,
    parcelsPerTest: 100
);

// 估算产能
var estimator = new SimpleCapacityEstimator(thresholds);
var capacity = estimator.Estimate(new CapacityHistory
{
    TestResults = results.TestResults
});

Console.WriteLine($"安全区间: {capacity.SafeMinParcelsPerMinute:F0} - " +
                 $"{capacity.SafeMaxParcelsPerMinute:F0} 包裹/分钟");
```

## 技术债务

1. **CapacityTestingRunner 的实际集成**
   - 当前只是框架，未与仿真运行器实际集成
   - 需要支持动态重新初始化仿真环境

2. **Execution 层的策略应用**
   - 需要找到合适的切入点
   - 需要无缝集成到现有流程

3. **配置持久化**
   - 当前只有内存模型
   - 需要实现配置的加载和保存

4. **测试覆盖**
   - 需要更多单元测试
   - 需要集成测试验证端到端流程

## 总结

✅ **PR-08 核心价值**：
- 提供完整的拥堵检测和超载处置框架
- 不干预用户放包，只负责被动防守
- 策略可配置，适应不同现场需求
- 监控为主，提供决策建议而非强制控制

📊 **完成度**：
- Core 层：100%（抽象和实现完整）
- Observability 层：100%（指标定义完整）
- Simulation 层：80%（框架完成，需集成）
- Execution 层：0%（待实现）
- Host/Application 层：0%（待实现）

🎯 **下一步**：
1. Execution 层应用策略（优先级高）
2. Host 层配置 API（优先级高）
3. 产能测试实际集成（优先级中）
4. Grafana 仪表盘（优先级中）
5. 单元测试和文档补充（优先级低）
