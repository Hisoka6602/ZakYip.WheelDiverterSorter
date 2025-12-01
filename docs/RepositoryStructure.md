# ZakYip.WheelDiverterSorter 代码结构快照

> 本文档由 AI 基于当前仓库完整代码生成，用于后续架构重构与 PR 规划。
> 
> **生成时间**：2025-11-28
> 
> **维护说明**：后续任何 PR 改动项目结构或者增减文件都需要更新本文档。

---

## 1. 解决方案概览

- **解决方案文件**：`ZakYip.WheelDiverterSorter.sln`
- **目标框架**：.NET 8.0
- **主体项目列表**：

| 分类 | 项目名称 | 位置 |
|------|---------|------|
| 应用入口 | ZakYip.WheelDiverterSorter.Host | src/Host/ |
| 应用服务层 | ZakYip.WheelDiverterSorter.Application | src/Application/ |
| 核心层 | ZakYip.WheelDiverterSorter.Core | src/Core/ |
| 执行层 | ZakYip.WheelDiverterSorter.Execution | src/Execution/ |
| 驱动层 | ZakYip.WheelDiverterSorter.Drivers | src/Drivers/ |
| 入口层 | ZakYip.WheelDiverterSorter.Ingress | src/Ingress/ |
| 可观测性层 | ZakYip.WheelDiverterSorter.Observability | src/Observability/ |
| 通信层 | ZakYip.WheelDiverterSorter.Communication | src/Infrastructure/ |
| 仿真库 | ZakYip.WheelDiverterSorter.Simulation | src/Simulation/ |
| 仿真CLI | ZakYip.WheelDiverterSorter.Simulation.Cli | src/Simulation/Cli/ |
| 分析器 | ZakYip.WheelDiverterSorter.Analyzers | src/ZakYip.WheelDiverterSorter.Analyzers/ |

- **测试项目**：

| 项目名称 | 测试类型 |
|---------|---------|
| ZakYip.WheelDiverterSorter.Core.Tests | 核心层单元测试 |
| ZakYip.WheelDiverterSorter.Execution.Tests | 执行层单元测试 |
| ZakYip.WheelDiverterSorter.Drivers.Tests | 驱动层单元测试 |
| ZakYip.WheelDiverterSorter.Ingress.Tests | 入口层单元测试 |
| ZakYip.WheelDiverterSorter.Communication.Tests | 通信层单元测试 |
| ZakYip.WheelDiverterSorter.Observability.Tests | 可观测性层单元测试 |
| ZakYip.WheelDiverterSorter.Host.Application.Tests | 应用服务单元测试 |
| ZakYip.WheelDiverterSorter.Host.IntegrationTests | 主机集成测试 |
| ZakYip.WheelDiverterSorter.E2ETests | 端到端测试 |
| ZakYip.WheelDiverterSorter.ArchTests | 架构合规性测试 |
| ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests | 技术债合规性测试 |
| ZakYip.WheelDiverterSorter.Benchmarks | 性能基准测试 |

#### ArchTests 关键测试类

| 测试类 | 职责 |
|-------|------|
| ApplicationLayerDependencyTests | Application 层依赖约束 |
| DuplicateTypeDetectionTests | 重复类型检测 |
| ExecutionPathPipelineTests | PR-SD4: Execution 层管线依赖约束（中间件不依赖 Drivers/Core.Hardware） |
| HalConsolidationTests | HAL 层收敛约束 |
| HostLayerConstraintTests | Host 层约束 |
| RoutingTopologyLayerTests | 路由/拓扑分层约束 |

#### E2ETests 关键测试类

| 测试类 | 职责 |
|-------|------|
| CompleteSortingFlowE2ETests | PR-SD4: 完整分拣流程端到端测试（路径生成→执行职责分离验证） |
| DenseTrafficSimulationTests | 高密度包裹仿真测试 |
| FaultRecoveryScenarioTests | 故障恢复场景测试 |
| ParcelSortingWorkflowTests | 包裹分拣工作流测试 |
| PerformanceBaselineTests | 性能基准测试 |
| RuleEngineIntegrationTests | 规则引擎集成测试 |

- **工具项目**：

| 项目名称 | 用途 |
|---------|-----|
| ZakYip.WheelDiverterSorter.Tools.Reporting | 仿真报告分析工具 |
| ZakYip.WheelDiverterSorter.Tools.SafeExecutionStats | SafeExecution 统计工具 |
| Profiling/ | 性能剖析脚本（非项目，Shell/PowerShell 脚本） |

---

## 2. 项目依赖关系

以下依赖关系基于各项目 `.csproj` 文件中的 `<ProjectReference>` 标签提取。

```
ZakYip.WheelDiverterSorter.Host
├── ZakYip.WheelDiverterSorter.Application    # DI 聚合层入口
├── ZakYip.WheelDiverterSorter.Core
└── ZakYip.WheelDiverterSorter.Observability
# PR-H1: Host 不再直接依赖 Execution/Drivers/Ingress/Communication/Simulation
# 这些依赖现在通过 Application 层传递

ZakYip.WheelDiverterSorter.Application        # PR-H1: DI 聚合层
├── ZakYip.WheelDiverterSorter.Core
├── ZakYip.WheelDiverterSorter.Execution
├── ZakYip.WheelDiverterSorter.Drivers
├── ZakYip.WheelDiverterSorter.Ingress
├── ZakYip.WheelDiverterSorter.Communication
├── ZakYip.WheelDiverterSorter.Observability
└── ZakYip.WheelDiverterSorter.Simulation     # PR-H1: Application 现在可以依赖 Simulation

ZakYip.WheelDiverterSorter.Execution
├── ZakYip.WheelDiverterSorter.Core
└── ZakYip.WheelDiverterSorter.Observability

ZakYip.WheelDiverterSorter.Drivers
└── ZakYip.WheelDiverterSorter.Core
# PR-RS11: Drivers 只依赖 Core，不再依赖 Execution/Communication
# 所有 HAL 接口已统一迁移至 Core/Hardware/（IEmcResourceLockManager 等）

ZakYip.WheelDiverterSorter.Ingress
└── ZakYip.WheelDiverterSorter.Core

ZakYip.WheelDiverterSorter.Observability
└── ZakYip.WheelDiverterSorter.Core

ZakYip.WheelDiverterSorter.Communication
├── ZakYip.WheelDiverterSorter.Core
└── ZakYip.WheelDiverterSorter.Observability

ZakYip.WheelDiverterSorter.Simulation           # PR-TD6: 改为 Library 项目
├── ZakYip.WheelDiverterSorter.Core
├── ZakYip.WheelDiverterSorter.Execution
├── ZakYip.WheelDiverterSorter.Drivers
├── ZakYip.WheelDiverterSorter.Ingress
└── ZakYip.WheelDiverterSorter.Observability

ZakYip.WheelDiverterSorter.Simulation.Cli       # PR-TD6: 新增 CLI 入口项目
├── ZakYip.WheelDiverterSorter.Simulation
└── ZakYip.WheelDiverterSorter.Communication

ZakYip.WheelDiverterSorter.Analyzers
└── (无项目依赖，仅依赖 Microsoft.CodeAnalysis)

ZakYip.WheelDiverterSorter.Tools.Reporting
└── ZakYip.WheelDiverterSorter.Core

ZakYip.WheelDiverterSorter.Tools.SafeExecutionStats
└── (无项目依赖)
```

**依赖层次说明**：

- **Core** 是最底层，不依赖其他业务项目，定义核心抽象（包括 HAL 接口）和领域模型
- **Observability** 依赖 Core，提供监控、日志、告警等基础设施
- **Ingress** 依赖 Core，处理传感器和包裹检测
- **Communication** 依赖 Core 和 Observability，负责与上游 RuleEngine 的通信
- **Execution** 依赖 Core 和 Observability，负责分拣编排和路径执行
- **Drivers** 只依赖 Core（PR-RS11），实现具体硬件驱动（实现 Core/Hardware/ 定义的 HAL 接口）
- **Simulation** 依赖除 Host 和 Application 外的所有项目，提供仿真运行环境
- **Application** 是 DI 聚合层（PR-H1），依赖 Core、Execution、Drivers、Ingress、Communication、Observability、Simulation，提供统一的服务注册入口
- **Host** 是顶层应用入口，只依赖 Application、Core、Observability（PR-H1: 依赖收缩），通过 Application 层间接访问其他项目的服务

> **PR-RS8 / PR-RS11 依赖约束澄清**：
> - **Execution 与 Drivers 互不依赖**，两者都只依赖 Core 中的 HAL 抽象接口（位于 `Core/Hardware/`）以及 Observability 基础设施
> - **PR-RS11**: Drivers 不再依赖 Communication，所有原 Communication 层的 EMC 锁管理接口（`IEmcResourceLockManager`、`EmcLockEvent`）已迁移至 Core/Hardware/

### 2.1 层级架构约束（Architecture Constraints）

根据 `copilot-instructions.md` 规范，项目依赖必须遵循以下严格约束，由 `ArchTests` 项目中的 `ApplicationLayerDependencyTests` 强制执行：

#### Host 层约束（PR-RS9 更新）
- **允许依赖**：Application、Core、Observability
- **禁止直接依赖**：Execution、Drivers、Ingress、Communication、Simulation
- **说明**：Host 层通过 Application 层间接访问 Execution/Drivers/Ingress/Communication/Simulation 的服务

**Host 层"薄层"原则（PR-RS9 强化）**：
- **职责边界**：Host 只负责 DI 配置、API Controller 壳、启动引导、Swagger 文档
- **禁止的内容**：
  - 业务服务接口定义（`I*Service`，ISystemStateManager 除外）
  - Command/Repository/Adapter/Middleware 等业务模式类型
  - Application/Commands/Pipeline/Repositories 等业务目录
