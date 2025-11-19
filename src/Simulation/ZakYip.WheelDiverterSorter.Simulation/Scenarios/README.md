# Simulation Scenarios | 仿真场景

This directory contains predefined simulation scenarios for testing the wheel diverter sorter system under various conditions.

本目录包含预定义的仿真场景，用于在各种条件下测试摆轮分拣系统。

## Overview | 概述

Simulation scenarios allow us to test the system's behavior under different combinations of:
- Friction variance (simulating different parcel speeds)
- Dropout probability (simulating package loss)
- Sorting modes (Formal, FixedChute, RoundRobin)

仿真场景允许我们在以下不同组合条件下测试系统行为：
- 摩擦差异（模拟不同的包裹速度）
- 掉包概率（模拟包裹丢失）
- 分拣模式（正式、固定格口、轮询）

## Core Invariant | 核心不变量

**The most critical invariant that all scenarios must validate:**
```
SortedToWrongChuteCount == 0
```

Under NO circumstances should the system mis-sort a parcel to the wrong chute, even under extreme conditions.

**所有场景必须验证的最关键不变量：**
在任何情况下，即使在极端条件下，系统也不应将包裹错分到错误的格口。

## Predefined Scenarios | 预定义场景

### Scenario A: Baseline (Low Friction, No Dropout) | 场景A：基线（低摩擦差异、无掉包）

**Purpose:** Establish baseline performance with minimal environmental variance.

**目的：** 在最小环境差异下建立基线性能。

**Configuration:**
- Friction Model: MinFactor = 0.95, MaxFactor = 1.05
- Dropout Probability: 0% (no dropout)
- Sorting Modes: Formal, FixedChute, RoundRobin

**Expected Outcomes:**
- All parcels should be sorted successfully (Status = SortedToTargetChute)
- SortedToWrongChuteCount = 0
- Timeout and dropout counts should be 0 or minimal

**预期结果：**
- 所有包裹应成功分拣（状态 = SortedToTargetChute）
- SortedToWrongChuteCount = 0
- 超时和掉包计数应为 0 或极少

### Scenario B: High Friction Variance | 场景B：大摩擦差异

**Purpose:** Test system behavior with significant speed variations between parcels.

**目的：** 测试包裹间存在显著速度差异时的系统行为。

**Configuration:**
- Friction Model: MinFactor = 0.7, MaxFactor = 1.3
- Dropout Probability: 0% (no dropout)

**Expected Outcomes:**
- Some parcels may experience Timeout status (due to extreme friction causing TTL violations)
- For all non-timeout parcels with Status = SortedToTargetChute:
  - FinalChuteId MUST equal TargetChuteId
- **SortedToWrongChuteCount MUST be 0**

**预期结果：**
- 部分包裹可能出现超时状态（由于极端摩擦导致 TTL 违规）
- 对于所有状态为 SortedToTargetChute 的非超时包裹：
  - FinalChuteId 必须等于 TargetChuteId
- **SortedToWrongChuteCount 必须为 0**

### Scenario C: Medium Friction + Small Dropout | 场景C：中等摩擦差异 + 小概率掉包

**Purpose:** Test system resilience to moderate environmental challenges.

**目的：** 测试系统对中等环境挑战的弹性。

**Configuration:**
- Friction Model: MinFactor = 0.9, MaxFactor = 1.1
- Dropout Probability: 5% per segment

**Expected Outcomes:**
- Some parcels will have Status = Dropped (expected with 5% dropout rate)
- For all parcels with Status = SortedToTargetChute:
  - Sensor events must be complete and within TTL
  - FinalChuteId MUST equal TargetChuteId
- **SortedToWrongChuteCount MUST be 0**

**预期结果：**
- 部分包裹状态为 Dropped（预期 5% 掉包率）
- 对于所有状态为 SortedToTargetChute 的包裹：
  - 传感器事件必须完整且在 TTL 内
  - FinalChuteId 必须等于 TargetChuteId
- **SortedToWrongChuteCount 必须为 0**

### Scenario D: Extreme Pressure (High Friction + High Dropout) | 场景D：极端压力（极端摩擦 + 高掉包率）

**Purpose:** Stress test to validate system reliability under worst-case conditions.

**目的：** 压力测试，验证最坏情况下的系统可靠性。

**Configuration:**
- Friction Model: MinFactor = 0.6, MaxFactor = 1.4
- Dropout Probability: 20% per segment

**Expected Outcomes:**
- High counts of Timeout and Dropped status expected
- Despite extreme conditions:
  - **SortedToWrongChuteCount MUST still be 0**
  - Any parcel that completes sorting must be sorted correctly

**预期结果：**
- 预期较高的超时和掉包状态计数
- 尽管条件极端：
  - **SortedToWrongChuteCount 仍必须为 0**
  - 任何完成分拣的包裹必须正确分拣

## Using Scenarios | 使用场景

### In Code | 在代码中

```csharp
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;

// Create a scenario | 创建场景
var scenario = ScenarioDefinitions.CreateScenarioA("Formal", parcelCount: 20);

// Run the scenario (in a test or simulation runner) | 运行场景
var summary = await RunScenarioAsync(scenario);

// Validate invariants | 验证不变量
Assert.Equal(0, summary.SortedToWrongChuteCount);
```

### In Tests | 在测试中

All scenarios are tested in `ZakYip.WheelDiverterSorter.E2ETests/SimulationScenariosTests.cs`.

所有场景在 `ZakYip.WheelDiverterSorter.E2ETests/SimulationScenariosTests.cs` 中测试。

Run simulation tests | 运行仿真测试:
```bash
dotnet test --filter "FullyQualifiedName~SimulationScenariosTests"
```

## Alignment with System Documentation | 与系统文档对齐

These scenarios align with | 这些场景与以下文档对齐：

1. **PATH_FAILURE_DETECTION_GUIDE.md**: Scenarios test various failure modes (timeout, dropout) and verify the system correctly handles them without mis-sorting.
   
   场景测试各种故障模式（超时、掉包），并验证系统正确处理它们而不会错分。

2. **ERROR_CORRECTION_MECHANISM.md**: Scenarios validate that even when errors occur (RuleEngine timeout, path execution failure), parcels are routed to exception chutes rather than wrong chutes.
   
   场景验证即使发生错误（规则引擎超时、路径执行失败），包裹也会被路由到异常格口而非错误格口。

## Adding New Scenarios | 添加新场景

To add a new scenario | 添加新场景：

1. Add a static factory method to `ScenarioDefinitions.cs`:
   ```csharp
   public static SimulationScenario CreateScenarioX(string sortingMode, int parcelCount = 20)
   {
       return new SimulationScenario
       {
           ScenarioName = $"场景X-描述-{sortingMode}",
           Options = new SimulationOptions
           {
               // Configure options...
           },
           Expectations = null // or define specific expectations
       };
   }
   ```

2. Add a corresponding test in `SimulationScenariosTests.cs`

3. Ensure the test validates the core invariant: `SortedToWrongChuteCount == 0`
   
   确保测试验证核心不变量：`SortedToWrongChuteCount == 0`
