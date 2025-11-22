# 分拣主流程调用链分析

本文档记录 ZakYip 摆轮分拣机当前的分拣主流程调用链，为 PR-2 Orchestrator 重构提供分析基础。

## 文档版本

- **创建时间**: 2025-11-22
- **最后更新**: 2025-11-22
- **作者**: PR-2 分析
- **状态**: 初始分析版本

---

## 1. 概述

当前的分拣流程分散在多个层次和服务中：

1. **Host 层**: 控制器（Controllers）、后台服务（BackgroundService）
2. **Application 层**: 仅有接口定义（ISimulationOrchestratorService），无具体实现
3. **Execution 层**: 路径生成、路径执行
4. **Core 层**: 业务规则、状态管理
5. **Ingress 层**: 传感器检测
6. **Communication 层**: 上游通讯

**问题**: 
- 业务逻辑分散在多个服务和层次中
- 没有单一入口可以完整理解整条分拣链路
- Application 层缺少实际的业务编排服务

---

## 2. 分拣主流程的主要入口

### 2.1 真实硬件入口（生产环境）

```
面板 IO 状态变化
  ↓
IPanelInputReader.GetButtonState()
  ↓
ISystemStateManager.HandlePanelInput()
  ↓
状态机切换到 Running
  ↓
传感器触发事件
  ↓
IParcelDetectionService.ParcelDetected 事件
  ↓
ParcelSortingOrchestrator.OnParcelDetected()
```

**关键文件**:
- `src/Host/ZakYip.WheelDiverterSorter.Host/StateMachine/SystemStateManager.cs`
- `src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Services/ParcelDetectionService.cs`
- `src/Host/ZakYip.WheelDiverterSorter.Host/Services/ParcelSortingOrchestrator.cs`

### 2.2 仿真入口（测试环境）

#### 方式 1: 场景仿真（长跑测试）

```
POST /api/simulation/run-scenario-e
  ↓
SimulationController.RunScenarioE()
  ↓
ISimulationScenarioRunner.RunAsync()
  ↓
模拟传感器触发
  ↓
IParcelDetectionService.ParcelDetected 事件
  ↓
ParcelSortingOrchestrator.OnParcelDetected()
```

**关键文件**:
- `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/SimulationController.cs`
- `src/Simulation/ZakYip.WheelDiverterSorter.Simulation/Services/SimulationScenarioRunner.cs`

#### 方式 2: 面板仿真

```
POST /api/simulation/panel/start
  ↓
SimulationController.SimulatePanelStart()
  ↓
IPanelInputReader.SimulateButtonPress()
  ↓
ISystemStateManager.HandlePanelInput()
  ↓
状态机切换到 Running
```

**关键文件**:
- `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/SimulationController.cs`
- `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Simulated/SimulatedPanelInputReader.cs`

#### 方式 3: 手动触发分拣（调试）

```
POST /api/simulation/test/sort
  ↓
SimulationTestController.TriggerDebugSort()
  ↓
DebugSortService.ExecuteDebugSortAsync()
  ↓
ISwitchingPathGenerator.GeneratePath()
  ↓
ISwitchingPathExecutor.ExecuteAsync()
```

**关键文件**:
- `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/SimulationTestController.cs`
- `src/Host/ZakYip.WheelDiverterSorter.Host/Services/DebugSortService.cs`

**注意**: DebugSortService 跳过了包裹创建和上游路由请求，直接执行路径生成和执行。

---

## 3. 完整分拣流程详细调用链

### 3.1 主流程（正常分拣模式）

