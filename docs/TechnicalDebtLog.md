# 技术债务详细日志 (Technical Debt Log)

> 本文档存放每个技术债/重构 PR 的详细过程记录。
>
> **索引位置**：`docs/RepositoryStructure.md` 的第 5 章节保留技术债的 ID、当前状态和简短摘要，详细描述全部在本文件中。
>
> **阅读说明**：
> - Copilot 在 `RepositoryStructure.md` 技术债索引表中点击"详情"链接时，跳转到本文件对应章节
> - 技术债登记点依然在 `RepositoryStructure.md`（通过 ID + 状态表），本文件仅作详细过程补充

---

## 目录

- [TD-001] Execution 根目录文件过多 (PR-TD4)
- [TD-002] Drivers 层依赖 Execution 层 (PR-TD4)
- [TD-003] Core 层 Abstractions 与 Drivers 层重复 (PR-TD4, PR-C6)
- [TD-004] LineModel/Configuration 目录文件过多
- [TD-005] 重复 Options 类定义 (PR-TD5)
- [TD-006] Host 层 Controllers 数量过多 (PR3)
- [TD-007] Host/Services 目录混合多种类型 (PR3)
- [TD-008] Simulation 项目既是库又是可执行程序 (PR-TD6)
- [TD-009] 接口多层别名 (PR5)
- [TD-010] Execution 层 Abstractions 与 Core 层职责边界 (PR-C4)
- [TD-011] 缺少统一的 DI 注册中心 (PR3, PR-H1)
- [TD-012] 遗留拓扑类型待清理 (PR-C3, PR-C6)
- [TD-013] Host 层直接依赖过多下游项目 (PR-H1)
- [TD-014] Host 层包含业务接口/Commands/Repository (PR-H2)
- [TD-015] 部分 README.md 可能过时 (PR5)
- [TD-016] 命名空间与物理路径不一致 (PR4, PR-RS12)
- [TD-017] Simulation 项目边界 (PR5)
- [TD-018] 厂商配置收拢 (PR-C2, PR-TD7)
- [TD-019] Ingress 对 Drivers 解耦 (PR-TD7)
- [TD-020] 内联枚举待迁移 (PR-TD6, PR-C5)
- [TD-021] HAL 层收敛与 IDiverterController 清理 (PR-C6)
- [TD-022] IWheelDiverterActuator 重复抽象 (PR-TD9)
- [TD-023] Ingress 层冗余 UpstreamFacade (PR-TD8)
- [TD-024] ICongestionDetector 重复接口 (PR-S1)
- [TD-025] CommunicationLoggerAdapter 纯转发适配器 (PR-S2)
- [TD-026] Facade/Adapter 防线规则 (PR-S2)
- [TD-027] DTO/Options/Utilities 统一规范 (PR-S3)
- [TD-028] 事件 & DI 扩展影分身清理 (PR-S6)
- [TD-029] 配置模型瘦身 (PR-SD5)
- [TD-030] Core 混入 LiteDB 持久化实现 (PR-RS13)
- [TD-031] Upstream 协议文档收敛 (PR-DOC-UPSTREAM01)
- [TD-032] Tests 与 Tools 结构规范 (PR-RS-TESTS01)
- [TD-033] 单一权威实现表扩展 & 自动化验证 (PR-RS-SINGLEAUTH01)
- [TD-034] 配置缓存统一 (PR-CONFIG-HOTRELOAD01)

---

## [TD-001] Execution 根目录文件过多

**状态**：✅ 已解决 (PR-TD4)

**问题描述**：
- `ISwitchingPathExecutor.cs`、`AnomalyDetector.cs`、`ConveyorSegment.cs` 等文件直接放在项目根目录
- 建议：按职责归类到对应子目录（如 Abstractions/、Segments/）

**解决方案**：
- `ISwitchingPathExecutor` 已移至 `Core/Abstractions/Execution/`
- `AnomalyDetector` 已移至 `Execution/Diagnostics/`
- `ConveyorSegment` 已移至 `Execution/Segments/`
- `PathExecutionService` 已移至 `Execution/PathExecution/`
- `DefaultStrategyFactory`、`DefaultSystemRunStateService` 已移至 `Execution/Infrastructure/`
- `NodeHealthServiceExtensions` 已移至 `Execution/Extensions/`
- 新增 ArchTest 规则确保 Execution 根目录不再堆放业务类型

---

## [TD-002] Drivers 层依赖 Execution 层

**状态**：✅ 已解决 (PR-TD4)

**问题描述**：
- `ZakYip.WheelDiverterSorter.Drivers.csproj` 引用了 `Execution` 项目
- 这违反了分层架构原则，驱动层应该是底层，不应依赖执行层
- 建议：将相关依赖移到 Core 层，或通过接口解耦

**解决方案**：
- Drivers.csproj 已移除对 Execution 的 ProjectReference
- 所有驱动抽象接口定义在 `Core/Hardware/` (PR-C6 已从 `Core/Abstractions/Drivers/` 迁移)
- 新增 ArchTest 规则 `Drivers_ShouldNotDependOn_Execution()` 防止倒退

---

## [TD-003] Core 层 Abstractions 与 Drivers 层重复

**状态**：✅ 已解决 (PR-TD4, PR-C6 进一步收敛)

**问题描述**：
- `Core/Abstractions/Drivers/` 和 `Drivers/Abstractions/` 存在重复定义
- 部分接口通过 `global using` 别名指向 Core 层
- 建议：统一接口定义位置，删除重复的抽象层

**PR-TD4 解决方案**：
- `Drivers/Abstractions/` 目录已删除
- 所有驱动抽象接口仅存在于 `Core/Abstractions/Drivers/`
- 新增 ArchTest 规则 `Drivers_ShouldNotHaveAbstractionsDirectory()` 防止重生

**PR-C6 进一步收敛**：
- `Core/Abstractions/Drivers/` 目录已删除
- 所有硬件相关接口统一迁移到 `Core/Hardware/` 的对应子目录
- 新增 ArchTest 规则防止创建平行硬件抽象层

---

## [TD-004] LineModel/Configuration 目录文件过多

**状态**：✅ 已解决 (PR-TD-ZERO01, PR-TD-ZERO02)

**问题描述**：
- 包含 50+ 文件，混合了配置模型、仓储接口、LiteDB 实现
- 建议：拆分为 Models/、Repositories/Interfaces/、Repositories/LiteDb/ 等子目录

**解决方案（PR-TD-ZERO01 + PR-TD-ZERO02）**：

1. **目录结构已完成拆分**：
   - `Models/`: 22 个配置模型文件（纯配置模型和相关枚举/值对象）
   - `Repositories/Interfaces/`: 11 个仓储接口文件
   - `Validation/`: 1 个验证器文件 (`IoEndpointValidator.cs`)
   - Configuration 目录根下无平铺的 .cs 文件

2. **结构防线测试已完善**：
   - `ConfigurationDirectoryStructureTests` 测试类（6 个测试方法）
   - 验证直接子目录只允许 { "Models", "Repositories", "Validation" }
   - 验证 Configuration 目录根下禁止平铺 .cs 文件
   - 验证各子目录职责单一

3. **LiteDB 实现已迁移**：
   - 根据 TD-030，LiteDB 仓储实现已迁移到 `Configuration.Persistence` 项目
   - `Repositories/` 目录下只保留 `Interfaces/` 子目录

