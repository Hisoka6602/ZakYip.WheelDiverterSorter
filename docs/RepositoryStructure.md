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
| 仿真层 | ZakYip.WheelDiverterSorter.Simulation | src/Simulation/ |
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
├── ZakYip.WheelDiverterSorter.Application
├── ZakYip.WheelDiverterSorter.Core
├── ZakYip.WheelDiverterSorter.Execution
├── ZakYip.WheelDiverterSorter.Drivers
├── ZakYip.WheelDiverterSorter.Ingress
├── ZakYip.WheelDiverterSorter.Observability
├── ZakYip.WheelDiverterSorter.Communication
└── ZakYip.WheelDiverterSorter.Simulation

ZakYip.WheelDiverterSorter.Application
├── ZakYip.WheelDiverterSorter.Core
├── ZakYip.WheelDiverterSorter.Execution
├── ZakYip.WheelDiverterSorter.Communication
└── ZakYip.WheelDiverterSorter.Observability

ZakYip.WheelDiverterSorter.Execution
├── ZakYip.WheelDiverterSorter.Core
└── ZakYip.WheelDiverterSorter.Observability

ZakYip.WheelDiverterSorter.Drivers
├── ZakYip.WheelDiverterSorter.Core
├── ZakYip.WheelDiverterSorter.Execution
└── ZakYip.WheelDiverterSorter.Communication

ZakYip.WheelDiverterSorter.Ingress
└── ZakYip.WheelDiverterSorter.Core

ZakYip.WheelDiverterSorter.Observability
└── ZakYip.WheelDiverterSorter.Core

ZakYip.WheelDiverterSorter.Communication
├── ZakYip.WheelDiverterSorter.Core
└── ZakYip.WheelDiverterSorter.Observability

ZakYip.WheelDiverterSorter.Simulation
├── ZakYip.WheelDiverterSorter.Core
├── ZakYip.WheelDiverterSorter.Execution
├── ZakYip.WheelDiverterSorter.Drivers
├── ZakYip.WheelDiverterSorter.Ingress
└── ZakYip.WheelDiverterSorter.Observability

ZakYip.WheelDiverterSorter.Analyzers
└── (无项目依赖，仅依赖 Microsoft.CodeAnalysis)

ZakYip.WheelDiverterSorter.Tools.Reporting
└── ZakYip.WheelDiverterSorter.Core

ZakYip.WheelDiverterSorter.Tools.SafeExecutionStats
└── (无项目依赖)
```

**依赖层次说明**：

- **Core** 是最底层，不依赖其他业务项目，定义核心抽象和领域模型
- **Observability** 依赖 Core，提供监控、日志、告警等基础设施
- **Ingress** 依赖 Core，处理传感器和包裹检测
- **Communication** 依赖 Core 和 Observability，负责与上游 RuleEngine 的通信
- **Execution** 依赖 Core 和 Observability，负责分拣编排和路径执行
- **Drivers** 依赖 Core、Execution 和 Communication，实现具体硬件驱动
- **Application** 依赖 Core、Execution、Communication 和 Observability，提供应用服务/用例服务
- **Simulation** 依赖除 Host 和 Application 外的所有项目，提供仿真运行环境
- **Host** 是顶层应用入口，依赖 Application 和所有业务项目

### 2.1 层级架构约束（Architecture Constraints）

根据 `copilot-instructions.md` 规范，项目依赖必须遵循以下严格约束，由 `ArchTests` 项目中的 `ApplicationLayerDependencyTests` 强制执行：

#### Host 层约束
- **允许依赖**：Application、Core、Observability
- **禁止越级访问**：不能直接依赖 Execution/Drivers/Core 中的业务接口

#### Application 层约束
- **允许依赖**：Core、Execution、Drivers、Ingress、Communication、Observability
- **禁止依赖**：Host、Simulation、Analyzers

#### 反向依赖禁止
以下项目 **禁止** 依赖 Application（避免循环依赖）：
- Core
- Execution
- Drivers
- Ingress
- Communication
- Observability
- Simulation

#### 预期依赖链路
```
Host → Application → Core/Execution/Drivers/Ingress/Communication/Observability
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

---

## 3. 各项目内部结构

### 3.1 ZakYip.WheelDiverterSorter.Application

**项目职责**：应用服务层，封装 Core + Execution + Drivers + Ingress + Communication 的组合逻辑，提供应用服务/用例服务。Host 层通过引用此项目获取所有应用服务。

