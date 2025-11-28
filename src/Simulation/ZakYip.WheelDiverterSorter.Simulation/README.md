# 摆轮分拣系统仿真项目

## 简介

本项目是一个独立的仿真 Console 宿主，用于模拟摆轮分拣系统的运行。它通过依赖注入挂载"模拟驱动 + 模拟传感器 + 模拟上游 RuleEngine"，在本地一次性跑完一批"虚拟包裹"，并输出每个包裹的分拣结果与统计汇总。

## 特点

- **无需硬件**: 完全在内存中运行，不连接任何真实硬件设备
- **快速验证**: 快速验证分拣逻辑和路径生成算法
- **摩擦模拟**: 支持随机摩擦因子，模拟包裹在传送带上的速度差异
- **掉包模拟**: 支持包裹中途掉落场景，验证异常处理
- **不同输送线长度**: 每个格口的输送线长度各不相同（800-1500mm）
- **统计报告**: 自动生成详细中文统计报告，包括成功率、格口分布、行程时间等
- **灵活配置**: 支持多种分拣模式和自定义参数
- **"不允许错分"验证**: 自动验证 `SortedToWrongChute` 计数始终为 0

## 使用方法

### 运行仿真

```bash
dotnet run --project ZakYip.WheelDiverterSorter.Simulation
```

### 配置说明

编辑 `appsettings.Simulation.json` 来配置仿真参数：

```json
{
  "Simulation": {
    "ParcelCount": 50,                    // 包裹数量
    "LineSpeedMmps": 500,                 // 线速（毫米/秒）
    "ParcelInterval": "00:00:00.500",     // 包裹间隔（时间跨度格式）
    "SortingMode": "RoundRobin",          // 分拣模式：Formal / FixedChute / RoundRobin
    "FixedChuteIds": [1, 2, 3, 4, 5],    // 固定格口ID列表
    "ExceptionChuteId": 999,              // 异常格口ID
    "IsEnableRandomFriction": true,       // 是否启用摩擦模拟
    "IsEnableRandomDropout": true,        // 是否启用掉包模拟
    "FrictionModel": {
      "MinFactor": 0.85,                  // 最小摩擦因子
      "MaxFactor": 1.15,                  // 最大摩擦因子
      "IsDeterministic": true,            // 是否使用确定性随机
      "Seed": 42                          // 随机种子
    },
    "DropoutModel": {
      "DropoutProbabilityPerSegment": 0.02, // 每段掉包概率
      "AllowedSegments": null,            // 允许掉包的段（null=所有段）
      "Seed": 123                         // 随机种子
    },
    "IsEnableVerboseLogging": true,       // 是否启用详细日志
    "IsPauseAtEnd": true                  // 是否在结束时等待用户按键
  }
}
```

### 分拣模式

- **RoundRobin**: 按顺序轮流分配到 `FixedChuteIds` 中的格口
- **FixedChute**: 随机分配到 `FixedChuteIds` 中的格口
- **Formal**: 使用包裹ID的哈希值模拟真实规则引擎的决策

### 命令行参数

也可以通过命令行参数覆盖配置：

```bash
dotnet run --project ZakYip.WheelDiverterSorter.Simulation -- \
  --Simulation:ParcelCount=100 \
  --Simulation:SortingMode=RoundRobin
```

## 输出示例

```
═══════════════════════════════════════════════════════════
                   仿真配置摘要
═══════════════════════════════════════════════════════════
包裹数量：          50
线速：              500 mm/s
包裹间隔：          0.50 秒
分拣模式：          RoundRobin
异常格口：          999
随机故障注入：      禁用
详细日志：          启用
═══════════════════════════════════════════════════════════

... (处理日志) ...

═══════════════════════════════════════════════════════════
                   仿真统计报告
═══════════════════════════════════════════════════════════
总包裹数：          50
成功分拣数：        50
失败分拣数：        0
成功率：            100.00 %
总耗时：            26.45 秒
平均每包耗时：      528.96 毫秒

格口分拣统计：
───────────────────────────────────────────────────────────
格口ID            分拣数量            百分比            
───────────────────────────────────────────────────────────
1               10              20.00%
2               10              20.00%
3               10              20.00%
4               10              20.00%
5               10              20.00%
═══════════════════════════════════════════════════════════
```

## 项目结构