**防线测试**：
- `TechnicalDebtComplianceTests.ConfigurationDirectoryStructureTests.ConfigurationDirectoryShouldOnlyHaveAllowedSubdirectories`
- `TechnicalDebtComplianceTests.ConfigurationDirectoryStructureTests.ConfigurationDirectoryShouldNotHaveFlatCsFiles`
- `TechnicalDebtComplianceTests.ConfigurationDirectoryStructureTests.RepositoriesShouldHaveCorrectStructure`
- `TechnicalDebtComplianceTests.ConfigurationDirectoryStructureTests.ModelsShouldOnlyContainConfigurationModels`
- `TechnicalDebtComplianceTests.ConfigurationDirectoryStructureTests.ValidationShouldOnlyContainValidators`
- `TechnicalDebtComplianceTests.ConfigurationDirectoryStructureTests.GenerateConfigurationDirectoryStructureReport`

**完成情况**：
- ✅ 目录拆分完成（Models/Repositories/Validation）
- ✅ 结构防线测试已添加并通过
- ✅ 状态更新为已解决 (PR-TD-ZERO02)

---

## [TD-005] 重复 Options 类定义

**状态**：✅ 已解决 (PR-TD5)

**问题描述**：
- `UpstreamConnectionOptions` 在 `Execution/Orchestration/SortingOrchestrator.cs` 中定义（仅含 FallbackTimeoutSeconds 属性）
- `Core/Sorting/Policies/UpstreamConnectionOptions.cs` 中定义了完整的上游连接配置类
- 两者职责不同但命名相同，容易造成混淆

**验证结果**：
- 经代码审查确认，`UpstreamConnectionOptions` 仅存在于 `Core/Sorting/Policies/` 中，不存在重复定义
- `SortingOrchestrator` 通过 `IOptions<UpstreamConnectionOptions>` 注入使用 Core 层的完整配置
- 其中 `FallbackTimeoutSeconds` 属性用于上游路由超时计算的降级逻辑

---

## [TD-006] Host 层 Controllers 数量过多

**状态**：✅ 已解决 (PR3)

**问题描述**：
- 18 个 Controller，部分功能可能可以合并
- `LeadshineIoDriverConfigController`、`ModiConfigController`、`ShuDiNiaoConfigController` 可考虑合并为统一的驱动配置 Controller

**解决方案**：
- 已合并为统一的 `HardwareConfigController`
- 提供 `/api/hardware/leadshine`、`/api/hardware/modi`、`/api/hardware/shudiniao` 端点

---

## [TD-007] Host/Services 目录混合多种类型

**状态**：✅ 已解决 (PR3)

**问题描述**：
- 包含 Workers、扩展方法、业务服务、运行时配置
- 建议：拆分为 Workers/、Extensions/、BusinessServices/ 等

**解决方案**：
- 已拆分为 `Services/Workers/`（后台任务）
- `Services/Extensions/`（DI扩展方法）
- `Services/Application/`（应用服务）

---

## [TD-008] Simulation 项目既是库又是可执行程序

**状态**：✅ 已解决 (PR-TD6)

**问题描述**：
- `OutputType` 为 `Exe`，同时被 Host 项目引用
- 这种设计可能导致构建和部署复杂性

**解决方案**：
- Simulation 项目的 `OutputType` 改为 `Library`
- 新增 `ZakYip.WheelDiverterSorter.Simulation.Cli` 项目作为独立的命令行入口
- Simulation.Cli 引用 Simulation 库，Host 只引用 Simulation 库
- 在 `TechnicalDebtComplianceTests` 中新增 `InterfacesAndDtosShouldNotContainInlineEnums` 测试防止内联枚举

---

## [TD-009] 接口多层别名

**状态**：✅ 已解决 (PR5)

**问题描述**：
- `Drivers/Abstractions/IWheelDiverterDriver.cs` 仅包含 `global using` 指向 Core 层
- 这种间接引用增加了理解成本

**解决方案**：
- 删除了 Observability 层的 alias-only 文件：
  - `ParcelFinalStatus.cs`
  - `AlarmLevel.cs`
  - `AlarmType.cs`
  - `AlertSeverity.cs`
  - `SystemClockAliases.cs`
- 删除了 Communication 层的 `EmcLockNotificationType.cs`
- 为受影响的文件添加了显式 using 语句

---

## [TD-010] Execution 层 Abstractions 与 Core 层职责边界

**状态**：✅ 已解决 (PR-C4)

**问题描述**：
- 两层都定义了 `ISensorEventProvider`、`IUpstreamRoutingClient` 等接口
- 建议：明确哪些接口属于核心契约（Core），哪些属于执行层特定（Execution）

**验证结果**：
- 跨层核心契约（`ISensorEventProvider`、`IUpstreamRoutingClient`、`IUpstreamContractMapper`、`IIoLinkageDriver`）仅在 `Core/Abstractions/` 中定义
- Execution 和 Drivers 中不存在重复定义
- Execution 中的接口（如 `IPathExecutionService`、`IAnomalyDetector` 等）均为执行层特有的抽象
- 职责边界已清晰

---

## [TD-011] 缺少统一的 DI 注册中心

**状态**：✅ 已解决 (PR3, PR-H1)

**问题描述**：
- 各项目都有自己的 `*ServiceExtensions.cs` 扩展方法
- Host 的 Program.cs 需要调用多个扩展方法来完成注册
- 建议：考虑提供统一的 `AddWheelDiverterSorter()` 方法

**PR3 解决方案**：
- 新增 `WheelDiverterSorterServiceCollectionExtensions.AddWheelDiverterSorter()` 方法
- Program.cs 只需调用这一个方法即可完成所有服务注册

**PR-H1 增强**：
- DI 聚合逻辑下沉到 Application 层
- Host 层只保留薄包装（AddWheelDiverterSorterHost）

---

## [TD-012] 遗留拓扑类型待清理

**状态**：✅ 已解决 (PR-C3, PR-C6)

**问题描述**：
- `Core/LineModel/Topology/Legacy/` 目录下的类型已标记为 `[Obsolete]`
- 包括：`LineTopology`, `DiverterNodeConfig`, `ChuteConfig`, `TopologyNode`, `TopologyEdge`, `DeviceBinding`
- 接口：`ILineTopologyService`, `IDeviceBindingService`, `IVendorIoMapper`

**PR-C3 解决方案**：
- 删除了整个 `Core/LineModel/Topology/Legacy/` 目录
- `IVendorIoMapper` 和 `VendorIoAddress` 迁移到 `Core/Abstractions/Drivers/`（仍在使用）
- 删除了未使用的 `TopologyServiceExtensions.cs`
- 新增 ArchTests 规则禁止再次创建 Legacy 目录

**PR-C6 位置更新**：
- `IVendorIoMapper` 和 `VendorIoAddress` 已从 `Core/Abstractions/Drivers/` 迁移到 `Core/Hardware/Mappings/`

---

## [TD-013] Host 层直接依赖过多下游项目

**状态**：✅ 已解决 (PR-H1)

**问题描述**：
- Host 项目直接引用 Execution/Drivers/Ingress/Communication/Simulation
- Host 层应只依赖 Application，由 Application 统一编排下游项目

**解决方案**：
- Host.csproj 移除对 Execution/Drivers/Ingress/Communication/Simulation 的直接 ProjectReference
- Host 现在只依赖 Application/Core/Observability
- 在 Application 层创建统一 DI 入口 `AddWheelDiverterSorter()`
- Host 层的 `AddWheelDiverterSorterHost()` 是 Application 层的薄包装
- 更新 ArchTests 强制执行新的依赖约束

---

## [TD-014] Host 层包含业务接口/Commands/Repository

**状态**：✅ 已解决 (PR-H2)

**问题描述**：
- Host/Application/Services/ 目录包含重复的服务接口和实现
- Host/Commands/ 目录包含 ChangeParcelChuteCommand 相关类型
- Host/Pipeline/ 目录包含 UpstreamAssignmentAdapter