```
ZakYip.WheelDiverterSorter.Application/
├── Services/                           # 应用服务实现
│   ├── CachedDriverConfigurationRepository.cs    # 带缓存的IO驱动器配置仓储
│   ├── CachedSensorConfigurationRepository.cs    # 带缓存的感应IO配置仓储
│   ├── CachedSwitchingPathGenerator.cs           # 带缓存的路径生成器
│   ├── ChangeParcelChuteService.cs               # 改口服务实现（PR-A2新增）
│   ├── CommunicationConfigService.cs             # 通信配置服务实现（PR-A2新增）
│   ├── CommunicationStatsService.cs              # 通信统计服务
│   ├── CongestionDataCollector.cs                # 拥堵数据收集器
│   ├── DebugSortService.cs                       # 调试分拣服务实现（PR-A2新增）
│   ├── IChangeParcelChuteService.cs              # 改口服务接口（PR-A2新增）
│   ├── ICommunicationConfigService.cs            # 通信配置服务接口（PR-A2新增）
│   ├── IDebugSortService.cs                      # 调试分拣服务接口（PR-A2新增）
│   ├── IIoLinkageConfigService.cs                # IO联动配置服务接口（PR-A2新增）
│   ├── ILoggingConfigService.cs                  # 日志配置服务接口
│   ├── IPreRunHealthCheckService.cs              # 运行前健康检查服务接口
│   ├── ISimulationOrchestratorService.cs         # 仿真编排服务接口
│   ├── ISystemConfigService.cs                   # 系统配置服务接口
│   ├── InMemoryRoutePlanRepository.cs            # 内存路由计划仓储
│   ├── IoLinkageConfigService.cs                 # IO联动配置服务实现（PR-A2新增）
│   ├── LoggingConfigService.cs                   # 日志配置服务实现
│   ├── OptimizedSortingService.cs                # 性能优化的分拣服务
│   ├── PreRunHealthCheckService.cs               # 运行前健康检查服务实现
│   ├── SimulationModeProvider.cs                 # 仿真模式提供者
│   ├── SorterMetrics.cs                          # 分拣系统性能指标服务
│   └── SystemConfigService.cs                    # 系统配置服务实现
└── ApplicationServiceExtensions.cs     # DI 扩展方法 (AddWheelDiverterApplication)
```

#### 关键类型概览

- `ISystemConfigService`/`SystemConfigService`：系统配置的业务逻辑，包括验证、更新、默认模板生成
- `ILoggingConfigService`/`LoggingConfigService`：日志配置的查询、更新、重置操作
- `IPreRunHealthCheckService`/`PreRunHealthCheckService`：运行前验证所有关键配置是否就绪
- `ICommunicationConfigService`/`CommunicationConfigService`：通信配置的业务逻辑，包括连接测试、热更新（PR-A2新增）
- `IIoLinkageConfigService`/`IoLinkageConfigService`：IO联动配置的业务逻辑，包括IO点操作（PR-A2新增）
- `IDebugSortService`/`DebugSortService`：调试分拣服务，用于测试分拣流程（PR-A2新增）
- `IChangeParcelChuteService`/`ChangeParcelChuteService`：改口服务，处理包裹目标格口变更请求（PR-A2新增）
- `ISimulationModeProvider`/`SimulationModeProvider`：判断系统当前是否运行在仿真模式下
- `SorterMetrics`：分拣系统性能指标，包括计数器、直方图等
- `OptimizedSortingService`：集成了指标收集、对象池和优化内存管理的分拣服务
- `CachedSwitchingPathGenerator`：带缓存优化的路径生成器包装器
- `CongestionDataCollector`：收集系统当前拥堵指标快照
- `ApplicationServiceExtensions`：提供 `AddWheelDiverterApplication()` 统一注册所有应用服务

### 3.2 ZakYip.WheelDiverterSorter.Host

**项目职责**：Web API 主机入口，负责 DI 容器配置、API Controller 定义、启动引导和 Swagger 文档生成。不包含业务逻辑，业务逻辑委托给 Execution、Core 等底层项目。

```
ZakYip.WheelDiverterSorter.Host/
├── Application/
│   └── Services/                    # 应用层服务（配置缓存、系统配置服务等）
├── Commands/                        # CQRS 命令定义与处理器
├── Controllers/                     # API 控制器（16个，PR3合并后）
│   ├── AlarmsController.cs
│   ├── ChuteAssignmentTimeoutController.cs
│   ├── ChutePathTopologyController.cs
│   ├── CommunicationController.cs
│   ├── DivertsController.cs
│   ├── HealthController.cs
│   ├── HardwareConfigController.cs  # PR3: 统一硬件配置控制器（合并雷赛/莫迪/数递鸟）
│   ├── IoLinkageController.cs
│   ├── LoggingConfigController.cs
│   ├── PanelConfigController.cs
│   ├── PolicyController.cs
│   ├── SimulationConfigController.cs
│   ├── SimulationController.cs
│   ├── SystemConfigController.cs
│   ├── SystemOperationsController.cs
│   └── ApiControllerBase.cs
├── Health/                          # 健康检查提供者
├── Models/                          # API 请求/响应 DTO
│   ├── Communication/
│   ├── Config/
│   └── Panel/
├── Pipeline/                        # HTTP 管道中间件（上游分配适配器）
├── Services/                        # PR3/PR-A2: 重组为按类型分类的子目录，应用服务已移至 Application 层
│   ├── Extensions/                  # DI 扩展方法
│   │   ├── ConfigurationRepositoryServiceExtensions.cs
│   │   ├── HealthCheckServiceExtensions.cs
│   │   ├── MiddleConveyorServiceExtensions.cs
│   │   ├── RuntimeProfileServiceExtensions.cs
│   │   ├── SimulationServiceExtensions.cs
│   │   ├── SortingServiceExtensions.cs
│   │   ├── SystemStateServiceExtensions.cs
│   │   ├── TopologyServiceExtensions.cs
│   │   └── WheelDiverterSorterServiceCollectionExtensions.cs  # PR3: 统一DI入口
│   ├── RuntimeProfiles/             # 运行时配置文件
│   │   ├── ProductionRuntimeProfile.cs
│   │   ├── SimulationRuntimeProfile.cs
│   │   └── PerformanceTestRuntimeProfile.cs
│   └── Workers/                     # 后台工作服务
│       ├── AlarmMonitoringWorker.cs
│       ├── BootHostedService.cs
│       └── RouteTopologyConsistencyCheckWorker.cs
├── StateMachine/                    # 系统状态机（启动/运行/停止）
├── Swagger/                         # Swagger 配置与过滤器
├── Program.cs                       # 应用入口点（PR3: 简化为单一 AddWheelDiverterSorter() 调用）
├── appsettings.json                 # 配置文件
├── nlog.config                      # NLog 日志配置
└── Dockerfile                       # Docker 构建文件
```

