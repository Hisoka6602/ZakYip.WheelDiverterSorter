# ZakYip.WheelDiverterSorter

[![.NET Build and Test](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/Hisoka6602/ZakYip.WheelDiverterSorter/branch/main/graph/badge.svg)](https://codecov.io/gh/Hisoka6602/ZakYip.WheelDiverterSorter)

直线摆轮分拣系统 - 基于方向控制的包裹自动分拣解决方案

## 系统概述

基于直线摆轮（Wheel Diverter）的包裹自动分拣系统。包裹通过传感器检测进入系统，在输送线上单向移动，经过配置的摆轮节点时，根据转向方向（左/右/直行）分流到目标格口。

### 系统拓扑

```
                    ┌─────────┐
                    │  格口B   │ (摆轮D1左转)
                    └────▲────┘
                         │
入口传感器 ──▶ [摆轮D1] ──▶ [摆轮D2] ──▶ [摆轮D3] ──▶ 末端(异常口999)
    │              │           │           │
    ▼              ▼           ▼           ▼
 创建包裹     ┌─────────┐ ┌─────────┐ ┌─────────┐
             │  格口A   │ │  格口C   │ │  格口E   │
             │(D1右转)  │ │(D2右转)  │ │(D3右转)  │
             └─────────┘ └─────────┘ └─────────┘
                    ┌─────────┐ ┌─────────┐
                    │  格口D   │ │  格口F   │
                    │(D2左转)  │ │(D3左转)  │
                    └─────────┘ └─────────┘
```

**说明**：
- 每个摆轮前有感应传感器（FrontSensor）检测包裹到达
- 摆轮支持三个方向：左转、右转、直行
- 包裹沿输送线单向移动，无法后退
- 未分拣的包裹最终到达末端异常格口

### 分拣流程

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ 1. 包裹检测   │────▶│ 2. 格口分配   │────▶│ 3. 路径生成   │
│ 入口传感器触发 │     │ 上游/固定/轮询 │     │ 查询拓扑配置  │
│ 创建包裹实体  │     │              │     │              │
└──────────────┘     └──────────────┘     └──────────────┘
                                                │
                                                ▼
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ 6. 完成分拣   │◀────│ 5. 确认落格   │◀────│ 4. 路径执行   │
│ 记录结果     │     │ 格口传感器确认 │     │ 控制摆轮转向  │
└──────────────┘     └──────────────┘     └──────────────┘
```

**异常处理**：任意步骤失败（超时/设备异常/连接失败）→ 路由到异常格口

### 核心特点

- ✅ 方向控制模式（左/右/直行）
- ✅ 传感器驱动，实时跟踪包裹位置
- ✅ LiteDB 动态配置，支持运行时热更新
- ✅ 多协议通信（TCP/SignalR/MQTT/HTTP）
- ✅ 完整异常处理，自动路由到异常格口
- ✅ 三种分拣模式（正式/指定落格/循环落格）

## 项目结构

```
src/
├── Host/           # ASP.NET Core 宿主应用（API、后台服务）
├── Application/    # 应用服务层，DI 聚合入口
├── Core/           # 核心领域模型、配置仓储、HAL 抽象
├── Execution/      # 分拣执行管线、路径执行、SortingOrchestrator
├── Drivers/        # 硬件驱动（雷赛/西门子/摩迪/书迪鸟/仿真）
├── Ingress/        # 传感器管理、包裹检测
├── Communication/  # 上游通信（TCP/SignalR/MQTT/HTTP）
├── Observability/  # 监控指标、日志、告警
└── Simulation/     # 仿真运行环境

tests/              # 测试项目（单元/集成/E2E/架构/性能）
monitoring/         # Prometheus/Grafana 配置
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

| 端点 | 说明 |
|------|------|
| `GET/PUT /api/config/system` | 系统配置（分拣模式、异常格口等） |
| `GET/PUT /api/config/communication` | 上游通信配置 |
| `GET/PUT /api/config/chute-path-topology` | 格口路径拓扑 |
| `GET /healthz` | 进程级健康检查 |
| `GET /health/line` | 线体级健康检查 |

## 分拣模式

| 模式 | 说明 | 使用场景 |
|------|------|----------|
| Formal | 与上游 RuleEngine 集成 | 生产环境 |
| FixedChute | 所有包裹发送到固定格口 | 调试测试 |
| RoundRobin | 按配置列表循环分配 | 均匀分布测试 |

## 上游通信数据结构

系统支持与上游 RuleEngine 通过多种协议（TCP/SignalR/MQTT/HTTP）进行通信。以下是通信过程中使用的核心数据结构：

### 通信流程

```
┌──────────────────┐                      ┌──────────────────┐
│   分拣系统        │                      │   RuleEngine     │
│  (WheelDiverter) │                      │   (上游系统)      │
└────────┬─────────┘                      └────────┬─────────┘
         │                                         │
         │  1. ParcelDetectionNotification         │
         │  ─────────────────────────────────────▶ │
         │  (包裹检测通知)                          │
         │                                         │
         │  2. ChuteAssignmentResponse             │
         │  ◀───────────────────────────────────── │
         │  (格口分配响应)                          │
         │                                         │
```

### 数据结构定义

#### ParcelDetectionNotification（包裹检测通知）

当系统检测到包裹时，发送此通知给 RuleEngine。

```json
{
  "ParcelId": 1701446263000,
  "DetectionTime": "2024-12-01T18:57:43+08:00",
  "Metadata": {
    "SensorId": "Sensor001",
    "LineId": "Line01"
  }
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `ParcelId` | long | ✅ | 包裹ID（毫秒时间戳） |
| `DetectionTime` | DateTimeOffset | ✅ | 检测时间 |
| `Metadata` | Dictionary<string, string> | ❌ | 额外的元数据（可选） |

#### ChuteAssignmentRequest（格口分配请求）

分拣系统向上游请求格口分配时使用。

```json
{
  "ParcelId": 1701446263000,
  "RequestTime": "2024-12-01T18:57:43+08:00"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `ParcelId` | long | ✅ | 包裹ID（毫秒时间戳） |
| `RequestTime` | DateTimeOffset | ✅ | 请求时间 |

#### ChuteAssignmentResponse（格口分配响应）

上游 RuleEngine 返回的格口分配结果。

```json
{
  "ParcelId": 1701446263000,
  "ChuteId": 101,
  "IsSuccess": true,
  "ErrorMessage": null,
  "ResponseTime": "2024-12-01T18:57:43.500+08:00"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `ParcelId` | long | ✅ | 包裹ID（毫秒时间戳） |
| `ChuteId` | long | ✅ | 目标格口ID（数字ID） |
| `IsSuccess` | bool | ✅ | 是否成功（默认 true） |
| `ErrorMessage` | string | ❌ | 错误消息（如果失败） |
| `ResponseTime` | DateTimeOffset | ✅ | 响应时间 |

#### ChuteAssignmentEventArgs（格口分配事件参数）

系统内部事件传递使用的数据结构（定义在 Core 层）。

```json
{
  "ParcelId": 1701446263000,
  "ChuteId": 101,
  "NotificationTime": "2024-12-01T18:57:43.500+08:00",
  "Metadata": null
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `ParcelId` | long | ✅ | 包裹ID |
| `ChuteId` | long | ✅ | 分配的格口ID |
| `NotificationTime` | DateTimeOffset | ✅ | 通知时间 |
| `Metadata` | Dictionary<string, string> | ❌ | 额外的元数据（可选） |

### 源码位置

| 数据结构 | 位置 |
|---------|------|
| `ParcelDetectionNotification` | `src/Communication/Models/` |
| `ChuteAssignmentRequest` | `src/Communication/Models/` |
| `ChuteAssignmentResponse` | `src/Communication/Models/` |
| `ChuteAssignmentEventArgs` | `src/Core/Abstractions/Upstream/` |
| `IUpstreamRoutingClient` | `src/Core/Abstractions/Upstream/` |

## 文档导航

| 文档 | 说明 |
|------|------|
| [docs/RepositoryStructure.md](docs/RepositoryStructure.md) | 仓库结构、技术债索引 |
| [docs/DOCUMENTATION_INDEX.md](docs/DOCUMENTATION_INDEX.md) | 完整文档索引 |
| [.github/copilot-instructions.md](.github/copilot-instructions.md) | Copilot 约束说明 |

## 技术栈

- .NET 8.0
- ASP.NET Core
- LiteDB
- Prometheus + Grafana

---

**维护团队：** ZakYip Development Team