**解决方案**：
- 删除 `Host/Application/` 目录，业务服务接口和实现已移至 Application 层
- 删除 `Host/Commands/` 目录，改口命令由 Application 层的 IChangeParcelChuteService 处理
- 删除 `Host/Pipeline/` 目录，上游适配器已移至 Execution 层
- 更新 DivertsController 使用 IChangeParcelChuteService
- 新增 ArchTests.HostLayerConstraintTests 强制执行：
  - 禁止接口定义（除 ISystemStateManager）
  - 禁止 Command/Repository/Adapter/Middleware 命名的类型
  - 禁止 Application/Commands/Pipeline/Repositories 目录
- Controller 依赖约束为顾问性测试（预留后续 PR 修复）

---

## [TD-015] 部分 README.md 可能过时

**状态**：✅ 已解决 (PR5)

**问题描述**：
- `Drivers/README.md`、`Simulation/README.md` 等需要验证是否与当前代码一致

**解决方案**：
- 更新了 `Drivers/README.md` 和 `Simulation/README.md`
- 反映当前 Vendors 结构和公共 API 定义

---

## [TD-016] 命名空间与物理路径不一致

**状态**：✅ 已解决 (PR-RS12)

**问题描述**：
- 需要检查所有命名空间是否与项目/目录结构对应

**PR4 解决方案**：
- `Core/LineModel/Configuration` 已按 Models/Repositories/Validation 拆分
- 命名空间与路径一致

**PR-RS12 完成**：
- 验证所有 594 个 C# 源文件的命名空间与物理路径 100% 对齐
- 新增 `ArchTests.NamespaceConsistencyTests` 架构防线测试：
  - `AllSourceFiles_ShouldHaveNamespaceMatchingPhysicalPath` - 验证命名空间与物理路径一致
  - `AllSourceFiles_ShouldHaveCorrectRootNamespace` - 验证根命名空间以 ZakYip.WheelDiverterSorter 开头
  - `Namespaces_ShouldNotSkipDirectoryLevels` - 验证命名空间不跳级
  - `GenerateNamespaceConsistencyReport` - 生成对齐报告
- 配合 `TechnicalDebtComplianceTests.NamespaceLocationTests` 双重防线

**对齐率统计**（PR-RS12 验证结果）：
| 项目 | 文件数 | 对齐率 |
|------|--------|--------|
| ZakYip.WheelDiverterSorter.Analyzers | 4 | 100% |
| ZakYip.WheelDiverterSorter.Application | 30 | 100% |
| ZakYip.WheelDiverterSorter.Communication | 46 | 100% |
| ZakYip.WheelDiverterSorter.Core | 260 | 100% |
| ZakYip.WheelDiverterSorter.Drivers | 65 | 100% |
| ZakYip.WheelDiverterSorter.Execution | 48 | 100% |
| ZakYip.WheelDiverterSorter.Host | 67 | 100% |
| ZakYip.WheelDiverterSorter.Ingress | 20 | 100% |
| ZakYip.WheelDiverterSorter.Observability | 28 | 100% |
| ZakYip.WheelDiverterSorter.Simulation | 25 | 100% |
| ZakYip.WheelDiverterSorter.Simulation.Cli | 1 | 100% |
| **总计** | **594** | **100%** |

---

## [TD-017] Simulation 项目边界

**状态**：✅ 已解决 (PR5)

**问题描述**：
- Simulation 既是独立可执行程序又被 Host 引用，边界不清晰

**解决方案**：
- 在 Simulation/README.md 中明确定义了公共 API：
  - `ISimulationScenarioRunner`
  - `SimulationOptions`
  - `SimulationSummary`
- 与内部实现的区分，Host 层只应使用公共 API

---

## [TD-018] 厂商配置收拢

**状态**：✅ 已完成 (PR-C2, PR-TD7)

**问题描述**：
- 厂商配置分散在多个位置

**PR-C2 完成**：
- `LeadshineOptions`, `LeadshineDiverterConfigDto` 从 Drivers 根目录移动到 `Vendors/Leadshine/Configuration/`
- `S7Options`, `S7DiverterConfigDto` 从 Drivers 根目录移动到 `Vendors/Siemens/Configuration/`
- `LeadshineSensorOptions`, `LeadshineSensorConfigDto` 从 Ingress 移动到 `Drivers/Vendors/Leadshine/Configuration/`
- 创建了 `SiemensS7ServiceCollectionExtensions` 统一 DI 扩展

**PR-TD7 完成**：
- `LeadshineCabinetIoOptions` 重命名为厂商无关的 `CabinetIoOptions`，添加 `VendorProfileKey` 字段关联厂商实现
- 创建 `ModiOptions`（`Vendors/Modi/Configuration/`）
- 创建 `ShuDiNiaoOptions`（`Vendors/ShuDiNiao/Configuration/`）
- 创建 `SimulatedOptions`（`Vendors/Simulated/Configuration/`）
- 创建 `ISensorVendorConfigProvider` 接口和 `LeadshineSensorVendorConfigProvider` 实现
- Ingress 不再直接引用 `Drivers.Vendors.*` 命名空间，通过抽象接口获取配置

---

## [TD-019] Ingress 对 Drivers 解耦

**状态**：✅ 已完成 (PR-TD7, PR-C6)

**问题描述**：
- PR-C2 为了让 Ingress 使用 Drivers 中的配置类，新增了 Ingress -> Drivers 的项目引用

**PR-TD7 解决方案**：
- 创建 `ISensorVendorConfigProvider` 抽象接口在 Core 层
- Ingress 通过该接口获取传感器配置，不再直接引用 `Drivers.Vendors.*` 命名空间
- `LeadshineSensorFactory` 使用 `ISensorVendorConfigProvider` 替代直接配置引用
- Drivers 层的 `LeadshineIoServiceCollectionExtensions` 负责注册 `ISensorVendorConfigProvider` 实现

**PR-C6 位置更新**：
- `ISensorVendorConfigProvider` 已从 `Core/Abstractions/Drivers/` 迁移到 `Core/Hardware/Providers/`

---

## [TD-020] 内联枚举待迁移

**状态**：✅ 已解决 (PR-TD6, PR-C5)

**问题描述**：

**接口文件中的内联枚举**：
- `IWheelDiverterDevice.cs` 中定义了 `WheelDiverterState` 枚举
- `IWheelProtocolMapper.cs` 中定义了 `WheelCommandResultType`, `WheelDeviceState` 枚举

**已迁移位置**：
- 所有枚举已迁移到 `Core/Enums/Hardware/` 目录：
  - `WheelDiverterState.cs`
  - `WheelCommandResultType.cs`
  - `WheelDeviceState.cs`

**DTO 文件中的内联枚举**：
- `ChutePathTopologyDto.cs` 中定义了 `SimulationStepType`, `StepStatus` 枚举

**已迁移位置**：
- 所有枚举已迁移到 `Core/Enums/Simulation/` 目录：
  - `SimulationStepType.cs`
  - `StepStatus.cs`

**PR-C5 补充**：
- 已为所有枚举成员添加 `[Description]` 特性和完整的中文注释

---

## [TD-021] HAL 层收敛与 IDiverterController 清理

**状态**：✅ 已解决 (PR-C6)

**问题描述**：

**Core/Abstractions/Drivers 双轨结构**：
- Core 中存在 `Abstractions/Drivers/` 和 `Hardware/` 两个平行的硬件抽象目录
- 部分接口在两处都有定义，职责边界不清晰