```
【步骤 1: 包裹检测】
传感器触发
  ↓
IParcelDetectionService.ParcelDetected 事件
  ↓
ParcelSortingOrchestrator.OnParcelDetected()
  ├─ PR-42: 创建本地包裹实体（Parcel-First 语义）
  ├─ 记录包裹创建时间
  └─ 验证系统状态（必须为 Running）

【步骤 2: 状态与超载检查】
ParcelSortingOrchestrator.OnParcelDetected()
  ├─ ISystemRunStateService.ValidateParcelCreation()
  │   └─ 检查系统是否处于 Running 状态
  ├─ CongestionDataCollector.RecordParcelEntry()
  │   └─ 记录包裹进入时间
  ├─ ICongestionDetector.Detect()
  │   └─ 检测当前拥堵等级
  └─ IOverloadHandlingPolicy.Evaluate()
      └─ 评估是否需要超载处置

【步骤 3A: 正常路由 - 从 RuleEngine 获取格口】
ParcelSortingOrchestrator.GetChuteFromRuleEngineAsync()
  ├─ PR-42: 验证包裹已存在（Parcel-First Invariant 1）
  ├─ IRuleEngineClient.NotifyParcelDetectedAsync()
  │   └─ 发送包裹到达通知到上游
  ├─ 等待上游推送格口分配（带超时）
  │   └─ IRuleEngineClient.ChuteAssignmentReceived 事件
  ├─ ParcelSortingOrchestrator.OnChuteAssignmentReceived()
  │   ├─ PR-42: 验证响应的包裹已存在（Parcel-First Invariant 2）
  │   └─ 记录路由绑定时间
  └─ 返回目标格口ID

【步骤 3B: 固定格口模式】
使用配置的 FixedChuteId

【步骤 3C: 轮询模式】
ParcelSortingOrchestrator.GetNextRoundRobinChute()
  └─ 轮询选择下一个可用格口

【步骤 4: 路径生成】
ParcelSortingOrchestrator.ProcessSortingAsync()
  ├─ ISwitchingPathGenerator.GeneratePath(targetChuteId)
  │   └─ 返回 SwitchingPath（包含摆轮切换序列）
  ├─ PR-14: PathHealthChecker.ValidatePath()
  │   └─ 检查路径是否经过不健康节点
  └─ PR-08C: 二次超载检查（路径规划阶段）
      └─ 检查路径执行时间是否超过剩余 TTL

【步骤 5: 路径执行】
ParcelSortingOrchestrator.ProcessSortingAsync()
  ├─ ISwitchingPathExecutor.ExecuteAsync(path)
  │   └─ 按顺序切换每个摆轮
  ├─ 记录执行结果
  │   ├─ 成功: CongestionDataCollector.RecordParcelCompletion(true)
  │   └─ 失败: CongestionDataCollector.RecordParcelCompletion(false)
  └─ 失败处理: IPathFailureHandler.HandlePathFailure()

【步骤 6: 清理】
ParcelSortingOrchestrator.ProcessSortingAsync()
  ├─ 清理包裹路径记录
  ├─ PR-42: 清理包裹创建记录
  └─ 记录包裹追踪日志（PR-10）
```

### 3.2 异常处理流程

#### 3.2.1 重复触发异常

```
IParcelDetectionService.DuplicateTriggerDetected 事件
  ↓
ParcelSortingOrchestrator.OnDuplicateTriggerDetected()
  ├─ PR-42: 创建本地包裹实体
  ├─ 通知上游（但不等待响应）
  └─ 直接路由到异常格口
```

#### 3.2.2 上游超时

```
GetChuteFromRuleEngineAsync()
  ├─ 等待超时（默认 10 秒）
  └─ 返回异常格口ID
```

#### 3.2.3 路径生成失败

```
ProcessSortingAsync()
  ├─ GeneratePath() 返回 null
  ├─ 尝试生成到异常格口的路径
  └─ 如果异常格口路径也失败，记录错误并返回
```

#### 3.2.4 节点健康检查失败（PR-14）

```
ProcessSortingAsync()
  ├─ PathHealthChecker.ValidatePath() 返回不健康
  ├─ 记录节点降级事件
  └─ 重新生成到异常格口的路径
```

#### 3.2.5 路径执行失败

```
ExecuteAsync() 返回失败
  ├─ 记录失败原因
  ├─ IPathFailureHandler.HandlePathFailure()
  └─ CongestionDataCollector.RecordParcelCompletion(false)
```

---

## 4. 关键服务职责分析

### 4.1 ParcelSortingOrchestrator（当前主编排器）

**位置**: `src/Host/ZakYip.WheelDiverterSorter.Host/Services/ParcelSortingOrchestrator.cs`

**职责**:
1. ✅ 订阅包裹检测事件
2. ✅ 创建本地包裹实体（PR-42 Parcel-First）
3. ✅ 验证系统状态
4. ✅ 拥堵检测与超载处置（PR-08）
5. ✅ 请求上游路由
6. ✅ 等待上游格口分配
7. ✅ 生成摆轮切换路径
8. ✅ 执行路径
9. ✅ 异常处理
10. ✅ 包裹追踪日志（PR-10）

