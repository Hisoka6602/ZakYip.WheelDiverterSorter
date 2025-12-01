# ZakYip.WheelDiverterSorter.Simulation

仿真服务库，提供仿真场景运行器和结果统计。

## 职责

- 提供 `ISimulationScenarioRunner` 接口
- 支持多种仿真场景（摩擦/掉包/高负载等）
- 生成统计报告

## 使用方式

```bash
# 通过 CLI 运行仿真
dotnet run --project src/Simulation/ZakYip.WheelDiverterSorter.Simulation.Cli
```

## 关键规范

> **禁止创建影分身**：仿真使用现有层的接口，不创建仿真专用的硬编码路径。

- 使用 `Drivers/Vendors/Simulated/` 的模拟驱动
- 使用 `InMemoryRuleEngineClient` 模拟上游
