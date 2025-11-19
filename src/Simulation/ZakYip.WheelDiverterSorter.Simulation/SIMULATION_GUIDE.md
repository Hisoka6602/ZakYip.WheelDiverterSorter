# 仿真系统使用指南

## 概述

这是一个完整的轮播分拣系统仿真环境，使用模拟驱动和传感器实现真实场景的仿真。支持三种分拣模式，并提供摩擦因子和掉包模拟能力。

## 功能特性

### 1. 三种分拣模式

- **RoundRobin（轮询）**: 按顺序轮流分配到各个格口
- **FixedChute（固定格口）**: 随机分配到预定义的格口列表
- **Formal（正式模式）**: 基于包裹ID哈希模拟真实规则引擎决策

### 2. 摩擦模型仿真

模拟包裹在传送带上由于摩擦因子导致的速度差异：

- **MinFactor/MaxFactor**: 定义摩擦因子范围（例如 0.85-1.15 表示 ±15% 的速度变化）
- **IsDeterministic**: 是否使用固定随机种子（用于可重现测试）
- **应用方式**: 摩擦因子应用于每段理想行程时间，不修改皮带线速

**示例配置**:
```json
"FrictionModel": {
  "MinFactor": 0.85,
  "MaxFactor": 1.15,
  "IsDeterministic": true,
  "Seed": 42
}
```

### 3. 掉包模型仿真

模拟包裹在输送过程中掉落的场景：

- **DropoutProbabilityPerSegment**: 每段的掉包概率（0.0-1.0）
- **AllowedSegments**: 允许掉包的段列表（null 表示所有段都允许）
- **Seed**: 随机数种子（用于可重现测试）

**示例配置**:
```json
"DropoutModel": {
  "DropoutProbabilityPerSegment": 0.02,
  "AllowedSegments": ["D1-D2", "D2-D3"],
  "Seed": 123
}
```

### 4. 不同输送线长度

每个格口的输送线长度各不相同（800mm-1500mm），模拟真实场景中的长度差异。

### 5. 增强的统计报告

仿真结束后输出完整统计：

- **状态分布**: 成功、超时、掉包、执行错误等各状态的数量和百分比
- **行程时间**: 平均/最小/最大行程时间
- **格口分布**: 各格口的分拣数量统计
- **"不允许错分"验证**: `SortedToWrongChute` 计数必须始终为 0 ✓

## 配置文件

主配置文件: `appsettings.Simulation.json`

### 完整配置示例

```json
{
  "Simulation": {
    "ParcelCount": 50,
    "LineSpeedMmps": 500,
    "ParcelInterval": "00:00:00.500",
    "SortingMode": "RoundRobin",
    "FixedChuteIds": [ 1, 2, 3, 4, 5 ],
    "ExceptionChuteId": 999,
    "IsEnableRandomFriction": true,
    "IsEnableRandomDropout": true,
    "FrictionModel": {
      "MinFactor": 0.85,
      "MaxFactor": 1.15,
      "IsDeterministic": true,
      "Seed": 42
    },
    "DropoutModel": {
      "DropoutProbabilityPerSegment": 0.02,
      "AllowedSegments": null,
      "Seed": 123
    },
    "IsEnableVerboseLogging": true,
    "IsPauseAtEnd": true
  }
}
```

## 运行仿真

### 方式一：使用默认配置

```bash
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run
```

### 方式二：命令行覆盖配置

```bash
dotnet run -- --Simulation:ParcelCount=100 --Simulation:SortingMode=Formal
```

### 方式三：使用自定义配置文件

修改 `appsettings.Simulation.json` 后运行。

## 输出示例

```
═══════════════════════════════════════════════════════════
                   仿真配置摘要
═══════════════════════════════════════════════════════════
包裹数量：          50
线速：              500 mm/s
包裹间隔：          0.50 秒
分拣模式：          RoundRobin
摩擦模拟：          启用
  - 摩擦因子范围：  0.85 - 1.15
  - 确定性模式：    是
掉包模拟：          启用
  - 掉包概率：      2.00 %
═══════════════════════════════════════════════════════════

[处理过程...]

═══════════════════════════════════════════════════════════
                   仿真统计报告
═══════════════════════════════════════════════════════════
总包裹数：                50
成功分拣到目标格口：      47
超时：                    0
掉包：                    3
执行错误：                0
规则引擎超时：            0
分拣到错误格口：          0 ✓
成功率：                  94.00 %
总耗时：                  29.76 秒
平均每包处理时间：        595.18 毫秒
平均行程时间：            5478.25 毫秒
最小行程时间：            0.00 毫秒
最大行程时间：            10963.20 毫秒
═══════════════════════════════════════════════════════════
```