**优点**:
- 包含了完整的分拣流程逻辑
- PR-42 Parcel-First 语义验证到位
- 支持多种超载处置策略

**问题**:
- 在 Host/Services 层，不在 Application 层
- 方法较长（OnParcelDetected 约 180 行，ProcessSortingAsync 约 260 行）
- 部分步骤可以拆分为更小的方法

### 4.2 DebugSortService

**位置**: `src/Host/ZakYip.WheelDiverterSorter.Host/Services/DebugSortService.cs`

**职责**:
1. 手动触发分拣（跳过包裹创建和上游路由）
2. 验证系统状态
3. 生成路径
4. 执行路径
5. 记录指标

**问题**:
- 与 ParcelSortingOrchestrator 有重复逻辑（路径生成和执行）
- 仅供测试使用，但逻辑分散

### 4.3 SimulationController

**位置**: `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/SimulationController.cs`

**职责**:
1. 启动仿真场景
2. 模拟面板按钮
3. 模拟信号塔输出
4. 创建仿真包裹

**问题**:
- 控制器包含太多业务逻辑
- ISimulationOrchestratorService 接口已定义但无实现

### 4.4 SystemStateManager

**位置**: `src/Host/ZakYip.WheelDiverterSorter.Host/StateMachine/SystemStateManager.cs`

**职责**:
1. 管理系统状态机
2. 处理面板输入
3. 执行状态切换
4. 验证状态转换合法性

### 4.5 IParcelDetectionService

**位置**: `src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Services/ParcelDetectionService.cs`

**职责**:
1. 监听传感器触发
2. 生成包裹ID
3. 检测重复触发
4. 发布包裹检测事件

### 4.6 ISwitchingPathGenerator

**位置**: `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Services/SwitchingPathGenerator.cs`

**职责**:
1. 根据目标格口ID生成摆轮切换路径
2. 计算每段的 TTL 时间

### 4.7 ISwitchingPathExecutor

**位置**: `src/Execution/ZakYip.WheelDiverterSorter.Execution/`

**职责**:
1. 执行摆轮切换路径
2. 按顺序切换每个摆轮
3. 等待 TTL 时间
4. 返回执行结果

### 4.8 IRuleEngineClient

**位置**: `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/`

**职责**:
1. 连接到上游 RuleEngine
2. 发送包裹到达通知
3. 接收格口分配推送

---

## 5. 重复逻辑和环形调用

### 5.1 重复逻辑

#### 路径生成和执行
- **ParcelSortingOrchestrator**: 包含完整流程
- **DebugSortService**: 包含简化流程（无上游路由）

建议: 将路径生成和执行提取为独立方法，供两者复用。

#### 系统状态检查
- **ParcelSortingOrchestrator**: OnParcelDetected 中检查
- **DebugSortService**: ExecuteDebugSortAsync 中检查
- **SimulationController**: 多个端点中检查

建议: 统一通过 ISystemRunStateService 进行状态验证。

#### 指标记录
- **ParcelSortingOrchestrator**: 记录拥堵、超载等指标
- **DebugSortService**: 记录分拣成功/失败指标
- **执行器**: 记录路径执行指标

建议: 统一通过 IMetricsRecorder 接口记录。

### 5.2 环形调用

**未发现明显的环形调用**。

当前流程是单向的：
```
事件触发 → Orchestrator → 路径生成 → 路径执行 → 结果记录
```

---

## 6. 现有 Application 层结构

### 6.1 目录结构

```
src/Host/ZakYip.WheelDiverterSorter.Host/Application/
└── Services/
    ├── ISimulationOrchestratorService.cs  ✅ 接口已定义
    ├── ISystemConfigService.cs            ✅ 接口已定义
    └── SystemConfigService.cs             ✅ 实现已存在
```

### 6.2 ISimulationOrchestratorService 接口分析

