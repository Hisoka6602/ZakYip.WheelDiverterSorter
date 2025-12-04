# ZakYip.WheelDiverterSorter

[![.NET Build and Test](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/Hisoka6602/ZakYip.WheelDiverterSorter/branch/main/graph/badge.svg)](https://codecov.io/gh/Hisoka6602/ZakYip.WheelDiverterSorter)

直线摆轮分拣系统 - 基于方向控制的包裹自动分拣解决方案

## 系统概述

基于直线摆轮（Wheel Diverter）的包裹自动分拣系统。包裹通过传感器检测进入系统，在输送线上单向移动，经过配置的摆轮节点时，根据转向方向（左/右/直行）分流到目标格口。

### 系统拓扑

```
              格口B(右转)  格口D(右转)  格口F(右转)
                   ↑           ↑           ↑
入口传感器 ──▶ [摆轮D1] ──▶ [摆轮D2] ──▶ [摆轮D3] ──▶ 末端(异常口999)
    │              ↓           ↓           ↓
    ▼         格口A(左转)  格口C(左转)  格口E(左转)
 创建包裹
```

**说明**：
- 格口分布在摆轮两侧（图中上侧/下侧，对应配置中的 Right/Left）
- 每个摆轮前有感应传感器（FrontSensor）检测包裹到达
- 摆轮支持三个方向：左转、右转、直行
- 包裹沿输送线单向移动，无法后退
- 未分拣的包裹最终到达末端异常格口

### 分拣流程

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ 1. 包裹检测   │────▶│ 2. 格口分配   │────▶│ 3. 路径生成   │
│ 入口传感器触发 │     │ 上游异步推送  │     │ 查询拓扑配置  │
│ 创建包裹实体  │     │ 或固定/轮询   │     │              │
└──────────────┘     └──────────────┘     └──────────────┘
                                                │
                                                ▼
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ 6. 完成分拣   │◀────│ 5. 确认落格   │◀────│ 4. 路径执行   │
│ 记录结果     │     │ 格口传感器确认 │     │ 控制摆轮转向  │
└──────────────┘     └──────────────┘     └──────────────┘
```

**步骤说明**：
- **步骤 2（格口分配）**：系统发送检测通知后不等待，上游 RuleEngine 异步推送格口分配。详见 [上游通信模型](#上游通信数据结构)。
- **异常处理**：任意步骤失败（超时/设备异常/连接失败）→ 路由到异常格口

### 包裹超时与丢失判定

> **详细协议字段与时序请参考** [docs/guides/UPSTREAM_CONNECTION_GUIDE.md](docs/guides/UPSTREAM_CONNECTION_GUIDE.md)

系统基于输送线长度和速度自动计算超时时间：

- **分配超时**：包裹检测后超时未收到格口分配 → 标记为 `Timeout`，路由到异常格口
- **落格超时**：格口分配后超时未确认落格 → 标记为 `Timeout`，路由到异常格口
- **包裹丢失**：超过最大存活时间且无法定位 → 标记为 `Lost`，从缓存清除

> **超时 vs 丢失的区别**：
> - **超时**：包裹仍在输送线上，可以导向异常口
> - **丢失**：包裹已不在输送线上，无法导向异常口，必须从缓存清除

### 核心特点

- ✅ 方向控制模式（左/右/直行）
- ✅ 传感器驱动，实时跟踪包裹位置
- ✅ LiteDB 动态配置，支持运行时热更新
- ✅ 多协议通信（TCP/SignalR/MQTT）
- ✅ 完整异常处理，自动路由到异常格口
- ✅ 三种分拣模式（正式/指定落格/循环落格）
- ✅ 多厂商硬件支持（雷赛/西门子/数递鸟/仿真）

### 系统架构

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Host (ASP.NET Core)                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │ Controllers │  │ StateMachine│  │   Workers   │  │    Swagger/API      │ │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └─────────────────────┘ │
└─────────┼────────────────┼────────────────┼─────────────────────────────────┘
          │                │                │
          ▼                ▼                ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Application (DI 聚合层)                              │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐                 │
│  │ Config Service │  │ Sorting Service│  │ Health Service │                 │
│  └────────────────┘  └────────────────┘  └────────────────┘                 │
└───────────────────────────────┬─────────────────────────────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
        ▼                       ▼                       ▼
┌───────────────┐      ┌───────────────┐      ┌───────────────────────────────┐
│   Execution   │      │    Ingress    │      │        Infrastructure         │
│ ┌───────────┐ │      │ ┌───────────┐ │      │  ┌─────────────────────────┐  │
│ │Orchestrator│ │      │ │  Sensors  │ │      │  │    Communication        │  │
│ │  Pipeline │ │      │ │ Detection │ │      │  │  (TCP/SignalR/MQTT)     │  │
│ └───────────┘ │      │ └───────────┘ │      │  └─────────────────────────┘  │
└───────┬───────┘      └───────────────┘      │  ┌─────────────────────────┐  │
        │                                      │  │ Config.Persistence     │  │
        ▼                                      │  │     (LiteDB)           │  │
┌───────────────┐      ┌───────────────┐      │  └─────────────────────────┘  │
│    Drivers    │      │  Simulation   │      └───────────────────────────────┘
│ ┌───────────┐ │      │ ┌───────────┐ │
│ │ Leadshine │ │      │ │ Scenarios │ │
│ │ ShuDiNiao │ │      │ │  Runner   │ │
│ │ Siemens   │ │      │ └───────────┘ │
│ │ Simulated │ │      └───────────────┘
│ └───────────┘ │
└───────────────┘
        │
        ▼
┌───────────────────────────────────────────────────────────────────────────┐
│                            Core (领域模型)                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │   Hardware   │  │   LineModel  │  │   Sorting    │  │  Abstractions│   │
│  │  (HAL 抽象)  │  │ (配置/拓扑)  │  │  (分拣逻辑)  │  │  (上游接口)  │   │
│  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
└───────────────────────────────────────────────────────────────────────────┘
```

**依赖方向**：Host → Application → (Execution/Drivers/Ingress/Infrastructure/Simulation) → Core

## 拓扑分拣路径演示

> 本节提供一个完整的 3 摆轮 6 格口分拣场景演示，展示包裹从检测到落格的完整过程，包括时序、通信和摆轮控制细节。

### 场景设定

**拓扑配置**：3 摆轮 6 格口 + 1 异常口
```
        格口2(右)   格口4(右)   格口6(右)
            ↑         ↑         ↑
入口S0 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 异常口999
   ↓        ↓         ↓         ↓
创建包裹  格口1(左)  格口3(左)  格口5(左)
```

**线体假设**：
- **线体 A**（入口到摆轮 D1）：长度 2.0m，速度 1.0m/s → 行程时间 2.0s
- **线体 B**（摆轮 D1 到 D2）：长度 1.5m，速度 1.0m/s → 行程时间 1.5s
- **线体 C**（摆轮 D2 到 D3）：长度 1.5m，速度 1.0m/s → 行程时间 1.5s
- **摆轮切换时间**：0.3s（摆轮从接收命令到完成转向）

**包裹示例**：
- **包裹 P001**：目标格口 4（右转，位于摆轮 D2）
- **ParcelId**：`P001_20251204162530`（包裹 ID + 时间戳）

### 完整流程时序

#### 时刻 T0 (00:00.000) - 包裹检测

**1. 入口传感器触发**
```
入口传感器 S0 检测到包裹 → 触发事件
```

**2. 系统创建包裹实体**
```csharp
Parcel parcel = new()
{
    ParcelId = "P001_20251204162530",
    CreatedAt = DateTime.Now,  // 2025-12-04 16:25:30.000
    Status = ParcelStatus.Created
};
```

**3. 发送检测通知到上游（Fire-and-Forget）**

系统通过 TCP（TouchSocket）发送 `ParcelDetectionNotification` 到上游 RuleEngine：

```json
{
  "messageType": "ParcelDetectionNotification",
  "parcelId": "P001_20251204162530",
  "detectedAt": "2025-12-04T16:25:30.000+08:00",
  "sensorId": "S0",
  "sequenceNumber": 1001
}
```

**系统行为**：发送后**不等待**响应，继续执行后续逻辑。

---

#### 时刻 T1 (00:00.500) - 上游推送格口分配

**上游 RuleEngine 异步推送格口分配通知**

上游通过 TCP 推送 `ChuteAssignmentNotification`：

```json
{
  "messageType": "ChuteAssignmentNotification",
  "parcelId": "P001_20251204162530",
  "assignedChuteId": 4,
  "assignedAt": "2025-12-04T16:25:30.500+08:00",
  "priority": "Normal"
}
```

**系统处理**：
1. 接收格口分配，更新包裹状态：
   ```csharp
   parcel.TargetChuteId = 4;
   parcel.Status = ParcelStatus.Assigned;
   ```

2. 生成分拣路径（查询拓扑配置）：
   ```csharp
   SwitchingPath path = pathGenerator.GeneratePath(chuteId: 4);
   // 结果：
   // - 摆轮 D1: Straight（直通）
   // - 摆轮 D2: Right（右转，目标格口）
   // - 摆轮 D3: Straight（包裹已分走）
   ```

---

#### 时刻 T2 (00:02.000) - 包裹到达摆轮 D1

**1. 摆轮 D1 前置传感器触发**
```
传感器 S1 检测到包裹到达摆轮 D1
```

**2. 系统发送摆轮 D1 控制命令（数递鸟协议）**

系统根据路径段 `D1: Straight`，发送数递鸟 Modbus TCP 命令：

**命令格式**：
- **功能码**：0x06（写单个寄存器）
- **寄存器地址**：40001（摆轮 D1 方向寄存器）
- **值**：0（直通）
  - 0 = 直通
  - 1 = 左转（45°）
  - 2 = 右转（-45°）

**Modbus TCP 请求帧**：
```
00 01  // 事务标识符
00 00  // 协议标识符（Modbus）
00 06  // 长度（6 字节）
01     // 单元标识符（摆轮 D1）
06     // 功能码（写单个寄存器）
9C 41  // 寄存器地址（40001）
00 00  // 值（0 = 直通）
```

**摆轮 D1 响应**：
```
00 01  // 事务标识符（回显）
00 00  // 协议标识符
00 06  // 长度
01     // 单元标识符
06     // 功能码
9C 41  // 寄存器地址
00 00  // 值（确认）
```

**耗时**：通信 + 摆轮切换 = 0.05s + 0.3s = 0.35s

---

#### 时刻 T3 (00:03.500) - 包裹到达摆轮 D2（目标摆轮）

**1. 摆轮 D2 前置传感器触发**
```
传感器 S2 检测到包裹到达摆轮 D2
```

**2. 系统发送摆轮 D2 控制命令（数递鸟协议）**

系统根据路径段 `D2: Right`，发送数递鸟 Modbus TCP 命令：

**Modbus TCP 请求帧**：
```
00 02  // 事务标识符
00 00  // 协议标识符
00 06  // 长度
02     // 单元标识符（摆轮 D2）
06     // 功能码
9C 41  // 寄存器地址（40001）
00 02  // 值（2 = 右转）
```

**摆轮 D2 响应**：
```
00 02  // 事务标识符（回显）
00 00  // 协议标识符
00 06  // 长度
02     // 单元标识符
06     // 功能码
9C 41  // 寄存器地址
00 02  // 值（确认右转）
```

**摆轮 D2 执行右转**：摆轮从 0° 转向 -45°，包裹被导向右侧格口 4。

**耗时**：通信 + 摆轮切换 = 0.05s + 0.3s = 0.35s

---

#### 时刻 T4 (00:04.200) - 包裹落入目标格口

**1. 格口 4 传感器触发**
```
格口 4 传感器 S4 检测到包裹落格 → 触发落格确认事件
```

**2. 系统更新包裹状态**
```csharp
parcel.Status = ParcelStatus.Completed;
parcel.ActualChuteId = 4;
parcel.CompletedAt = DateTime.Now;  // 2025-12-04 16:25:34.200
```

**3. 发送分拣完成通知到上游**

系统通过 TCP 发送 `SortingCompletedNotification`：

```json
{
  "messageType": "SortingCompletedNotification",
  "parcelId": "P001_20251204162530",
  "targetChuteId": 4,
  "actualChuteId": 4,
  "finalStatus": "Success",
  "completedAt": "2025-12-04T16:25:34.200+08:00",
  "totalDurationMs": 4200
}
```

**分拣完成！总耗时：4.2 秒**

---

### 各部分耗时汇总

| 阶段 | 起止时刻 | 耗时 | 说明 |
|------|---------|------|------|
| 包裹检测 → 上游分配 | T0 → T1 | 0.5s | 上游 RuleEngine 决策时间 |
| 上游分配 → 到达 D1 | T1 → T2 | 1.5s | 线体 A 行程时间（2.0s - 0.5s） |
| 到达 D1 → 到达 D2 | T2 → T3 | 1.5s | 线体 B 行程时间 |
| 到达 D2 → 落格确认 | T3 → T4 | 0.7s | 摆轮 D2 切换（0.35s）+ 格口行程（0.35s） |
| **总计** | T0 → T4 | **4.2s** | 从检测到落格的完整时间 |

### 异常场景示例

**场景 1：上游超时（未收到格口分配）**

如果在 T0 + 5s（配置的超时时间）后仍未收到上游格口分配，系统会：
1. 标记包裹为 `Timeout`
2. 自动生成异常格口路径（所有摆轮 `Straight`）
3. 包裹最终流向异常口 999

**场景 2：摆轮通信失败**

如果在 T2 时刻发送摆轮 D1 命令失败（3 次重试后）：
1. 记录错误日志
2. 包裹继续流向下游（摆轮可能保持上一状态）
3. 发送 `SortingCompletedNotification` 时 `finalStatus = "DeviceError"`

**场景 3：落格超时（格口传感器无响应）**

如果在 T3 + 3s（配置的落格超时）后仍未检测到格口确认：
1. 标记包裹为 `Timeout`
2. 假设包裹丢失或传感器故障
3. 发送 `finalStatus = "Timeout"` 通知到上游

### 关键配置参数

```json
{
  "ChuteAssignmentTimeout": {
    "timeoutMs": 5000,
    "comment": "格口分配超时时间（上游响应最大等待时间）"
  },
  "ChuteDropTimeout": {
    "timeoutMs": 3000,
    "comment": "落格确认超时时间（格口传感器最大等待时间）"
  },
  "ConveyorSpeed": {
    "metersPerSecond": 1.0,
    "comment": "输送线速度"
  },
  "DiverterSwitchTime": {
    "milliseconds": 300,
    "comment": "摆轮切换时间（接收命令到完成转向）"
  }
}
```

### 多包裹并发处理

系统支持多包裹同时在线分拣，每个包裹独立跟踪：

```
T0: 包裹 P001 检测 → 开始流程
T2: 包裹 P002 检测 → 开始流程（与 P001 并行）
T2: 包裹 P001 到达 D1 → 摆轮 D1 切换为 P001 的目标方向
T4: 包裹 P002 到达 D1 → 摆轮 D1 切换为 P002 的目标方向
T4: 包裹 P001 落格完成
T6: 包裹 P002 落格完成
```

**并发约束**：
- 同一摆轮在同一时刻只能服务一个包裹
- 包裹间距必须大于摆轮切换时间（0.3s）以避免冲突
- 系统通过包裹缓存队列管理并发包裹状态

---

**相关文档**：
- 完整拓扑模型：[docs/TOPOLOGY_LINEAR_N_DIVERTERS.md](docs/TOPOLOGY_LINEAR_N_DIVERTERS.md)
- 上游通信协议：[docs/guides/UPSTREAM_CONNECTION_GUIDE.md](docs/guides/UPSTREAM_CONNECTION_GUIDE.md)

## 项目结构

```
src/
├── Host/               # ASP.NET Core 宿主应用（API、后台服务、状态机）
├── Application/        # 应用服务层，DI 聚合入口
├── Core/               # 核心领域模型、配置仓储接口、HAL 抽象
├── Execution/          # 分拣执行管线、路径执行、SortingOrchestrator
├── Drivers/            # 硬件驱动（雷赛/西门子/书迪鸟/仿真）
├── Ingress/            # 传感器管理、包裹检测
├── Infrastructure/     # 基础设施层
│   ├── Communication/              # 上游通信（TCP/SignalR/MQTT）
│   └── Configuration.Persistence/  # LiteDB 配置持久化
├── Observability/      # 监控指标、日志、告警、安全执行服务
├── Simulation/         # 仿真服务库
│   ├── Simulation/     # 仿真服务库（Library）
│   ├── Simulation.Cli/ # 仿真命令行入口（Exe）
│   └── Simulation.Scenarios/  # 仿真场景定义
└── Analyzers/          # Roslyn 代码分析器

tests/                  # 测试项目
├── Core.Tests/         # 核心层单元测试
├── Execution.Tests/    # 执行层单元测试
├── Drivers.Tests/      # 驱动层单元测试
├── Ingress.Tests/      # 入口层单元测试
├── Communication.Tests/# 通信层单元测试
├── Observability.Tests/# 可观测性层单元测试
├── Host.Application.Tests/  # 应用服务单元测试
├── Host.IntegrationTests/   # 主机集成测试
├── E2ETests/           # 端到端测试
├── ArchTests/          # 架构合规性测试
├── TechnicalDebtComplianceTests/  # 技术债合规性测试
└── Benchmarks/         # 性能基准测试

tools/                  # 工具项目
├── Reporting/          # 仿真报告分析工具
├── SafeExecutionStats/ # SafeExecution 统计工具
└── Profiling/          # 性能剖析脚本

monitoring/             # Prometheus/Grafana 配置
```

## 快速开始

### 运行项目

```bash
cd src/Host/ZakYip.WheelDiverterSorter.Host
dotnet run
```

默认监听端口：5000（HTTP），访问 Swagger UI：http://localhost:5000/swagger

### 运行测试

```bash
dotnet test
```

### 生产环境部署

```bash
dotnet publish src/Host/ZakYip.WheelDiverterSorter.Host -c Release -o out/host
cd out/host
DOTNET_ENVIRONMENT=Production ASPNETCORE_URLS=http://0.0.0.0:5000 ./ZakYip.WheelDiverterSorter.Host
```

## API 概览

### 系统与健康检查

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/system/status` | GET | 系统状态查询（支持高并发） |
| `/api/system/restart` | POST | 系统重启 |
| `/health/ready` | GET | 就绪状态检查（Kubernetes readiness probe） |
| `/health/prerun` | GET | 运行前健康检查 |
| `/health/drivers` | GET | 驱动健康状态检查 |

### 配置管理

所有业务配置统一采用 **1 小时滑动缓存** + **热更新机制**：
- **读取性能**：首次访问 LiteDB，后续 1 小时内从内存缓存返回（100-500 倍性能提升）
- **更新语义**：PUT 请求更新配置后立即生效，无需重启
- **缓存策略**：滑动过期（持续使用则持续有效），高优先级（不易被淘汰）

详见：[系统配置指南](docs/guides/SYSTEM_CONFIG_GUIDE.md)

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/config/system` | GET/PUT | 系统配置（分拣模式、异常格口等） |
| `/api/config/system/sorting-mode` | GET/PUT | 分拣模式配置 |
| `/api/config/communication` | GET/PUT | 上游通信配置 |
| `/api/config/chute-path-topology` | GET/PUT | 格口路径拓扑配置 |
| `/api/config/chute-assignment-timeout` | GET/PUT | 格口分配超时配置 |
| `/api/config/io-linkage` | GET/PUT | IO 联动配置 |
| `/api/config/panel` | GET/PUT | 面板配置 |
| `/api/config/logging` | GET/PUT | 日志配置 |
| `/api/config/simulation` | GET/PUT | 仿真配置 |

### 硬件配置

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/hardware/leadshine` | GET/PUT | 雷赛 IO 卡配置 |
| `/api/hardware/leadshine/sensors` | GET/PUT | 雷赛传感器配置 |
| `/api/hardware/shudiniao` | GET/PUT | 数递鸟摆轮配置 |

### 业务操作

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/diverts/change-chute` | POST | 改口操作 |
| `/api/alarms` | GET | 获取告警列表 |
| `/api/alarms/acknowledge` | POST | 确认告警 |
| `/api/policy/exception-routing` | GET/PUT | 异常路由策略 |
| `/api/policy/overload` | GET/PUT | 超载策略 |

### 通信与仿真

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/communication/status` | GET | 通信状态查询 |
| `/api/communication/test` | POST | 通信测试 |
| `/api/simulation/run-scenario-e` | POST | 运行仿真场景 |
| `/api/simulation/status` | GET | 仿真状态查询 |
| `/api/simulation/panel/*` | POST | 面板仿真操作 |

## 分拣模式

| 模式 | 说明 | 使用场景 |
|------|------|----------|
| Formal | 与上游 RuleEngine 集成 | 生产环境 |
| FixedChute | 所有包裹发送到固定格口 | 调试测试 |
| RoundRobin | 按配置列表循环分配 | 均匀分布测试 |

## 上游通信概述

系统与上游 RuleEngine 采用 **Fire-and-Forget** 异步通信模式。支持多种协议（TCP/SignalR/MQTT），默认使用 TCP。

> **详细协议说明**：字段定义、示例 JSON、时序图、超时/丢失规则请参考 [上游连接配置指南](docs/guides/UPSTREAM_CONNECTION_GUIDE.md)

### 通信流程

```
┌──────────────────┐                      ┌──────────────────┐
│   分拣系统        │                      │   RuleEngine     │
│  (WheelDiverter) │                      │   (上游系统)      │
└────────┬─────────┘                      └────────┬─────────┘
         │                                         │
         │  1. ParcelDetectionNotification         │
         │  ─────────────────────────────────────▶ │
         │  (检测通知: 仅通知，不等待)              │
         │                                         │
         │  2. ChuteAssignmentNotification         │
         │  ◀───────────────────────────────────── │
         │  (格口分配: 上游异步推送)               │
         │                                         │
         │  3. SortingCompletedNotification        │
         │  ─────────────────────────────────────▶ │
         │  (落格完成: FinalStatus=Success/Timeout/Lost)
         │                                         │
```

**关键点**：
- 系统发送检测通知后**不等待**格口分配，继续执行后续逻辑
- 格口分配由上游异步推送，通过事件接收
- 所有通知都是 fire-and-forget 模式

### 支持的协议

| 协议 | 实现类 | 状态 | 使用场景 |
|------|--------|------|----------|
| TCP (默认) | `TouchSocketTcpRuleEngineClient` | ✅ 推荐 | 生产环境、高性能、低延迟 |
| SignalR | `SignalRRuleEngineClient` | ✅ 可用 | Web 集成、实时双向通信 |
| MQTT | `MqttRuleEngineClient` | ✅ 可用 | 物联网场景、轻量级发布/订阅 |
| InMemory | `InMemoryRuleEngineClient` | ✅ 测试用 | 单元测试、集成测试 |

> **注意**：HTTP 协议支持已移除 (PR-UPSTREAM01)。系统使用 TouchSocket 实现的 TCP 客户端作为默认通信方式。

### 协议切换方法

通过配置文件或 API 动态切换通信协议，支持热更新（无需重启）：

#### 方法1：修改 appsettings.json

```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",           // 协议类型: Tcp, SignalR, Mqtt
    "ConnectionMode": "Client",  // Client 或 Server 模式
    "TcpServer": "192.168.1.100:9000",  // TCP 服务器地址
    "EnableAutoReconnect": true,
    "TimeoutMs": 5000
  }
}
```

**协议配置说明**：
- **Tcp**: `"Mode": "Tcp"`, 需配置 `TcpServer` (地址:端口)
- **SignalR**: `"Mode": "SignalR"`, 需配置 `SignalRHub` (URL)
- **MQTT**: `"Mode": "Mqtt"`, 需配置 `MqttBroker` (地址)

#### 方法2：通过 API 动态切换

```http
PUT /api/config/communication
Content-Type: application/json

{
  "mode": "SignalR",
  "connectionMode": "Client",
  "signalRHub": "https://ruleengine.example.com/sortingHub"
}
```

**生效时间**：配置更新后立即生效，系统会自动断开旧连接并使用新配置重新连接。

详细配置说明请参考：[上游连接配置指南](docs/guides/UPSTREAM_CONNECTION_GUIDE.md)

## 硬件驱动支持

系统支持多种硬件厂商的设备，所有厂商实现位于 `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/` 目录。

### 厂商驱动完整性

| 厂商 | 摆轮驱动 | EMC控制 | 传送带 | IO联动 | 传感器 | 整体状态 |
|------|---------|---------|--------|--------|--------|---------|
| Leadshine（雷赛） | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ **生产可用** |
| Siemens（西门子） | ✅ | ❌ | ❌ | ❌ | ❌ | ⚠️ **部分可用** |
| ShuDiNiao（书迪鸟） | ✅ | ❌ | ❌ | ❌ | ❌ | ⚠️ **部分可用** |
| Simulated（仿真） | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ **测试可用** |

**说明**：
- ✅ **Leadshine（雷赛）**：功能完整，适合生产环境使用
- ⚠️ **Siemens/ShuDiNiao**：仅实现摆轮驱动，其他功能需要使用 Leadshine 或 Simulated 补充
- ✅ **Simulated（仿真）**：完整实现所有功能，适合开发测试

### 驱动切换方法

通过配置文件指定使用的硬件厂商：

#### 方法1：修改 appsettings.json

```json
{
  "Leadshine": {
    "CardIndex": 0,
    "IpAddress": "192.168.1.10",
    "Port": 502
  },
  "VendorProfile": "Leadshine"  // 指定使用的厂商
}
```

#### 方法2：通过 API 动态切换

```http
PUT /api/hardware/leadshine
Content-Type: application/json

{
  "cardIndex": 0,
  "ipAddress": "192.168.1.10",
  "port": 502
}
```

**支持的厂商配置**：
- `Leadshine`: 雷赛 IO 卡 (生产环境推荐)
- `ShuDiNiao`: 书迪鸟摆轮控制器 (仅摆轮)
- `Simulated`: 仿真模式 (测试开发)

> **混合使用**：可以配置多个厂商的驱动，系统会根据设备类型选择对应的实现。例如：摆轮使用 ShuDiNiao，传感器和 IO 使用 Leadshine。

## 系统状态与三色灯联动

系统定义了 6 种核心状态，通过状态机严格管理状态转换。每个状态对应不同的三色灯显示和蜂鸣器行为。

### 系统状态定义

| 状态代码 | 中文名称 | 说明 | 允许的操作 |
|---------|---------|------|-----------|
| `Booting` | 启动中 | 系统正在启动和初始化 | 等待初始化完成 |
| `Ready` | 就绪 | 系统已就绪，可以开始运行 | 启动运行 |
| `Running` | 运行中 | 系统正常运行，执行分拣任务 | 暂停、停止 |
| `Paused` | 暂停 | 系统已暂停，可恢复运行 | 恢复运行、停止 |
| `Faulted` | 故障 | 系统发生故障，需要处理 | 故障复位 |
| `EmergencyStop` | 急停 | 触发急停按钮，系统紧急停止 | 急停复位 |

### 状态转换规则

```
                    ┌─────────────┐
                    │   Booting   │
                    │  (启动中)    │
                    └──────┬──────┘
                           │ 初始化完成
                           ▼
                    ┌─────────────┐
            ┌──────▶│    Ready    │◀──────┐
            │       │   (就绪)     │       │
            │       └──────┬──────┘       │
            │              │ 启动         │
            │              ▼              │
            │       ┌─────────────┐       │
            │       │   Running   │       │
            │       │  (运行中)    │       │
            │       └──────┬──────┘       │
            │              │              │
        停止│         暂停 │ 恢复         │ 故障复位
            │              ▼              │
            │       ┌─────────────┐       │
            │       │   Paused    │       │
            │       │   (暂停)     │       │
            │       └─────────────┘       │
            │                             │
            └──────────────┬──────────────┘
                           │
                     故障  │  急停
                           ▼
                    ┌─────────────┐       ┌─────────────┐
                    │   Faulted   │       │ EmergencyStop│
                    │   (故障)     │       │   (急停)     │
                    └─────────────┘       └─────────────┘
                           │                      │
                           └──────────────────────┘
                                  复位后 → Ready
```

### 状态与三色灯/蜂鸣器联动

系统通过 IO 联动机制自动控制三色灯和蜂鸣器，可通过 `/api/config/io-linkage` 配置：

| 系统状态 | 红灯 | 黄灯 | 绿灯 | 蜂鸣器 | 说明 |
|---------|------|------|------|--------|------|
| Booting | 🔴 常亮 | ⚫ 熄灭 | ⚫ 熄灭 | 🔕 静音 | 系统初始化中 |
| Ready | ⚫ 熄灭 | 🟡 常亮 | ⚫ 熄灭 | 🔕 静音 | 等待启动命令 |
| Running | ⚫ 熄灭 | ⚫ 熄灭 | 🟢 常亮 | 🔕 静音 | 正常分拣运行 |
| Paused | ⚫ 熄灭 | 🟡 闪烁 | ⚫ 熄灭 | 🔕 静音 | 暂停状态 |
| Faulted | 🔴 闪烁 | ⚫ 熄灭 | ⚫ 熄灭 | 🔔 间歇鸣叫 | 设备故障 |
| EmergencyStop | 🔴 常亮 | 🟡 常亮 | ⚫ 熄灭 | 🔔 持续鸣叫 | 紧急停止 |

**配置说明**：
- 上表为典型配置，实际行为可通过 `IoLinkageOptions` 自定义
- 支持为每个状态配置多个 IO 联动点（如中段皮带、入口门等）
- 三色灯控制通过 `ISignalTowerOutput` 接口实现
- 闪烁间隔、持续时长等参数可配置

**相关 API**：
- `GET /api/system/status` - 查询当前系统状态
- `GET /api/config/io-linkage` - 查询 IO 联动配置
- `PUT /api/config/io-linkage` - 更新 IO 联动配置

> **重要提示**：任何状态的增删改都必须同步更新本文档，这是硬性要求。

---

## 数递鸟（ShuDiNiao）摆轮通信协议

数递鸟摆轮采用固定 7 字节的 TCP 通信协议，支持双向通信。

### 协议帧结构

```
┌──────┬──────┬──────┬──────┬──────┬──────┬──────┐
│Byte 0│Byte 1│Byte 2│Byte 3│Byte 4│Byte 5│Byte 6│
├──────┼──────┼──────┼──────┼──────┼──────┼──────┤
│ 0x51 │ 0x52 │ 0x57 │Device│ Type │ Data │ 0xFE │
│起始1 │起始2 │长度  │地址  │消息类型│数据 │结束 │
└──────┴──────┴──────┴──────┴──────┴──────┴──────┘
```

| 字节位置 | 名称 | 值 | 说明 |
|---------|------|-----|------|
| Byte 0 | 起始字节1 | `0x51` | 固定值 |
| Byte 1 | 起始字节2 | `0x52` | 固定值 |
| Byte 2 | 长度字节 | `0x57` | 固定值（7字节） |
| Byte 3 | 设备地址 | `0x51`, `0x52`, ... | 标识设备编号 |
| Byte 4 | 消息类型 | `0x51`/`0x52`/`0x53` | 见消息类型表 |
| Byte 5 | 数据字节 | 根据消息类型 | 状态码或命令码 |
| Byte 6 | 结束字符 | `0xFE` | 固定值 |

### 消息类型（Byte 4）

| 代码 | 名称 | 方向 | 说明 |
|------|------|------|------|
| `0x51` | DeviceStatus | 设备 → 服务端 | 设备状态上报（信息一） |
| `0x52` | ControlCommand | 服务端 → 设备 | 控制命令（信息二） |
| `0x53` | ResponseAndCompletion | 设备 → 服务端 | 应答与完成（信息三） |

### 信息一：设备状态上报（设备 → 服务端）

设备主动上报当前状态，用于实时监控设备健康状况。

**报文格式**（Byte 5 为状态码）：

| 状态码 (Byte 5) | 状态名称 | 说明 |
|----------------|----------|------|
| `0x50` | Standby (待机) | 设备已上电但未运行 |
| `0x51` | Running (运行) | 设备正常运行中 |
| `0x52` | EmergencyStop (急停) | 设备触发急停 |
| `0x53` | Fault (故障) | 设备发生故障 |

**示例报文**：
```
设备地址 0x51 上报运行状态：
51 52 57 51 51 51 FE
│  │  │  │  │  │  └─ 结束字符
│  │  │  │  │  └──── 状态码：Running (0x51)
│  │  │  │  └─────── 消息类型：DeviceStatus (0x51)
│  │  │  └────────── 设备地址 (0x51)
│  │  └───────────── 长度 (0x57)
│  └──────────────── 起始字节2
└─────────────────── 起始字节1
```

### 信息二：控制命令（服务端 → 设备）

服务端向设备发送控制指令，控制摆轮转向和运行状态。

**报文格式**（Byte 5 为命令码）：

| 命令码 (Byte 5) | 命令名称 | 说明 |
|----------------|----------|------|
| `0x51` | Run (运行) | 启动设备运行 |
| `0x52` | Stop (停止) | 停止设备运行 |
| `0x53` | TurnLeft (左摆) | 摆轮向左转 |
| `0x54` | ReturnCenter (回中) | 摆轮回到中间位置（直通） |
| `0x55` | TurnRight (右摆) | 摆轮向右转 |

**示例报文**：
```
发送左摆命令到设备 0x51：
51 52 57 51 52 53 FE
│  │  │  │  │  │  └─ 结束字符
│  │  │  │  │  └──── 命令码：TurnLeft (0x53)
│  │  │  │  └─────── 消息类型：ControlCommand (0x52)
│  │  │  └────────── 设备地址 (0x51)
│  │  └───────────── 长度 (0x57)
│  └──────────────── 起始字节2
└─────────────────── 起始字节1
```

### 信息三：应答与完成（设备 → 服务端）

设备响应控制命令，包括命令应答和动作完成两类消息。

**报文格式**（Byte 5 为应答/完成码）：

| 应答码 (Byte 5) | 名称 | 类型 | 说明 |
|----------------|------|------|------|
| `0x51` | RunAck | 应答 | 运行命令已接收 |
| `0x52` | StopAck | 应答 | 停止命令已接收 |
| `0x53` | TurnLeftAck | 应答 | 左摆命令已接收 |
| `0x54` | ReturnCenterAck | 应答 | 回中命令已接收 |
| `0x55` | TurnRightAck | 应答 | 右摆命令已接收 |
| `0x56` | TurnLeftComplete | 完成 | 左摆动作已完成 |
| `0x57` | ReturnCenterComplete | 完成 | 回中动作已完成 |
| `0x58` | TurnRightComplete | 完成 | 右摆动作已完成 |

**示例报文**：
```
设备 0x51 应答左摆命令：
51 52 57 51 53 53 FE
│  │  │  │  │  │  └─ 结束字符
│  │  │  │  │  └──── 应答码：TurnLeftAck (0x53)
│  │  │  │  └─────── 消息类型：ResponseAndCompletion (0x53)
│  │  │  └────────── 设备地址 (0x51)
│  │  └───────────── 长度 (0x57)
│  └──────────────── 起始字节2
└─────────────────── 起始字符1

设备 0x51 上报左摆完成：
51 52 57 51 53 56 FE
                │  └─ 完成码：TurnLeftComplete (0x56)
```

### 心跳机制

数递鸟设备通过**周期性状态上报**实现心跳检测：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| 状态上报周期 | 1000ms | 设备每秒主动上报一次状态（信息一） |
| 心跳超时阈值 | 5000ms | 超过 5 秒未收到状态上报，判定为设备离线 |
| 重连间隔 | 3000ms | TCP 连接断开后，每 3 秒尝试重连一次 |

**检测机制**：
1. 设备每 1 秒主动发送一次状态上报（信息一）
2. 服务端记录最后收到状态上报的时间戳
3. 若超过 5 秒未收到状态上报，标记设备为 `离线` 状态
4. 离线设备的所有控制命令将被拒绝
5. TCP 连接断开时，服务端每 3 秒尝试重新建立连接

**相关配置**：
- `ShuDiNiaoOptions.HeartbeatTimeoutMs` - 心跳超时时间（默认 5000ms）
- `ShuDiNiaoOptions.ReconnectIntervalMs` - 重连间隔（默认 3000ms）

### 故障机制

数递鸟驱动实现了完善的故障检测和恢复机制：

#### 1. 连接故障

| 故障类型 | 检测方式 | 处理策略 |
|---------|---------|----------|
| TCP 连接断开 | Socket 异常 | 每 3 秒自动重连，记录错误日志 |
| 连接超时 | 连接建立超过 5 秒 | 放弃本次连接，等待下一轮重连 |

#### 2. 通信故障

| 故障类型 | 检测方式 | 处理策略 |
|---------|---------|----------|
| 心跳超时 | 超过 5 秒无状态上报 | 标记设备离线，拒绝控制命令 |
| 帧格式错误 | 起始/结束字节不匹配 | 丢弃错误帧，记录警告日志 |
| 数据校验失败 | 枚举值不合法 | 丢弃错误帧，记录警告日志 |
| 应答超时 | 发送命令后 3 秒内无应答 | 标记命令失败，返回超时错误 |

#### 3. 设备故障

| 故障类型 | 检测方式 | 处理策略 |
|---------|---------|----------|
| 设备上报故障状态 | 状态码 = 0x53 (Fault) | 记录设备故障，停止发送控制命令 |
| 设备急停 | 状态码 = 0x52 (EmergencyStop) | 记录急停事件，等待设备复位 |
| 动作执行失败 | 超时未收到完成消息 | 记录动作失败，重试或跳过 |

**故障恢复流程**：
1. 检测到故障后，停止向该设备发送新的控制命令
2. 持续监控设备状态上报（信息一）
3. 设备状态恢复为 `Running` (0x51) 后，自动恢复控制
4. TCP 连接故障时，自动重连成功后恢复通信

**日志记录**：
- 所有故障事件记录在 NLog 日志中，级别为 `Warning` 或 `Error`
- 通信帧记录在 `Trace` 级别（生产环境默认关闭）

### 完整通信时序示例

```
服务端                    数递鸟设备 (0x51)
  │                           │
  │◀──── 状态上报 ────────────│  51 52 57 51 51 51 FE (Running)
  │                           │
  │──── 发送左摆命令 ─────────▶│  51 52 57 51 52 53 FE (TurnLeft)
  │                           │
  │◀──── 左摆应答 ────────────│  51 52 57 51 53 53 FE (TurnLeftAck)
  │                           │
  │      [设备执行左摆动作]     │
  │                           │
  │◀──── 左摆完成 ────────────│  51 52 57 51 53 56 FE (TurnLeftComplete)
  │                           │
  │◀──── 状态上报 ────────────│  51 52 57 51 51 51 FE (Running)
  │                           │
  │──── 发送停止命令 ─────────▶│  51 52 57 51 52 52 FE (Stop)
  │                           │
  │◀──── 停止应答 ────────────│  51 52 57 51 53 52 FE (StopAck)
  │                           │
  │◀──── 状态上报 ────────────│  51 52 57 51 51 50 FE (Standby)
```

**相关代码**：
- `ShuDiNiaoProtocol.cs` - 协议打包和解析
- `ShuDiNiaoWheelDiverterDriver.cs` - 驱动实现
- `ShuDiNiaoWheelDiverterDriverManager.cs` - 连接管理和心跳检测

> **重要提示**：数递鸟协议的任何修改都必须同步更新本文档，这是硬性要求。

---

## 已知限制

### 上游通信
- ❌ HTTP 协议已移除，不再支持 (PR-UPSTREAM01)
- ⚠️ 原生 `TcpRuleEngineClient` 已被 `TouchSocketTcpRuleEngineClient` 替代
- ⚠️ 18个 Communication API 验证测试失败（待修复）

### 硬件驱动
- ❌ Modi 摆轮驱动未实现（文档中曾提及但代码中不存在）
- ⚠️ Siemens S7 PLC：仅实现摆轮驱动，缺少 EMC/传送带/IO联动
- ⚠️ 数递鸟（ShuDiNiao）：仅实现摆轮驱动，缺少 EMC/传送带/IO联动
- ℹ️ 生产环境建议使用 Leadshine 或根据需要混合配置多个厂商


## 文档导航

### 核心文档

| 文档 | 说明 |
|------|------|
| [docs/RepositoryStructure.md](docs/RepositoryStructure.md) | 仓库结构、技术债索引（**必读**） |
| [docs/DOCUMENTATION_INDEX.md](docs/DOCUMENTATION_INDEX.md) | 完整文档索引 |
| [.github/copilot-instructions.md](.github/copilot-instructions.md) | Copilot 约束说明 |

### 使用指南

| 文档 | 说明 |
|------|------|
| [docs/guides/API_USAGE_GUIDE.md](docs/guides/API_USAGE_GUIDE.md) | API 使用指南 |
| [docs/guides/SYSTEM_CONFIG_GUIDE.md](docs/guides/SYSTEM_CONFIG_GUIDE.md) | 系统配置指南 |
| [docs/guides/UPSTREAM_CONNECTION_GUIDE.md](docs/guides/UPSTREAM_CONNECTION_GUIDE.md) | **上游协议权威文档**（字段表/时序/超时规则） |
| [docs/guides/VENDOR_EXTENSION_GUIDE.md](docs/guides/VENDOR_EXTENSION_GUIDE.md) | 厂商扩展开发 |

### 架构文档

| 文档 | 说明 |
|------|------|
| [docs/ARCHITECTURE_PRINCIPLES.md](docs/ARCHITECTURE_PRINCIPLES.md) | 架构原则 |
| [docs/CODING_GUIDELINES.md](docs/CODING_GUIDELINES.md) | 编码规范 |
| [docs/TOPOLOGY_LINEAR_N_DIVERTERS.md](docs/TOPOLOGY_LINEAR_N_DIVERTERS.md) | N 摆轮线性拓扑模型 |

## 技术栈

| 类别 | 技术 | 说明 |
|------|------|------|
| 运行时 | .NET 8.0 | 长期支持版本 |
| Web 框架 | ASP.NET Core | Web API 和后台服务 |
| 数据库 | LiteDB | 嵌入式 NoSQL 数据库，配置持久化 |
| 监控 | Prometheus + Grafana | 指标收集与可视化 |
| 日志 | NLog | 结构化日志 |
| API 文档 | Swagger/OpenAPI | 自动生成的 API 文档 |
| 测试 | xUnit + Moq | 单元测试和集成测试 |
| 代码分析 | Roslyn Analyzers | 编译时代码规范检查 |

## 运行模式

系统支持两种运行环境：

| 环境模式 | 说明 | 使用场景 |
|----------|------|----------|
| Production | 使用真实硬件驱动 | 生产环境、与物理设备连接 |
| Simulation | 使用仿真驱动 | 开发测试、功能验证、性能测试 |

通过 `ASPNETCORE_ENVIRONMENT` 环境变量或 `/api/system/status` 接口可查询当前运行模式。

---

**文档版本**：2.1 (TD-035)  
**最后更新**：2025-12-04  
**维护团队**：ZakYip Development Team