## 仿真场景

系统提供多个预定义场景用于测试不同条件：

| 场景 | 摩擦因子 | 掉包率 | 特点 |
|------|----------|--------|------|
| **A (基线)** | 0.95-1.05 (±5%) | 0% | 理想环境，验证基本功能 |
| **B (高摩擦)** | 0.7-1.3 (±30%) | 0% | 测试高摩擦变化 |
| **C (中等摩擦+小掉包)** | 0.9-1.1 (±10%) | 5% | 轻微异常场景 |
| **D (极端压力)** | 0.6-1.4 (±40%) | 20% | 最严苛测试 |
| **E (高摩擦有丢失)** | 0.7-1.3 (±30%) | 10% | 现实复杂场景 ⭐新增 |
| **HD-1/2** | - | - | 高密度包裹场景 |
| **HD-3A/3B** | - | - | 高密度策略变体 |

详细的场景 E 文档请参考：[SCENARIO_E_DOCUMENTATION.md](../../SCENARIO_E_DOCUMENTATION.md)

## 验收标准

✅ 切换启动项目为 `ZakYip.WheelDiverterSorter.Simulation` 可运行  
✅ 不连接硬件即可完成仿真  
✅ 日志中可见摩擦导致的到达时间差异  
✅ 部分包裹出现 Timeout / Dropped 状态  
✅ `SortedToWrongChute` 计数始终为 0  
✅ 三种分拣模式（Formal/FixedChute/RoundRobin）均可正常运行  
✅ 场景 E（高摩擦有丢失）正常运行并通过所有测试

## 技术实现

### 核心组件

1. **ParcelTimelineFactory**: 为每个包裹生成完整时间轴
   - 应用摩擦因子到各段行程时间
   - 根据概率判定是否掉包
   - 生成传感器事件序列

2. **SimulationRunner**: 协调整个仿真流程
   - 生成虚拟包裹
   - 调用 RuleEngine 获取格口分配
   - 使用 MockSwitchingPathExecutor 执行路径
   - 收集并统计结果

3. **SimulationReportPrinter**: 输出中文仿真报告
   - 配置摘要
   - 详细统计（状态分布、格口分布、行程时间）

### 数据模型

- **ParcelSimulationResultEventArgs**: 单个包裹的仿真结果
- **ParcelSimulationStatus**: 包裹状态枚举
- **SimulationSummary**: 汇总统计数据

## 扩展性

### 添加新的仿真模型

1. 在 `Configuration/` 目录创建新的 Options 类
2. 在 `SimulationOptions` 中添加引用
3. 在 `ParcelTimelineFactory` 或 `SimulationRunner` 中实现逻辑

### 自定义分拣模式

在 `Program.cs` 中添加新的格口分配函数：

```csharp
static Func<long, int> CreateCustomAssignmentFunc(SimulationOptions options)
{
    // 自定义逻辑
    return parcelId => /* 返回格口ID */;
}
```

## 故障排查

### 问题：仿真一直显示成功率 100%

- 检查 `IsEnableRandomDropout` 是否启用
- 检查 `DropoutProbabilityPerSegment` 是否大于 0

### 问题：行程时间没有变化

- 检查 `IsEnableRandomFriction` 是否启用
- 检查 `MinFactor` 和 `MaxFactor` 是否设置了有效范围

### 问题：SortedToWrongChute > 0

- **这是严重问题！** 表示系统将包裹分拣到了错误的格口
- 检查 `MockSwitchingPathExecutor` 实现
- 检查路径生成逻辑

## 参考资料

- [系统架构文档](../README.md)
- [路由配置指南](../SYSTEM_CONFIG_GUIDE.md)
- [性能测试指南](../PERFORMANCE_TESTING_QUICKSTART.md)