**定义的方法**:
1. `StartScenarioEAsync()` - 启动场景 E 长跑仿真
2. `StopSimulationAsync()` - 停止仿真
3. `GetSimulationStatus()` - 获取仿真状态
4. `SimulatePanelStartAsync()` - 模拟面板启动
5. `SimulatePanelStopAsync()` - 模拟面板停止
6. `SimulatePanelEmergencyStopAsync()` - 模拟面板急停
7. `SimulateSignalTowerAsync()` - 模拟信号塔
8. `CreateSimulationParcelAsync()` - 创建仿真包裹
9. `CreateBatchSimulationParcelsAsync()` - 批量创建仿真包裹

**问题**: 
- ✅ 接口已定义
- ❌ **无具体实现类**
- ❌ 当前逻辑分散在 SimulationController 中

---

## 7. PR-2 重构后的架构

### 7.1 新增 Application 层 Orchestrator

#### 新增服务

**ISortingOrchestrator** (`src/Host/ZakYip.WheelDiverterSorter.Host/Application/Services/ISortingOrchestrator.cs`)

```csharp
public interface ISortingOrchestrator
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task<SortingResult> ProcessParcelAsync(long parcelId, string sensorId, CancellationToken cancellationToken = default);
    Task<SortingResult> ExecuteDebugSortAsync(string parcelId, int targetChuteId, CancellationToken cancellationToken = default);
}
```

**SortingOrchestrator** (`src/Host/ZakYip.WheelDiverterSorter.Host/Application/Services/SortingOrchestrator.cs`)

职责：
- 统一的分拣流程编排入口
- 将长流程拆分为多个小方法（15-40 行/方法）
- 保持 PR-42 Parcel-First 语义
- 支持调试分拣（跳过上游路由）

**核心方法拆分**：

```csharp
// 主流程入口
Task<SortingResult> ProcessParcelAsync(long parcelId, string sensorId, ...)

// 流程步骤分解
1. CreateParcelEntityAsync()           // 创建本地包裹实体（PR-42）
2. ValidateSystemStateAsync()          // 验证系统状态
3. DetectCongestionAndOverloadAsync()  // 拥堵检测与超载评估
4. DetermineTargetChuteAsync()         // 确定目标格口
   ├─ GetChuteFromRuleEngineAsync()    // 正式模式：从上游获取
   ├─ GetFixedChute()                  // 固定格口模式
   └─ GetNextRoundRobinChute()         // 轮询模式
5. ExecuteSortingWorkflowAsync()       // 执行分拣工作流
   ├─ GeneratePathOrExceptionAsync()   // 生成路径
   ├─ ValidatePathHealthAsync()        // 路径健康检查（PR-14）
   ├─ CheckSecondaryOverloadAsync()    // 二次超载检查（PR-08C）
   ├─ ExecutePathWithTrackingAsync()   // 执行路径
   └─ RecordSortingResultAsync()       // 记录结果
```

#### 架构改进

**分层清晰**：

```
┌──────────────────────────────────────────────────┐
│ Host 层 (Controllers / DI / API)                 │
│  - SimulationTestController                      │
│  - ParcelSortingWorker (BackgroundService)       │
└──────────────────────────────────────────────────┘
                     ↓ 调用
┌──────────────────────────────────────────────────┐
│ Application 层 (Business Orchestration) [PR-2]   │
│  - ISortingOrchestrator                          │
│  - SortingOrchestrator (新增)                    │
│    ├─ 统一的分拣流程编排                          │
│    ├─ 小方法拆分，易于测试                         │
│    └─ 保持所有现有语义（PR-42, PR-14, PR-08）     │
└──────────────────────────────────────────────────┘
                     ↓ 调用
┌──────────────────────────────────────────────────┐
│ Execution 层 (Path Generation & Execution)       │
│  - ISwitchingPathGenerator                       │
│  - ISwitchingPathExecutor                        │
└──────────────────────────────────────────────────┘
```

**优点**：

1. ✅ **统一入口**：所有分拣流程通过 `ISortingOrchestrator` 接口
2. ✅ **方法拆分**：大方法拆分为 15+ 个小方法，每个 15-40 行
3. ✅ **易于测试**：每个步骤可独立测试
4. ✅ **向后兼容**：保留 `ParcelSortingOrchestrator` 用于渐进迁移
5. ✅ **遵守规范**：所有仓库编码规范（ISystemClock、nullable、SafeExecutionService）