**解决方案**：
- 删除 `Core/Abstractions/Drivers/` 目录
- 所有硬件相关接口统一迁移到 `Core/Hardware/` 的对应子目录：
  - `Hardware/Ports/`: IInputPort, IOutputPort
  - `Hardware/IoLinkage/`: IIoLinkageDriver
  - `Hardware/Devices/`: IWheelDiverterDriver, IWheelDiverterDriverManager, IWheelProtocolMapper, IEmcController
  - `Hardware/Mappings/`: IVendorIoMapper, VendorIoAddress
  - `Hardware/Providers/`: ISensorVendorConfigProvider
- 新增 ArchTest 规则防止创建平行硬件抽象层

**IDiverterController 中间层**：
- 存在 `IDiverterController` (基于角度的低级接口) 和 `IWheelDiverterDriver` (基于方向的高级接口) 两层抽象
- `RelayWheelDiverterDriver` 作为适配器桥接两者，增加了复杂度

**解决方案**：
- 删除 `IDiverterController` 接口
- 删除 `RelayWheelDiverterDriver` 适配器
- 创建直接实现 `IWheelDiverterDriver` 的驱动类：
  - `LeadshineWheelDiverterDriver` (原 LeadshineDiverterController)
  - `S7WheelDiverterDriver` (原 S7DiverterController)
- 更新 `LeadshineVendorDriverFactory` 和 `SiemensS7ServiceCollectionExtensions` 使用新驱动类

---

## [TD-022] IWheelDiverterActuator 重复抽象

**状态**：✅ 已解决 (PR-TD9)

**问题描述**：
- `IWheelDiverterActuator` 与 `IWheelDiverterDriver` 方法签名完全相同，属于重复抽象
- `IVendorDriverFactory` 同时暴露 `CreateWheelDiverterDrivers()` 和 `CreateWheelDiverterActuators()` 两个方法
- `SimulatedWheelDiverterActuator` 是唯一的 `IWheelDiverterActuator` 实现，`Leadshine` 实现返回空列表

**解决方案**：
- 删除 `Core/Hardware/IWheelDiverterActuator.cs` 接口（与 `IWheelDiverterDriver` 语义重复）
- 删除 `Drivers/Vendors/Simulated/SimulatedWheelDiverterActuator.cs` 实现类
- 从 `IVendorDriverFactory` 移除 `CreateWheelDiverterActuators()` 方法
- 更新所有厂商工厂实现（`LeadshineVendorDriverFactory`、`SimulatedVendorDriverFactory`）
- 摆轮控制统一通过 `IWheelDiverterDriver`（方向接口）或 `IWheelDiverterDevice`（命令接口）暴露
- 新增 ArchTest 规则防止重新引入重复的摆轮控制接口

---

## [TD-023] Ingress 层冗余 UpstreamFacade

**状态**：✅ 已解决 (PR-TD8)

**问题描述**：
- Ingress 层存在 `IUpstreamFacade`、`UpstreamFacade`、`IUpstreamChannel`、`IUpstreamCommandSender`、`HttpUpstreamChannel` 等类型
- 这些类型虽然被定义和注册（`AddUpstreamServices`），但 `AddUpstreamServices` 从未被调用
- 上游通信实际使用的是 Communication 层的 `IUpstreamRoutingClient`

**解决方案**：
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

---

## [TD-024] ICongestionDetector 重复接口

**状态**：✅ 已解决 (PR-S1)

**问题描述**：
- `Core/Sorting/Interfaces/ICongestionDetector.cs` 定义了 `DetectCongestionLevel(CongestionMetrics)` 方法
- `Core/Sorting/Runtime/ICongestionDetector.cs` 定义了 `Detect(in CongestionSnapshot)` 方法
- 两个接口语义相同，但方法签名不同，导致存在两套实现：
  - `ThresholdCongestionDetector` - 实现 Interfaces 版本
  - `ThresholdBasedCongestionDetector` - 实现 Runtime 版本

**解决方案**：
- 统一接口位置：`Core/Sorting/Interfaces/ICongestionDetector.cs`
- 接口包含两个方法，支持两种输入格式：
  - `DetectCongestionLevel(CongestionMetrics metrics)` - 使用 class 输入
  - `Detect(in CongestionSnapshot snapshot)` - 使用 readonly struct 输入（高性能版本）
- 删除了 `Core/Sorting/Runtime/ICongestionDetector.cs` 重复接口
- 合并实现为单一类：`ThresholdCongestionDetector`
- 删除了 `ThresholdBasedCongestionDetector` 类及其配置类 `CongestionThresholds`
- 更新了测试文件使用统一的 `ReleaseThrottleConfiguration` 配置
- **规则**：同一职责禁止再创建第二个平行接口

---

## [TD-025] CommunicationLoggerAdapter 纯转发适配器

**状态**：✅ 已解决 (PR-S2)

**问题描述**：
- `Communication/Infrastructure/CommunicationLoggerAdapter.cs` 是纯转发类，仅包装 `ILogger` 接口
- 所有方法都是简单的一行转发调用，没有任何附加值

**解决方案**：
- 删除 `CommunicationLoggerAdapter` 类
- 删除 `ICommunicationLogger` 接口（位于 `ICommunicationInfrastructure.cs`）
- 更新 `DefaultCommunicationInfrastructure` 直接使用 `ILogger`
- 更新 `ExponentialBackoffRetryPolicy` 直接使用 `ILogger`
- 更新 `SimpleCircuitBreaker` 直接使用 `ILogger`
- 新增 TechnicalDebtComplianceTests 规则 `ShouldNotHavePureForwardingFacadeAdapterTypes` 检测纯转发类型

---

## [TD-026] Facade/Adapter 防线规则

**状态**：✅ 新增 (PR-S2)

**详细说明**：
- 新增测试规则 `PureForwardingTypeDetectionTests.ShouldNotHavePureForwardingFacadeAdapterTypes`

**纯转发类型定义**（满足以下条件判定为影分身，应删除）：
- 类型以 `*Facade` / `*Adapter` / `*Wrapper` / `*Proxy` 结尾
- 只持有 1~2 个服务接口字段
- 方法体只做直接调用另一个服务的方法，没有：
  - 类型转换/协议映射逻辑
  - 事件订阅/转发机制
  - 状态跟踪
  - 批量操作聚合
  - 验证或重试逻辑

**合法的 Adapter/Facade**（应保留）：
- 有明确的类型转换逻辑（如 `SensorEventProviderAdapter`）
- 有协议适配逻辑（如 `ShuDiNiaoWheelDiverterDeviceAdapter`）
- 有状态跟踪（如 `LeadshineDiscreteIoPort`）

---

## [TD-027] DTO/Options/Utilities 统一规范

**状态**：✅ 新增 (PR-S3)

**DTO/Model/Response 类型统一命名规则**：
- `*Configuration`: 持久化配置模型（存储在 LiteDB），位于 `Core/LineModel/Configuration/Models/`
- `*Options`: 运行时配置选项（通过 IOptions<T> 注入），位于各项目的 `Configuration/` 目录
- `*Request`: API 请求模型，位于 `Host/Models/` 或 `Host/Models/Config/`
- `*Response`: API 响应模型，位于 `Host/Models/` 或 `Host/Models/Config/`
- `*Dto`: 跨层数据传输对象（仅在必要时使用）

**已清理的重复类型**：
- 删除 `Ingress/Configuration/SensorConfiguration.cs`（未使用，与 Core 层 SensorConfiguration 重复）

**已知的同名类型**（有明确职责区分）：
- `OperationResult` (Core/Results/) - 完整的操作结果类型，带 ErrorCode 支持
- `OperationResult` (Core/LineModel/Routing/) - 简化的内部操作结果类型（PR-S5 重命名为 RouteComputationResult）

