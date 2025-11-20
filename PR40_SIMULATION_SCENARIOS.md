# PR-40: 启动 & IO 高复杂度仿真场景文档

## 概述

PR-40 围绕"启动"和"IO"两个高风险区域，建立了系统级的仿真场景库，把典型和极端情况都覆盖，并用测试项目把这些场景固定下来，保证后续改动不会悄悄破坏启动流程和 IO 行为。

## 一、启动过程高级仿真

### 1.1 启动阶段建模

系统启动过程被明确拆分为以下阶段：

| 阶段 | 枚举值 | 描述 |
|------|--------|------|
| 未启动 | `NotStarted` | 系统尚未开始启动流程 |
| 配置加载中 | `Bootstrapping` | 配置加载、DI 装配完成 |
| 驱动初始化中 | `DriversInitializing` | 驱动握手、自检 |
| 通讯连接中 | `CommunicationConnecting` | 与 RuleEngine 建立连接 |
| 健康检查中 | `HealthChecking` | 核心模块就绪性检查 |
| 就绪稳定 | `HealthStable` | 所有核心模块 Ready，可接单 |
| 启动失败 | `Failed` | 启动过程中发生不可恢复错误 |

**核心接口扩展**：

```csharp
public interface ISystemStateManager
{
    // ... 原有成员 ...
    
    /// <summary>获取当前启动阶段信息</summary>
    BootstrapStageInfo? CurrentBootstrapStage { get; }
    
    /// <summary>获取启动阶段历史</summary>
    IReadOnlyList<BootstrapStageInfo> GetBootstrapHistory(int count = 10);
}
```

**启动阶段信息**：

```csharp
public record class BootstrapStageInfo
{
    public required BootstrapStage Stage { get; init; }
    public required DateTimeOffset EnteredAt { get; init; }
    public string? Message { get; init; }
    public bool IsSuccess { get; init; } = true;
    public string? FailureReason { get; init; }
}
```

### 1.2 启动仿真场景

#### 场景 STARTUP-ColdStart：冷启动仿真

**目的**：模拟系统冷启动过程，验证启动阶段的健康状态演进和包裹降级处理。

**配置特点**：
- 包裹数量：10（默认）
- 包裹间隔：1000ms（较长间隔，便于观察启动过程）
- 故障注入：上游延迟 1000-2000ms（模拟上游初始不可用）

**验证点**：
1. 健康检查端点在整个冷启动过程中的状态演进符合预期
2. 启动过程中收到的包裹请求全部走安全降级（例如暂不接单或统一异常格口）
3. 进入 HealthStable 后流程切换为正常逻辑

**使用方法**：

```csharp
var scenario = ScenarioDefinitions.CreateStartupColdStart(parcelCount: 10);
```

#### 场景 STARTUP-Failure：启动失败仿真

**目的**：模拟启动过程中驱动或通讯失败，验证系统降级行为和健康状态报告。

**配置特点**：
- 包裹数量：5（默认）
- 包裹间隔：1000ms
- 故障注入：
  - 第一个摆轮故障
  - 上游严重延迟 5000-10000ms（模拟无法连接）

**验证点**：
1. 系统不崩溃，仍然能对外暴露清晰的"不可用/降级"健康状态
2. 日志遵守去重规则，不刷屏
3. SafeExecutor 正常捕获各类启动阶段异常

**使用方法**：

```csharp
var scenario = ScenarioDefinitions.CreateStartupFailure(parcelCount: 5);
```

### 1.3 启动仿真测试用例

测试类：`StartupSimulationTests`

测试用例包括：
- `Startup_BootstrapStages_AreTrackedCorrectly`：验证启动阶段追踪
- `Startup_CurrentBootstrapStage_IsAvailable`：验证当前启动阶段可用
- `Startup_HealthCheckEndpoint_ReflectsBootstrapProgress`：验证健康检查端点反映启动进度
- `ColdStartScenario_IsWellDefined`：验证冷启动场景定义
- `StartupFailureScenario_IsWellDefined`：验证启动失败场景定义
- `Startup_MultipleBootCycles_MaintainHistory`：验证多次启动周期维护历史
- `StartupScenarios_HaveConsistentConfiguration`：验证启动场景配置一致性
- `Startup_StateTransition_FromBootingToReady`：验证状态转移

## 二、IO 高复杂度仿真

### 2.1 IO 行为模型

#### IO 行为模式

| 模式 | 枚举值 | 描述 |
|------|--------|------|
| 理想模式 | `Ideal` | 无抖动、无丢包、无延迟 |
| 混沌模式 | `Chaos` | 带抖动、随机延迟、偶发丢失 |

#### 传感器故障选项扩展

