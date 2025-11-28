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
| 核心层 | ZakYip.WheelDiverterSorter.Core | src/Core/ |
| 执行层 | ZakYip.WheelDiverterSorter.Execution | src/Execution/ |
| 驱动层 | ZakYip.WheelDiverterSorter.Drivers | src/Drivers/ |
| 入口层 | ZakYip.WheelDiverterSorter.Ingress | src/Ingress/ |
| 可观测性层 | ZakYip.WheelDiverterSorter.Observability | src/Observability/ |
| 通信层 | ZakYip.WheelDiverterSorter.Communication | src/Infrastructure/ |
| 仿真层 | ZakYip.WheelDiverterSorter.Simulation | src/Simulation/ |
| 分析器 | ZakYip.WheelDiverterSorter.Analyzers | src/Analyzers/ |

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
├── ZakYip.WheelDiverterSorter.Core
├── ZakYip.WheelDiverterSorter.Execution
├── ZakYip.WheelDiverterSorter.Drivers
├── ZakYip.WheelDiverterSorter.Ingress
├── ZakYip.WheelDiverterSorter.Observability
├── ZakYip.WheelDiverterSorter.Communication
└── ZakYip.WheelDiverterSorter.Simulation

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
- **Simulation** 依赖除 Host 外的所有项目，提供仿真运行环境
- **Host** 是顶层应用入口，依赖所有业务项目

---

## 3. 各项目内部结构

### 3.1 ZakYip.WheelDiverterSorter.Host

**项目职责**：Web API 主机入口，负责 DI 容器配置、API Controller 定义、启动引导和 Swagger 文档生成。不包含业务逻辑，业务逻辑委托给 Execution、Core 等底层项目。

```
ZakYip.WheelDiverterSorter.Host/
├── Application/
│   └── Services/                    # 应用层服务（配置缓存、系统配置服务等）
├── Commands/                        # CQRS 命令定义与处理器
├── Controllers/                     # API 控制器（18个）
│   ├── AlarmsController.cs
│   ├── ChuteAssignmentTimeoutController.cs
│   ├── ChutePathTopologyController.cs
│   ├── CommunicationController.cs
│   ├── DivertsController.cs
│   ├── HealthController.cs
│   ├── IoLinkageController.cs
│   ├── LeadshineIoDriverConfigController.cs
│   ├── LoggingConfigController.cs
│   ├── ModiConfigController.cs
│   ├── PanelConfigController.cs
│   ├── PolicyController.cs
│   ├── ShuDiNiaoConfigController.cs
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
├── Services/                        # 后台服务与扩展方法
│   ├── RuntimeProfiles/             # 运行时配置文件
│   └── ...
├── StateMachine/                    # 系统状态机（启动/运行/停止）
├── Swagger/                         # Swagger 配置与过滤器
├── Program.cs                       # 应用入口点
├── appsettings.json                 # 配置文件
├── nlog.config                      # NLog 日志配置
└── Dockerfile                       # Docker 构建文件
```

#### 关键类型概览

- `Program.cs`：应用启动入口，配置 DI 容器、注册所有服务、配置中间件
- `SystemStateManager`（位于 StateMachine/）：管理系统启动/运行/停止状态转换
- `BootHostedService`（位于 Services/）：系统启动引导服务，按顺序初始化各子系统
- `ApiControllerBase`（位于 Controllers/）：所有 API 控制器的基类，提供统一响应格式
- `OptimizedSortingService`（位于 Services/）：分拣服务的 Host 层封装
- `CachedSwitchingPathGenerator`（位于 Services/）：带缓存的路径生成器适配器

---