#### DI 注册

```csharp
// src/Host/ZakYip.WheelDiverterSorter.Host/Services/SortingServiceExtensions.cs

// PR-2: 注册新的 Application 层分拣编排服务（推荐使用）
services.AddSingleton<ISortingOrchestrator, SortingOrchestrator>();

// 注册旧的包裹分拣编排服务（向后兼容，逐步迁移）
services.AddSingleton<ParcelSortingOrchestrator>();
```

### 7.2 更新后的分拣流程

#### 推荐流程（使用新 Orchestrator）

```
【真实硬件 / 仿真】
传感器触发
  ↓
IParcelDetectionService.ParcelDetected 事件
  ↓
【方式 1: 通过旧 Orchestrator（现有）】
ParcelSortingOrchestrator.OnParcelDetected()
  └─ 完整的分拣流程（910 行）

【方式 2: 通过新 Orchestrator（推荐）】
ISortingOrchestrator.ProcessParcelAsync()
  ├─ 1. CreateParcelEntityAsync()           [15 lines]
  ├─ 2. ValidateSystemStateAsync()          [20 lines]
  ├─ 3. DetectCongestionAndOverloadAsync()  [40 lines]
  ├─ 4. DetermineTargetChuteAsync()         [15 lines]
  └─ 5. ExecuteSortingWorkflowAsync()       [30 lines]
       ├─ 5.1 GeneratePathOrExceptionAsync()
       ├─ 5.2 ValidatePathHealthAsync()
       ├─ 5.3 CheckSecondaryOverloadAsync()
       ├─ 5.4 ExecutePathWithTrackingAsync()
       └─ 5.5 RecordSortingResultAsync()
```

#### 调试分拣流程

```
【测试环境】
POST /api/simulation/test/sort
  ↓
SimulationTestController.TriggerDebugSort()
  ↓
DebugSortService.ExecuteDebugSortAsync()
  └─ 检查系统状态后，可以选择：
      
【方式 1: 直接调用底层（现有）】
├─ ISwitchingPathGenerator.GeneratePath()
└─ ISwitchingPathExecutor.ExecuteAsync()

【方式 2: 通过 Orchestrator（推荐）】
└─ ISortingOrchestrator.ExecuteDebugSortAsync()
    └─ 统一的路径生成和执行逻辑
```

### 7.3 迁移建议

#### 立即可用

- ✅ 新代码直接使用 `ISortingOrchestrator`
- ✅ 通过 DI 注入 `ISortingOrchestrator` 而非 `ParcelSortingOrchestrator`

#### 渐进迁移

**Phase 1（当前 PR-2）**:
- ✅ 创建 `ISortingOrchestrator` 和 `SortingOrchestrator`
- ✅ 注册到 DI 容器
- ✅ 保持 `ParcelSortingOrchestrator` 向后兼容

**Phase 2（未来 PR）**:
- 更新 `ParcelSortingWorker` 使用新 Orchestrator
- 更新 `DebugSortService` 使用新 Orchestrator
- 创建 `SimulationOrchestratorService` 实现

**Phase 3（未来 PR）**:
- 所有服务迁移完成
- 移除旧的 `ParcelSortingOrchestrator`
- 更新所有文档

#### 兼容性保证

- ✅ 现有测试继续通过（使用旧 Orchestrator）
- ✅ 现有 API 端点不受影响
- ✅ 所有 PR-42 Parcel-First 语义保持不变
- ✅ 所有超载处置策略（PR-08）保持不变
- ✅ 所有节点健康检查（PR-14）保持不变

---

### 7.1 目标

将分拣主流程完整收口到 Application 层的 Orchestrator，让任何人只看 Application 层就能理解整条分拣链路。

### 7.2 架构设计建议

#### 方案 1: 单一 SortingOrchestrator（推荐）

```
Application/
├── Services/
│   ├── ISortingOrchestrator.cs           ← 新增：统一分拣编排接口
│   ├── SortingOrchestrator.cs            ← 新增：分拣编排实现
│   ├── ISimulationOrchestratorService.cs ← 保留：仿真编排接口
│   ├── SimulationOrchestratorService.cs  ← 新增：仿真编排实现
│   ├── ISystemConfigService.cs           ← 保留
│   └── SystemConfigService.cs            ← 保留
```