```csharp
public sealed record class SensorFaultOptions
{
    // 原有成员...
    
    /// <summary>IO 行为模式</summary>
    public IoBehaviorMode BehaviorMode { get; init; } = IoBehaviorMode.Ideal;
    
    /// <summary>传感器延迟范围（毫秒）</summary>
    public (int Min, int Max)? SensorDelayRangeMs { get; init; }
    
    /// <summary>传感器丢失概率（0.0-1.0）</summary>
    public decimal SensorLossProbability { get; init; } = 0.0m;
    
    /// <summary>是否启用去抖策略</summary>
    public bool IsEnableDebouncing { get; init; } = true;
    
    /// <summary>去抖时间窗口（毫秒）</summary>
    public int DebounceWindowMs { get; init; } = 100;
}
```

### 2.2 IO 仿真场景

#### 场景 IO-SensorJitter：传感器抖动仿真

**目的**：模拟传感器高频抖动，验证去抖策略的有效性。

**配置特点**：
- 包裹数量：30（默认）
- 传感器抖动：
  - 每次抖动触发 5 次
  - 80ms 内触发
  - 50% 概率抖动
- 去抖策略：可选启用/禁用
- 去抖窗口：100ms

**验证点**：
1. 启用去抖时，最终计算出来的事件次数符合预期，不误判多次进出
2. 未启用去抖时，可以清楚地看到错误行为（用于对比和说明文档）

**使用方法**：

```csharp
// 去抖启用
var scenarioWithDebounce = ScenarioDefinitions.CreateIoSensorJitter(
    parcelCount: 30, 
    enableDebouncing: true);

// 去抖禁用
var scenarioWithoutDebounce = ScenarioDefinitions.CreateIoSensorJitter(
    parcelCount: 30, 
    enableDebouncing: false);
```

#### 场景 IO-ChaosMode：IO 混沌模式仿真

**目的**：模拟 IO 层混沌行为（抖动、延迟、丢失），验证系统鲁棒性。

**配置特点**：
- 包裹数量：50（默认）
- IO 行为模式：混沌模式
- 传感器抖动：20% 概率，3 次触发，50ms 间隔
- 传感器延迟：10-100ms 随机延迟
- 传感器丢失：5% 概率
- 去抖启用，窗口 100ms

**验证点**：
1. 系统在不理想 IO 条件下仍能保持基本功能
2. 错误被正确检测和报告
3. 不会因为 IO 混沌导致系统崩溃或死锁

**使用方法**：

```csharp
var scenario = ScenarioDefinitions.CreateIoChaosMode(parcelCount: 50);
```

#### 场景 IO-StressTest：IO 压力测试

**目的**：满负荷 IO 压力测试，验证线程安全和性能。

**配置特点**：
- 包裹数量：200（默认）
- 包裹间隔：150ms（高密度）
- 拓扑：10 个摆轮，10 个格口，总长 20000mm
- 最小安全头距：200mm / 200ms

**验证点**：
1. 线程安全集合和锁策略在高压下没有死锁
2. 控制循环仍能保持在目标周期之内（例如 10ms/20ms 一次轮询）
3. 总体 CPU 与内存占用在可控范围

**使用方法**：

```csharp
var scenario = ScenarioDefinitions.CreateIoStressTest(parcelCount: 200);
```

#### 场景 IO-ConfigError：IO 配置错误仿真

**目的**：模拟 IO 配置错误，验证系统降级行为。

**配置特点**：
- 包裹数量：20（默认）
- 故障注入：传感器故障，30% 概率读取错误

**验证点**：
1. 自检可以检测到明显的配置错误并输出清晰日志
2. 运行中出现配置错误时，系统走安全降级路径
3. 不会继续错误落格

**使用方法**：

```csharp
var scenario = ScenarioDefinitions.CreateIoConfigError(parcelCount: 20);
```

### 2.3 IO 仿真测试用例

测试类：`IoSimulationTests`

测试用例包括：
- `SensorJitterScenario_WithDebouncingEnabled_IsWellDefined`：验证去抖启用的传感器抖动场景
- `SensorJitterScenario_WithDebouncingDisabled_IsWellDefined`：验证去抖禁用的传感器抖动场景
- `ChaosModeScenario_IsWellDefined`：验证混沌模式场景
- `StressTestScenario_IsWellDefined`：验证压力测试场景
- `ConfigErrorScenario_IsWellDefined`：验证配置错误场景
- `SensorFaultOptions_IdealMode_HasExpectedDefaults`：验证理想模式默认值
- `SensorFaultOptions_ChaosMode_CanBeConfigured`：验证混沌模式配置
- 以及其他验证场景一致性和参数化的测试

## 三、测试覆盖率与结构

### 3.1 覆盖率要求

PR-40 合并后，要求整体覆盖率进一步提升（目标 ≥ 85%），特别强调：