### 3.2 ZakYip.WheelDiverterSorter.Core

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
│   ├── Configuration/               # 配置模型与仓储接口
│   │   ├── SystemConfiguration.cs
│   │   ├── ChutePathTopologyConfig.cs
│   │   ├── IoLinkageConfiguration.cs
│   │   ├── ISystemConfigurationRepository.cs
│   │   ├── LiteDbSystemConfigurationRepository.cs
│   │   └── ...（30+ 配置相关文件）
│   ├── Events/
│   ├── Orchestration/               # 路由拓扑一致性检查
│   ├── Routing/                     # 路由计划模型
│   ├── Runtime/                     # 运行时模型
│   ├── Segments/                    # 输送段模型
│   ├── Services/                    # 线体服务接口
│   ├── Topology/                    # 拓扑与路径生成
│   │   ├── SorterTopology.cs
│   │   ├── SwitchingPath.cs
│   │   ├── ISwitchingPathGenerator.cs
│   │   └── DefaultSwitchingPathGenerator.cs
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
├── Topology/                        # 设备拓扑（遗留）
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

### 3.3 ZakYip.WheelDiverterSorter.Execution

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
├── ISwitchingPathExecutor.cs        # 路径执行器接口
├── PathExecutionService.cs          # 路径执行服务
├── DefaultStrategyFactory.cs
├── DefaultSystemRunStateService.cs
└── ...
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

### 3.4 ZakYip.WheelDiverterSorter.Drivers

**项目职责**：硬件驱动实现层，封装与具体硬件设备（雷赛 IO 卡、西门子 PLC、摩迪/书迪鸟摆轮协议等）的通信细节。

```
ZakYip.WheelDiverterSorter.Drivers/
├── Abstractions/                    # 驱动层抽象（部分已迁移到 Core）
│   ├── IWheelDiverterDriver.cs      # 指向 Core 层的别名
│   ├── IDiverterController.cs
│   ├── IInputPort.cs
│   ├── IOutputPort.cs
│   └── ...
├── Diagnostics/                     # 驱动诊断
│   └── RelayWheelDiverterSelfTest.cs
├── Vendors/                         # 厂商特定实现
│   ├── Leadshine/                   # 雷赛 IO 卡驱动
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
│   │   └── IoMapping/
│   ├── Siemens/                     # 西门子 S7 PLC 驱动
│   │   ├── S7Connection.cs
│   │   ├── S7DiverterController.cs
│   │   ├── S7InputPort.cs
│   │   └── S7OutputPort.cs
│   ├── Modi/                        # 摩迪摆轮协议驱动
│   │   ├── ModiProtocol.cs
│   │   ├── ModiWheelDiverterDriver.cs
│   │   └── ModiSimulatedDevice.cs
│   ├── ShuDiNiao/                   # 书迪鸟摆轮协议驱动
│   │   ├── ShuDiNiaoProtocol.cs
│   │   ├── ShuDiNiaoWheelDiverterDriver.cs
│   │   ├── ShuDiNiaoWheelDiverterDriverManager.cs
│   │   └── ShuDiNiaoSimulatedDevice.cs
│   └── Simulated/                   # 仿真驱动实现
│       ├── SimulatedWheelDiverterDevice.cs
│       ├── SimulatedWheelDiverterActuator.cs
│       ├── SimulatedConveyorSegmentDriver.cs
│       ├── SimulatedIoLinkageDriver.cs
│       ├── SimulatedVendorDriverFactory.cs
│       └── IoMapping/
├── FactoryBasedDriverManager.cs     # 工厂模式驱动管理器
├── HardwareSwitchingPathExecutor.cs # 硬件路径执行器
├── WheelCommandExecutor.cs          # 摆轮命令执行器
├── IoLinkageExecutor.cs             # IO 联动执行器
├── DriverServiceExtensions.cs       # DI 扩展方法
└── DriverOptions.cs                 # 驱动配置选项
```

#### 关键类型概览