**Utilities 目录位置规范**：
- 允许的位置：
  - `Core/Utilities/` - 公共工具类（如 ISystemClock）
  - `Core/LineModel/Utilities/` - LineModel 内部工具类（使用 file-scoped class）
  - `Observability/Utilities/` - 可观测性相关工具类
- 禁止在其他项目中新增 Utilities 目录
- 项目特定工具应使用 `file static class` 保持文件作用域

**防线测试**：
- `DuplicateTypeDetectionTests.UtilitiesDirectoriesShouldFollowConventions()`
- `DuplicateTypeDetectionTests.ShouldNotHaveUnusedDtoOrOptionsTypes()` - 检测未使用的 DTO/Options 类型
- `DuplicateTypeDetectionTests.ShouldNotHaveDuplicateTypeNameAcrossNamespaces()` - 检测同名不同命名空间类型

---

## [TD-028] 事件 & DI 扩展影分身清理

**状态**：✅ 新增 (PR-S6)

**事件类型跨层重名清理**：
- **问题**：`SensorEvent` 同时存在于 Ingress/Models/ 和 Simulation/Services/，IDE 搜索时需要凭感觉判断
- **解决方案**：
  - 保留 `Ingress/Models/SensorEvent` 为现实世界传感器事件模型
  - 将仿真侧 `SensorEvent` 重命名为 `SimulatedSensorEvent`
  - 文件移动到 `Simulation/Models/SimulatedSensorEvent.cs`
- **防线测试**：`EventAndExtensionDuplicateDetectionTests.EventTypesShouldNotBeDuplicatedAcrossLayers()`

**DI 扩展类跨项目重名清理**：
- **问题**：`WheelDiverterSorterServiceCollectionExtensions` 同时存在于 Application 和 Host 层
- **解决方案**：
  - 保留 `Application/Extensions/WheelDiverterSorterServiceCollectionExtensions` 为唯一 DI 聚合入口
  - 将 Host 层扩展类重命名为 `WheelDiverterSorterHostServiceCollectionExtensions`
  - 文件位于 `Host/Services/Extensions/WheelDiverterSorterHostServiceCollectionExtensions.cs`
- **防线测试**：`EventAndExtensionDuplicateDetectionTests.ServiceCollectionExtensionsShouldBeUniquePerProject()`

---

## [TD-029] 配置模型瘦身

**状态**：✅ 新增 (PR-SD5)

**问题描述**：
- Core/LineModel/Configuration/Models 中存在仅被测试使用的配置模型

**已删除的模型**：
- `IoPointConfiguration.cs` - 统一的 IO 点配置模型（无生产代码使用）
- `LineSegmentConfig.cs` - 线体段配置（无生产代码使用，仅在文档注释中引用）
- `PanelIoOptions.cs` - 面板 IO 配置选项（无任何使用）
- `SignalTowerOptions.cs` - 信号塔配置选项（无任何使用）

**已删除的测试文件**：
- `tests/ZakYip.WheelDiverterSorter.Core.Tests/IoPointConfigurationTests.cs`
- `tests/ZakYip.WheelDiverterSorter.Core.Tests/LineModel/LineSegmentConfigTests.cs`

**更新的注释引用**：
- `ChutePathTopologyConfig.cs` - 移除了对 LineSegmentConfig 的文档引用
- `ChutePathTopologyController.cs` - 移除了对 LineSegmentConfig 的文档引用

**配置模型数量变化**：从 26 个减少到 22 个

**防线测试**：
- `DuplicateTypeDetectionTests.ConfigurationModelsShouldHaveProductionUsage()` - 验证配置模型在生产代码中有实际使用

---

## [TD-030] Core 混入 LiteDB 持久化实现

**状态**：✅ 已解决 (PR-RS13)

**问题描述**：
- Core/LineModel/Configuration/Repositories/ 中混入了 LiteDB 的具体实现
- Core 项目直接引用 LiteDB NuGet 包
- 这违反了 "Core 只定义抽象" 的原则
- 将来如果需要支持其他持久化方式（EF Core、文件配置等），会进一步污染 Core

**解决方案**：
1. 新建 `ZakYip.WheelDiverterSorter.Configuration.Persistence` 项目在 `src/Infrastructure/`
2. 将 12 个 LiteDB 仓储实现文件移动到新项目
3. 更新命名空间为 `ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb`
4. 从 Core.csproj 移除 LiteDB 包引用
5. 从 Core 配置模型中移除 `[BsonId]` 属性，改在 LiteDbMapperConfig 中通过 BsonMapper 配置
6. Application 层添加对 Configuration.Persistence 的依赖，负责 DI 注册

**移动的文件**：
- `LiteDbChutePathTopologyRepository.cs`
- `LiteDbCommunicationConfigurationRepository.cs`
- `LiteDbDriverConfigurationRepository.cs`
- `LiteDbIoLinkageConfigurationRepository.cs`
- `LiteDbLoggingConfigurationRepository.cs`
- `LiteDbMapperConfig.cs`
- `LiteDbPanelConfigurationRepository.cs`
- `LiteDbRouteConfigurationRepository.cs`
- `LiteDbSensorConfigurationRepository.cs`
- `LiteDbSystemConfigurationRepository.cs`
- `LiteDbWheelBindingsRepository.cs`
- `LiteDbWheelDiverterConfigurationRepository.cs`

**更新的 Core 模型（移除 [BsonId]）**：
- `SystemConfiguration.cs`
- `IoLinkageConfiguration.cs`
- `LoggingConfiguration.cs`
- `PanelConfiguration.cs`

**依赖关系**：
- `Configuration.Persistence → Core` (允许)
- `Configuration.Persistence → Observability` (允许，如需要)
- `Application → Configuration.Persistence` (允许，负责 DI 注册)
- `Configuration.Persistence → Host/Application/Simulation` (禁止)

**防线测试**：
- `PersistenceLayerComplianceTests.Core_ShouldNotReferenceLiteDB()` - Core 不引用 LiteDB 包
- `PersistenceLayerComplianceTests.Core_ShouldNotHaveLiteDbDirectory()` - Core 中无 LiteDb 目录
- `PersistenceLayerComplianceTests.Core_ShouldNotHaveLiteDBUsings()` - Core 源文件无 using LiteDB
- `PersistenceLayerComplianceTests.ConfigurationPersistence_ShouldContainLiteDbRepositories()` - 新项目包含仓储实现
- `PersistenceLayerComplianceTests.ConfigurationPersistence_ShouldReferenceLiteDB()` - 新项目引用 LiteDB
- `ApplicationLayerDependencyTests.Application_ShouldOnlyDependOn_AllowedProjects()` - Application 允许依赖 Configuration.Persistence
- `ApplicationLayerDependencyTests.ConfigurationPersistence_ShouldNotDependOn_HostApplicationSimulation()` - 新项目不依赖 Host/Application/Simulation
- `ApplicationLayerDependencyTests.ConfigurationPersistence_ShouldOnlyDependOn_CoreOrObservability()` - 新项目只依赖 Core/Observability

---

## [TD-031] Upstream 协议文档收敛

**状态**：✅ 已解决 (PR-DOC-UPSTREAM01)

**问题描述**：
- 上游协议字段表、示例 JSON、流程说明同时出现在 README 和 UPSTREAM_CONNECTION_GUIDE.md 中，造成"文档影分身"
- README 中"格口分配"步骤描述容易被理解为"同步请求/响应"模式，而非实际的"fire-and-forget 通知 + 异步回推"模式
- 多处维护相同内容增加了文档不一致的风险

**解决方案（PR-DOC-UPSTREAM01）**：