1. **启动流程涉及的核心状态机**
   - `SystemStateManager` 启动阶段追踪
   - `BootstrapStageInfo` 记录和查询

2. **IO 抽象层及其混沌仿真路径**
   - `SensorFaultOptions` 各种模式配置
   - 传感器抖动、延迟、丢失逻辑

3. **健康检查与自检逻辑**
   - 启动过程中的健康状态演进
   - 故障降级行为

### 3.2 结构要求

所有新建仿真场景必须：

1. **有对应的测试用例**（即使是集成级别）
   - `StartupSimulationTests`：11 个测试用例
   - `IoSimulationTests`：21 个测试用例

2. **有最少一份配套文档或注释**
   - 本文档（`PR40_SIMULATION_SCENARIOS.md`）
   - 代码中的 XML 注释详细说明每个场景的目的和验证点

3. **保持与已存在仿真文档风格一致**
   - 场景命名规范：`{领域}-{场景名}-{描述}`
   - 使用统一的验证点格式
   - 提供使用示例代码

### 3.3 验收标准

✅ 能通过命令或脚本快速运行各仿真场景：

```bash
# 运行所有启动和 IO 仿真测试
dotnet test --filter "FullyQualifiedName~StartupSimulationTests|FullyQualifiedName~IoSimulationTests"

# 运行特定场景测试
dotnet test --filter "FullyQualifiedName~StartupSimulationTests.ColdStartScenario_IsWellDefined"
```

✅ 各仿真场景都有明确断言，不是"纯跑一遍"：
- 所有测试都包含 `Assert` 语句
- 验证场景配置的完整性
- 验证场景行为的正确性

✅ 覆盖率报告显示：
- 启动与 IO 相关代码块的覆盖率明显提升
- 未引入大量未测试的新代码

## 四、使用指南

### 4.1 快速开始

1. **查看可用场景**：

```csharp
// 启动场景
var coldStart = ScenarioDefinitions.CreateStartupColdStart();
var startupFailure = ScenarioDefinitions.CreateStartupFailure();

// IO 场景
var sensorJitter = ScenarioDefinitions.CreateIoSensorJitter(30, true);
var chaosMode = ScenarioDefinitions.CreateIoChaosMode(50);
var stressTest = ScenarioDefinitions.CreateIoStressTest(200);
var configError = ScenarioDefinitions.CreateIoConfigError(20);
```

2. **运行测试验证**：

```bash
# 运行所有 PR-40 相关测试
dotnet test --filter "FullyQualifiedName~StartupSimulationTests|FullyQualifiedName~IoSimulationTests"
```

3. **查看启动阶段追踪**：

```csharp
var stateManager = serviceProvider.GetRequiredService<ISystemStateManager>();
await stateManager.BootAsync();

// 查看当前阶段
var currentStage = stateManager.CurrentBootstrapStage;
Console.WriteLine($"当前阶段: {currentStage?.Stage}, 进入时间: {currentStage?.EnteredAt}");

// 查看历史
var history = stateManager.GetBootstrapHistory(10);
foreach (var stage in history)
{
    Console.WriteLine($"{stage.EnteredAt}: {stage.Stage} - {stage.Message}");
}
```

### 4.2 扩展新场景

1. 在 `ScenarioDefinitions.cs` 中添加新的静态方法
2. 使用 `SimulationScenario` 记录类定义场景
3. 配置 `SimulationOptions`、`FaultInjection` 等选项
4. 在测试类中添加对应的验证测试
5. 更新本文档

### 4.3 注意事项

- 启动阶段追踪功能需要 `SystemStateManager` 或 `SystemStateManagerWithBoot` 正确注册
- IO 混沌模式会引入随机性，测试时注意使用固定的随机种子以保证可重现性
- 压力测试场景会消耗较多资源，建议在 CI 环境中限制并发执行数量

## 五、参考资料

- **启动状态机**：`src/Host/ZakYip.WheelDiverterSorter.Host/StateMachine/`
- **仿真场景定义**：`src/Simulation/ZakYip.WheelDiverterSorter.Simulation/Scenarios/ScenarioDefinitions.cs`
- **传感器故障选项**：`src/Simulation/ZakYip.WheelDiverterSorter.Simulation/Configuration/SensorFaultOptions.cs`
- **集成测试**：`tests/ZakYip.WheelDiverterSorter.Host.IntegrationTests/`

## 六、版本历史

- **v1.0 (PR-40)**：初始版本
  - 启动阶段建模（7 个阶段）
  - 2 个启动仿真场景（冷启动、启动失败）
  - 4 个 IO 仿真场景（传感器抖动、混沌模式、压力测试、配置错误）
  - 32 个集成测试用例
  - IO 行为模式扩展（理想/混沌）
  - 去抖策略验证