**ISortingOrchestrator 职责**:
1. 处理包裹检测事件（从真实 IO 或仿真）
2. 验证系统状态和包裹创建条件
3. 执行拥堵检测和超载处置
4. 请求上游路由或使用本地路由策略
5. 生成和执行摆轮切换路径
6. 处理异常和记录追踪日志

**SimulationOrchestratorService 职责**:
1. 实现 ISimulationOrchestratorService 接口
2. 启动/停止仿真场景
3. 模拟面板操作
4. 创建仿真包裹（内部调用 SortingOrchestrator）

**优点**:
- 清晰的职责分离
- Application 层包含所有业务编排逻辑
- Host 层只负责 API 端点和 DI 配置

#### 方案 2: 多级 Orchestrator

```
Application/
├── Services/
│   ├── ISortingWorkflowOrchestrator.cs      ← 顶层：完整分拣工作流
│   ├── SortingWorkflowOrchestrator.cs
│   ├── IParcelProcessingService.cs          ← 中层：包裹处理
│   ├── ParcelProcessingService.cs
│   ├── IRoutingService.cs                   ← 中层：路由决策
│   ├── RoutingService.cs
│   ├── IPathExecutionService.cs             ← 中层：路径执行
│   └── PathExecutionService.cs
```

**优点**: 更细粒度的拆分，易于单元测试

**缺点**: 增加复杂度，可能过度设计

### 7.3 重构步骤建议

#### 阶段 1: 创建 Application 层结构

1. 创建 `Application/Services/ISortingOrchestrator.cs` 接口
2. 创建 `Application/Services/SortingOrchestrator.cs` 实现
3. 将 `ParcelSortingOrchestrator` 的核心逻辑迁移到 `SortingOrchestrator`

#### 阶段 2: 拆分为小方法

将 `SortingOrchestrator` 的长方法拆分为多个小方法：

```csharp
// 原: OnParcelDetected (180 lines)
// 拆分为:
- CreateParcelEntityAsync()           // 创建包裹实体
- ValidateSystemStateAsync()          // 验证系统状态
- DetectCongestionAndOverloadAsync()  // 拥堵和超载检测
- DetermineTargetChuteAsync()         // 确定目标格口
- ExecuteSortingWorkflowAsync()       // 执行分拣工作流

// 原: ProcessSortingAsync (260 lines)
// 拆分为:
- GeneratePathOrExceptionAsync()      // 生成路径（含异常处理）
- ValidatePathHealthAsync()           // 验证路径健康
- CheckSecondaryOverloadAsync()       // 二次超载检查
- ExecutePathWithTrackingAsync()      // 执行路径并追踪
- RecordSortingResultAsync()          // 记录分拣结果
```

#### 阶段 3: 实现 SimulationOrchestratorService

1. 创建 `Application/Services/SimulationOrchestratorService.cs`
2. 实现 `ISimulationOrchestratorService` 接口
3. 将 SimulationController 的业务逻辑迁移到 SimulationOrchestratorService
4. SimulationController 只保留 API 端点定义

#### 阶段 4: 统一入口

确保所有分拣入口都通过 `SortingOrchestrator`：

- ✅ 真实 IO → ParcelDetectionService → SortingOrchestrator
- ✅ 仿真场景 → SimulationOrchestratorService → SortingOrchestrator
- ✅ 面板仿真 → SimulationOrchestratorService → StateManager → SortingOrchestrator
- ✅ 手动触发 → DebugSortService → SortingOrchestrator（复用路径生成和执行）

#### 阶段 5: 清理和测试

1. 删除 Host 层的重复逻辑
2. 更新依赖注入配置
3. 运行现有测试确保无破坏
4. 新增 Orchestrator 级别的单元/集成测试

---

## 8. 测试覆盖

### 8.1 现有测试

#### E2E 测试
- `PanelStartupToSortingE2ETests` - 面板启动到分拣完整流程
- `ConfigApiLongRunSimulationTests` - API 配置驱动的仿真
- `DenseTrafficSimulationTests` - 高密度流量仿真

#### 集成测试
- `AlertFlowIntegrationTests` - 告警流程
- `PathFailureIntegrationTests` - 路径失败处理