#### 关键类型概览

- `Program.cs`：应用启动入口，通过 `AddWheelDiverterSorter()` 单一入口配置所有服务
- `SystemStateManager`（位于 StateMachine/）：管理系统启动/运行/停止状态转换
- `BootHostedService`（位于 Services/Workers/）：系统启动引导服务，按顺序初始化各子系统
- `ApiControllerBase`（位于 Controllers/）：所有 API 控制器的基类，提供统一响应格式
- `HardwareConfigController`（位于 Controllers/）：统一硬件配置控制器，提供 /api/hardware/leadshine、/api/hardware/modi、/api/hardware/shudiniao 端点
- `WheelDiverterSorterServiceCollectionExtensions`（位于 Services/Extensions/）：统一 DI 入口，提供 `AddWheelDiverterSorter()` 方法

**注意**：PR-A2 将原 Host/Services/Application 目录下的服务（OptimizedSortingService、SorterMetrics、DebugSortService 等）统一移至 Application 层。Host 层不再包含应用服务实现，只负责 DI 配置和 API Controller 定义。

---

### 3.3 ZakYip.WheelDiverterSorter.Core

**项目职责**：定义核心领域模型、抽象接口和业务规则。是整个解决方案的基础层，不依赖任何其他业务项目。

```
ZakYip.WheelDiverterSorter.Core/
├── Abstractions/
│   ├── Drivers/                     # 驱动层抽象接口
│   │   ├── IWheelDiverterDriver.cs
│   │   ├── IDiverterController.cs
│   │   ├── IInputPort.cs
│   │   ├── IOutputPort.cs
│   │   ├── IIoLinkageDriver.cs
│   │   └── ...
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
│   ├── Hardware/
│   ├── Monitoring/
│   ├── Parcel/
│   ├── Sorting/
│   └── System/
├── Hardware/                        # 硬件设备抽象接口
│   ├── IWheelDiverterActuator.cs
│   ├── IWheelDiverterDevice.cs
│   ├── IConveyorDriveController.cs
│   ├── ISensorInputReader.cs
│   └── ...
├── IoBinding/                       # IO 绑定模型
│   ├── IoBindingProfile.cs
│   ├── SensorBinding.cs
│   └── ActuatorBinding.cs
├── LineModel/                       # 线体模型（核心领域）
│   ├── Bindings/
│   ├── Chutes/                      # 格口相关
│   ├── Configuration/               # 配置模型与仓储（PR4 重构后）
│   │   ├── Models/                  # 纯配置模型类（26个文件）
│   │   │   ├── SystemConfiguration.cs
│   │   │   ├── ChutePathTopologyConfig.cs
│   │   │   ├── IoLinkageConfiguration.cs
│   │   │   ├── CommunicationConfiguration.cs
│   │   │   ├── LoggingConfiguration.cs
│   │   │   └── ...
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
│   │   └── Legacy/                  # 遗留拓扑类型（PR4 迁移）
│   │       ├── LineTopology.cs      # [Obsolete] 遗留线体拓扑
│   │       ├── DiverterNodeConfig.cs# [Obsolete] 遗留摆轮节点配置
│   │       ├── ChuteConfig.cs       # [Obsolete] 遗留格口配置
│   │       ├── TopologyNode.cs      # [Obsolete] 遗留拓扑节点
│   │       ├── TopologyEdge.cs      # [Obsolete] 遗留拓扑边
│   │       ├── DeviceBinding.cs     # [Obsolete] 遗留设备绑定
│   │       ├── ILineTopologyService.cs  # [Obsolete] 遗留拓扑服务接口
│   │       ├── IDeviceBindingService.cs # [Obsolete] 遗留设备绑定接口
│   │       ├── IVendorIoMapper.cs   # [Obsolete] 遗留厂商IO映射接口
│   │       └── Services/
│   │           ├── JsonLineTopologyService.cs   # [Obsolete]
│   │           └── JsonDeviceBindingService.cs  # [Obsolete]
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
└── Utilities/                       # 工具类
    ├── ISystemClock.cs
    └── LocalSystemClock.cs
```

