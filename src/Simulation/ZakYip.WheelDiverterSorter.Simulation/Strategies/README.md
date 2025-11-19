# Strategy Experiment Framework / 策略实验框架

## Quick Start / 快速开始

Run the demo to see the framework in action:

```bash
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run strategy-experiment-demo
```

This will:
1. Run 3 different strategy profiles (Baseline, Aggressive, Conservative)
2. Simulate 500 parcels for each profile with the same random seed
3. Generate CSV and Markdown comparison reports in `./reports/strategy/`

## Overview / 概述

The Strategy Experiment Framework enables A/B/N comparison testing of different strategy parameters (OverloadPolicy, congestion thresholds, routing parameters) in the simulation environment.

策略实验框架支持在仿真环境中对不同策略参数（OverloadPolicy、拥堵阈值、路由参数）进行 A/B/N 对比测试。

### Key Features / 主要特性

- **Fair Comparison / 公平对比**: All profiles run with the same virtual parcel flow (same random seed, release rhythm, upstream instructions)
- **Multi-dimensional Metrics / 多维度指标**: Compare throughput, success rate, exception rate, overload events, latency
- **Automated Reports / 自动报表**: Generate CSV and Markdown reports for analysis

## Architecture / 架构

### Components / 组件

1. **Core Layer** (`ZakYip.Sorting.Core.Overload`)
   - `StrategyProfile`: Strategy configuration model
   - `IStrategyFactory`: Strategy instance factory interface

2. **Execution Layer** (`ZakYip.WheelDiverterSorter.Execution`)
   - `DefaultStrategyFactory`: Default implementation

3. **Simulation Layer** (`ZakYip.WheelDiverterSorter.Simulation.Strategies`)
   - `StrategyExperimentConfig`: Experiment configuration
   - `StrategyExperimentResult`: Experiment results
   - `StrategyExperimentRunner`: Experiment runner
   - `StrategyExperimentReportWriter`: Report generator

## Configuration / 配置

### Strategy Profile Example / 策略 Profile 示例

See `simulation-config/strategy-profiles/example-profiles.json` for sample configurations.

```json
{
  "profiles": [
    {
      "profileName": "Baseline",
      "description": "基线策略（生产默认配置）",
      "overloadPolicy": {
        "enabled": true,
        "forceExceptionOnSevere": true,
        "forceExceptionOnTimeout": true,
        "minRequiredTtlMs": 500,
        "minArrivalWindowMs": 200
      }
    }
  ]
}
```

## Usage / 使用方法

### 1. Demo Mode / 演示模式

```bash
dotnet run strategy-experiment-demo
```

### 2. Programmatic API / 编程接口

```csharp
var factory = new DefaultStrategyFactory(logger);
var runner = new StrategyExperimentRunner(factory, logger);

var config = new StrategyExperimentConfig
{
    ParcelCount = 1000,
    ReleaseInterval = TimeSpan.FromMilliseconds(300),
    RandomSeed = 12345,
    Profiles = profiles,
    OutputDirectory = "./reports/strategy"
};

var results = await runner.RunExperimentAsync(config);
```

## Reports / 报表

### CSV Report / CSV 报表

Compact format for easy import into spreadsheet tools:
```csv
ProfileName,Description,TotalParcels,SuccessParcels,ExceptionParcels,SuccessRatio,...
Baseline,基线策略,500,476,24,0.9537,...
```

### Markdown Report / Markdown 报表

Detailed comparison with:
- Overall comparison table
- Detailed configuration for each profile
- Statistics and overload reason distribution

## Documentation / 文档

See `docs/STRATEGY_EXPERIMENT_GUIDE.md` for comprehensive documentation.

## Production Impact / 生产影响

**Zero production impact**: This framework only runs in simulation environment and does not affect production systems.

**生产零影响**：此框架仅在仿真环境运行，不影响生产系统。

## Next Steps / 后续步骤

The current implementation provides a complete framework with placeholder simulation data. To enable real experiments:

1. Integrate `StrategyExperimentRunner` with actual `SimulationRunner`
2. Support injecting different `IOverloadHandlingPolicy` instances into simulation environment
3. Collect real statistics from simulation runs

当前实现提供了完整框架和占位数据。要启用真实实验：

1. 将 `StrategyExperimentRunner` 与实际的 `SimulationRunner` 集成
2. 支持向仿真环境注入不同的 `IOverloadHandlingPolicy` 实例
3. 从仿真运行中收集真实统计数据