```
ZakYip.WheelDiverterSorter.Simulation/
├── Configuration/                    # 仿真配置
│   ├── SimulationOptions.cs         # 仿真配置模型 [公共API]
│   ├── FrictionModelOptions.cs      # 摩擦模型配置
│   ├── DropoutModelOptions.cs       # 掉包模型配置
│   ├── DenseParcelStrategy.cs       # 密集包裹策略
│   └── SensorFaultOptions.cs        # 传感器故障配置
├── Services/                         # 仿真服务
│   ├── ISimulationScenarioRunner.cs # 场景运行器接口 [公共API]
│   ├── SimulationScenarioRunner.cs  # 场景运行器实现
│   ├── SimulationRunner.cs          # 仿真主运行器
│   ├── ParcelTimelineFactory.cs     # 包裹时间轴工厂
│   ├── SimulationReportPrinter.cs   # 报告打印器
│   ├── CapacityTestingRunner.cs     # 容量测试运行器
│   └── CongestionMetricsCollector.cs # 拥堵指标收集器
├── Results/                          # 仿真结果模型
│   ├── ParcelSimulationResult.cs    # 包裹仿真结果
│   ├── ParcelSimulationStatus.cs    # 包裹状态枚举
│   └── SimulationSummary.cs         # 汇总统计 [公共API]
├── Scenarios/                        # 场景定义
│   ├── SimulationScenario.cs        # 仿真场景定义
│   ├── ScenarioDefinitions.cs       # 预定义标准测试场景
│   ├── ChaosScenarioDefinitions.cs  # 混沌测试场景
│   ├── ParcelExpectation.cs         # 包裹期望结果
│   └── SimulationScenarioSerializer.cs
├── Strategies/                       # 策略实验
│   ├── StrategyExperimentRunner.cs  # 策略实验运行器
│   ├── StrategyExperimentConfig.cs  # 策略实验配置
│   ├── StrategyExperimentResult.cs  # 实验结果
│   └── Reports/
│       └── StrategyExperimentReportWriter.cs
├── Program.cs                        # 入口程序（独立可执行）
├── appsettings.Simulation.json      # 仿真配置文件
├── appsettings.LongRun.json         # 长时运行配置
├── simulation-config/               # 仿真拓扑配置
├── reports/                         # 报告输出目录
├── SIMULATION_GUIDE.md              # 详细使用指南
└── README.md                        # 本文件
```

## 公共 API 与内部实现

### 公共 API（供 Host 层使用）

Host 层可以通过 `AddSimulationServices()` 方法注册仿真服务，并使用以下公共接口：

- **`ISimulationScenarioRunner`**: 场景运行器接口，提供 `RunScenarioAsync()` 方法
- **`SimulationOptions`**: 仿真配置模型，用于配置仿真参数
- **`SimulationSummary`**: 仿真结果汇总，包含成功率、错分数等统计信息

**使用示例**（在 Host 层）：

```csharp
// 注册仿真服务（在 Host 启动时）
services.AddSimulationServices(configuration);

// 通过 API 触发仿真
public class SimulationController
{
    private readonly ISimulationScenarioRunner _runner;
    
    public async Task<SimulationSummary> RunSimulation(SimulationOptions options)
    {
        return await _runner.RunScenarioAsync(options);
    }
}
```

### 内部实现（仅限 Simulation 项目内部使用）

以下类型为内部实现细节，Host 层不应直接依赖：

- `SimulationRunner`: 仿真主运行器实现
- `ParcelTimelineFactory`: 包裹时间轴工厂
- `SimulationReportPrinter`: 报告打印器
- `ScenarioDefinitions`: 预定义场景集合
- `Strategies/` 目录下的策略实验相关类

## 依赖关系

本项目引用以下项目，使用其提供的接口和服务：

- `ZakYip.WheelDiverterSorter.Core` - 核心模型和接口
- `ZakYip.WheelDiverterSorter.Execution` - 路径执行器
- `ZakYip.WheelDiverterSorter.Drivers` - 驱动程序（使用 Simulated 实现）
- `ZakYip.WheelDiverterSorter.Ingress` - 入口检测
- `ZakYip.WheelDiverterSorter.Observability` - 可观测性

**注意**: 本项目既是独立可执行程序（Console Host），也被 `Host` 项目引用以提供 API 触发仿真功能。

## 扩展

未来可以在 `Scenarios/` 目录中添加各种仿真场景：

- 高负载场景
- 故障注入场景
- 特殊分拣规则场景
- 性能基准测试场景

## 注意事项

- 仿真使用 `Drivers/Vendors/Simulated/` 下的模拟驱动器，不进行真实的硬件通信
- 仿真使用内存 RuleEngine 客户端（`InMemoryRuleEngineClient`），不连接真实的规则引擎
- 所有接口均来自现有层，没有仿真专用的硬编码路径
- **重要**: `SortedToWrongChute` 计数必须始终为 0，如果出现非零值说明系统存在严重问题

## 详细文档

请参阅 [SIMULATION_GUIDE.md](./SIMULATION_GUIDE.md) 获取更详细的使用指南和技术说明。
