# 策略实验指南 / Strategy Experiment Guide

## 目的 / Purpose

策略实验框架用于在仿真环境中对多组策略参数（主要是 OverloadPolicy / 拥堵阈值 / 路由相关参数）进行 A/B/N 对比试验。

The strategy experiment framework is used to conduct A/B/N comparison tests of multiple sets of strategy parameters (mainly OverloadPolicy / congestion thresholds / routing-related parameters) in the simulation environment.

### 主要功能 / Main Features

- **公平对比 / Fair Comparison**: 在同一条"虚拟包裹流"下运行（相同随机种子、相同放包节奏、相同上游指令分布）
- **多维度指标 / Multi-dimensional Metrics**: 对比总处理件数、正常落格比例、异常口比例、Overload 触发次数、平均/最大分拣延迟
- **报表输出 / Report Output**: 自动生成 CSV + Markdown 报表，供人工分析、调参

## 架构设计 / Architecture Design

### 分层结构 / Layered Structure

1. **Core 层 / Core Layer** (`ZakYip.Sorting.Core.Overload`)
   - `StrategyProfile`: 策略配置 Profile 模型
   - `IStrategyFactory`: 策略实例工厂抽象

2. **Execution 层 / Execution Layer** (`ZakYip.WheelDiverterSorter.Execution`)
   - `DefaultStrategyFactory`: 默认策略工厂实现

3. **Simulation 层 / Simulation Layer** (`ZakYip.WheelDiverterSorter.Simulation.Strategies`)
   - `StrategyExperimentConfig`: 实验配置模型
   - `StrategyExperimentResult`: 实验结果模型
   - `StrategyExperimentRunner`: 实验运行器
   - `StrategyExperimentReportWriter`: 报表写入器

### 关键约束 / Key Constraints

- **生产环境零影响 / Zero Production Impact**: 生产实例仍只使用当前默认策略，本功能完全限定在仿真场景
- **单线体模拟 / Single Line Simulation**: 不引入多线体概念，仍然是"一条线体 = 一个实例"

## 配置文件结构 / Configuration File Structure

### 策略 Profile 配置示例 / Strategy Profile Configuration Example

位置 / Location: `simulation-config/strategy-profiles/example-profiles.json`

```json
{
  "profiles": [
    {
      "profileName": "Baseline",
      "description": "基线策略（生产默认配置）",
      "overloadPolicy": {
        "enabled": true,
        "forceExceptionOnSevere": true,
        "forceExceptionOnOverCapacity": false,
        "forceExceptionOnTimeout": true,
        "forceExceptionOnWindowMiss": false,
        "maxInFlightParcels": null,
        "minRequiredTtlMs": 500,
        "minArrivalWindowMs": 200
      },
      "routeTimeBudgetFactor": 1.0
    },
    {
      "profileName": "AggressiveOverload",
      "description": "更激进的超载策略（更低阈值，更早触发异常）",
      "overloadPolicy": {
        "enabled": true,
        "forceExceptionOnSevere": true,
        "forceExceptionOnOverCapacity": true,
        "forceExceptionOnTimeout": true,
        "forceExceptionOnWindowMiss": true,
        "maxInFlightParcels": 50,
        "minRequiredTtlMs": 800,
        "minArrivalWindowMs": 300
      },
      "routeTimeBudgetFactor": 0.9
    }
  ]
}
```

### 配置字段说明 / Configuration Field Description

#### StrategyProfile

| 字段 / Field | 类型 / Type | 说明 / Description |
|-------------|------------|-------------------|
| `profileName` | string | Profile 名称，如 "Baseline", "Aggressive" |
| `description` | string | 中文描述，方便报表展示 |
| `overloadPolicy` | OverloadPolicyConfiguration | 超载策略配置 |
| `routeTimeBudgetFactor` | decimal? | 路由时间预算系数（可选，默认 1.0） |

#### OverloadPolicyConfiguration

| 字段 / Field | 类型 / Type | 默认值 / Default | 说明 / Description |
|-------------|------------|-----------------|-------------------|
| `enabled` | bool | true | 是否启用超载检测 |
| `forceExceptionOnSevere` | bool | true | 严重拥堵时是否直接路由到异常口 |
| `forceExceptionOnOverCapacity` | bool | false | 超过在途包裹容量时是否强制异常 |
| `forceExceptionOnTimeout` | bool | true | TTL不足时是否强制异常 |
| `forceExceptionOnWindowMiss` | bool | false | 到达窗口不足时是否强制异常 |
| `maxInFlightParcels` | int? | null | 最大允许在途包裹数（null表示不限制） |
| `minRequiredTtlMs` | double | 500 | 最小所需剩余TTL（毫秒） |
| `minArrivalWindowMs` | double | 200 | 最小到达窗口（毫秒） |

## 如何执行策略实验 / How to Execute Strategy Experiments

### 命令行方式 / Command Line

```bash
dotnet run --project ZakYip.WheelDiverterSorter.Simulation \
  -- scenario strategy-experiment \
  --profiles "simulation-config/strategy-profiles/example-profiles.json" \
  --parcel-count 1000 \
  --release-interval 300ms \
  --seed 12345 \
  --output "./reports/strategy"
```

### 参数说明 / Parameter Description

| 参数 / Parameter | 说明 / Description |
|-----------------|-------------------|
| `--profiles` | 策略 Profile 配置文件路径 |
| `--parcel-count` | 每个策略下仿真的包裹数量 |
| `--release-interval` | 放包间隔（如 "300ms"） |
| `--seed` | 固定随机种子，确保各策略一致 |
| `--output` | 报表输出目录 |

### 编程方式 / Programmatic API

