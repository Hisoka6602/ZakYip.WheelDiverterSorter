# 场景 E：高摩擦有丢失 (High Friction with Dropout)

## 场景概述

场景 E 是一个新增的仿真场景，用于测试系统在**高摩擦因子**和**包裹丢失**同时存在的情况下的鲁棒性和正确性。

## 场景参数

| 参数 | 值 | 说明 |
|------|-----|------|
| **摩擦因子范围** | 0.7 - 1.3 | 高摩擦变化（±30%） |
| **掉包概率** | 10% | 中等掉包率 |
| **确定性模式** | 是 | 使用固定随机种子 (42) |
| **线速** | 1000 mm/s | 标准传送带速度 |
| **包裹间隔** | 500 ms | 标准包裹间隔 |

## 测试目标

该场景验证系统在复杂环境下的以下特性：

1. **不允许错分**：`SortedToWrongChute` 计数必须始终为 0 ✓
2. **容错能力**：系统能够处理由摩擦和掉包导致的异常情况
3. **状态准确性**：正确区分超时、掉包和成功分拣的包裹
4. **多模式支持**：在所有分拣模式（Formal、FixedChute、RoundRobin）下都能正常工作

## 预期结果

- **部分包裹超时或掉包**：由于高摩擦和10%掉包率，预期会有一定比例的包裹无法成功分拣
- **成功分拣的包裹正确到达目标格口**：所有标记为"成功分拣到目标格口"的包裹必须在正确的格口
- **无错分**：系统不应将任何包裹分拣到错误的格口

## 典型运行结果示例

```
═══════════════════════════════════════════════════════════
                   仿真统计报告
═══════════════════════════════════════════════════════════
总包裹数：                10
成功分拣到目标格口：      8
超时：                    0
掉包：                    2
执行错误：                0
规则引擎超时：            0
分拣到错误格口：          0 ✓
成功率：                  80.00 %
═══════════════════════════════════════════════════════════
```

## 如何运行

### 方法一：单元测试

```bash
cd ZakYip.WheelDiverterSorter.E2ETests
dotnet test --filter "DisplayName~ScenarioE"
```

### 方法二：可执行仿真

```bash
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run -- \
  --Simulation:ParcelCount=10 \
  --Simulation:IsEnableRandomFriction=true \
  --Simulation:IsEnableRandomDropout=true \
  --Simulation:FrictionModel:MinFactor=0.7 \
  --Simulation:FrictionModel:MaxFactor=1.3 \
  --Simulation:DropoutModel:DropoutProbabilityPerSegment=0.1 \
  --Simulation:IsPauseAtEnd=false
```

### 方法三：综合测试脚本

```bash
./test-all-simulations.sh
```

## 与其他场景的对比

| 场景 | 摩擦因子 | 掉包率 | 特点 |
|------|----------|--------|------|
| A (基线) | 0.95-1.05 (±5%) | 0% | 理想环境，无异常 |
| B (高摩擦) | 0.7-1.3 (±30%) | 0% | 只有摩擦变化 |
| C (中等摩擦+小掉包) | 0.9-1.1 (±10%) | 5% | 轻微异常 |
| D (极端压力) | 0.6-1.4 (±40%) | 20% | 最严苛环境 |
| **E (高摩擦有丢失)** | **0.7-1.3 (±30%)** | **10%** | **现实复杂场景** |

## 应用场景

场景 E 模拟的是真实生产环境中可能遇到的情况：

- 传送带磨损导致的摩擦系数变化
- 包裹质量问题（轻包裹易被吹走）
- 传感器偶尔检测失败
- 机械部件老化

## 验证标准

✅ **必须满足**：
- `SortedToWrongChuteCount == 0`：无错分
- 总包裹数等于各状态之和
- 成功分拣的包裹 `FinalChuteId == TargetChuteId`

✅ **预期行为**：
- 成功率通常在 70%-90% 之间
- 掉包率接近配置的 10%
- 部分包裹可能因高摩擦导致超时

## 相关文件

- 场景定义：`ZakYip.WheelDiverterSorter.Simulation/Scenarios/ScenarioDefinitions.cs`
- 单元测试：`ZakYip.WheelDiverterSorter.E2ETests/SimulationScenariosTests.cs`
- 测试脚本：`test-all-simulations.sh`
- 仿真配置：`ZakYip.WheelDiverterSorter.Simulation/appsettings.Simulation.json`

## 技术实现

场景 E 通过以下方式实现：

1. **摩擦模拟**：为每段路径应用 0.7-1.3 的随机因子
2. **掉包模拟**：每段路径有 10% 的概率触发掉包事件
3. **确定性随机**：使用固定种子确保测试可重复
4. **状态跟踪**：准确记录每个包裹的最终状态

## 维护建议

- 定期运行场景 E 测试以验证系统鲁棒性
- 如果成功率持续低于 70%，需要检查系统配置
- 调整掉包率和摩擦因子可模拟不同的生产环境