### 8.2 需要新增的测试

#### Orchestrator 单元测试
```csharp
// Application.Tests/Services/SortingOrchestratorTests.cs
[Fact]
public async Task CreateParcelEntityAsync_ShouldCreateParcelWithTimestamp()

[Fact]
public async Task ValidateSystemStateAsync_ShouldRejectWhenNotRunning()

[Fact]
public async Task DetermineTargetChuteAsync_FormalMode_ShouldRequestFromUpstream()

[Fact]
public async Task DetermineTargetChuteAsync_FixedChuteMode_ShouldUseFixedChute()

[Fact]
public async Task DetermineTargetChuteAsync_RoundRobinMode_ShouldRotateChutes()

[Fact]
public async Task ExecuteSortingWorkflowAsync_Success_ShouldRecordMetrics()

[Fact]
public async Task ExecuteSortingWorkflowAsync_PathGenerationFails_ShouldRouteToException()
```

#### Orchestrator 集成测试
```csharp
// Host.IntegrationTests/OrchestratorIntegrationTests.cs
[Fact]
public async Task FullSortingFlow_FromSensor_ToChute_ShouldSucceed()

[Fact]
public async Task FullSortingFlow_UpstreamTimeout_ShouldRouteToException()

[Fact]
public async Task FullSortingFlow_Overload_ShouldTriggerOverloadPolicy()
```

---

## 9. 注意事项

### 9.1 保持 PR-42 Parcel-First 语义

在重构过程中，必须保持 PR-42 引入的 Parcel-First 语义：

1. **Invariant 1**: 上游请求必须引用已存在的本地包裹
2. **Invariant 2**: 上游响应必须匹配已存在的本地包裹
3. 不允许创建"幽灵包裹"（仅有上游响应，无本地实体）

### 9.2 保持所有仿真和 E2E 测试通过

根据仓库规则：
> Copilot 修改分拣逻辑/通讯/IO/面板时，必须保持所有仿真和 E2E 测试通过

### 9.3 遵守 SafeExecutionService 规则

所有后台任务必须通过 `ISafeExecutionService` 包裹：
> 所有可能抛出异常的后台任务、循环、IO/通讯回调必须通过 SafeExecutionService 执行

### 9.4 使用 ISystemClock 获取时间

所有时间获取必须通过 `ISystemClock` 接口：
> 所有时间一律通过 ISystemClock 获取

---

## 10. 参考文档

- `ARCHITECTURE_PRINCIPLES.md` - 架构原则
- `CODING_GUIDELINES.md` - 编码规范
- `PR42_PARCEL_FIRST_SPECIFICATION.md` - Parcel-First 语义规范
- `PR38_IMPLEMENTATION_SUMMARY.md` - 上游连接重试规则
- `PR37_IMPLEMENTATION_SUMMARY.md` - SafeExecutionService 使用规范
- `E2E_TESTING_SUMMARY.md` - E2E 测试策略
- `TESTING_STRATEGY.md` - 测试策略

---

## 附录: 关键代码位置速查表

| 组件 | 文件路径 |
|------|---------|
| **ParcelSortingOrchestrator** | `src/Host/ZakYip.WheelDiverterSorter.Host/Services/ParcelSortingOrchestrator.cs` |
| **DebugSortService** | `src/Host/ZakYip.WheelDiverterSorter.Host/Services/DebugSortService.cs` |
| **SimulationController** | `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/SimulationController.cs` |
| **SimulationTestController** | `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/SimulationTestController.cs` |
| **SystemStateManager** | `src/Host/ZakYip.WheelDiverterSorter.Host/StateMachine/SystemStateManager.cs` |
| **ParcelDetectionService** | `src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Services/ParcelDetectionService.cs` |
| **SwitchingPathGenerator** | `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Services/SwitchingPathGenerator.cs` |
| **SwitchingPathExecutor** | `src/Execution/ZakYip.WheelDiverterSorter.Execution/` |
| **RuleEngineClient** | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/` |
| **ISimulationOrchestratorService** | `src/Host/ZakYip.WheelDiverterSorter.Host/Application/Services/ISimulationOrchestratorService.cs` |
| **E2E Tests** | `tests/ZakYip.WheelDiverterSorter.E2ETests/` |

---

**文档结束**