1. **收敛上游协议的"单一权威文档"**：
   - 将字段表、示例 JSON、超时/丢失规则、时序说明收敛到 `docs/guides/UPSTREAM_CONNECTION_GUIDE.md`
   - 明确两次 fire-and-forget 通知模型：
     - 入口检测：发送 `ParcelDetectionNotification`（仅通知），上游稍后推送 `ChuteAssignmentNotification` 回来
     - 落格完成：发送 `SortingCompletedNotification`（含 FinalStatus=Success/Timeout/Lost）
   - 解释配置字段与协议字段之间的关系（ChuteAssignmentTimeout.SafetyFactor/FallbackTimeoutSeconds/LostDetectionSafetyFactor）

2. **精简 README 中协议相关内容**：
   - 保留高层"分拣流程"框架，但调整第 2 步的文案，从"格口分配 – 上游/固定/轮询"改为明确的异步推送说明
   - 保留"包裹超时与丢失判定"小节，但加入显式链接引导到详细协议文档
   - 移除 README 中的重复字段表/JSON 示例，避免以后 README 与指南同时需要维护

3. **Copilot 行为约束更新**：
   - 在 `.github/copilot-instructions.md` 中新增规则：任何修改上游协议相关代码/DTO/文档的 PR，Copilot 必须优先读取：
     - `docs/guides/UPSTREAM_CONNECTION_GUIDE.md`
     - `docs/RepositoryStructure.md` 的"单一权威实现表"和"技术债索引"章节

**权威文档位置**：
- 上游协议详细说明：`docs/guides/UPSTREAM_CONNECTION_GUIDE.md`
- 时序图参考：`docs/UPSTREAM_SEQUENCE_FIREFORGET.md`

**规则**：
- 以后所有上游协议字段解释、示例 JSON、时序图只允许在指南中出现一份
- 其他文档只做高层引用，链接到权威文档

---

## [TD-032] Tests 与 Tools 结构规范

**状态**：✅ 已解决 (PR-RS-TESTS01)

**问题描述**：
- 测试项目和工具项目缺少统一的结构规则
- 未来容易在 tests/tools 里重新定义 DTO/Options/Enums 等，违反"单一权威实现"的原则
- 没有显式记录测试项目的依赖边界和职责

**解决方案 (PR-RS-TESTS01)**：

1. **在 RepositoryStructure.md 中补充文档**：
   - 新增"测试项目结构"章节，描述每个测试项目的职责和依赖边界
   - 新增"工具项目结构"章节，描述工具项目的职责和依赖方向
   - 更新解决方案概览中的测试项目列表

2. **新增结构防线测试 (TechnicalDebtComplianceTests)**：
   - `TestProjectsStructureTests.ShouldNotDefineDomainModelsInTests()`
     - 检测测试项目中是否定义了 Core/Domain 命名空间的类型
     - 允许测试辅助类型（Mock/Stub/Fake/Test/Helper 等命名模式）
   - `TestProjectsStructureTests.ShouldNotHaveLegacyDirectoriesInTests()`
     - 沿用 src 目录的规则，测试项目也禁止 Legacy 目录
   - `TestProjectsStructureTests.ShouldNotUseGlobalUsingsInTests()`
     - 沿用 src 目录的规则，测试项目也禁止 global using
   - `TestProjectsStructureTests.ShouldNotDuplicateProductionTypesInTests()`
     - 警告性检测：在测试项目中发现与 src 同名的类型
   - `TestProjectsStructureTests.ToolsShouldNotDefineDomainModels()`
     - 工具项目不应定义 Core/Domain 命名空间的业务模型
   - `TestProjectsStructureTests.GenerateTestProjectsStructureReport()`
     - 生成测试项目结构报告

3. **更新 copilot-instructions.md**：
   - 当 PR 改动 tests/ 或 tools/ 目录时，Copilot 必须先看：
     - `docs/RepositoryStructure.md` 的"测试项目结构/工具项目结构"章节
     - `TechnicalDebtComplianceTests` 中的结构测试列表

**测试项目结构约束**：

| 约束 | 说明 |
|------|------|
| ❌ 禁止定义 Core 命名空间类型 | 测试项目不应定义 `ZakYip.WheelDiverterSorter.Core.*` 命名空间的类型 |
| ❌ 禁止 Legacy 目录 | 沿用 src 目录规则 |
| ❌ 禁止 global using | 沿用 src 目录规则 |
| ✅ 允许测试辅助类型 | Mock/Stub/Fake/Test/Helper 等命名模式 |
| ✅ 允许引用 src 项目 | 用于测试 |

**工具项目结构约束**：

| 约束 | 说明 |
|------|------|
| ❌ 禁止定义 Core/Domain 类型 | 工具项目不应定义业务模型 |
| ✅ 允许引用 Core 项目 | 获取模型定义 |
| ✅ 使用工具项目命名空间 | 工具专用类型应使用 `*.Tools.*` 命名空间 |

**防线测试**：
- `TechnicalDebtComplianceTests.TestProjectsStructureTests.ShouldNotDefineDomainModelsInTests`
- `TechnicalDebtComplianceTests.TestProjectsStructureTests.ShouldNotHaveLegacyDirectoriesInTests`
- `TechnicalDebtComplianceTests.TestProjectsStructureTests.ShouldNotUseGlobalUsingsInTests`
- `TechnicalDebtComplianceTests.TestProjectsStructureTests.ToolsShouldNotDefineDomainModels`

---

## [TD-033] 单一权威实现表扩展 & 自动化验证

**状态**：✅ 已解决 (PR-RS-SINGLEAUTH01)

**问题描述**：
- 单一权威实现表（6.1 节）只覆盖了部分概念（HAL/硬件抽象、Ingress 抽象、配置服务），缺少 Upstream 契约、配置 Options、事件等概念的系统化列入
- 现有架构测试（DuplicateTypeDetectionTests、ApplicationLayerDependencyTests 等）的规则是硬编码的，和文档是两套独立的规则
- 文档与测试没有联动，容易导致"文档写的和实际测试规则不一样"的隐性技术债

**解决方案 (PR-RS-SINGLEAUTH01)**：

1. **扩展 6.1"单一权威实现表"**：
   - 新增 **上游契约/事件** 行：明确 `ChuteAssignmentEventArgs`, `SortingCompletedNotification` (Core 事件) 和传输 DTO (`ParcelDetectionNotification`, `ChuteAssignmentNotification`, `SortingCompletedNotificationDto`) 的权威位置
   - 新增 **运行时 Options** 行：明确 `UpstreamConnectionOptions`, `SortingSystemOptions`, `RoutingOptions` (Core) 和通信/厂商 Options 的权威位置
   - 更新现有行的测试防线列，添加 `SingleAuthorityCatalogTests` 引用

2. **新增 SingleAuthorityCatalogTests 测试类**：
   - 解析 `docs/RepositoryStructure.md` 中 6.1 表格
   - 对表格中的每个条目：
     - 验证权威类型存在于指定目录
     - 扫描解决方案确保禁止位置没有匹配模式的类型定义
   - 自动化验证使文档成为"源数据"，测试读取并执行

3. **重构现有硬编码规则**：
   - 将 `DuplicateTypeDetectionTests.CoreAbstractionInterfacesShouldOnlyBeDefinedInCore()` 等测试的模式抽取为可配置常量
   - 减少与权威表重复的硬编码规则

**扩展的权威表条目**：

