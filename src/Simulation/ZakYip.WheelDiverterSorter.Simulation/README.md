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

必须遵守的接口约定：
- 驱动：使用 `Drivers/Vendors/Simulated/` 的 `IWheelDiverterDriver` 实现
- 上游：使用 `InMemoryRuleEngineClient` 实现 `IUpstreamRoutingClient`
- 传感器：使用 `MockSensor` 实现 `ISensor`

违反检测：`TechnicalDebtComplianceTests` 会检测影分身类型。