```csharp
using ZakYip.Sorting.Core.Overload;
using ZakYip.WheelDiverterSorter.Simulation.Strategies;
using ZakYip.WheelDiverterSorter.Execution;

// 创建策略工厂
var strategyFactory = new DefaultStrategyFactory(logger);

// 创建实验运行器
var experimentRunner = new StrategyExperimentRunner(strategyFactory, logger);

// 定义实验配置
var config = new StrategyExperimentConfig
{
    ParcelCount = 1000,
    ReleaseInterval = TimeSpan.FromMilliseconds(300),
    RandomSeed = 12345,
    Profiles = new[]
    {
        new StrategyProfile
        {
            ProfileName = "Baseline",
            Description = "基线策略",
            OverloadPolicy = new OverloadPolicyConfiguration
            {
                Enabled = true,
                ForceExceptionOnTimeout = true,
                MinRequiredTtlMs = 500
            }
        },
        // ... 更多 profiles
    },
    OutputDirectory = "./reports/strategy"
};

// 运行实验
var results = await experimentRunner.RunExperimentAsync(config);
```

## 报表格式 / Report Format

### CSV 报表 / CSV Report

文件名示例 / Filename Example: `strategy-experiment-2025-11-18-123456.csv`

```csv
ProfileName,Description,TotalParcels,SuccessParcels,ExceptionParcels,SuccessRatio,ExceptionRatio,OverloadEvents,AvgLatencyMs,MaxLatencyMs
Baseline,基线策略,1000,980,20,0.9800,0.0200,15,450.00,1200.00
AggressiveOverload,更激进的超载策略,1000,950,50,0.9500,0.0500,80,380.00,900.00
```

### Markdown 报表 / Markdown Report

文件名示例 / Filename Example: `strategy-experiment-2025-11-18-123456.md`

生成的 Markdown 报表包含：

- 整体对比表格
- 每个 Profile 的详细配置
- 统计结果
- Overload 原因分布（如果有）

## 最佳实践 / Best Practices

### 1. 策略对比场景 / Strategy Comparison Scenarios

- **基线对比 / Baseline Comparison**: 始终包含一个"基线"Profile，对应当前生产配置
- **单变量实验 / Single Variable Experiments**: 每次只调整一个参数，方便分析影响
- **极端场景测试 / Extreme Scenario Testing**: 测试激进和保守两个极端，找到平衡点

### 2. 随机种子选择 / Random Seed Selection

- 使用**固定种子**确保公平对比
- 可以运行**多个种子**的实验，观察策略在不同流量模式下的表现
- 建议种子值: `12345`, `67890`, `11111`, `22222`, `33333`

### 3. 包裹数量选择 / Parcel Count Selection

- **快速验证 / Quick Validation**: 100-500 个包裹
- **标准测试 / Standard Testing**: 1000-2000 个包裹
- **压力测试 / Stress Testing**: 5000+ 个包裹

### 4. 调参建议 / Parameter Tuning Recommendations

调整生产 OverloadPolicy 前，**务必**在仿真中跑一轮新旧策略对比：

Before adjusting production OverloadPolicy, **always** run a comparison between new and old strategies in simulation:

1. 定义 Baseline Profile（当前生产配置）
2. 定义 NewStrategy Profile（计划调整的配置）
3. 运行实验，对比关键指标
4. 分析报表，确认新策略带来的改进
5. 在生产环境小流量灰度测试
6. 全量上线

### 5. 报表分析要点 / Report Analysis Points

关注以下关键指标 / Focus on the following key metrics:

- **成功率 / Success Rate**: 应尽可能高（≥95%）
- **Overload 次数 / Overload Events**: 适中即可，不是越少越好（过于保守可能导致拥堵）
- **平均延迟 / Average Latency**: 反映系统效率
- **最大延迟 / Max Latency**: 反映极端情况处理能力

### 6. 场景切换 / Scenario Switching

可以为不同的业务场景创建不同的 Profile 配置文件：

- `high-volume-profiles.json`: 高峰期配置
- `maintenance-profiles.json`: 维护期配置
- `low-volume-profiles.json`: 低峰期配置

## 扩展说明 / Extension Notes

### 未来扩展方向 / Future Extension Directions

1. **路由策略 Profile 化 / Routing Strategy Profiling**
   - 在 `StrategyProfile` 中增加路由相关参数
   - 在 `IStrategyFactory` 中增加 `CreateRoutePolicy()` 方法

2. **多线体实验 / Multi-Line Experiments**
   - 支持在单次实验中运行多条线体
   - 对比不同线体配置的性能

3. **实时监控集成 / Real-time Monitoring Integration**
   - 将实验结果推送到 Prometheus
   - 在 Grafana 中可视化对比

4. **自动调参 / Auto-tuning**
   - 基于实验结果自动推荐最优参数组合
   - 支持超参数搜索算法

## 故障排查 / Troubleshooting

### 常见问题 / Common Issues

1. **配置文件格式错误 / Configuration File Format Error**
   - 检查 JSON 格式是否正确
   - 确认所有必填字段都已填写

2. **报表未生成 / Report Not Generated**
   - 检查输出目录是否有写权限
   - 查看日志中是否有错误信息

3. **实验结果不一致 / Inconsistent Experiment Results**
   - 确认使用了相同的 `randomSeed`
   - 检查是否有外部因素影响（如系统负载）

## 技术支持 / Technical Support

如有问题或建议，请：

- 查看项目 GitHub Issues
- 参考项目文档: `DOCUMENTATION_INDEX.md`
- 查看实现总结: `PR18_IMPLEMENTATION_SUMMARY.md`（待创建）

---

**注意 / Note**: 本功能仅在仿真环境中使用，不会影响生产系统的运行。

This feature is only used in the simulation environment and will not affect the operation of the production system.