| 概念 | 权威类型 | 权威位置 | 禁止位置 |
|------|---------|---------|---------|
| 上游契约/事件 | `ChuteAssignmentEventArgs`, `SortingCompletedNotification`, `DwsMeasurement` (Core)<br/>`ParcelDetectionNotification`, `ChuteAssignmentNotification`, `SortingCompletedNotificationDto` (DTO) | `Core/Abstractions/Upstream/`<br/>`Infrastructure/Communication/Models/` | 其他项目定义 `*Parcel*Notification`, `*AssignmentNotification`, `SortingCompleted*` |
| 运行时 Options | `UpstreamConnectionOptions`, `SortingSystemOptions`, `RoutingOptions` (Core)<br/>`TcpOptions`, `SignalROptions`, `MqttOptions` (Communication)<br/>`LeadshineOptions`, `S7Options`, `ShuDiNiaoOptions` (Vendors) | `Core/Sorting/Policies/`<br/>`Infrastructure/Communication/Configuration/`<br/>`Drivers/Vendors/<VendorName>/Configuration/` | Host 中定义运行时选项<br/>Core 中定义厂商命名 Options<br/>同名 Options 跨项目重复 |

**新增防线测试**：
- `TechnicalDebtComplianceTests.SingleAuthorityCatalogTests.AuthoritativeTypesShouldExistInSpecifiedLocations`
- `TechnicalDebtComplianceTests.SingleAuthorityCatalogTests.ForbiddenPatternsShouldNotExistInForbiddenLocations`
- `TechnicalDebtComplianceTests.SingleAuthorityCatalogTests.ParseAndValidateSingleAuthorityTable`

**变更影响**：
- 以后只要修改 `RepositoryStructure.md` 中的权威表，测试就会自动验证新规则
- 减少了"文档与测试脱节"的技术债风险
- 新增/修改概念时，只需更新文档表格，无需额外修改测试代码

---

## [TD-034] 配置缓存统一

**状态**：✅ 已解决 (PR-CONFIG-HOTRELOAD01)

**问题描述**：
- 历史上配置管理层可能存在分散的缓存实现（如 `Cached*Repository`、`*OptionsProvider` 等自带缓存字段的类型）
- 缺少统一的配置缓存策略，导致缓存逻辑散落在不同层级
- 配置热更新语义不明确，可能存在"更新后不立即生效"的问题
- 缺少架构测试防止未来再次出现配置缓存"影分身"

**验收现状 (PR-CONFIG-HOTRELOAD01 分析)**：

✅ **基础设施已完备**：
- `ISlidingConfigCache` 及其实现 `SlidingConfigCache` 已存在于 `Application/Services/Caching/`
- 采用 1 小时滑动过期策略，基于 `IMemoryCache` 实现
- 支持 `GetOrAddAsync` / `Set` / `Remove` / `TryGetValue` 方法

✅ **所有配置服务已统一使用**：
- `SystemConfigService`：使用 `ISlidingConfigCache`，更新后通过 `Set()` 立即刷新缓存
- `CommunicationConfigService`：使用 `ISlidingConfigCache`，更新后立即刷新
- `IoLinkageConfigService`：使用 `ISlidingConfigCache`，更新后立即刷新
- `LoggingConfigService`：使用 `ISlidingConfigCache`，更新后立即刷新
- `VendorConfigService`：使用 `ISlidingConfigCache`，管理 Driver/Sensor/WheelDiverter 三组配置，更新后立即刷新

✅ **无分散缓存实现**：
- 扫描 `src/` 目录未发现 `Cached*Repository`、`*OptionsProvider`、`*ConfigProvider` 等自带缓存的类型
- `Configuration.Persistence` 层的 LiteDB 仓储不包含任何 `MemoryCache` 或 `ConcurrentDictionary` 缓存逻辑
- `ConcurrentDictionary` 仅用于运行时状态跟踪（如 AlarmService、SimulationRunner），非配置缓存

✅ **测试已覆盖热更新**：
- `ConfigurationHotUpdateTests`：验证配置更新后立即生效
- 测试覆盖：CommunicationConfig 并发更新、重置、连接模式切换等场景

**解决方案 (PR-CONFIG-HOTRELOAD01)**：

1. **更新 RepositoryStructure.md**：
   - 在 6.1"单一权威实现表"新增"配置缓存/热更新管道"条目
   - 明确权威位置：`Application/Services/Caching/`（`ISlidingConfigCache`, `SlidingConfigCache`）
   - 明确禁止位置：Controller、Drivers、Execution、Ingress、Configuration.Persistence 等其它层
   - 禁止出现类型模式：`*ConfigCache`, `*OptionsProvider`, `*Cached*Repository`（正则匹配）

2. **更新 TechnicalDebtLog.md**：
   - 新增 TD-034 条目，状态：✅ 已解决
   - 说明：分散配置缓存实现已统一为 `ISlidingConfigCache`，无历史遗留债务

3. **更新 SYSTEM_CONFIG_GUIDE.md**：
   - 新增"配置热更新与缓存语义"章节
   - 说明 1 小时滑动缓存策略：首次读取访问 LiteDB，后续 1 小时内命中内存缓存
   - 说明配置更新语义：API PUT 后，先写持久化，再立即调用 `Set()` 刷新缓存，确保下一次读取必定是新值

4. **新增 ArchTests 防线测试**：
   - `TechnicalDebtComplianceTests.ConfigCacheShadowTests`：
     - 禁止在非 `Application/Services/Caching/` 位置出现 `*ConfigCache`, `*OptionsProvider`, `*Cached*Repository` 类型
     - 禁止在 `Configuration.Persistence` 层使用 `IMemoryCache` 或 `ConcurrentDictionary`（仅检测字段声明）
   - `SingleAuthorityCatalogTests`：
     - 自动解析 RepositoryStructure.md 6.1 表格中的"配置缓存"行
     - 验证权威类型存在于指定目录
     - 验证禁止位置没有匹配模式的类型

5. **文档更新**：
   - 在 README.md 配置章节增加缓存语义说明

**单一权威实现表条目 (6.1)**：

| 概念 | 权威类型 | 权威位置 | 禁止位置 | 测试防线 |
|------|---------|---------|---------|----------|
| 配置缓存/热更新管道 | `ISlidingConfigCache`<br/>`SlidingConfigCache` | `Application/Services/Caching/` | ❌ Configuration.Persistence 中自带缓存<br/>❌ Host/Controllers 中自定义缓存<br/>❌ Core/Execution/Drivers/Ingress 中实现配置缓存<br/>❌ 其他项目中定义 `*ConfigCache`, `*OptionsProvider`, `*Cached*Repository` | `ConfigCacheShadowTests`<br/>`SingleAuthorityCatalogTests` |

**新增防线测试**：

```csharp
// TechnicalDebtComplianceTests/ConfigCacheShadowTests.cs

[Fact]
public void ConfigCache_Should_Only_Exist_In_Application_Services_Caching()
{
    // 扫描所有项目，禁止在非权威位置出现匹配模式的类型
    var forbiddenPatterns = new[] { "*ConfigCache", "*OptionsProvider", "*Cached*Repository" };
    var allowedNamespace = "ZakYip.WheelDiverterSorter.Application.Services.Caching";
    
    // 排除允许的类型（ISlidingConfigCache, SlidingConfigCache, CachedSwitchingPathGenerator）
    var allowedTypes = new[] { "ISlidingConfigCache", "SlidingConfigCache", "CachedSwitchingPathGenerator" };
    
    var violations = FindTypesByPattern(forbiddenPatterns)
        .Where(t => !allowedTypes.Contains(t.Name))
        .Where(t => !t.Namespace.StartsWith(allowedNamespace))
        .ToList();
    
    Assert.Empty(violations); // 禁止影分身缓存实现
}

[Fact]
public void Configuration_Persistence_Should_Not_Have_Cache_Fields()
{
    var persistenceAssembly = typeof(LiteDbSystemConfigurationRepository).Assembly;
    
    var typesWithCacheFields = persistenceAssembly.GetTypes()
        .Where(t => t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(f => f.FieldType.Name.Contains("MemoryCache") || 
                      f.FieldType.Name.Contains("ConcurrentDictionary")))
        .ToList();
    
    Assert.Empty(typesWithCacheFields); // Persistence 层不应自带缓存
}
```