- `HardwareSwitchingPathExecutor`：硬件路径执行器，将路径指令下发到真实硬件
- `FactoryBasedDriverManager`：基于工厂模式的驱动管理器，支持多厂商设备
- `LeadshineDiverterController`（位于 Vendors/Leadshine/）：雷赛摆轮控制器实现
- `S7DiverterController`（位于 Vendors/Siemens/）：西门子 S7 PLC 摆轮控制器
- `ShuDiNiaoWheelDiverterDriver`（位于 Vendors/ShuDiNiao/）：书迪鸟摆轮驱动实现
- `SimulatedWheelDiverterDevice`（位于 Vendors/Simulated/）：仿真摆轮设备，用于测试
- `IoLinkageExecutor`：IO 联动执行器，处理传感器与摆轮的联动逻辑

---

### 3.5 ZakYip.WheelDiverterSorter.Ingress

**项目职责**：入口层，负责传感器事件监听、包裹检测、上游通信门面封装。

```
ZakYip.WheelDiverterSorter.Ingress/
├── Adapters/                        # 适配器
│   └── SensorEventProviderAdapter.cs
├── Configuration/                   # 传感器配置
│   ├── SensorConfiguration.cs
│   ├── LeadshineSensorOptions.cs
│   ├── MockSensorConfigDto.cs
│   └── ParcelDetectionOptions.cs
├── Models/                          # 入口层模型
│   ├── ParcelDetectedEventArgs.cs
│   ├── SensorEvent.cs
│   ├── SensorHealthStatus.cs
│   └── ...
├── Sensors/                         # 传感器实现
│   ├── LeadshineSensor.cs
│   ├── LeadshineSensorFactory.cs
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

### 3.6 ZakYip.WheelDiverterSorter.Communication

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

### 3.7 ZakYip.WheelDiverterSorter.Observability

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

### 3.8 ZakYip.WheelDiverterSorter.Simulation

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

### 3.9 ZakYip.WheelDiverterSorter.Analyzers

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

5. **多处存在重复的 Options 类定义**
   - `UpstreamConnectionOptions` 在 `Execution/Orchestration/SortingOrchestrator.cs` 中定义
   - 与 `Core/Sorting/Policies/UpstreamConnectionOptions.cs` 可能重复
   - 建议：统一配置选项的定义位置

### 5.3 代码组织问题

6. **Host 层 Controllers 数量过多**
   - 18 个 Controller，部分功能可能可以合并
   - `LeadshineIoDriverConfigController`、`ModiConfigController`、`ShuDiNiaoConfigController` 可考虑合并为统一的驱动配置 Controller

7. **Host/Services 目录混合了多种类型**
   - 包含 Workers、扩展方法、业务服务、运行时配置
   - 建议：拆分为 Workers/、Extensions/、BusinessServices/ 等

8. **Simulation 项目既是库又是可执行程序**
   - `OutputType` 为 `Exe`，同时被 Host 项目引用
   - 这种设计可能导致构建和部署复杂性

### 5.4 技术债务

9. **部分接口存在多层别名**
   - `Drivers/Abstractions/IWheelDiverterDriver.cs` 仅包含 `global using` 指向 Core 层
   - 这种间接引用增加了理解成本

10. **Execution 层 Abstractions 与 Core 层 Abstractions 的职责边界不清**
    - 两层都定义了 `ISensorEventProvider`、`IUpstreamRoutingClient` 等接口
    - 建议：明确哪些接口属于核心契约（Core），哪些属于执行层特定（Execution）

11. **缺少统一的 DI 注册中心**
    - 各项目都有自己的 `*ServiceExtensions.cs` 扩展方法
    - Host 的 Program.cs 需要调用多个扩展方法来完成注册
    - 建议：考虑提供统一的 `AddWheelDiverterSorter()` 方法

### 5.5 文档与命名

12. **部分 README.md 可能过时**
    - `Drivers/README.md`、`Simulation/README.md` 等需要验证是否与当前代码一致

13. **部分命名空间与物理路径不一致**
    - 需要检查所有命名空间是否与项目/目录结构对应

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

**文档版本**：1.0  
**最后更新**：2025-11-28  
**维护团队**：ZakYip Development Team