- **详细目录清单**：见 [3.2 Host 层结构约束](#host-层结构约束pr-rs9-强化)

#### Application 层约束（PR-H1 更新）
- **允许依赖**：Core、Execution、Drivers、Ingress、Communication、Observability、Simulation
- **禁止依赖**：Host、Analyzers
- **说明**：Application 层现在是 DI 聚合层，负责统一编排所有下游项目的服务注册

#### Execution 层约束（PR-RS8 新增）
- **允许依赖**：Core、Observability
- **禁止依赖**：Drivers、Communication、Ingress、Host、Application、Simulation
- **说明**：Execution 层负责分拣编排和路径执行，通过 Core/Hardware/ 定义的 HAL 接口访问硬件能力，由 DI 在运行时注入具体实现

#### Drivers 层约束（PR-RS8 新增, PR-RS11 更新）
- **允许依赖**：Core（可选 Observability）
- **禁止依赖**：Execution、Communication、Ingress、Host、Application、Simulation
- **当前状态**：PR-RS11 已完成 - Drivers 仅依赖 Core，`IEmcResourceLockManager` 和 `EmcLockEvent` 已迁移至 Core/Hardware/Devices
- **说明**：Drivers 层实现 Core/Hardware/ 定义的 HAL 接口，封装具体厂商硬件的驱动逻辑

> **重要约束**：**Execution 与 Drivers 互不依赖**，这是分层架构的核心原则。两者通过 Core/Hardware/ 定义的接口解耦，由 Application 层在 DI 容器中组装。

#### 反向依赖禁止
以下项目 **禁止** 依赖 Application（避免循环依赖）：
- Core
- Execution
- Drivers
- Ingress
- Communication
- Observability
- Simulation

#### 预期依赖链路（PR-H1 更新）
```
Host → Application → Core/Execution/Drivers/Ingress/Communication/Observability/Simulation
```

### 2.2 编码规范约束（Coding Standards）

由 `TechnicalDebtComplianceTests` 项目中的测试强制执行：

#### 禁止使用 global using
- **当前状态**：代码库中 **不存在** 任何 `global using` 语句（PR-C1 已清理完成）
- **规则**：禁止新增或保留任何 `global using`；所有依赖必须通过显式 `using` 表达
- **原因**：降低代码可读性，隐藏依赖关系，不利于分层架构维护
- **测试**：`CodingStandardsComplianceTests.ShouldNotUseGlobalUsing()` 全面阻止该语法再次出现
- **替代方案**：在每个文件中显式添加所需的 `using` 语句
- **说明**：SDK 默认生成的隐式 usings（位于 `obj/` 目录下的 `*.GlobalUsings.g.cs` 文件）不在检查范围内，因为这些是构建时自动生成的，不影响代码可读性

#### 禁止 Legacy 目录和命名模式 (PR-C3 新增)
- **当前状态**：代码库中 **不存在** 任何 `*/Legacy/*` 目录或带 `*Legacy*`、`*Deprecated*` 命名的类型
- **规则**：
  - 禁止创建 Legacy 目录
  - 禁止创建带 `Legacy` 或 `Deprecated` 命名的公共类型
  - 过时代码必须在同一次重构中完全删除，不保留过渡实现
- **测试**：
  - `DuplicateTypeDetectionTests.ShouldNotHaveLegacyDirectories()` - 禁止 Legacy 目录
  - `LegacyCodeDetectionTests.ShouldNotHaveLegacyNamedTypes()` - 禁止 Legacy 命名
  - `LegacyCodeDetectionTests.ShouldNotHaveDeprecatedNamedTypes()` - 禁止 Deprecated 命名

#### Abstractions 位置约束 (PR-C3 新增, PR-C6 更新)
- **规则**：`Abstractions` 目录只能存在于以下位置：
  - `Core/ZakYip.WheelDiverterSorter.Core/Abstractions/`（**不再包含 Drivers 子目录**）
  - `Infrastructure/ZakYip.WheelDiverterSorter.Communication/Abstractions/`
- **PR-C6 变更**：原 `Core/Abstractions/Drivers/` 已删除，硬件相关抽象统一迁移至 `Core/Hardware/` 的对应子目录
- **测试**：`DuplicateTypeDetectionTests.AbstractionsShouldOnlyExistInAllowedLocations()`

#### HAL 层约束 (PR-C6 新增, PR-RS11 更新)
- **规则**：HAL 已收敛到 `Core/Hardware/`，**禁止增加新的平行硬件抽象层**
- **允许的位置**：
  - `Core/Hardware/Ports/` - IO 端口接口 (IInputPort, IOutputPort)
  - `Core/Hardware/IoLinkage/` - IO 联动接口 (IIoLinkageDriver)
  - `Core/Hardware/Devices/` - 设备驱动接口 (IWheelDiverterDriver, IEmcController, IEmcResourceLockManager 等)
  - `Core/Hardware/Mappings/` - IO 映射接口 (IVendorIoMapper)
  - `Core/Hardware/Providers/` - 配置提供者接口 (ISensorVendorConfigProvider)
- **PR-RS11 变更**：`IEmcResourceLockManager` 从 `Communication/Abstractions/` 迁移至 `Core/Hardware/Devices/`，`EmcLockEvent` 从 `Communication/Models/` 迁移至 `Core/Events/Communication/`
- **禁止的位置**：
  - `Core/Abstractions/Drivers/` (已删除)
  - `Core/Drivers/`, `Core/Adapters/`, `Core/HardwareAbstractions/` 等平行目录
- **测试**：
  - `DuplicateTypeDetectionTests.Core_ShouldNotHaveParallelHardwareAbstractionLayers()`
  - `DuplicateTypeDetectionTests.Core_Hardware_ShouldHaveStandardSubdirectories()`
  - `ApplicationLayerDependencyTests.Drivers_ShouldNotDependOn_Execution_Or_Communication()` (PR-RS11 新增)
  - `ApplicationLayerDependencyTests.Drivers_ShouldOnlyDependOn_CoreOrObservability()` (PR-RS11 新增)

> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：HAL 接口的完整权威列表和禁止位置。

---

## 3. 各项目内部结构

### 3.1 ZakYip.WheelDiverterSorter.Application

**项目职责**：应用服务层 & DI 聚合层（PR-H1），封装 Core + Execution + Drivers + Ingress + Communication + Simulation 的组合逻辑，提供应用服务/用例服务，同时作为 Host 层的统一 DI 入口。

```
ZakYip.WheelDiverterSorter.Application/
├── Extensions/                          # PR-H1: DI 扩展方法（统一服务注册入口）
│   └── WheelDiverterSorterServiceCollectionExtensions.cs
├── Services/                           # 应用服务实现（按职责分组）
│   ├── Caching/                        # 缓存相关服务
│   │   ├── CachedDriverConfigurationRepository.cs
│   │   ├── CachedSensorConfigurationRepository.cs
│   │   ├── CachedSwitchingPathGenerator.cs
│   │   └── InMemoryRoutePlanRepository.cs
│   ├── Config/                         # 配置服务（接口+实现）
│   │   ├── ISystemConfigService.cs, SystemConfigService.cs
│   │   ├── ILoggingConfigService.cs, LoggingConfigService.cs
│   │   ├── ICommunicationConfigService.cs, CommunicationConfigService.cs
│   │   ├── IIoLinkageConfigService.cs, IoLinkageConfigService.cs
│   │   └── IVendorConfigService.cs, VendorConfigService.cs
│   ├── Debug/                          # 调试分拣服务
│   │   └── IDebugSortService.cs, DebugSortService.cs
│   ├── Health/                         # 健康检查服务
│   │   └── IPreRunHealthCheckService.cs, PreRunHealthCheckService.cs
│   ├── Metrics/                        # 性能指标服务
│   │   ├── CommunicationStatsService.cs
│   │   ├── CongestionDataCollector.cs
│   │   └── SorterMetrics.cs
│   ├── Simulation/                     # 仿真相关服务
│   │   ├── ISimulationOrchestratorService.cs
│   │   └── SimulationModeProvider.cs
│   ├── Sorting/                        # 分拣业务服务
│   │   ├── IChangeParcelChuteService.cs, ChangeParcelChuteService.cs
│   │   └── OptimizedSortingService.cs
│   └── Topology/                       # 拓扑服务
│       └── IChutePathTopologyService.cs, ChutePathTopologyService.cs
└── ApplicationServiceExtensions.cs     # DI 扩展方法 (AddWheelDiverterApplication)
```

> **注意**：Application 层包含众多配置/统计/辅助服务，上述目录树展示主要结构。完整服务列表请查看源码目录 `src/Application/ZakYip.WheelDiverterSorter.Application/Services/`。本文档不再逐一枚举所有服务类，避免文档频繁同步更新。

#### 关键角色（边界 & DI 入口）

- **`WheelDiverterSorterServiceCollectionExtensions`**（Extensions/）：统一 DI 入口，提供 `AddWheelDiverterSorter()` 方法
- **`ApplicationServiceExtensions`**：提供 `AddWheelDiverterApplication()` 注册所有应用服务

#### 核心配置服务（供 Controller 注入）

> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：配置服务的权威位置和禁止出现的位置。

- `ISystemConfigService` / `ILoggingConfigService` / `ICommunicationConfigService` / `IIoLinkageConfigService` / `IVendorConfigService`

#### 核心业务服务

- `IChangeParcelChuteService`：改口服务，处理包裹目标格口变更
- `IPreRunHealthCheckService`：运行前健康检查
- `ISimulationOrchestratorService`：仿真编排服务接口
- `OptimizedSortingService`：性能优化的分拣服务
- `SorterMetrics`：分拣系统性能指标

### 3.2 ZakYip.WheelDiverterSorter.Host

**项目职责**：Web API 主机入口，负责 DI 容器配置、API Controller 定义、启动引导和 Swagger 文档生成。**Host 必须保持"薄层"原则**：不包含业务逻辑，业务逻辑委托给 Application 层和下游项目。

> **核心原则（PR-RS9 强化）**：Host 层只依赖 Application/Core/Observability，通过 Application 层间接访问其他项目的服务。**Host 层不包含任何业务接口/命令/仓储/Adapter/业务中间件，只保留启动、状态机、Controller 与薄包装 DI 扩展。**

```
ZakYip.WheelDiverterSorter.Host/
├── Controllers/                     # API 控制器（16个）
│   ├── ApiControllerBase.cs
│   ├── AlarmsController.cs
│   ├── ChuteAssignmentTimeoutController.cs
│   ├── ChutePathTopologyController.cs
│   ├── CommunicationController.cs
│   ├── DivertsController.cs
│   ├── HardwareConfigController.cs
│   ├── HealthController.cs
│   ├── IoLinkageController.cs
│   ├── LoggingConfigController.cs
│   ├── PanelConfigController.cs
│   ├── PolicyController.cs
│   ├── SimulationConfigController.cs
│   ├── SimulationController.cs
│   ├── SystemConfigController.cs
│   └── SystemOperationsController.cs
├── Health/                          # 健康检查提供者
│   └── HostHealthStatusProvider.cs
├── Models/                          # API 请求/响应 DTO
│   ├── Communication/               # 通信相关 DTO
│   ├── Config/                      # 配置相关 DTO
│   └── Panel/                       # 面板相关 DTO
├── Services/                        # Host 层服务（仅 DI 扩展和 Workers）
│   ├── Extensions/                  # DI 扩展方法
│   │   ├── HealthCheckServiceExtensions.cs
│   │   ├── SystemStateServiceExtensions.cs
│   │   └── WheelDiverterSorterHostServiceCollectionExtensions.cs
│   └── Workers/                     # 后台工作服务
│       ├── AlarmMonitoringWorker.cs
│       ├── BootHostedService.cs
│       └── RouteTopologyConsistencyCheckWorker.cs
├── StateMachine/                    # 系统状态机
│   ├── BootstrapStage.cs
│   ├── ISystemStateManager.cs
│   ├── SystemState.cs
│   ├── SystemStateManager.cs
│   └── SystemStateManagerWithBoot.cs
├── Swagger/                         # Swagger 配置与过滤器
│   ├── IoDriverConfigurationSchemaFilter.cs
│   ├── WheelDiverterConfigurationSchemaFilter.cs
│   └── WheelDiverterControllerDocumentFilter.cs
├── Program.cs                       # 应用入口点
├── appsettings.json                 # 配置文件
├── nlog.config                      # NLog 日志配置
└── Dockerfile                       # Docker 构建文件
```

#### Host 层结构约束（PR-RS9 强化）

##### ✅ 允许的目录（白名单）

| 目录 | 用途 | 允许的内容 |
|------|------|-----------|
| `Controllers/` | API 端点 | API Controller 类，继承 ApiControllerBase |
| `Health/` | 健康检查 | 健康检查提供者类 |
| `Models/` | DTO | API 请求/响应模型（不含业务逻辑） |
| `Services/Extensions/` | DI 配置 | Host 层 DI 扩展方法（薄包装） |
| `Services/Workers/` | 后台任务 | BackgroundService / IHostedService 实现 |
| `StateMachine/` | 状态机 | 系统状态管理（唯一允许定义 `ISystemStateManager` 接口的位置） |
| `Swagger/` | API 文档 | Swagger 过滤器和配置 |
| `Properties/` | 项目属性 | launchSettings.json 等 |

##### ❌ 禁止的目录/概念（黑名单）

| 禁止的目录/概念 | 原因 | 应放置位置 |
|----------------|------|-----------|
| `Application/` | 业务服务应在 Application 层 | `Application/Services/` |
| `Commands/` | Command 模式应在 Application 层 | `Application/Services/Sorting/` |
| `Pipeline/` | 中间件/管道应在 Execution 层 | `Execution/Pipeline/` |
| `Repositories/` | 仓储实现应在 Core 层 | `Core/LineModel/Configuration/Repositories/` |
| `Adapters/` | 适配器应在对应业务层 | `Execution/` 或 `Drivers/` |
| `Middleware/` | 业务中间件应在 Execution 层 | `Execution/Pipeline/Middlewares/` |
| `I*Service` 接口（ISystemStateManager 除外）¹ | 业务服务接口应在 Application/Core 层 | `Application/Services/` |

> ¹ **ISystemStateManager 例外说明**：`ISystemStateManager` 是 Host 层状态机的核心接口，定义系统启动/运行/停止状态转换契约。该接口直接与 Host 层的启动引导职责绑定，不属于可下沉到 Application/Core 的业务服务，因此允许在 `StateMachine/` 目录定义。

##### 测试防线

| 测试类 | 约束内容 |
|-------|---------|
| `ArchTests.HostLayerConstraintTests` | 禁止接口定义、禁止业务模式类型、禁止业务目录 |
| `TechnicalDebtComplianceTests.HostLayerComplianceTests` | 禁止 Commands/Application/Pipeline 等目录 |

#### 关键类型概览

- **`Program.cs`**：应用启动入口，调用 `AddWheelDiverterSorterHost()` 完成所有服务注册
- **`SystemStateManager`**（StateMachine/）：系统启动/运行/停止状态转换管理
- **`BootHostedService`**（Services/Workers/）：启动引导服务，按顺序初始化各子系统
- **`ApiControllerBase`**（Controllers/）：所有 Controller 的基类，提供统一响应格式
- **`WheelDiverterSorterHostServiceCollectionExtensions`**（Services/Extensions/）：Host 层薄包装，调用 Application 层的 `AddWheelDiverterSorter()`

---

### 3.3 ZakYip.WheelDiverterSorter.Core

**项目职责**：定义核心领域模型、抽象接口和业务规则。是整个解决方案的基础层，不依赖任何其他业务项目。

```
ZakYip.WheelDiverterSorter.Core/
├── Abstractions/
│   ├── Execution/                   # 执行层抽象
│   │   └── ICongestionDataCollector.cs
│   ├── Ingress/                     # 入口层抽象
│   │   └── ISensorEventProvider.cs
│   └── Upstream/                    # 上游通信抽象
│       ├── IUpstreamRoutingClient.cs
│       └── IUpstreamContractMapper.cs
├── Chaos/                           # 混沌工程支持
│   ├── ChaosInjectionOptions.cs
│   ├── ChaosInjectionService.cs
│   └── IChaosInjector.cs
├── Enums/                           # 枚举定义
│   ├── Communication/
│   ├── Hardware/                    # PR-TD6: 新增 WheelDiverterState, WheelCommandResultType, WheelDeviceState
│   ├── Monitoring/
│   ├── Parcel/
│   ├── Simulation/                  # PR-TD6: 新增目录，包含 SimulationStepType, StepStatus
│   ├── Sorting/
│   └── System/
├── Hardware/                        # PR-C6: HAL（硬件抽象层）统一目录
│   ├── Ports/                       # IO 端口接口
│   │   ├── IInputPort.cs
│   │   └── IOutputPort.cs
│   ├── IoLinkage/                   # IO 联动接口
│   │   └── IIoLinkageDriver.cs
│   ├── Devices/                     # 设备驱动接口
│   │   ├── IWheelDiverterDriver.cs
│   │   ├── IWheelDiverterDriverManager.cs
│   │   ├── IWheelProtocolMapper.cs
│   │   ├── IEmcController.cs
│   │   ├── IEmcResourceLockManager.cs  # PR-RS11: 从 Communication 迁移
│   │   └── (WheelCommandResult, WheelDeviceStatus 等值对象)
│   ├── Mappings/                    # IO 映射接口
│   │   ├── IVendorIoMapper.cs
│   │   └── VendorIoAddress.cs
│   ├── Providers/                   # 配置提供者接口
│   │   └── ISensorVendorConfigProvider.cs
│   ├── IWheelDiverterDevice.cs      # 摆轮设备接口（命令模式）
│   ├── IConveyorDriveController.cs  # 传送带驱动控制器接口
│   ├── ISensorInputReader.cs        # 传感器输入读取接口
│   ├── HardwareEventArgs.cs         # 硬件事件参数
│   └── VendorCapabilities.cs        # 厂商能力声明
├── IoBinding/                       # IO 绑定模型
│   ├── IoBindingProfile.cs
│   ├── SensorBinding.cs
│   └── ActuatorBinding.cs
├── LineModel/                       # 线体模型（核心领域）
│   ├── Bindings/
│   ├── Chutes/                      # 格口相关
│   ├── Configuration/               # 配置模型与仓储（PR4 重构后，PR-SD5 瘦身）
│   │   ├── Models/                  # 纯配置模型类（22个文件，PR-SD5 删除4个未使用模型）
│   │   │   ├── SystemConfiguration.cs
│   │   │   ├── CabinetIoOptions.cs          # PR-TD7: 厂商无关控制面板IO配置（原 LeadshineCabinetIoOptions）
│   │   │   ├── ChutePathTopologyConfig.cs
│   │   │   ├── IoLinkageConfiguration.cs
│   │   │   ├── CommunicationConfiguration.cs
│   │   │   ├── LoggingConfiguration.cs
│   │   │   └── ...
│   │   │   # PR-SD5 已删除：IoPointConfiguration.cs, LineSegmentConfig.cs, PanelIoOptions.cs, SignalTowerOptions.cs
│   │   ├── Repositories/            # 仓储层
│   │   │   ├── Interfaces/          # 仓储接口（11个文件）
│   │   │   │   ├── ISystemConfigurationRepository.cs
│   │   │   │   ├── IChutePathTopologyRepository.cs
│   │   │   │   ├── IRouteConfigurationRepository.cs
│   │   │   │   └── ...
│   │   │   └── LiteDb/              # LiteDB 实现（12个文件）
│   │   │       ├── LiteDbSystemConfigurationRepository.cs
│   │   │       ├── LiteDbRouteConfigurationRepository.cs
│   │   │       ├── LiteDbMapperConfig.cs
│   │   │       └── ...
│   │   └── Validation/              # 配置验证
│   │       └── IoEndpointValidator.cs
│   ├── Events/
│   ├── Orchestration/               # 路由拓扑一致性检查
│   ├── Routing/                     # 路由计划模型
│   ├── Runtime/                     # 运行时模型
│   ├── Segments/                    # 输送段模型
│   ├── Services/                    # 线体服务接口
│   ├── Topology/                    # 拓扑与路径生成
│   │   ├── SorterTopology.cs        # 当前标准拓扑模型
│   │   ├── SwitchingPath.cs         # 摆轮切换路径
│   │   ├── ISwitchingPathGenerator.cs
│   │   ├── DefaultSwitchingPathGenerator.cs
│   │   └── SwitchingPathSegment.cs  # 路径段模型
│   ├── Tracing/                     # 追踪接口
│   └── Utilities/
├── Results/                         # 操作结果模型
│   ├── OperationResult.cs
│   └── ErrorCodes.cs
├── Sorting/                         # 分拣业务模型
│   ├── Contracts/                   # 请求/响应契约
│   ├── Events/                      # 分拣事件
│   ├── Exceptions/
│   ├── Interfaces/                  # 分拣接口
│   ├── Models/                      # 分拣模型
│   ├── Orchestration/               # 编排接口
│   │   ├── ISortingOrchestrator.cs
│   │   └── ISortingExceptionHandler.cs
│   ├── Overload/                    # 超载处理
│   ├── Pipeline/                    # 分拣管道
│   ├── Policies/                    # 分拣策略
│   ├── Runtime/                     # 运行时
│   └── Strategy/                    # 格口选择策略
└── Utilities/                       # 工具类（通用公共工具）
    ├── ISystemClock.cs              # 系统时钟抽象接口
    └── LocalSystemClock.cs          # 本地系统时钟实现
```

#### Core 层工具类位置规范（PR-SD6 新增）

Core 层采用"统一工具 + 领域特化工具"的结构：

| 位置 | 用途 | 类型要求 |
|------|------|----------|
| `Core/Utilities/` | 通用公共工具（如 ISystemClock） | 公开接口和实现类 |
| `Core/LineModel/Utilities/` | LineModel 专用工具（如 ChuteIdHelper, LoggingHelper） | 必须使用 `file static class` |
| `Observability/Utilities/` | 可观测性相关工具（如 ISafeExecutionService） | 公开接口和实现类 |

**规则**：
1. 通用工具（被多个项目使用）放在 `Core/Utilities/`
2. 领域专用工具（仅 LineModel 内部使用）放在 `Core/LineModel/Utilities/`，必须使用 `file static class` 限制作用域
3. **禁止**在其他位置新建 `*Helper`、`*Utils`、`*Utilities` 类（除非是 `file static class`）
4. **禁止**同名工具类在多个命名空间中定义

**防线测试**：`TechnicalDebtComplianceTests.DuplicateTypeDetectionTests.UtilityTypesShouldNotBeDuplicatedAcrossNamespaces`

#### 关键类型概览

- `ISortingOrchestrator`（位于 Sorting/Orchestration/）：分拣编排服务接口，定义核心业务流程入口
- `ISwitchingPathGenerator`（位于 LineModel/Topology/）：摆轮路径生成器接口，根据目标格口生成摆轮指令序列
- `IWheelDiverterDriver`（位于 Hardware/Devices/）：摆轮驱动器抽象接口，定义左转/右转/直通操作
- `IUpstreamRoutingClient`（位于 Abstractions/Upstream/）：上游路由客户端抽象，用于请求格口分配
- `ISystemClock`（位于 Utilities/）：系统时钟抽象，所有时间获取必须通过此接口
- `SwitchingPath`（位于 LineModel/Topology/）：摆轮切换路径模型，包含目标格口和切换段序列
- `SystemConfiguration`（位于 LineModel/Configuration/）：系统配置模型，包含异常格口等核心参数
- `ChutePathTopologyConfig`（位于 LineModel/Configuration/）：格口-路径拓扑配置

---

### 3.4 ZakYip.WheelDiverterSorter.Execution

**项目职责**：分拣业务编排实现层，负责协调包裹从"入口→请求格口→路径生成→路径执行"的完整流程。

```
ZakYip.WheelDiverterSorter.Execution/
├── Concurrency/                     # 并发控制
│   ├── ConcurrentSwitchingPathExecutor.cs
│   ├── DiverterResourceLockManager.cs
│   ├── MonitoredParcelQueue.cs
│   ├── PriorityParcelQueue.cs
│   └── ...
├── Diagnostics/                     # 诊断与异常检测（PR-TD4）
│   └── AnomalyDetector.cs
├── Events/                          # 执行事件
│   ├── PathExecutionFailedEventArgs.cs
│   └── PathSwitchedEventArgs.cs
├── Extensions/                      # DI 扩展方法（PR-TD4: 新增）
│   └── NodeHealthServiceExtensions.cs
├── Health/                          # 健康监控
│   ├── NodeHealthMonitorService.cs
│   ├── NodeHealthRegistry.cs
│   └── PathHealthChecker.cs
├── Infrastructure/                  # 基础设施实现（PR-TD4）
│   ├── DefaultStrategyFactory.cs
│   └── DefaultSystemRunStateService.cs
├── Orchestration/                   # 核心编排实现
│   ├── SortingOrchestrator.cs       # 分拣编排器主实现
│   └── SortingExceptionHandler.cs
├── PathExecution/                   # 路径执行服务（PR-TD4）
│   ├── IPathExecutionService.cs
│   ├── PathExecutionService.cs
│   └── PathFailureHandler.cs
├── Pipeline/                        # 分拣管道中间件
│   └── Middlewares/
│       ├── OverloadEvaluationMiddleware.cs
│       ├── PathExecutionMiddleware.cs
│       ├── RoutePlanningMiddleware.cs
│       ├── TracingMiddleware.cs
│       └── UpstreamAssignmentMiddleware.cs
├── Routing/                         # 路由相关
├── Segments/                        # 输送段实现（PR-TD4）
│   ├── ConveyorSegment.cs
│   └── MiddleConveyorCoordinator.cs
├── SelfTest/                        # 自检功能
│   ├── SystemSelfTestCoordinator.cs
│   └── DefaultConfigValidator.cs
├── Strategy/                        # 格口选择策略实现
│   ├── CompositeChuteSelectionService.cs
│   ├── FixedChuteSelectionStrategy.cs
│   ├── FormalChuteSelectionStrategy.cs
│   └── RoundRobinChuteSelectionStrategy.cs
└── ZakYip.WheelDiverterSorter.Execution.csproj
# PR-TD4: Execution 根目录不再有业务类型，所有文件已归档到子目录
```

#### 关键类型概览

- `SortingOrchestrator`（位于 Orchestration/）：分拣编排器核心实现，协调整个分拣流程
- `ISwitchingPathExecutor`（位于 Core/Abstractions/Execution/）：摆轮路径执行器接口，按段顺序执行摆轮切换
- `PathExecutionService`（位于 PathExecution/）：路径执行服务实现，处理路径执行细节
- `ConcurrentSwitchingPathExecutor`（位于 Concurrency/）：支持并发的路径执行器
- `DiverterResourceLockManager`（位于 Concurrency/）：摆轮资源锁管理器，防止并发冲突
- `PathHealthChecker`（位于 Health/）：路径健康检查器，执行前验证路径可用性
- `ConveyorSegment`（位于 Segments/）：中段皮带段实现

---

### 3.5 ZakYip.WheelDiverterSorter.Drivers

**项目职责**：硬件驱动实现层，封装与具体硬件设备（雷赛 IO 卡、西门子 PLC、摩迪/书迪鸟摆轮协议等）的通信细节。所有厂商相关实现和配置类都集中在 `Vendors/<VendorName>/` 目录下。

```
ZakYip.WheelDiverterSorter.Drivers/
├── Diagnostics/                     # 驱动诊断
│   └── RelayWheelDiverterSelfTest.cs
├── Vendors/                         # 厂商特定实现（所有厂商配置和实现集中于此）
│   ├── Leadshine/                   # 雷赛 IO 卡驱动
│   │   ├── Configuration/           # 雷赛配置类
│   │   │   ├── LeadshineOptions.cs          # 雷赛控制器配置
│   │   │   ├── LeadshineDiverterConfigDto.cs # 摆轮配置DTO
│   │   │   ├── LeadshineSensorOptions.cs    # 传感器配置
│   │   │   ├── LeadshineSensorConfigDto.cs  # 传感器配置DTO
│   │   │   └── LeadshineSensorVendorConfigProvider.cs  # PR-TD7: 实现 ISensorVendorConfigProvider
│   │   ├── IoMapping/               # IO映射
│   │   │   └── LeadshineIoMapper.cs
│   │   ├── LTDMC.cs                 # 雷赛 SDK P/Invoke 封装
│   │   ├── LTDMC.dll                # 雷赛原生 DLL
│   │   ├── LeadshineInputPort.cs
│   │   ├── LeadshineOutputPort.cs
│   │   ├── LeadshineDiverterController.cs
│   │   ├── LeadshineConveyorSegmentDriver.cs
│   │   ├── LeadshineIoLinkageDriver.cs
│   │   ├── LeadshineEmcController.cs
│   │   ├── CoordinatedEmcController.cs
│   │   ├── LeadshineVendorDriverFactory.cs
│   │   └── LeadshineIoServiceCollectionExtensions.cs  # DI 扩展（包含 ISensorVendorConfigProvider 注册）
│   ├── Siemens/                     # 西门子 S7 PLC 驱动
│   │   ├── Configuration/           # 西门子配置类
│   │   │   ├── S7Options.cs                 # S7 PLC 配置
│   │   │   └── S7DiverterConfigDto.cs       # 摆轮配置DTO
│   │   ├── S7Connection.cs
│   │   ├── S7DiverterController.cs
│   │   ├── S7DiverterConfig.cs
│   │   ├── S7InputPort.cs
│   │   ├── S7OutputPort.cs
│   │   └── SiemensS7ServiceCollectionExtensions.cs    # DI 扩展
│   ├── Modi/                        # 摩迪摆轮协议驱动
│   │   ├── Configuration/           # PR-TD7: 摩迪配置类
│   │   │   └── ModiOptions.cs               # 摩迪通信配置选项
│   │   ├── ModiProtocol.cs
│   │   ├── ModiProtocolEnums.cs
│   │   ├── ModiWheelDiverterDriver.cs
│   │   ├── ModiSimulatedDevice.cs
│   │   └── ModiWheelServiceCollectionExtensions.cs    # DI 扩展
│   ├── ShuDiNiao/                   # 书迪鸟摆轮协议驱动
│   │   ├── Configuration/           # PR-TD7: 书迪鸟配置类
│   │   │   └── ShuDiNiaoOptions.cs          # 书迪鸟通信配置选项
│   │   ├── ShuDiNiaoProtocol.cs
│   │   ├── ShuDiNiaoProtocolEnums.cs
│   │   ├── ShuDiNiaoWheelDiverterDriver.cs
│   │   ├── ShuDiNiaoWheelDiverterDriverManager.cs
│   │   ├── ShuDiNiaoSimulatedDevice.cs
│   │   └── ShuDiNiaoWheelServiceCollectionExtensions.cs # DI 扩展
│   └── Simulated/                   # 仿真驱动实现
│       ├── Configuration/           # PR-TD7: 仿真配置类
│       │   └── SimulatedOptions.cs          # 仿真行为配置选项
│       ├── IoMapping/
│       │   └── SimulatedIoMapper.cs
│       ├── SimulatedWheelDiverterDevice.cs
│       ├── SimulatedConveyorSegmentDriver.cs
│       ├── SimulatedIoLinkageDriver.cs
│       ├── SimulatedVendorDriverFactory.cs
│       └── SimulatedDriverServiceCollectionExtensions.cs # DI 扩展
├── FactoryBasedDriverManager.cs     # 工厂模式驱动管理器
├── HardwareSwitchingPathExecutor.cs # 硬件路径执行器
├── WheelCommandExecutor.cs          # 摆轮命令执行器
├── IoLinkageExecutor.cs             # IO 联动执行器
├── DriverServiceExtensions.cs       # 通用 DI 扩展方法（已弃用，推荐使用厂商特定扩展）
└── DriverOptions.cs                 # 驱动配置选项（包含 Sensor 属性用于传感器配置）
```

**厂商目录结构规范**:
- 每个厂商目录 (`Vendors/<VendorName>/`) 必须包含该厂商所有相关代码：
  - `Configuration/` - 配置类 (Options, Config, DTO)
  - `IoMapping/` - IO映射实现（如适用）
  - 驱动实现文件
  - `<VendorName>ServiceCollectionExtensions.cs` - DI 扩展方法

#### 关键类型概览

- `HardwareSwitchingPathExecutor`：硬件路径执行器，将路径指令下发到真实硬件
- `FactoryBasedDriverManager`：基于工厂模式的驱动管理器，支持多厂商设备
- `LeadshineDiverterController`（位于 Vendors/Leadshine/）：雷赛摆轮控制器实现
- `S7DiverterController`（位于 Vendors/Siemens/）：西门子 S7 PLC 摆轮控制器
- `ShuDiNiaoWheelDiverterDriver`（位于 Vendors/ShuDiNiao/）：书迪鸟摆轮驱动实现
- `SimulatedWheelDiverterDevice`（位于 Vendors/Simulated/）：仿真摆轮设备，用于测试
- `IoLinkageExecutor`：IO 联动执行器，处理传感器与摆轮的联动逻辑

---

### 3.6 ZakYip.WheelDiverterSorter.Ingress

**项目职责**：入口层，负责传感器事件监听、包裹检测。

**PR-TD7 变更**：Ingress 项目不再直接引用 `Drivers.Vendors.*` 命名空间，而是通过 Core 层的厂商无关抽象 `ISensorVendorConfigProvider` 获取传感器配置。

**PR-TD8 变更**：删除了冗余的 `Upstream/` 目录（`IUpstreamFacade`、`UpstreamFacade`、`IUpstreamChannel`、`HttpUpstreamChannel` 等），上游通信统一使用 `Communication` 层的 `IUpstreamRoutingClient`。

```
ZakYip.WheelDiverterSorter.Ingress/
├── Adapters/                        # 适配器
│   └── SensorEventProviderAdapter.cs
├── Configuration/                   # 传感器配置（通用配置）
│   ├── SensorConfiguration.cs
│   ├── SensorOptions.cs             # PR-TD7: 厂商无关，通过 ISensorVendorConfigProvider 获取配置
│   ├── MockSensorConfigDto.cs
│   └── ParcelDetectionOptions.cs
├── Models/                          # 入口层模型
│   ├── ParcelDetectedEventArgs.cs
│   ├── SensorEvent.cs               # PR-S6: 真实传感器事件模型（唯一的 SensorEvent 定义）
│   ├── SensorHealthStatus.cs
│   └── ...
├── Sensors/                         # 传感器实现
│   ├── LeadshineSensor.cs           # 使用 Core.Abstractions.Drivers 接口
│   ├── LeadshineSensorFactory.cs    # PR-TD7: 使用 ISensorVendorConfigProvider 替代直接配置引用
│   ├── MockSensor.cs
│   └── MockSensorFactory.cs
├── Services/                        # 服务实现
│   ├── ParcelDetectionService.cs
│   └── SensorHealthMonitor.cs
├── IParcelDetectionService.cs       # 包裹检测服务接口
├── ISensor.cs                       # 传感器接口
├── ISensorFactory.cs                # 传感器工厂接口
└── SensorServiceExtensions.cs       # DI 扩展方法（使用 ISensorVendorConfigProvider）
```

#### 关键类型概览

- `IParcelDetectionService`：包裹检测服务接口，监听传感器事件并触发 ParcelDetected 事件
- `ParcelDetectionService`（位于 Services/）：包裹检测服务实现
- `ISensor`：传感器抽象接口
- `SensorEvent`（位于 Models/）：真实传感器事件模型（PR-S6: Simulation 层的同名类型已重命名为 `SimulatedSensorEvent`）
- `LeadshineSensor`（位于 Sensors/）：雷赛传感器实现
- `LeadshineSensorFactory`（位于 Sensors/）：雷赛传感器工厂，通过 `ISensorVendorConfigProvider` 获取配置
- `SensorHealthMonitor`（位于 Services/）：传感器健康监控服务

---

### 3.7 ZakYip.WheelDiverterSorter.Communication

**项目职责**：通信基础设施层，实现与上游 RuleEngine 的多协议通信（TCP/SignalR/MQTT/HTTP），支持客户端和服务器两种模式。

```
ZakYip.WheelDiverterSorter.Communication/
├── Abstractions/                    # 通信抽象接口
│   ├── IRuleEngineServer.cs
│   ├── IRuleEngineHandler.cs
│   ├── IUpstreamConnectionManager.cs
│   └── IUpstreamRoutingClientFactory.cs
# PR-RS11: IEmcResourceLockManager 已迁移至 Core/Hardware/Devices/
├── Adapters/                        # 适配器
│   └── DefaultUpstreamContractMapper.cs
├── Clients/                         # 客户端实现（实现 Core 层的 IUpstreamRoutingClient）
│   ├── TcpRuleEngineClient.cs
│   ├── SignalRRuleEngineClient.cs
│   ├── MqttRuleEngineClient.cs
│   ├── HttpRuleEngineClient.cs
│   ├── InMemoryRuleEngineClient.cs
│   ├── RuleEngineClientBase.cs
│   └── EmcResourceLockManager*.cs   # 实现 Core/Hardware/Devices/IEmcResourceLockManager
├── Configuration/                   # 通信配置
│   ├── RuleEngineConnectionOptions.cs
│   ├── TcpOptions.cs
│   ├── SignalROptions.cs
│   ├── MqttOptions.cs
│   └── HttpOptions.cs
├── Gateways/                        # 上游网关
│   ├── TcpUpstreamSortingGateway.cs
│   ├── SignalRUpstreamSortingGateway.cs
│   ├── HttpUpstreamSortingGateway.cs
│   └── UpstreamSortingGatewayFactory.cs
├── Health/                          # 健康检查
│   └── RuleEngineUpstreamHealthChecker.cs
├── Infrastructure/                  # 基础设施
├── Models/                          # 通信模型
│   ├── ChuteAssignmentRequest.cs
│   ├── ChuteAssignmentResponse.cs
│   └── ParcelDetectionNotification.cs
# PR-RS11: EmcLockEvent 已迁移至 Core/Events/Communication/
├── Servers/                         # 服务器实现
│   ├── TcpRuleEngineServer.cs
│   ├── SignalRRuleEngineServer.cs
│   └── MqttRuleEngineServer.cs
├── UpstreamRoutingClientFactory.cs  # 客户端工厂（创建 IUpstreamRoutingClient）
├── RuleEngineServerFactory.cs       # 服务器工厂
└── CommunicationServiceExtensions.cs # DI 扩展方法
```

#### 关键类型概览

> **PR-U1 架构变更**: IRuleEngineClient 已合并到 Core 层的 IUpstreamRoutingClient 接口，UpstreamRoutingClientAdapter 已删除。
> 所有客户端实现现在直接实现 IUpstreamRoutingClient 接口。

- `IUpstreamRoutingClient`（位于 Core/Abstractions/Upstream/）：上游路由客户端统一接口，定义连接、断开、通知包裹到达等操作
- `TcpRuleEngineClient`（位于 Clients/）：TCP 协议客户端实现，实现 IUpstreamRoutingClient
- `SignalRRuleEngineClient`（位于 Clients/）：SignalR 协议客户端实现，实现 IUpstreamRoutingClient
- `MqttRuleEngineClient`（位于 Clients/）：MQTT 协议客户端实现，实现 IUpstreamRoutingClient
- `UpstreamRoutingClientFactory`：根据配置创建对应协议的 IUpstreamRoutingClient 实例
- `RuleEngineUpstreamHealthChecker`（位于 Health/）：上游连接健康检查

---

### 3.8 ZakYip.WheelDiverterSorter.Observability

**项目职责**：可观测性层，提供监控指标（Prometheus）、告警、追踪日志、安全执行服务等基础设施。

```
ZakYip.WheelDiverterSorter.Observability/
├── Runtime/                         # 运行时监控
│   ├── Health/
│   └── RuntimePerformanceCollector.cs
├── Tracing/                         # 追踪与日志清理
│   ├── FileBasedParcelTraceSink.cs
│   ├── LogCleanupHostedService.cs
│   ├── ILogCleanupPolicy.cs
│   └── DefaultLogCleanupPolicy.cs
├── Utilities/                       # 基础设施工具
│   ├── ISafeExecutionService.cs     # 安全执行服务接口
│   ├── SafeExecutionService.cs      # 安全执行服务实现
│   ├── ILogDeduplicator.cs
│   ├── LogDeduplicator.cs
│   └── InfrastructureServiceExtensions.cs
├── AlarmService.cs                  # 告警服务
├── AlertHistoryService.cs           # 告警历史服务
├── PrometheusMetrics.cs             # Prometheus 指标定义
├── ParcelLifecycleLogger.cs         # 包裹生命周期日志
├── ParcelTimelineCollector.cs       # 包裹时间线收集器
├── MarkdownReportWriter.cs          # Markdown 报告生成
└── ObservabilityServiceExtensions.cs # DI 扩展方法
```

#### 关键类型概览

- `ISafeExecutionService`（位于 Utilities/）：安全执行服务接口，所有后台任务必须通过此服务包裹
- `SafeExecutionService`（位于 Utilities/）：安全执行服务实现，捕获异常防止进程崩溃
- `PrometheusMetrics`：Prometheus 指标定义，包含分拣计数、延迟直方图等
- `AlarmService`：告警服务，处理系统告警的生成与通知
- `ParcelLifecycleLogger`：包裹生命周期日志记录器
- `FileBasedParcelTraceSink`（位于 Tracing/）：基于文件的包裹追踪日志输出
- `LogCleanupHostedService`（位于 Tracing/）：日志清理后台服务

---

### 3.9 ZakYip.WheelDiverterSorter.Simulation

**项目职责**：仿真服务库（PR-TD6: 改为 Library 项目），提供仿真场景运行器、配置模型和结果统计等公共 API，供 Host 层和 Simulation.Cli 使用。

**PR-TD6 重构说明**：
- 项目 OutputType 从 `Exe` 改为 `Library`
- 命令行入口程序（Program.cs）移动到新项目 `ZakYip.WheelDiverterSorter.Simulation.Cli`
- Application 层和 Host 层只使用 Simulation 库的公共 API

**PR-S6 重构说明**：
- `SensorEvent` 类重命名为 `SimulatedSensorEvent`，与 Ingress 层的真实传感器事件区分
- 文件位置从 `Services/ParcelTimelineFactory.cs` 移动到 `Models/SimulatedSensorEvent.cs`

```
ZakYip.WheelDiverterSorter.Simulation/
├── Configuration/                   # 仿真配置 [公共 API]
│   ├── SimulationOptions.cs         # 仿真配置模型 [公共 API]
│   ├── DenseParcelStrategy.cs
│   ├── FrictionModelOptions.cs
│   ├── DropoutModelOptions.cs
│   ├── SensorFaultOptions.cs
│   └── ...
├── Models/                          # PR-S6: 仿真模型
│   └── SimulatedSensorEvent.cs      # PR-S6: 仿真层传感器事件（从 SensorEvent 重命名）
├── Results/                         # 仿真结果模型 [公共 API]
│   ├── SimulationSummary.cs         # 仿真汇总统计 [公共 API]
│   ├── ParcelSimulationResult.cs
│   └── ParcelSimulationStatus.cs
├── Scenarios/                       # 场景定义
│   ├── SimulationScenario.cs
│   ├── ScenarioDefinitions.cs
│   ├── ChaosScenarioDefinitions.cs
│   └── ParcelExpectation.cs
├── Services/                        # 仿真服务
│   ├── ISimulationScenarioRunner.cs # 场景运行器接口 [公共 API]
│   ├── SimulationScenarioRunner.cs  # 场景运行器实现
│   ├── SimulationRunner.cs          # 仿真运行器
│   ├── CapacityTestingRunner.cs     # 容量测试运行器
│   └── SimulationReportPrinter.cs
├── Strategies/                      # 策略实验
│   ├── StrategyExperimentRunner.cs
│   ├── StrategyExperimentConfig.cs
│   └── Reports/
├── appsettings.Simulation.json      # 仿真配置文件（供 CLI 使用）
├── appsettings.LongRun.json         # 长时运行配置
├── simulation-config/               # 仿真拓扑配置
└── reports/                         # 报告输出目录
```

#### 公共 API

Host 层和 Application 层应该只使用以下公共 API：

- **`ISimulationScenarioRunner`**: 场景运行器接口，提供 `RunScenarioAsync()` 方法
- **`SimulationOptions`**: 仿真配置模型，用于配置仿真参数
- **`SimulationSummary`**: 仿真结果汇总，包含成功率、错分数等统计信息

#### 关键类型概览

- `SimulationRunner`（位于 Services/）：仿真主运行器，协调仿真流程
- `SimulationScenarioRunner`（位于 Services/）：场景运行器，执行具体的仿真场景
- `SimulationScenario`（位于 Scenarios/）：仿真场景定义，包含包裹序列、期望结果等
- `StrategyExperimentRunner`（位于 Strategies/）：策略实验运行器，用于 A/B 测试不同策略
- `ScenarioDefinitions`（位于 Scenarios/）：预定义的标准测试场景集合

---

### 3.9.1 ZakYip.WheelDiverterSorter.Simulation.Cli

**项目职责**：仿真命令行入口程序（PR-TD6 新增），提供独立可执行的仿真控制台应用。

**PR-TD6 说明**：从 Simulation 项目中分离出来的 CLI 入口，引用 Simulation 库项目。

```
ZakYip.WheelDiverterSorter.Simulation.Cli/
├── Program.cs                       # 命令行入口程序
├── appsettings.Simulation.json      # 仿真配置文件
├── appsettings.LongRun.json         # 长时运行配置
└── ZakYip.WheelDiverterSorter.Simulation.Cli.csproj
```

#### 运行方式

```bash
# 运行仿真
dotnet run --project src/Simulation/ZakYip.WheelDiverterSorter.Simulation.Cli

# 通过命令行参数覆盖配置
dotnet run --project src/Simulation/ZakYip.WheelDiverterSorter.Simulation.Cli -- \
  --Simulation:ParcelCount=100 \
  --Simulation:SortingMode=RoundRobin
```

---

### 3.10 ZakYip.WheelDiverterSorter.Analyzers

**项目职责**：Roslyn 代码分析器，在编译时强制执行编码规范（禁止直接使用 DateTime.Now、要求 BackgroundService 使用 SafeExecutionService 等）。

```
ZakYip.WheelDiverterSorter.Analyzers/
├── ApiControllerResponseTypeAnalyzer.cs   # API 响应类型检查
├── BackgroundServiceSafeExecutionAnalyzer.cs # 后台服务安全执行检查
├── DateTimeNowUsageAnalyzer.cs            # DateTime.Now 使用检查
├── UtcTimeUsageAnalyzer.cs                # UTC 时间使用检查
├── AnalyzerReleases.Shipped.md
├── AnalyzerReleases.Unshipped.md
└── ZakYip.WheelDiverterSorter.Analyzers.csproj
```

#### 关键类型概览

- `DateTimeNowUsageAnalyzer`：检测并报告直接使用 DateTime.Now 或 DateTime.UtcNow 的代码
- `BackgroundServiceSafeExecutionAnalyzer`：检查 BackgroundService 是否使用 ISafeExecutionService 包裹执行逻辑
- `ApiControllerResponseTypeAnalyzer`：检查 API Controller 是否使用统一的 ApiResponse<T> 响应类型

---

### 3.10 工具项目

#### ZakYip.WheelDiverterSorter.Tools.Reporting

**项目职责**：仿真报告分析工具，解析仿真输出并生成统计报告。

```
ZakYip.WheelDiverterSorter.Tools.Reporting/
├── Analyzers/                       # 报告分析器
├── Models/                          # 报告模型
├── Writers/                         # 报告输出
├── Program.cs                       # 工具入口
└── ZakYip.WheelDiverterSorter.Tools.Reporting.csproj
```

#### ZakYip.WheelDiverterSorter.Tools.SafeExecutionStats

**项目职责**：SafeExecution 服务执行统计分析工具。

```
ZakYip.WheelDiverterSorter.Tools.SafeExecutionStats/
├── Program.cs
└── ZakYip.WheelDiverterSorter.Tools.SafeExecutionStats.csproj
```

#### tools/Profiling

**项目职责**：性能剖析脚本集合（非 .NET 项目）。

```
tools/Profiling/
├── counters-monitor.ps1             # Windows 性能计数器监控
├── counters-monitor.sh              # Linux 性能计数器监控
├── trace-sampling.ps1               # Windows 采样追踪
├── trace-sampling.sh                # Linux 采样追踪
└── README.md
```

---

## 4. 跨项目的关键类型与职责

> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：系统性防止影分身的权威实现表。

### 4.1 分拣编排核心

> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：分拣编排和拓扑/路径生成的权威位置。

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `ISortingOrchestrator` | Core/Sorting/Orchestration/ | 分拣编排服务接口，定义 ProcessParcelAsync 等核心入口方法 |
| `SortingOrchestrator` | Execution/Orchestration/ | 分拣编排器实现，协调包裹从检测到落格的完整流程 |
| `ISwitchingPathGenerator` | Core/LineModel/Topology/ | 路径生成器接口，根据格口 ID 生成摆轮切换路径 |
| `DefaultSwitchingPathGenerator` | Core/LineModel/Topology/ | 默认路径生成器实现，基于拓扑配置生成路径 |
| `ISwitchingPathExecutor` | Execution/ | 路径执行器接口，按段执行摆轮切换指令 |

### 4.2 上游通信

> **PR-U1 架构变更**: IRuleEngineClient 已合并到 IUpstreamRoutingClient，UpstreamRoutingClientAdapter 已删除。
>
> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：上游通信接口的权威位置和禁止出现的位置。

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `IUpstreamRoutingClient` | Core/Abstractions/Upstream/ | **唯一**上游路由客户端接口，定义连接、断开、通知包裹到达等操作 |
| `ChuteAssignmentEventArgs` | Core/Abstractions/Upstream/ | 格口分配事件参数，用于上游推送格口分配 |
| `TcpRuleEngineClient` | Communication/Clients/ | TCP 协议客户端实现，实现 IUpstreamRoutingClient |
| `SignalRRuleEngineClient` | Communication/Clients/ | SignalR 协议客户端实现，实现 IUpstreamRoutingClient |
| `MqttRuleEngineClient` | Communication/Clients/ | MQTT 协议客户端实现，实现 IUpstreamRoutingClient |
| `HttpRuleEngineClient` | Communication/Clients/ | HTTP 协议客户端实现（仅用于测试），实现 IUpstreamRoutingClient |
| `UpstreamRoutingClientFactory` | Communication/ | 根据配置创建对应协议的 IUpstreamRoutingClient 实例 |

### 4.3 硬件驱动抽象

> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：HAL 接口的完整权威列表和禁止位置。

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `IWheelDiverterDriver` | Core/Hardware/Devices/ | 摆轮驱动器接口，定义左转/右转/直通/停止操作（唯一摆轮控制抽象） |
| `IWheelDiverterDevice` | Core/Hardware/ | 摆轮设备接口，命令模式（ExecuteAsync(WheelCommand)） |
| `IInputPort` | Core/Hardware/Ports/ | 输入端口接口，读取传感器状态 |
| `IOutputPort` | Core/Hardware/Ports/ | 输出端口接口，控制继电器/指示灯 |
| `IIoLinkageDriver` | Core/Hardware/IoLinkage/ | IO 联动驱动接口 |

> **PR-TD9 注**: 摆轮控制统一通过 `IWheelDiverterDriver`（方向接口）或 `IWheelDiverterDevice`（命令接口）暴露，
> 不再允许引入与上述接口语义重叠的其他抽象（如已删除的 `IDiverterController`、`IWheelDiverterActuator`）。

### 4.4 配置与仓储

> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：配置模型和仓储的权威位置。

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `SystemConfiguration` | Core/LineModel/Configuration/ | 系统配置模型，包含异常格口 ID、版本等 |
| `ISystemConfigurationRepository` | Core/LineModel/Configuration/ | 系统配置仓储接口 |
| `ChutePathTopologyConfig` | Core/LineModel/Configuration/ | 格口-路径拓扑配置模型 |
| `IoLinkageConfiguration` | Core/LineModel/Configuration/ | IO 联动配置模型 |

### 4.5 基础设施服务

> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：日志/指标服务和系统时钟的权威位置。

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `ISystemClock` | Core/Utilities/ | 系统时钟抽象，所有时间获取必须通过此接口 |
| `ISafeExecutionService` | Observability/Utilities/ | 安全执行服务接口，捕获异常防止进程崩溃 |
| `PrometheusMetrics` | Observability/ | Prometheus 指标定义与收集 |
| `AlarmService` | Observability/ | 告警服务，处理系统告警 |

### 4.6 仿真相关

> **详见 [6. 单一权威实现 & 禁止影分身](#6-单一权威实现--禁止影分身)**：仿真的权威位置和禁止出现的位置。

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `SimulatedWheelDiverterDevice` | Drivers/Vendors/Simulated/ | 仿真摆轮设备实现 |
| `SimulatedVendorDriverFactory` | Drivers/Vendors/Simulated/ | 仿真驱动工厂 |
| `SimulationRunner` | Simulation/Services/ | 仿真主运行器 |
| `SimulationScenario` | Simulation/Scenarios/ | 仿真场景定义模型 |

### 4.7 传感器配置三层架构 (PR-TD10)

传感器配置采用三层架构，实现厂商解耦和运行时切换：

```
┌─────────────────────────────────────────────────────────────────────────┐
│  配置加载链路                                                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  appsettings.json                                                       │
│       │                                                                 │
│       ▼                                                                 │
│  ┌────────────────────────────────────────────────────┐                 │
│  │ 第一层：厂商 Options                                 │                 │
│  │ Drivers/Vendors/{Vendor}/Configuration/            │                 │
│  │ ├── LeadshineSensorOptions.cs                      │                 │
│  │ └── LeadshineSensorConfigDto.cs                    │                 │
│  └────────────────────────────────────────────────────┘                 │
│       │                                                                 │
│       │ DI 注入                                                         │
│       ▼                                                                 │
│  ┌────────────────────────────────────────────────────┐                 │
│  │ 第二层：HAL 抽象 (ISensorVendorConfigProvider)      │                 │
│  │ Core/Hardware/Providers/                           │                 │
│  │ ├── ISensorVendorConfigProvider.cs (接口)          │                 │
│  │ └── SensorConfigEntry (厂商无关的配置条目)          │                 │
│  │                                                    │                 │
│  │ 实现位于 Drivers 层:                                │                 │
│  │ └── LeadshineSensorVendorConfigProvider.cs         │                 │
│  │     (将 LeadshineSensorOptions → SensorConfigEntry) │                 │
│  └────────────────────────────────────────────────────┘                 │
│       │                                                                 │
│       │ 注入 ISensorVendorConfigProvider                                │
│       ▼                                                                 │
│  ┌────────────────────────────────────────────────────┐                 │
│  │ 第三层：消费层 (Ingress)                            │                 │
│  │ Ingress/Sensors/                                   │                 │
│  │ ├── LeadshineSensorFactory.cs                      │                 │
│  │ └── LeadshineSensor.cs                             │                 │
│  │                                                    │                 │
│  │ 只依赖 ISensorVendorConfigProvider 和 IInputPort   │                 │
│  │ 不依赖 Drivers.Vendors.* 命名空间                   │                 │
│  └────────────────────────────────────────────────────┘                 │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

**各层职责**：

| 层次 | 位置 | 职责 | 关键类型 |
|-----|------|-----|---------|
| 厂商 Options | Drivers/Vendors/{Vendor}/Configuration/ | 定义厂商特定的配置结构，直接对应硬件配置 | `LeadshineSensorOptions`, `LeadshineSensorConfigDto` |
| HAL 抽象 | Core/Hardware/Providers/ | 定义厂商无关的配置访问协议，实现类型转换 | `ISensorVendorConfigProvider`, `SensorConfigEntry` |
| HAL 实现 | Drivers/Vendors/{Vendor}/Configuration/ | 将厂商 Options 转换为通用配置条目 | `LeadshineSensorVendorConfigProvider` |
| 消费层 | Ingress/Sensors/ | 基于通用配置创建传感器实例 | `LeadshineSensorFactory` |

**为什么不是简单的 Options 包装器**：

- `ISensorVendorConfigProvider` 实现了类型转换（`LeadshineSensorConfigDto` → `SensorConfigEntry`）
- 实现厂商解耦：Ingress 层无需 `using Drivers.Vendors.*` 命名空间
- 支持运行时切换：DI 容器可以根据配置注入不同厂商的实现

---

## 5. 当前结构中已发现的问题标记

> 以下问题仅记录，本 PR 不做任何修改。这些问题将由后续 5 个重构 PR 分批解决。

### 5.1 层级职责混淆

1. **~~Execution 项目根目录文件过多~~** ✅ 已解决 (PR-TD4)
   - ~~`ISwitchingPathExecutor.cs`、`AnomalyDetector.cs`、`ConveyorSegment.cs` 等文件直接放在项目根目录~~
   - ~~建议：按职责归类到对应子目录（如 Abstractions/、Segments/）~~
   - **PR-TD4 解决方案**：
     - `ISwitchingPathExecutor` 已移至 `Core/Abstractions/Execution/`
     - `AnomalyDetector` 已移至 `Execution/Diagnostics/`
     - `ConveyorSegment` 已移至 `Execution/Segments/`
     - `PathExecutionService` 已移至 `Execution/PathExecution/`
     - `DefaultStrategyFactory`、`DefaultSystemRunStateService` 已移至 `Execution/Infrastructure/`
     - `NodeHealthServiceExtensions` 已移至 `Execution/Extensions/`
     - 新增 ArchTest 规则确保 Execution 根目录不再堆放业务类型

2. **~~Drivers 层依赖 Execution 层~~** ✅ 已解决 (PR-TD4)
   - ~~`ZakYip.WheelDiverterSorter.Drivers.csproj` 引用了 `Execution` 项目~~
   - ~~这违反了分层架构原则，驱动层应该是底层，不应依赖执行层~~
   - ~~建议：将相关依赖移到 Core 层，或通过接口解耦~~
   - **PR-TD4 解决方案**：
     - Drivers.csproj 已移除对 Execution 的 ProjectReference
     - 所有驱动抽象接口定义在 `Core/Hardware/` (PR-C6 已从 `Core/Abstractions/Drivers/` 迁移)
     - 新增 ArchTest 规则 `Drivers_ShouldNotDependOn_Execution()` 防止倒退

3. **~~Core 层 Abstractions 目录结构与 Drivers 层重复~~** ✅ 已解决 (PR-TD4, PR-C6 进一步收敛)
   - ~~`Core/Abstractions/Drivers/` 和 `Drivers/Abstractions/` 存在重复定义~~
   - ~~部分接口通过 `global using` 别名指向 Core 层~~
   - ~~建议：统一接口定义位置，删除重复的抽象层~~
   - **PR-TD4 解决方案**：
     - `Drivers/Abstractions/` 目录已删除
     - 所有驱动抽象接口仅存在于 `Core/Abstractions/Drivers/`
     - 新增 ArchTest 规则 `Drivers_ShouldNotHaveAbstractionsDirectory()` 防止重生
   - **PR-C6 进一步收敛**：
     - `Core/Abstractions/Drivers/` 目录已删除
     - 所有硬件相关接口统一迁移到 `Core/Hardware/` 的对应子目录
     - 新增 ArchTest 规则防止创建平行硬件抽象层

### 5.2 配置相关问题

4. **LineModel/Configuration 目录文件过多**
   - 包含 50+ 文件，混合了配置模型、仓储接口、LiteDB 实现
   - 建议：拆分为 Models/、Repositories/Interfaces/、Repositories/LiteDb/ 等子目录

5. **~~存在重复的 Options 类定义~~** ✅ 已解决 (PR-TD5)
   - ~~`UpstreamConnectionOptions` 在 `Execution/Orchestration/SortingOrchestrator.cs` 中定义（仅含 FallbackTimeoutSeconds 属性）~~
   - ~~`Core/Sorting/Policies/UpstreamConnectionOptions.cs` 中定义了完整的上游连接配置类~~
   - ~~两者职责不同但命名相同，容易造成混淆~~
   - **PR-TD5 验证结果**：经代码审查确认，`UpstreamConnectionOptions` 仅存在于 `Core/Sorting/Policies/` 中，不存在重复定义。`SortingOrchestrator` 通过 `IOptions<UpstreamConnectionOptions>` 注入使用 Core 层的完整配置，其中 `FallbackTimeoutSeconds` 属性用于上游路由超时计算的降级逻辑

### 5.3 代码组织问题

6. **~~Host 层 Controllers 数量过多~~** ✅ 已解决 (PR3)
   - ~~18 个 Controller，部分功能可能可以合并~~
   - ~~`LeadshineIoDriverConfigController`、`ModiConfigController`、`ShuDiNiaoConfigController` 可考虑合并为统一的驱动配置 Controller~~
   - **PR3 解决方案**：已合并为统一的 `HardwareConfigController`，提供 `/api/hardware/leadshine`、`/api/hardware/modi`、`/api/hardware/shudiniao` 端点

7. **~~Host/Services 目录混合了多种类型~~** ✅ 已解决 (PR3)
   - ~~包含 Workers、扩展方法、业务服务、运行时配置~~
   - ~~建议：拆分为 Workers/、Extensions/、BusinessServices/ 等~~
   - **PR3 解决方案**：已拆分为 `Services/Workers/`（后台任务）、`Services/Extensions/`（DI扩展方法）、`Services/Application/`（应用服务）

8. **~~Simulation 项目既是库又是可执行程序~~** ✅ 已解决 (PR-TD6)
   - ~~`OutputType` 为 `Exe`，同时被 Host 项目引用~~
   - ~~这种设计可能导致构建和部署复杂性~~
   - **PR-TD6 解决方案**：
     - Simulation 项目的 `OutputType` 改为 `Library`
     - 新增 `ZakYip.WheelDiverterSorter.Simulation.Cli` 项目作为独立的命令行入口
     - Simulation.Cli 引用 Simulation 库，Host 只引用 Simulation 库
     - 在 `TechnicalDebtComplianceTests` 中新增 `InterfacesAndDtosShouldNotContainInlineEnums` 测试防止内联枚举

### 5.4 技术债务

9. **~~部分接口存在多层别名~~** ✅ 已解决 (PR5)
   - ~~`Drivers/Abstractions/IWheelDiverterDriver.cs` 仅包含 `global using` 指向 Core 层~~
   - ~~这种间接引用增加了理解成本~~
   - **PR5 解决方案**：删除了 Observability 层的 alias-only 文件（`ParcelFinalStatus.cs`、`AlarmLevel.cs`、`AlarmType.cs`、`AlertSeverity.cs`、`SystemClockAliases.cs`），删除了 Communication 层的 `EmcLockNotificationType.cs`，并为受影响的文件添加了显式 using 语句。

10. **~~Execution 层 Abstractions 与 Core 层 Abstractions 的职责边界不清~~** ✅ 已解决 (PR-C4)
    - ~~两层都定义了 `ISensorEventProvider`、`IUpstreamRoutingClient` 等接口~~
    - ~~建议：明确哪些接口属于核心契约（Core），哪些属于执行层特定（Execution）~~
    - **PR-C4 验证结果**：
      - 跨层核心契约（`ISensorEventProvider`、`IUpstreamRoutingClient`、`IUpstreamContractMapper`、`IIoLinkageDriver`）仅在 `Core/Abstractions/` 中定义
      - Execution 和 Drivers 中不存在重复定义
      - Execution 中的接口（如 `IPathExecutionService`、`IAnomalyDetector` 等）均为执行层特有的抽象
      - 职责边界已清晰

11. **~~缺少统一的 DI 注册中心~~** ✅ 已解决 (PR3)
    - ~~各项目都有自己的 `*ServiceExtensions.cs` 扩展方法~~
    - ~~Host 的 Program.cs 需要调用多个扩展方法来完成注册~~
    - ~~建议：考虑提供统一的 `AddWheelDiverterSorter()` 方法~~
    - **PR3 解决方案**：新增 `WheelDiverterSorterServiceCollectionExtensions.AddWheelDiverterSorter()` 方法，Program.cs 只需调用这一个方法即可完成所有服务注册
    - **PR-H1 增强**：DI 聚合逻辑下沉到 Application 层，Host 层只保留薄包装（AddWheelDiverterSorterHost）

12. **~~遗留拓扑类型待清理~~** ✅ 已解决 (PR-C3, PR-C6 位置更新)
    - ~~`Core/LineModel/Topology/Legacy/` 目录下的类型已标记为 `[Obsolete]`~~
    - ~~包括：`LineTopology`, `DiverterNodeConfig`, `ChuteConfig`, `TopologyNode`, `TopologyEdge`, `DeviceBinding`~~
    - ~~接口：`ILineTopologyService`, `IDeviceBindingService`, `IVendorIoMapper`~~
    - **PR-C3 解决方案**：
      - 删除了整个 `Core/LineModel/Topology/Legacy/` 目录
      - `IVendorIoMapper` 和 `VendorIoAddress` 迁移到 `Core/Abstractions/Drivers/`（仍在使用）
      - 删除了未使用的 `TopologyServiceExtensions.cs`
      - 新增 ArchTests 规则禁止再次创建 Legacy 目录
    - **PR-C6 位置更新**：`IVendorIoMapper` 和 `VendorIoAddress` 已从 `Core/Abstractions/Drivers/` 迁移到 `Core/Hardware/Mappings/`

### 5.5 Host 层依赖收缩（PR-H1）

13. **~~Host 层直接依赖过多下游项目~~** ✅ 已解决 (PR-H1)
    - ~~Host 项目直接引用 Execution/Drivers/Ingress/Communication/Simulation~~
    - ~~Host 层应只依赖 Application，由 Application 统一编排下游项目~~
    - **PR-H1 解决方案**：
      - Host.csproj 移除对 Execution/Drivers/Ingress/Communication/Simulation 的直接 ProjectReference
      - Host 现在只依赖 Application/Core/Observability
      - 在 Application 层创建统一 DI 入口 `AddWheelDiverterSorter()`
      - Host 层的 `AddWheelDiverterSorterHost()` 是 Application 层的薄包装
      - 更新 ArchTests 强制执行新的依赖约束

### 5.5.1 Host 层继续瘦身（PR-H2）

14. **~~Host 层包含业务接口/Commands/Repository/Adapter/Middleware~~** ✅ 已解决 (PR-H2)
    - ~~Host/Application/Services/ 目录包含重复的服务接口和实现~~
    - ~~Host/Commands/ 目录包含 ChangeParcelChuteCommand 相关类型~~
    - ~~Host/Pipeline/ 目录包含 UpstreamAssignmentAdapter~~
    - **PR-H2 解决方案**：
      - 删除 `Host/Application/` 目录，业务服务接口和实现已移至 Application 层
      - 删除 `Host/Commands/` 目录，改口命令由 Application 层的 IChangeParcelChuteService 处理
      - 删除 `Host/Pipeline/` 目录，上游适配器已移至 Execution 层
      - 更新 DivertsController 使用 IChangeParcelChuteService
      - 新增 ArchTests.HostLayerConstraintTests 强制执行：
        - 禁止接口定义（除 ISystemStateManager）
        - 禁止 Command/Repository/Adapter/Middleware 命名的类型
        - 禁止 Application/Commands/Pipeline/Repositories 目录
      - Controller 依赖约束为顾问性测试（预留后续 PR 修复）

### 5.6 文档与命名

14. **~~部分 README.md 可能过时~~** ✅ 已解决 (PR5)
    - ~~`Drivers/README.md`、`Simulation/README.md` 等需要验证是否与当前代码一致~~
    - **PR5 解决方案**：更新了 `Drivers/README.md` 和 `Simulation/README.md`，反映当前 Vendors 结构和公共 API 定义

15. **~~部分命名空间与物理路径不一致~~** ✅ 部分解决 (PR4)
    - ~~需要检查所有命名空间是否与项目/目录结构对应~~
    - **PR4 解决方案**：`Core/LineModel/Configuration` 已按 Models/Repositories/Validation 拆分，命名空间与路径一致

16. **Simulation 项目边界已明确** ✅ 已解决 (PR5)
    - **问题**：Simulation 既是独立可执行程序又被 Host 引用，边界不清晰
    - **PR5 解决方案**：在 Simulation/README.md 中明确定义了公共 API（`ISimulationScenarioRunner`、`SimulationOptions`、`SimulationSummary`）与内部实现的区分，Host 层只应使用公共 API

### 5.7 厂商配置收拢相关（PR-C2, PR-TD7）

17. **厂商配置已完全收拢到 Drivers/Vendors/** ✅ 已完成 (PR-C2, PR-TD7)
    - **PR-C2 完成**：
      - `LeadshineOptions`, `LeadshineDiverterConfigDto` 从 Drivers 根目录移动到 `Vendors/Leadshine/Configuration/`
      - `S7Options`, `S7DiverterConfigDto` 从 Drivers 根目录移动到 `Vendors/Siemens/Configuration/`
      - `LeadshineSensorOptions`, `LeadshineSensorConfigDto` 从 Ingress 移动到 `Drivers/Vendors/Leadshine/Configuration/`
      - 创建了 `SiemensS7ServiceCollectionExtensions` 统一 DI 扩展
    - **PR-TD7 完成**：
      - ~~Core 层 `LeadshineCabinetIoOptions` 仍在 `Core/LineModel/Configuration/Models/` 中~~
      - **已解决**：重命名为厂商无关的 `CabinetIoOptions`，添加 `VendorProfileKey` 字段关联厂商实现
      - 创建 `ModiOptions`（`Vendors/Modi/Configuration/`）
      - 创建 `ShuDiNiaoOptions`（`Vendors/ShuDiNiao/Configuration/`）
      - 创建 `SimulatedOptions`（`Vendors/Simulated/Configuration/`）
      - 创建 `ISensorVendorConfigProvider` 接口和 `LeadshineSensorVendorConfigProvider` 实现
      - Ingress 不再直接引用 `Drivers.Vendors.*` 命名空间，通过抽象接口获取配置

18. **Ingress 对 Drivers 解耦** ✅ 已完成 (PR-TD7, PR-C6 位置更新)
    - ~~PR-C2 为了让 Ingress 使用 Drivers 中的配置类，新增了 Ingress -> Drivers 的项目引用~~
    - **PR-TD7 解决方案**：
      - 创建 `ISensorVendorConfigProvider` 抽象接口在 Core 层
      - Ingress 通过该接口获取传感器配置，不再直接引用 `Drivers.Vendors.*` 命名空间
      - `LeadshineSensorFactory` 使用 `ISensorVendorConfigProvider` 替代直接配置引用
      - Drivers 层的 `LeadshineIoServiceCollectionExtensions` 负责注册 `ISensorVendorConfigProvider` 实现
    - **PR-C6 位置更新**：`ISensorVendorConfigProvider` 已从 `Core/Abstractions/Drivers/` 迁移到 `Core/Hardware/Providers/`

### 5.8 内联枚举待迁移（PR-C2 白名单）✅ 已解决 (PR-TD6, PR-C5)

19. **接口文件中的内联枚举** ✅ 已解决 (PR-TD6)
    - ~~`IWheelDiverterDevice.cs` 中定义了 `WheelDiverterState` 枚举~~
    - ~~`IWheelProtocolMapper.cs` 中定义了 `WheelCommandResultType`, `WheelDeviceState` 枚举~~
    - **已迁移**：所有枚举已迁移到 `Core/Enums/Hardware/` 目录：
      - `WheelDiverterState.cs`
      - `WheelCommandResultType.cs`
      - `WheelDeviceState.cs`
    - **PR-C5 补充**：已为所有枚举成员添加 `[Description]` 特性和完整的中文注释

20. **DTO 文件中的内联枚举** ✅ 已解决 (PR-TD6)
    - ~~`ChutePathTopologyDto.cs` 中定义了 `SimulationStepType`, `StepStatus` 枚举~~
    - **已迁移**：所有枚举已迁移到 `Core/Enums/Simulation/` 目录：
      - `SimulationStepType.cs`
      - `StepStatus.cs`
    - **PR-C5 补充**：已为所有枚举成员添加 `[Description]` 特性和完整的中文注释

### 5.9 HAL 层收敛与 IDiverterController 清理（PR-C6）

21. **Core/Abstractions/Drivers 双轨结构** ✅ 已解决 (PR-C6)
    - ~~Core 中存在 `Abstractions/Drivers/` 和 `Hardware/` 两个平行的硬件抽象目录~~
    - ~~部分接口在两处都有定义，职责边界不清晰~~
    - **PR-C6 解决方案**：
      - 删除 `Core/Abstractions/Drivers/` 目录
      - 所有硬件相关接口统一迁移到 `Core/Hardware/` 的对应子目录：
        - `Hardware/Ports/`: IInputPort, IOutputPort
        - `Hardware/IoLinkage/`: IIoLinkageDriver
        - `Hardware/Devices/`: IWheelDiverterDriver, IWheelDiverterDriverManager, IWheelProtocolMapper, IEmcController
        - `Hardware/Mappings/`: IVendorIoMapper, VendorIoAddress
        - `Hardware/Providers/`: ISensorVendorConfigProvider
      - 新增 ArchTest 规则防止创建平行硬件抽象层

22. **IDiverterController 中间层** ✅ 已解决 (PR-C6)
    - ~~存在 `IDiverterController` (基于角度的低级接口) 和 `IWheelDiverterDriver` (基于方向的高级接口) 两层抽象~~
    - ~~`RelayWheelDiverterDriver` 作为适配器桥接两者，增加了复杂度~~
    - **PR-C6 解决方案**：
      - 删除 `IDiverterController` 接口
      - 删除 `RelayWheelDiverterDriver` 适配器
      - 创建直接实现 `IWheelDiverterDriver` 的驱动类：
        - `LeadshineWheelDiverterDriver` (原 LeadshineDiverterController)
        - `S7WheelDiverterDriver` (原 S7DiverterController)
      - 更新 `LeadshineVendorDriverFactory` 和 `SiemensS7ServiceCollectionExtensions` 使用新驱动类

### 5.10 摆轮控制抽象去重（PR-TD9）

23. **IWheelDiverterActuator 重复抽象** ✅ 已解决 (PR-TD9)
    - ~~`IWheelDiverterActuator` 与 `IWheelDiverterDriver` 方法签名完全相同，属于重复抽象~~
    - ~~`IVendorDriverFactory` 同时暴露 `CreateWheelDiverterDrivers()` 和 `CreateWheelDiverterActuators()` 两个方法~~
    - ~~`SimulatedWheelDiverterActuator` 是唯一的 `IWheelDiverterActuator` 实现，`Leadshine` 实现返回空列表~~
    - **PR-TD9 解决方案**：
      - 删除 `Core/Hardware/IWheelDiverterActuator.cs` 接口（与 `IWheelDiverterDriver` 语义重复）
      - 删除 `Drivers/Vendors/Simulated/SimulatedWheelDiverterActuator.cs` 实现类
      - 从 `IVendorDriverFactory` 移除 `CreateWheelDiverterActuators()` 方法
      - 更新所有厂商工厂实现（`LeadshineVendorDriverFactory`、`SimulatedVendorDriverFactory`）
      - 摆轮控制统一通过 `IWheelDiverterDriver`（方向接口）或 `IWheelDiverterDevice`（命令接口）暴露
      - 新增 ArchTest 规则防止重新引入重复的摆轮控制接口

### 5.11 上游路由 Facade / Middleware 去重（PR-TD8）

24. **Ingress 层冗余 UpstreamFacade** ✅ 已解决 (PR-TD8)
    - ~~Ingress 层存在 `IUpstreamFacade`、`UpstreamFacade`、`IUpstreamChannel`、`IUpstreamCommandSender`、`HttpUpstreamChannel` 等类型~~
    - ~~这些类型虽然被定义和注册（`AddUpstreamServices`），但 `AddUpstreamServices` 从未被调用~~
    - ~~上游通信实际使用的是 Communication 层的 `IUpstreamRoutingClient`~~
    - **PR-TD8 解决方案**：
      - 删除了整个 `Ingress/Upstream/` 目录，包括：
        - `IUpstreamFacade.cs` - 冗余的上游门面接口
        - `UpstreamFacade.cs` - 冗余的上游门面实现
        - `IUpstreamChannel.cs` - 冗余的上游通道接口
        - `IUpstreamCommandSender.cs` - 冗余的命令发送器接口
        - `IUpstreamEventListener.cs` - 冗余的事件监听器接口
        - `OperationResult.cs` - 冗余的操作结果模型
        - `UpstreamServiceExtensions.cs` - 从未被调用的 DI 扩展
        - `Configuration/IngressOptions.cs` - 冗余的配置选项
        - `Http/HttpUpstreamChannel.cs` - 冗余的 HTTP 通道实现
      - 删除了对应的测试文件：
        - `Ingress.Tests/Upstream/UpstreamFacadeTests.cs`
        - `Ingress.Tests/Upstream/HttpUpstreamChannelTests.cs`
      - 上游通信统一使用 Communication 层的 `IUpstreamRoutingClient`（定义在 Core/Abstractions/Upstream/）
      - 调用链简化为：Controller/Application → ISortingOrchestrator → IUpstreamRoutingClient → 具体协议客户端

### 5.12 接口影分身合并（PR-S1）

25. **ICongestionDetector 重复接口** ✅ 已解决 (PR-S1)
    - ~~`Core/Sorting/Interfaces/ICongestionDetector.cs` 定义了 `DetectCongestionLevel(CongestionMetrics)` 方法~~
    - ~~`Core/Sorting/Runtime/ICongestionDetector.cs` 定义了 `Detect(in CongestionSnapshot)` 方法~~
    - ~~两个接口语义相同，但方法签名不同，导致存在两套实现：~~
      - ~~`ThresholdCongestionDetector` - 实现 Interfaces 版本~~
      - ~~`ThresholdBasedCongestionDetector` - 实现 Runtime 版本~~
    - **PR-S1 解决方案**：
      - 统一接口位置：`Core/Sorting/Interfaces/ICongestionDetector.cs`
      - 接口包含两个方法，支持两种输入格式：
        - `DetectCongestionLevel(CongestionMetrics metrics)` - 使用 class 输入
        - `Detect(in CongestionSnapshot snapshot)` - 使用 readonly struct 输入（高性能版本）
      - 删除了 `Core/Sorting/Runtime/ICongestionDetector.cs` 重复接口
      - 合并实现为单一类：`ThresholdCongestionDetector`
      - 删除了 `ThresholdBasedCongestionDetector` 类及其配置类 `CongestionThresholds`
      - 更新了测试文件使用统一的 `ReleaseThrottleConfiguration` 配置
      - **规则**：同一职责禁止再创建第二个平行接口

### 5.13 纯转发 Facade/Adapter 清理（PR-S2）

26. **CommunicationLoggerAdapter 纯转发适配器** ✅ 已解决 (PR-S2)
    - ~~`Communication/Infrastructure/CommunicationLoggerAdapter.cs` 是纯转发类，仅包装 `ILogger` 接口~~
    - ~~所有方法都是简单的一行转发调用，没有任何附加值~~
    - **PR-S2 解决方案**：
      - 删除 `CommunicationLoggerAdapter` 类
      - 删除 `ICommunicationLogger` 接口（位于 `ICommunicationInfrastructure.cs`）
      - 更新 `DefaultCommunicationInfrastructure` 直接使用 `ILogger`
      - 更新 `ExponentialBackoffRetryPolicy` 直接使用 `ILogger`
      - 更新 `SimpleCircuitBreaker` 直接使用 `ILogger`
      - 新增 TechnicalDebtComplianceTests 规则 `ShouldNotHavePureForwardingFacadeAdapterTypes` 检测纯转发类型

27. **Facade/Adapter 防线规则** ✅ 新增 (PR-S2)
    - 新增测试规则 `PureForwardingTypeDetectionTests.ShouldNotHavePureForwardingFacadeAdapterTypes`
    - **纯转发类型定义**（满足以下条件判定为影分身，应删除）：
      - 类型以 `*Facade` / `*Adapter` / `*Wrapper` / `*Proxy` 结尾
      - 只持有 1~2 个服务接口字段
      - 方法体只做直接调用另一个服务的方法，没有：
        - 类型转换/协议映射逻辑
        - 事件订阅/转发机制
        - 状态跟踪
        - 批量操作聚合
        - 验证或重试逻辑
    - **合法的 Adapter/Facade**（应保留）：
      - 有明确的类型转换逻辑（如 `SensorEventProviderAdapter`）
      - 有协议适配逻辑（如 `ShuDiNiaoWheelDiverterDeviceAdapter`）
      - 有状态跟踪（如 `LeadshineDiscreteIoPort`）

---

## 6. 单一权威实现 & 禁止影分身

> 本章节集中列出所有容易出现"影分身"（重复抽象）的关键概念，明确唯一的权威实现位置，防止在不同项目中出现功能重叠的平行抽象。
>
> **核心原则**：每个业务概念只允许一个权威接口/实现，发现影分身必须立即登记技术债并规划收敛。

### 6.1 单一权威实现表

| 概念 | 权威接口 / 类型 | 权威所在项目 & 目录 | 禁止出现的位置 | 测试防线 |
|------|----------------|--------------------|--------------|---------| 
| **HAL / 硬件抽象层** | `IWheelDiverterDriver`, `IWheelDiverterDevice`, `IInputPort`, `IOutputPort`, `IIoLinkageDriver`, `IVendorIoMapper`, `ISensorVendorConfigProvider`, `IEmcController` | `Core/Hardware/**` (Ports/, Devices/, IoLinkage/, Mappings/, Providers/) | ❌ `Core/Abstractions/Drivers/`（已删除）<br/>❌ `Drivers/Abstractions/`<br/>❌ `Execution/` 中定义硬件接口<br/>❌ `Host/` 中定义硬件接口 | `ArchTests.HalConsolidationTests`<br/>`DuplicateTypeDetectionTests.Core_ShouldNotHaveParallelHardwareAbstractionLayers` |
| **上游通信 / RuleEngine 客户端** | `IUpstreamRoutingClient`, `IUpstreamContractMapper` | `Core/Abstractions/Upstream/` | ❌ `Execution/` 中定义 `IRuleEngineClient` 等平行接口<br/>❌ `Communication/` 中定义平行路由接口<br/>❌ `Ingress/Upstream/`（已删除）<br/>❌ `Host/` 中定义上游通信接口 | `ArchTests.RoutingTopologyLayerTests`<br/>`TechnicalDebtComplianceTests.TopologyShadowTests` |
| **拓扑 / 路径生成** | `ISwitchingPathGenerator`, `DefaultSwitchingPathGenerator`, `SwitchingPath`, `SwitchingPathSegment` | `Core/LineModel/Topology/` | ❌ `Execution/` 中定义新的 `*PathGenerator` 接口（除装饰器外）<br/>❌ `Drivers/` 中定义路径生成逻辑<br/>❌ `Application/` 中重新实现路径生成 | `ArchTests.RoutingTopologyLayerTests`<br/>`ArchTests.TopologyPathExecutionDefenseTests`<br/>`TechnicalDebtComplianceTests.SwitchingPathGenerationTests` |
| **路径执行** | `ISwitchingPathExecutor`, `IPathExecutionService` | `Core/Abstractions/Execution/` (接口)<br/>`Execution/PathExecution/` (实现) | ❌ `Drivers/` 中定义路径执行逻辑<br/>❌ `Core/` 中包含执行实现<br/>❌ `Host/` 中直接调用硬件 | `ArchTests.ExecutionPathPipelineTests` |
| **分拣编排** | `ISortingOrchestrator`, `SortingOrchestrator` | `Core/Sorting/Orchestration/` (接口)<br/>`Execution/Orchestration/` (实现) | ❌ `Host/` 中实现分拣逻辑<br/>❌ `Application/` 中重复实现编排器<br/>❌ `Drivers/` 中包含分拣逻辑 | `TechnicalDebtComplianceTests.SortingOrchestratorComplianceTests` |
| **配置服务** | `ISystemConfigService`, `ILoggingConfigService`, `ICommunicationConfigService`, `IIoLinkageConfigService`, `IVendorConfigService` | `Application/Services/Config/` | ❌ `Host/` 中重新定义配置服务接口<br/>❌ `Core/` 中实现配置服务<br/>❌ `Execution/` 中定义配置服务 | `ArchTests.HostLayerConstraintTests`<br/>`TechnicalDebtComplianceTests.HostLayerComplianceTests` |
| **配置模型** | `SystemConfiguration`, `ChutePathTopologyConfig`, `IoLinkageConfiguration`, `CommunicationConfiguration` 等 | `Core/LineModel/Configuration/Models/` | ❌ 其他项目中定义同名配置模型<br/>❌ `Host/Models/` 中定义持久化配置（只允许 DTO）<br/>❌ `Application/` 中重复定义配置模型 | `TechnicalDebtComplianceTests.DuplicateTypeDetectionTests` |
| **配置仓储** | `ISystemConfigurationRepository`, `IChutePathTopologyRepository` 等 | `Core/LineModel/Configuration/Repositories/Interfaces/` (接口)<br/>`Core/LineModel/Configuration/Repositories/LiteDb/` (实现) | ❌ `Host/` 中定义仓储接口或实现<br/>❌ `Application/` 中定义仓储（只使用缓存装饰器）<br/>❌ `Execution/` 中定义仓储 | `ArchTests.HostLayerConstraintTests` |
| **日志 / 指标** | `IParcelLifecycleLogger`, `PrometheusMetrics`, `AlarmService`, `ISafeExecutionService` | `Observability/` | ❌ `Host/` 中重新定义日志服务<br/>❌ `Execution/` 中定义指标收集<br/>❌ `Core/` 中实现日志服务 | `TechnicalDebtComplianceTests.LoggingConfigShadowTests` |
| **系统时钟** | `ISystemClock`, `LocalSystemClock` | `Core/Utilities/` | ❌ 其他项目中定义时钟接口<br/>❌ 直接使用 `DateTime.Now` 或 `DateTime.UtcNow` | `Analyzers.DateTimeNowUsageAnalyzer`<br/>`TechnicalDebtComplianceTests.DateTimeUsageComplianceTests`<br/>`TechnicalDebtComplianceTests.SystemClockShadowTests`<br/>`TechnicalDebtComplianceTests.AnalyzersComplianceTests` |
| **仿真** | `ISimulationScenarioRunner`, `SimulationRunner`, `SimulationOptions`, `SimulationSummary` | `Simulation/` (库项目)<br/>`Simulation.Cli/` (入口项目) | ❌ `Execution/` 中包含仿真专用逻辑<br/>❌ `Host/` 中实现仿真逻辑（只通过 API 调用）<br/>❌ `Drivers/` 中的仿真驱动之外定义仿真逻辑 | `TechnicalDebtComplianceTests.SimulationShadowTests` |
| **面板 / IO 联动** | `IoLinkageConfiguration`, `CabinetIoOptions`, `IIoLinkageDriver` | `Core/LineModel/Configuration/Models/` (配置)<br/>`Core/Hardware/IoLinkage/` (接口) | ❌ `Drivers/` 中硬编码面板逻辑（应通过配置）<br/>❌ `Host/` 中直接操作 IO<br/>❌ `Execution/` 中定义 IO 配置模型 | `TechnicalDebtComplianceTests.PanelConfigShadowTests`<br/>`TechnicalDebtComplianceTests.IoShadowTests` |
| **传感器事件** | `SensorEvent`, `ParcelDetectedEventArgs`, `IParcelDetectionService` | `Ingress/Models/` (事件模型)<br/>`Ingress/` (服务接口) | ❌ `Simulation/` 中定义同名 `SensorEvent`（已重命名为 `SimulatedSensorEvent`）<br/>❌ `Execution/` 中定义传感器事件 | `TechnicalDebtComplianceTests.SimulationEventTests`<br/>`EventAndExtensionDuplicateDetectionTests` |
| **DI 聚合入口** | `AddWheelDiverterSorter()`, `WheelDiverterSorterServiceCollectionExtensions` | `Application/Extensions/` | ❌ `Host/` 中重复定义同名扩展类（已重命名为 `WheelDiverterSorterHostServiceCollectionExtensions`）<br/>❌ 其他项目中定义全局 DI 聚合 | `EventAndExtensionDuplicateDetectionTests.ServiceCollectionExtensionsShouldBeUniquePerProject` |
| **摆轮控制** | `IWheelDiverterDriver` (方向接口)<br/>`IWheelDiverterDevice` (命令接口) | `Core/Hardware/Devices/`<br/>`Core/Hardware/` | ❌ 定义 `IDiverterController`（已删除）<br/>❌ 定义 `IWheelDiverterActuator`（已删除）<br/>❌ 其他语义重叠的摆轮控制接口 | `TechnicalDebtComplianceTests.WheelDiverterShadowTests`<br/>`ArchTests.HalConsolidationTests` |
| **拥堵检测** | `ICongestionDetector`, `ThresholdCongestionDetector` | `Core/Sorting/Interfaces/` (接口)<br/>`Core/Sorting/Runtime/` (实现) | ❌ `Core/Sorting/Runtime/ICongestionDetector.cs`（已删除）<br/>❌ 定义 `ThresholdBasedCongestionDetector`（已删除）<br/>❌ 其他平行拥堵检测接口 | `TechnicalDebtComplianceTests.DuplicateTypeDetectionTests` |
| **EMC 控制** | `IEmcController`, `IEmcResourceLockManager`, `EmcLockEvent`, `EmcLockEventArgs` | `Core/Hardware/Devices/` (控制器、锁管理接口)<br/>`Core/Events/Communication/` (事件模型) | ❌ `Communication/` 中定义 EMC 接口（PR-RS11 已迁移）<br/>❌ `Execution/` 中定义 EMC 接口<br/>❌ `Host/` 中直接操作 EMC | `TechnicalDebtComplianceTests.EmcShadowTests`<br/>`ApplicationLayerDependencyTests.Drivers_ShouldNotDependOn_Execution_Or_Communication` |

### 6.2 影分身处理流程

#### 6.2.1 发现新的影分身实现时

当发现代码中存在与上表"权威实现"语义重叠的类型时，必须按以下流程处理：

1. **立即登记技术债**：
   - 在本文档 `## 5. 当前结构中已发现的问题标记` 中新增条目
   - 标明：影分身位置、权威实现位置、影响范围

2. **在 TechnicalDebtComplianceTests 中新增防线**：
   - 如果该领域尚无防线测试，必须新增测试类或测试方法
   - 测试应检测该影分身类型的存在，并输出警告或失败

3. **在"单一权威实现表"的备注列登记**：
   - 在上表对应概念行的"测试防线"列追加说明
   - 标记为"技术债（需收敛）"直到完成清理

**示例登记格式**：

```markdown
### 5.XX 新发现的影分身（PR-XXX）

XX. **XXX 重复接口** ⚠️ 技术债
    - **位置**：`SomeProject/SomeDirectory/IDuplicateInterface.cs`
    - **权威实现**：`Core/Hardware/Devices/IWheelDiverterDriver.cs`
    - **影响**：调用方需要判断使用哪个接口
    - **处理建议**：删除重复接口，调用方切换到权威实现
    - **防线测试**：`TechnicalDebtComplianceTests.XxxShadowTests`（待新增）
```

#### 6.2.2 执行收敛 PR 时

当提交清理影分身的 PR 时，必须：

1. **更新本表**：
   - 确认权威实现位置正确
   - 从"禁止出现的位置"列确认已删除所有影分身
   - 更新"测试防线"列，确认防线测试已启用

2. **PR 描述必须包含**：
   - **被保留实现**：列出权威接口/类型的完整路径
   - **被删除实现**：列出所有被清理的影分身类型
   - **调用方变更**：列出受影响的调用方及其修改方式

3. **同步更新 `copilot-instructions.md`**：
   - 如果收敛涉及新的结构约束，需在编码规范中体现

**示例 PR 描述格式**：

```markdown
## PR-XXX: 清理 XXX 影分身

### 被保留实现（权威）
- `Core/Hardware/Devices/IWheelDiverterDriver.cs`

### 被删除实现（影分身）
- `Execution/Abstractions/IDiverterController.cs`
- `Drivers/Adapters/RelayWheelDiverterDriver.cs`

### 调用方变更
- `Execution/Orchestration/SortingOrchestrator.cs`：从 `IDiverterController` 切换到 `IWheelDiverterDriver`
- `Drivers/Vendors/Leadshine/LeadshineVendorDriverFactory.cs`：直接实现 `IWheelDiverterDriver`

### 新增/更新防线测试
- `ArchTests.HalConsolidationTests.ShouldNotHaveDuplicateDiverterInterface()`
```

### 6.3 快速查阅指南

当需要确定某个概念"应该放在哪里"时，按以下顺序查阅：

1. **首先查本表**：在 6.1 单一权威实现表中查找对应概念的"权威所在项目 & 目录"列
2. **然后查 copilot-instructions.md**：获取更详细的编码规范和约束说明
3. **最后查各项目的内部结构（第 3 节）**：了解具体的目录组织

**常见问题快速定位**：

| 我想要... | 查找位置 |
|----------|---------|
| 定义新的硬件接口 | → 6.1 表格 "HAL / 硬件抽象层" 行 → `Core/Hardware/**` |
| 定义新的配置模型 | → 6.1 表格 "配置模型" 行 → `Core/LineModel/Configuration/Models/` |
| 添加新的上游协议支持 | → 6.1 表格 "上游通信" 行 → 实现 `IUpstreamRoutingClient` → `Communication/Clients/` |
| 添加新的路径生成策略 | → 6.1 表格 "拓扑 / 路径生成" 行 → 实现 `ISwitchingPathGenerator` 或使用装饰器 |
| 添加新的配置服务 | → 6.1 表格 "配置服务" 行 → `Application/Services/Config/` |
| 添加新的日志/指标 | → 6.1 表格 "日志 / 指标" 行 → `Observability/` |
| 添加新的厂商驱动 | → 第 3.5 节 Drivers 结构 → `Drivers/Vendors/<VendorName>/` |

---

## 附录：目录树生成命令

本文档的目录树使用以下命令生成并手工整理：

```bash
# 生成项目目录树（深度 3 层）
find src/Host/ZakYip.WheelDiverterSorter.Host -type f -name "*.cs" | head -50

# 列出项目依赖
grep -r "ProjectReference" src/**/*.csproj
```

---

### 5.14 DTO/Options/Utilities 统一规范（PR-S3）

28. **DTO/Model/Response 类型统一命名规则** ✅ 新增 (PR-S3)
    - **问题**：相同业务概念在多个项目中存在多个字段相同的 DTO/Model/Response 类型
    - **统一命名规则**：
      - `*Configuration`: 持久化配置模型（存储在 LiteDB），位于 `Core/LineModel/Configuration/Models/`
      - `*Options`: 运行时配置选项（通过 IOptions<T> 注入），位于各项目的 `Configuration/` 目录
      - `*Request`: API 请求模型，位于 `Host/Models/` 或 `Host/Models/Config/`
      - `*Response`: API 响应模型，位于 `Host/Models/` 或 `Host/Models/Config/`
      - `*Dto`: 跨层数据传输对象（仅在必要时使用）
    - **已清理的重复类型**：
      - 删除 `Ingress/Configuration/SensorConfiguration.cs`（未使用，与 Core 层 SensorConfiguration 重复）
    - **已知的同名类型**（有明确职责区分）：
      - `OperationResult` (Core/Results/) - 完整的操作结果类型，带 ErrorCode 支持
      - `OperationResult` (Core/LineModel/Routing/) - 简化的内部操作结果类型（PR-S5 重命名为 RouteComputationResult）

### 5.15 事件 & DI 扩展影分身清理（PR-S6）

32. **事件类型跨层重名清理** ✅ 新增 (PR-S6)
    - **问题**：`SensorEvent` 同时存在于 Ingress/Models/ 和 Simulation/Services/，IDE 搜索时需要凭感觉判断
    - **解决方案**：
      - 保留 `Ingress/Models/SensorEvent` 为现实世界传感器事件模型
      - 将仿真侧 `SensorEvent` 重命名为 `SimulatedSensorEvent`
      - 文件移动到 `Simulation/Models/SimulatedSensorEvent.cs`
    - **防线测试**：`EventAndExtensionDuplicateDetectionTests.EventTypesShouldNotBeDuplicatedAcrossLayers()`

33. **DI 扩展类跨项目重名清理** ✅ 新增 (PR-S6)
    - **问题**：`WheelDiverterSorterServiceCollectionExtensions` 同时存在于 Application 和 Host 层
    - **解决方案**：
      - 保留 `Application/Extensions/WheelDiverterSorterServiceCollectionExtensions` 为唯一 DI 聚合入口
      - 将 Host 层扩展类重命名为 `WheelDiverterSorterHostServiceCollectionExtensions`
      - 文件位于 `Host/Services/Extensions/WheelDiverterSorterHostServiceCollectionExtensions.cs`
    - **防线测试**：`EventAndExtensionDuplicateDetectionTests.ServiceCollectionExtensionsShouldBeUniquePerProject()`

29. **Utilities 目录位置规范** ✅ 新增 (PR-S3)
    - **允许的 Utilities 目录位置**：
      - `Core/Utilities/` - 公共工具类（如 ISystemClock）
      - `Core/LineModel/Utilities/` - LineModel 内部工具类（使用 file-scoped class）
      - `Observability/Utilities/` - 可观测性相关工具类
    - **禁止在其他项目中新增 Utilities 目录**
    - **项目特定工具应使用 `file static class`** 保持文件作用域
    - **防线测试**：`DuplicateTypeDetectionTests.UtilitiesDirectoriesShouldFollowConventions()`

30. **未使用类型检测** ✅ 新增 (PR-S3)
    - **测试**：`DuplicateTypeDetectionTests.ShouldNotHaveUnusedDtoOrOptionsTypes()`
    - **检测范围**：以 `Dto`, `Options`, `Configuration`, `Config` 结尾的类型
    - **注意**：Options 类型可能通过 IOptions<T> 隐式绑定，检测结果为顾问性

31. **同名不同命名空间类型检测** ✅ 新增 (PR-S3)
    - **测试**：`DuplicateTypeDetectionTests.ShouldNotHaveDuplicateTypeNameAcrossNamespaces()`
    - **检测范围**：同名类型在不同命名空间中的定义
    - **输出**：顾问性报告，需人工确认是否为真正的重复

### 5.16 配置模型瘦身（PR-SD5）

34. **删除仅测试使用的配置模型** ✅ 新增 (PR-SD5)
    - **问题**：Core/LineModel/Configuration/Models 中存在仅被测试使用的配置模型
    - **已删除的模型**：
      - `IoPointConfiguration.cs` - 统一的 IO 点配置模型（无生产代码使用）
      - `LineSegmentConfig.cs` - 线体段配置（无生产代码使用，仅在文档注释中引用）
      - `PanelIoOptions.cs` - 面板 IO 配置选项（无任何使用）
      - `SignalTowerOptions.cs` - 信号塔配置选项（无任何使用）
    - **已删除的测试文件**：
      - `tests/ZakYip.WheelDiverterSorter.Core.Tests/IoPointConfigurationTests.cs`
      - `tests/ZakYip.WheelDiverterSorter.Core.Tests/LineModel/LineSegmentConfigTests.cs`
    - **更新的注释引用**：
      - `ChutePathTopologyConfig.cs` - 移除了对 LineSegmentConfig 的文档引用
      - `ChutePathTopologyController.cs` - 移除了对 LineSegmentConfig 的文档引用
    - **配置模型数量变化**：从 26 个减少到 22 个
    - **防线测试**：`DuplicateTypeDetectionTests.ConfigurationModelsShouldHaveProductionUsage()`
      - 验证配置模型在生产代码中有实际使用
      - 允许被其他已使用配置模型引用的 helper types

---

**文档版本**：3.1 (PR-RS11)  
**最后更新**：2025-12-01  
**维护团队**：ZakYip Development Team