#### 关键类型概览

- `ISortingOrchestrator`（位于 Sorting/Orchestration/）：分拣编排服务接口，定义核心业务流程入口
- `ISwitchingPathGenerator`（位于 LineModel/Topology/）：摆轮路径生成器接口，根据目标格口生成摆轮指令序列
- `IWheelDiverterDriver`（位于 Abstractions/Drivers/）：摆轮驱动器抽象接口，定义左转/右转/直通操作
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
├── Abstractions/                    # 执行层抽象接口
│   ├── ICongestionDataCollector.cs
│   ├── ISensorEventProvider.cs
│   ├── IUpstreamContractMapper.cs
│   ├── IUpstreamRoutingClient.cs
│   └── IWheelProtocolMapper.cs
├── Concurrency/                     # 并发控制
│   ├── ConcurrentSwitchingPathExecutor.cs
│   ├── DiverterResourceLockManager.cs
│   ├── MonitoredParcelQueue.cs
│   ├── PriorityParcelQueue.cs
│   └── ...
├── Events/                          # 执行事件
│   ├── PathExecutionFailedEventArgs.cs
│   └── PathSwitchedEventArgs.cs
├── Health/                          # 健康监控
│   ├── NodeHealthMonitorService.cs
│   ├── NodeHealthRegistry.cs
│   └── PathHealthChecker.cs
├── Orchestration/                   # 核心编排实现
│   ├── SortingOrchestrator.cs       # 分拣编排器主实现
│   └── SortingExceptionHandler.cs
├── Pipeline/                        # 分拣管道中间件
│   └── Middlewares/
│       ├── OverloadEvaluationMiddleware.cs
│       ├── PathExecutionMiddleware.cs
│       ├── RoutePlanningMiddleware.cs
│       ├── TracingMiddleware.cs
│       └── UpstreamAssignmentMiddleware.cs
├── SelfTest/                        # 自检功能
│   ├── SystemSelfTestCoordinator.cs
│   └── DefaultConfigValidator.cs
├── Strategy/                        # 格口选择策略实现
│   ├── CompositeChuteSelectionService.cs
│   ├── FixedChuteSelectionStrategy.cs
│   ├── FormalChuteSelectionStrategy.cs
│   └── RoundRobinChuteSelectionStrategy.cs
├── （根目录文件，约 25 个）         # 见问题标记 5.1.1
│   ├── ISwitchingPathExecutor.cs    # 路径执行器接口
│   ├── PathExecutionService.cs      # 路径执行服务
│   ├── AnomalyDetector.cs           # 异常检测器
│   ├── ConveyorSegment.cs           # 输送段模型
│   ├── DefaultStrategyFactory.cs
│   ├── DefaultSystemRunStateService.cs
│   └── ...
```

#### 关键类型概览

- `SortingOrchestrator`（位于 Orchestration/）：分拣编排器核心实现，协调整个分拣流程
- `ISwitchingPathExecutor`：摆轮路径执行器接口，按段顺序执行摆轮切换
- `PathExecutionService`：路径执行服务实现，处理路径执行细节
- `ConcurrentSwitchingPathExecutor`（位于 Concurrency/）：支持并发的路径执行器
- `DiverterResourceLockManager`（位于 Concurrency/）：摆轮资源锁管理器，防止并发冲突
- `PathHealthChecker`（位于 Health/）：路径健康检查器，执行前验证路径可用性
- `SortingPipeline`（位于 Pipeline/）：分拣管道，串联各中间件处理步骤

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
│   │   │   └── LeadshineSensorConfigDto.cs  # 传感器配置DTO
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
│   │   └── LeadshineIoServiceCollectionExtensions.cs  # DI 扩展
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
│   │   ├── Configuration/           # 摩迪配置类（如有需要）
│   │   ├── ModiProtocol.cs
│   │   ├── ModiProtocolEnums.cs
│   │   ├── ModiWheelDiverterDriver.cs
│   │   ├── ModiSimulatedDevice.cs
│   │   └── ModiWheelServiceCollectionExtensions.cs    # DI 扩展
│   ├── ShuDiNiao/                   # 书迪鸟摆轮协议驱动
│   │   ├── Configuration/           # 书迪鸟配置类（如有需要）
│   │   ├── ShuDiNiaoProtocol.cs
│   │   ├── ShuDiNiaoProtocolEnums.cs
│   │   ├── ShuDiNiaoWheelDiverterDriver.cs
│   │   ├── ShuDiNiaoWheelDiverterDriverManager.cs
│   │   ├── ShuDiNiaoSimulatedDevice.cs
│   │   └── ShuDiNiaoWheelServiceCollectionExtensions.cs # DI 扩展
│   └── Simulated/                   # 仿真驱动实现
│       ├── Configuration/           # 仿真配置类（如有需要）
│       ├── IoMapping/
│       │   └── SimulatedIoMapper.cs
│       ├── SimulatedWheelDiverterDevice.cs
│       ├── SimulatedWheelDiverterActuator.cs
│       ├── SimulatedConveyorSegmentDriver.cs
│       ├── SimulatedIoLinkageDriver.cs
│       ├── SimulatedVendorDriverFactory.cs
│       └── SimulatedDriverServiceCollectionExtensions.cs # DI 扩展
├── FactoryBasedDriverManager.cs     # 工厂模式驱动管理器
├── HardwareSwitchingPathExecutor.cs # 硬件路径执行器
├── WheelCommandExecutor.cs          # 摆轮命令执行器
├── IoLinkageExecutor.cs             # IO 联动执行器
├── DriverServiceExtensions.cs       # 通用 DI 扩展方法（已弃用，推荐使用厂商特定扩展）
└── DriverOptions.cs                 # 驱动配置选项
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

**项目职责**：入口层，负责传感器事件监听、包裹检测、上游通信门面封装。

**注意**：厂商相关配置类已移动到 `Drivers/Vendors/<VendorName>/Configuration/`，Ingress 项目通过引用 Drivers 项目使用这些配置。

```
ZakYip.WheelDiverterSorter.Ingress/
├── Adapters/                        # 适配器
│   └── SensorEventProviderAdapter.cs
├── Configuration/                   # 传感器配置（通用配置）
│   ├── SensorConfiguration.cs
│   ├── SensorOptions.cs             # 引用 Drivers 中的厂商配置
│   ├── MockSensorConfigDto.cs
│   └── ParcelDetectionOptions.cs
├── Models/                          # 入口层模型
│   ├── ParcelDetectedEventArgs.cs
│   ├── SensorEvent.cs
│   ├── SensorHealthStatus.cs
│   └── ...
├── Sensors/                         # 传感器实现
│   ├── LeadshineSensor.cs           # 使用 Drivers.Vendors.Leadshine.Configuration
│   ├── LeadshineSensorFactory.cs    # 使用 Drivers.Vendors.Leadshine.Configuration
│   ├── MockSensor.cs
│   └── MockSensorFactory.cs
├── Services/                        # 服务实现
│   ├── ParcelDetectionService.cs
│   └── SensorHealthMonitor.cs
├── Upstream/                        # 上游通信门面
│   ├── Configuration/
│   │   └── IngressOptions.cs
│   ├── Http/
│   │   └── HttpUpstreamChannel.cs
│   ├── IUpstreamFacade.cs
│   ├── UpstreamFacade.cs
│   └── UpstreamServiceExtensions.cs
├── IParcelDetectionService.cs       # 包裹检测服务接口
├── ISensor.cs                       # 传感器接口
├── ISensorFactory.cs                # 传感器工厂接口
└── SensorServiceExtensions.cs       # DI 扩展方法
```

#### 关键类型概览

- `IParcelDetectionService`：包裹检测服务接口，监听传感器事件并触发 ParcelDetected 事件
- `ParcelDetectionService`（位于 Services/）：包裹检测服务实现
- `ISensor`：传感器抽象接口
- `LeadshineSensor`（位于 Sensors/）：雷赛传感器实现
- `IUpstreamFacade`（位于 Upstream/）：上游通信门面接口
- `SensorHealthMonitor`（位于 Services/）：传感器健康监控服务

---

### 3.7 ZakYip.WheelDiverterSorter.Communication

**项目职责**：通信基础设施层，实现与上游 RuleEngine 的多协议通信（TCP/SignalR/MQTT/HTTP），支持客户端和服务器两种模式。

```
ZakYip.WheelDiverterSorter.Communication/
├── Abstractions/                    # 通信抽象接口
│   ├── IRuleEngineClient.cs
│   ├── IRuleEngineServer.cs
│   ├── IRuleEngineClientFactory.cs
│   ├── IRuleEngineHandler.cs
│   ├── IUpstreamConnectionManager.cs
│   └── IEmcResourceLockManager.cs
├── Adapters/                        # 适配器
│   ├── DefaultUpstreamContractMapper.cs
│   └── UpstreamRoutingClientAdapter.cs
├── Clients/                         # 客户端实现
│   ├── TcpRuleEngineClient.cs
│   ├── SignalRRuleEngineClient.cs
│   ├── MqttRuleEngineClient.cs
│   ├── HttpRuleEngineClient.cs
│   ├── InMemoryRuleEngineClient.cs
│   ├── RuleEngineClientBase.cs
│   └── EmcResourceLockManager*.cs
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
│   ├── ParcelDetectionNotification.cs
│   └── EmcLockEvent.cs
├── Servers/                         # 服务器实现
│   ├── TcpRuleEngineServer.cs
│   ├── SignalRRuleEngineServer.cs
│   └── MqttRuleEngineServer.cs
├── RuleEngineClientFactory.cs       # 客户端工厂
├── RuleEngineServerFactory.cs       # 服务器工厂
└── CommunicationServiceExtensions.cs # DI 扩展方法
```

#### 关键类型概览

- `IRuleEngineClient`（位于 Abstractions/）：规则引擎客户端接口，定义连接、断开、通知包裹到达等操作
- `TcpRuleEngineClient`（位于 Clients/）：TCP 协议客户端实现
- `SignalRRuleEngineClient`（位于 Clients/）：SignalR 协议客户端实现
- `MqttRuleEngineClient`（位于 Clients/）：MQTT 协议客户端实现
- `RuleEngineClientFactory`：根据配置创建对应协议的客户端实例
- `UpstreamRoutingClientAdapter`（位于 Adapters/）：将 IRuleEngineClient 适配为 IUpstreamRoutingClient
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

**项目职责**：仿真运行器，提供完整的分拣系统仿真环境，支持场景定义、策略实验、性能测试等。

```
ZakYip.WheelDiverterSorter.Simulation/
├── Configuration/                   # 仿真配置
│   ├── SimulationOptions.cs
│   ├── DenseParcelStrategy.cs
│   ├── SensorFaultOptions.cs
│   └── ...
├── Results/                         # 仿真结果模型
├── Scenarios/                       # 场景定义
│   ├── SimulationScenario.cs
│   ├── ScenarioDefinitions.cs
│   ├── ChaosScenarioDefinitions.cs
│   └── ParcelExpectation.cs
├── Services/                        # 仿真服务
│   ├── SimulationRunner.cs          # 仿真运行器
│   ├── SimulationScenarioRunner.cs  # 场景运行器
│   ├── CapacityTestingRunner.cs     # 容量测试运行器
│   └── SimulationReportPrinter.cs
├── Strategies/                      # 策略实验
│   ├── StrategyExperimentRunner.cs
│   ├── StrategyExperimentConfig.cs
│   └── Reports/
├── Program.cs                       # 仿真主入口（独立可执行）
├── appsettings.Simulation.json      # 仿真配置文件
├── appsettings.LongRun.json         # 长时运行配置
├── simulation-config/               # 仿真拓扑配置
└── reports/                         # 报告输出目录
```

#### 关键类型概览

- `SimulationRunner`（位于 Services/）：仿真主运行器，协调仿真流程
- `SimulationScenarioRunner`（位于 Services/）：场景运行器，执行具体的仿真场景
- `SimulationScenario`（位于 Scenarios/）：仿真场景定义，包含包裹序列、期望结果等
- `StrategyExperimentRunner`（位于 Strategies/）：策略实验运行器，用于 A/B 测试不同策略
- `ScenarioDefinitions`（位于 Scenarios/）：预定义的标准测试场景集合

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

### 4.1 分拣编排核心

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `ISortingOrchestrator` | Core/Sorting/Orchestration/ | 分拣编排服务接口，定义 ProcessParcelAsync 等核心入口方法 |
| `SortingOrchestrator` | Execution/Orchestration/ | 分拣编排器实现，协调包裹从检测到落格的完整流程 |
| `ISwitchingPathGenerator` | Core/LineModel/Topology/ | 路径生成器接口，根据格口 ID 生成摆轮切换路径 |
| `DefaultSwitchingPathGenerator` | Core/LineModel/Topology/ | 默认路径生成器实现，基于拓扑配置生成路径 |
| `ISwitchingPathExecutor` | Execution/ | 路径执行器接口，按段执行摆轮切换指令 |

### 4.2 上游通信

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `IUpstreamRoutingClient` | Core/Abstractions/Upstream/ | 上游路由客户端抽象，用于请求格口分配 |
| `IRuleEngineClient` | Communication/Abstractions/ | 规则引擎通信客户端接口，支持多协议 |
| `TcpRuleEngineClient` | Communication/Clients/ | TCP 协议客户端实现 |
| `SignalRRuleEngineClient` | Communication/Clients/ | SignalR 协议客户端实现 |
| `UpstreamRoutingClientAdapter` | Communication/Adapters/ | 适配 IRuleEngineClient 为 IUpstreamRoutingClient |

### 4.3 硬件驱动抽象

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `IWheelDiverterDriver` | Core/Abstractions/Drivers/ | 摆轮驱动器接口，定义左转/右转/直通/停止操作 |
| `IDiverterController` | Core/Abstractions/Drivers/ | 摆轮控制器接口（更高层抽象） |
| `IInputPort` | Core/Abstractions/Drivers/ | 输入端口接口，读取传感器状态 |
| `IOutputPort` | Core/Abstractions/Drivers/ | 输出端口接口，控制继电器/指示灯 |
| `IIoLinkageDriver` | Core/Abstractions/Drivers/ | IO 联动驱动接口 |

### 4.4 配置与仓储

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `SystemConfiguration` | Core/LineModel/Configuration/ | 系统配置模型，包含异常格口 ID、版本等 |
| `ISystemConfigurationRepository` | Core/LineModel/Configuration/ | 系统配置仓储接口 |
| `ChutePathTopologyConfig` | Core/LineModel/Configuration/ | 格口-路径拓扑配置模型 |
| `IoLinkageConfiguration` | Core/LineModel/Configuration/ | IO 联动配置模型 |

### 4.5 基础设施服务

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `ISystemClock` | Core/Utilities/ | 系统时钟抽象，所有时间获取必须通过此接口 |
| `ISafeExecutionService` | Observability/Utilities/ | 安全执行服务接口，捕获异常防止进程崩溃 |
| `PrometheusMetrics` | Observability/ | Prometheus 指标定义与收集 |
| `AlarmService` | Observability/ | 告警服务，处理系统告警 |

### 4.6 仿真相关

| 类型 | 位置 | 职责 |
|-----|------|-----|
| `SimulatedWheelDiverterDevice` | Drivers/Vendors/Simulated/ | 仿真摆轮设备实现 |
| `SimulatedVendorDriverFactory` | Drivers/Vendors/Simulated/ | 仿真驱动工厂 |
| `SimulationRunner` | Simulation/Services/ | 仿真主运行器 |
| `SimulationScenario` | Simulation/Scenarios/ | 仿真场景定义模型 |

---

## 5. 当前结构中已发现的问题标记

> 以下问题仅记录，本 PR 不做任何修改。这些问题将由后续 5 个重构 PR 分批解决。

### 5.1 层级职责混淆

1. **Execution 项目根目录文件过多**
   - `ISwitchingPathExecutor.cs`、`AnomalyDetector.cs`、`ConveyorSegment.cs` 等文件直接放在项目根目录
   - 建议：按职责归类到对应子目录（如 Abstractions/、Segments/）

2. **Drivers 层依赖 Execution 层**
   - `ZakYip.WheelDiverterSorter.Drivers.csproj` 引用了 `Execution` 项目
   - 这违反了分层架构原则，驱动层应该是底层，不应依赖执行层
   - 建议：将相关依赖移到 Core 层，或通过接口解耦

3. **Core 层 Abstractions 目录结构与 Drivers 层重复**
   - `Core/Abstractions/Drivers/` 和 `Drivers/Abstractions/` 存在重复定义
   - 部分接口通过 `global using` 别名指向 Core 层
   - 建议：统一接口定义位置，删除重复的抽象层

### 5.2 配置相关问题

4. **LineModel/Configuration 目录文件过多**
   - 包含 50+ 文件，混合了配置模型、仓储接口、LiteDB 实现
   - 建议：拆分为 Models/、Repositories/Interfaces/、Repositories/LiteDb/ 等子目录

5. **存在重复的 Options 类定义**
   - `UpstreamConnectionOptions` 在 `Execution/Orchestration/SortingOrchestrator.cs` 中定义（仅含 FallbackTimeoutSeconds 属性）
   - `Core/Sorting/Policies/UpstreamConnectionOptions.cs` 中定义了完整的上游连接配置类
   - 两者职责不同但命名相同，容易造成混淆
   - 建议：重命名 Execution 层的为 `UpstreamTimeoutOptions` 或合并到 Core 层的定义中

### 5.3 代码组织问题

6. **~~Host 层 Controllers 数量过多~~** ✅ 已解决 (PR3)
   - ~~18 个 Controller，部分功能可能可以合并~~
   - ~~`LeadshineIoDriverConfigController`、`ModiConfigController`、`ShuDiNiaoConfigController` 可考虑合并为统一的驱动配置 Controller~~
   - **PR3 解决方案**：已合并为统一的 `HardwareConfigController`，提供 `/api/hardware/leadshine`、`/api/hardware/modi`、`/api/hardware/shudiniao` 端点

7. **~~Host/Services 目录混合了多种类型~~** ✅ 已解决 (PR3)
   - ~~包含 Workers、扩展方法、业务服务、运行时配置~~
   - ~~建议：拆分为 Workers/、Extensions/、BusinessServices/ 等~~
   - **PR3 解决方案**：已拆分为 `Services/Workers/`（后台任务）、`Services/Extensions/`（DI扩展方法）、`Services/Application/`（应用服务）

8. **Simulation 项目既是库又是可执行程序**
   - `OutputType` 为 `Exe`，同时被 Host 项目引用
   - 这种设计可能导致构建和部署复杂性

### 5.4 技术债务

9. **~~部分接口存在多层别名~~** ✅ 已解决 (PR5)
   - ~~`Drivers/Abstractions/IWheelDiverterDriver.cs` 仅包含 `global using` 指向 Core 层~~
   - ~~这种间接引用增加了理解成本~~
   - **PR5 解决方案**：删除了 Observability 层的 alias-only 文件（`ParcelFinalStatus.cs`、`AlarmLevel.cs`、`AlarmType.cs`、`AlertSeverity.cs`、`SystemClockAliases.cs`），删除了 Communication 层的 `EmcLockNotificationType.cs`，并为受影响的文件添加了显式 using 语句。

10. **Execution 层 Abstractions 与 Core 层 Abstractions 的职责边界不清**
    - 两层都定义了 `ISensorEventProvider`、`IUpstreamRoutingClient` 等接口
    - 建议：明确哪些接口属于核心契约（Core），哪些属于执行层特定（Execution）

11. **~~缺少统一的 DI 注册中心~~** ✅ 已解决 (PR3)
    - ~~各项目都有自己的 `*ServiceExtensions.cs` 扩展方法~~
    - ~~Host 的 Program.cs 需要调用多个扩展方法来完成注册~~
    - ~~建议：考虑提供统一的 `AddWheelDiverterSorter()` 方法~~
    - **PR3 解决方案**：新增 `WheelDiverterSorterServiceCollectionExtensions.AddWheelDiverterSorter()` 方法，Program.cs 只需调用这一个方法即可完成所有服务注册

12. **遗留拓扑类型待清理** (PR4 标记)
    - `Core/LineModel/Topology/Legacy/` 目录下的类型已标记为 `[Obsolete]`
    - 包括：`LineTopology`, `DiverterNodeConfig`, `ChuteConfig`, `TopologyNode`, `TopologyEdge`, `DeviceBinding`
    - 接口：`ILineTopologyService`, `IDeviceBindingService`, `IVendorIoMapper`
    - 建议：后续版本逐步迁移到 `LineModel.Topology` 下的新类型（如 `SorterTopology`, `DiverterNode`）

### 5.5 文档与命名

13. **~~部分 README.md 可能过时~~** ✅ 已解决 (PR5)
    - ~~`Drivers/README.md`、`Simulation/README.md` 等需要验证是否与当前代码一致~~
    - **PR5 解决方案**：更新了 `Drivers/README.md` 和 `Simulation/README.md`，反映当前 Vendors 结构和公共 API 定义

14. **~~部分命名空间与物理路径不一致~~** ✅ 部分解决 (PR4)
    - ~~需要检查所有命名空间是否与项目/目录结构对应~~
    - **PR4 解决方案**：`Core/LineModel/Configuration` 已按 Models/Repositories/Validation 拆分，命名空间与路径一致

15. **Simulation 项目边界已明确** ✅ 已解决 (PR5)
    - **问题**：Simulation 既是独立可执行程序又被 Host 引用，边界不清晰
    - **PR5 解决方案**：在 Simulation/README.md 中明确定义了公共 API（`ISimulationScenarioRunner`、`SimulationOptions`、`SimulationSummary`）与内部实现的区分，Host 层只应使用公共 API

### 5.6 厂商配置收拢相关（PR-C2）

16. **厂商配置已部分移动到 Drivers/Vendors/** ✅ 部分完成 (PR-C2)
    - **已完成**：
      - `LeadshineOptions`, `LeadshineDiverterConfigDto` 从 Drivers 根目录移动到 `Vendors/Leadshine/Configuration/`
      - `S7Options`, `S7DiverterConfigDto` 从 Drivers 根目录移动到 `Vendors/Siemens/Configuration/`
      - `LeadshineSensorOptions`, `LeadshineSensorConfigDto` 从 Ingress 移动到 `Drivers/Vendors/Leadshine/Configuration/`
      - 创建了 `SiemensS7ServiceCollectionExtensions` 统一 DI 扩展
    - **待处理（技术债务）**：
      - Core 层 `LeadshineCabinetIoOptions` 仍在 `Core/LineModel/Configuration/Models/` 中
        - 该类被 `SystemConfiguration` 引用，移动需要更复杂的配置加载机制重构
        - 建议：后续 PR 将其移动到 Drivers/Vendors/Leadshine/Configuration/ 并更新配置绑定逻辑
      - Modi 和 ShuDiNiao 的配置类尚未提取到独立的 Configuration 目录
        - 当前这两个厂商的配置直接从 `WheelDiverterConfiguration` 中读取
        - 建议：后续 PR 提取厂商特定配置类到各自的 Configuration 目录

17. **Ingress 项目新增 Drivers 依赖**
    - PR-C2 为了让 Ingress 使用 Drivers 中的配置类，新增了 Ingress -> Drivers 的项目引用
    - 依赖链变为：Ingress -> Drivers -> Core/Communication
    - 这是为了避免配置类重复定义的权宜之计
    - **注意**：需确保 Ingress 不直接使用 Drivers 中的驱动实现类，仅使用配置类

### 5.7 内联枚举待迁移（PR-C2 白名单）

18. **接口文件中的内联枚举**
    - `IWheelDiverterDevice.cs` 中定义了 `WheelDiverterState` 枚举
    - `IWheelProtocolMapper.cs` 中定义了 `WheelCommandResultType`, `WheelDeviceState` 枚举
    - **建议**：后续 PR 将这些枚举迁移到 `Core/Enums/Hardware/` 目录

19. **DTO 文件中的内联枚举**
    - `ChutePathTopologyDto.cs` 中定义了 `SimulationStepType`, `StepStatus` 枚举
    - **建议**：后续 PR 将这些枚举迁移到 `Core/Enums/` 相应子目录

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

**文档版本**：1.2  
**最后更新**：2025-11-28  
**维护团队**：ZakYip Development Team