**配置热更新语义 (SYSTEM_CONFIG_GUIDE.md 新增章节)**：

### 配置热更新与缓存语义

系统采用统一的配置缓存机制 (`ISlidingConfigCache`)，确保配置读取高效且更新后立即生效。

#### 缓存策略

- **滑动过期时间**：1 小时
- **无绝对过期时间**：只要配置在被使用，缓存就会保持有效
- **缓存优先级**：高优先级 (CacheItemPriority.High)，减少内存压力时被淘汰的概率

#### 读取语义

- **首次读取**：访问 LiteDB 数据库，将配置加载到内存缓存
- **后续读取（1 小时内）**：直接从内存缓存返回，不访问数据库
- **性能优化**：命中缓存时返回 `Task.FromResult(cached)`，避免额外 Task 分配

#### 更新语义

- **写入顺序**：先写持久化 (LiteDB)，再刷新缓存 (`Set()`)
- **生效时间**：立即生效，下一次 `GetSystemConfig()` 等方法调用必定返回新值
- **无需重启**：配置更新不需要重启 Host 或重建 DI 容器

#### 适用范围

所有业务配置统一采用此缓存机制，包括：
- 系统配置 (`SystemConfiguration`)
- 通信配置 (`CommunicationConfiguration`)
- IO 联动配置 (`IoLinkageConfiguration`)
- 日志配置 (`LoggingConfiguration`)
- 厂商配置 (`DriverConfiguration`, `SensorConfiguration`, `WheelDiverterConfiguration`)

**变更影响**：
- 现有代码无需修改（已全部使用 `ISlidingConfigCache`）
- 新增架构测试防止未来出现影分身缓存实现
- 文档明确了配置热更新语义，便于运维和开发理解

---

## [TD-035] 上游通信协议完整性与驱动厂商可用性审计

**状态**：✅ 已解决 (当前 PR)

**问题描述**：
需要系统性审计所有上游通信协议和驱动厂商的实现完整性与可用性，并在文档中明确说明如何切换。

### 上游通信协议审计结果

**已实现的协议（6种客户端实现）**：

| 协议类型 | 实现类 | 状态 | 说明 |
|---------|-------|------|------|
| TCP (原生) | `TcpRuleEngineClient` | ⚠️ 已弃用 | 使用 .NET Socket 原生实现，已被 TouchSocket 替代 |
| TCP (TouchSocket) | `TouchSocketTcpRuleEngineClient` | ✅ 可用（默认） | 使用 TouchSocket 库，支持更好的性能和稳定性 |
| SignalR | `SignalRRuleEngineClient` | ✅ 可用 | 支持实时双向通信，适合 Web 集成场景 |
| MQTT | `MqttRuleEngineClient` | ✅ 可用 | 轻量级发布/订阅模式，适合物联网场景 |
| InMemory | `InMemoryRuleEngineClient` | ✅ 可用 | 内存模拟客户端，用于测试 |
| HTTP | - | ❌ 已移除 (PR-UPSTREAM01) | 不再支持 HTTP 协议 |

**工厂默认行为**（`UpstreamRoutingClientFactory`）：
- 配置 `Mode=Tcp` → 创建 `TouchSocketTcpRuleEngineClient`（默认）
- 配置 `Mode=SignalR` → 创建 `SignalRRuleEngineClient`
- 配置 `Mode=Mqtt` → 创建 `MqttRuleEngineClient`
- 无效配置 → 降级到 TCP (TouchSocket) 模式

**发现的问题**：
1. 存在两个 TCP 客户端实现（`TcpRuleEngineClient` 和 `TouchSocketTcpRuleEngineClient`），但工厂只使用 TouchSocket 版本
2. 原生 `TcpRuleEngineClient` 已事实上被弃用但未标记 `[Obsolete]`
3. 测试失败表明 Communication API 验证存在问题（18个测试失败）

### 驱动厂商审计结果

**已实现的厂商驱动（4种）**：

| 厂商 | 实现状态 | 核心驱动类 | 配置类 | 可用性 |
|------|---------|-----------|--------|--------|
| Leadshine（雷赛） | ✅ 完整 | `LeadshineWheelDiverterDriver`<br/>`LeadshineEmcController`<br/>`LeadshineConveyorSegmentDriver`<br/>`LeadshineIoLinkageDriver` | `LeadshineOptions`<br/>`LeadshineSensorOptions` | ✅ 生产可用 |
| Siemens（西门子） | ⚠️ 部分实现 | `S7WheelDiverterDriver` | `S7Options` | ⚠️ 仅摆轮驱动，缺少 EMC/传送带/联动 |
| ShuDiNiao（书迪鸟） | ⚠️ 部分实现 | `ShuDiNiaoWheelDiverterDriver`<br/>`ShuDiNiaoWheelDiverterDriverManager` | `ShuDiNiaoOptions` | ⚠️ 仅摆轮驱动，缺少 EMC/传送带/联动 |
| Simulated（仿真） | ✅ 完整 | `SimulatedWheelDiverterDevice`<br/>`SimulatedConveyorSegmentDriver`<br/>`SimulatedIoLinkageDriver` | `SimulatedOptions` | ✅ 测试/开发可用 |
| Modi（摩迪） | ❌ 缺失 | - | - | ❌ 文档中提及但未实现 |

**发现的问题**：
1. **Modi 厂商缺失**：`RepositoryStructure.md` 和 `README.md` 中提到 Modi 摆轮协议，但 `src/Drivers/Vendors/` 目录下不存在 Modi 实现
2. **Siemens/ShuDiNiao 实现不完整**：只有摆轮驱动，缺少：
   - EMC 控制器实现（`IEmcController`）
   - 传送带驱动（`IConveyorDriveController`）
   - IO 联动驱动（`IIoLinkageDriver`）
   - IO 端口实现（`IInputPort`/`IOutputPort`）
3. **配置选项未使用**：`ShuDiNiaoOptions` 和 `SimulatedOptions` 被标记为"可能未使用"（通过 `IOptions<T>` 绑定使用，但代码中无直接引用）

### 解决方案

#### 1. 上游通信协议清理与文档
- ✅ 确认 TouchSocket TCP 为默认实现
- ✅ 在 README.md 中添加协议切换方法说明
- ⚠️ 建议标记 `TcpRuleEngineClient` 为 `[Obsolete]` 或删除（后续 PR）
- ⚠️ 修复 Communication API 验证测试失败（后续 PR）

#### 2. 驱动厂商文档更新
- ✅ 更新 README.md 移除 Modi 引用
- ✅ 在文档中明确标注各厂商的实现完整性
- ✅ 添加驱动切换配置说明
- ⚠️ 建议完善 Siemens/ShuDiNiao 的其他驱动（后续 PR 或标记为待实现）

#### 3. 技术债文档更新
- ✅ 在 TechnicalDebtLog.md 中新增 TD-035 条目
- ✅ 在 RepositoryStructure.md 技术债索引中添加 TD-035 引用

### 文档变更列表

**README.md 新增章节**：
- "上游通信协议切换"：详细说明如何在 TCP/SignalR/MQTT 之间切换
- "驱动厂商切换"：详细说明如何配置和切换不同厂商的驱动
- "已知限制"：明确标注各厂商驱动的实现范围

**移除的误导信息**：
- ❌ Modi 摆轮协议引用（不存在的实现）
- ❌ 所有厂商都"完整可用"的误导性表述

---

**文档版本**：1.5 (TD-035)  
**最后更新**：2025-12-04  
**维护团队**：ZakYip Development Team
