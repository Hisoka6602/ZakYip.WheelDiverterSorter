# Architecture Overview

## 项目概述 / Project Overview

本项目是一个摆轮分拣机控制系统（Wheel Diverter Sorter），采用分层架构设计，实现了包裹的自动分拣功能。

This project is a Wheel Diverter Sorter control system that uses a layered architecture design to implement automatic parcel sorting.

## 分层架构 / Layered Architecture

### 核心层级 / Core Layers

```
┌─────────────────────────────────────────┐
│           Host / Simulation             │  ← 入口层（Entry Points）
├─────────────────────────────────────────┤
│         Infrastructure Layer            │  ← 基础设施层
│  (Drivers, Communication, Ingress)      │
├─────────────────────────────────────────┤
│          Execution Layer                │  ← 执行层（Business Logic）
├─────────────────────────────────────────┤
│            Core Layer                   │  ← 核心域模型（Domain Model）
├─────────────────────────────────────────┤
│          Sorting.Core                   │  ← 通用分拣领域
└─────────────────────────────────────────┘
```

### 层级职责 / Layer Responsibilities

#### 1. Core Layer (ZakYip.WheelDiverterSorter.Core)

**职责：**
- 定义领域模型和核心实体
- 定义接口契约（Interfaces）
- 包含领域事件（EventArgs）
- 配置模型和枚举定义
- **不依赖**任何上层项目（Host, Infrastructure, Simulation, Tools）

**关键命名空间：**
- `ZakYip.WheelDiverterSorter.Core`: 核心领域类型
- `ZakYip.WheelDiverterSorter.Core.Events`: 领域事件载荷
- `ZakYip.WheelDiverterSorter.Core.Enums`: 枚举定义
- `ZakYip.WheelDiverterSorter.Core.Configuration`: 配置模型
- `ZakYip.WheelDiverterSorter.Core.Runtime.Health`: 健康检查相关

**关键类型：**
- `SwitchingPath`: 摆轮路径
- `RoutePlan`: 路由计划
- `DiverterNode`: 摆轮节点
- `ISwitchingPathGenerator`: 路径生成器接口
- `IRoutePlanRepository`: 路由计划仓储接口

#### 2. Execution Layer (ZakYip.WheelDiverterSorter.Execution)

**职责：**
- 实现核心业务逻辑
- 路径执行、路由重规划
- 并发控制和资源锁管理
- 健康监控和自检
- **仅依赖** Core 和基础库（Microsoft.Extensions.*）

**关键命名空间：**
- `ZakYip.WheelDiverterSorter.Execution`: 执行服务
- `ZakYip.WheelDiverterSorter.Execution.Concurrency`: 并发控制
- `ZakYip.WheelDiverterSorter.Execution.Health`: 健康监控
- `ZakYip.WheelDiverterSorter.Execution.SelfTest`: 系统自检

**关键类型：**
- `ISwitchingPathExecutor`: 路径执行器接口
- `PathReroutingService`: 路径重规划服务
- `DiverterResourceLockManager`: 摆轮资源锁管理
- `NodeHealthRegistry`: 节点健康注册表

#### 3. Infrastructure Layer

包括以下项目：
- **Drivers**: 硬件驱动（摆轮、传感器、PLC通信）
- **Communication**: 上游系统通信（WCS/WMS对接）
- **Ingress**: 入口感应和包裹创建
- **Observability**: 可观测性（日志、追踪、指标）

**职责：**
- 与外部系统集成
- 硬件设备抽象和驱动
- 数据持久化实现
- 可依赖 Core 和 Execution

#### 4. Host Layer (ZakYip.WheelDiverterSorter.Host)

**职责：**
- 应用程序入口和依赖注入配置
- RESTful API 控制器
- API DTO 定义（*RequestDto / *ResponseDto）
- 后台服务（Workers）
- 系统组装和启动

**关键命名空间：**
- `ZakYip.WheelDiverterSorter.Host.Controllers`: Web API 控制器
- `ZakYip.WheelDiverterSorter.Host.Models`: API 请求/响应 DTO
- `ZakYip.WheelDiverterSorter.Host.Services`: 应用服务
- `ZakYip.WheelDiverterSorter.Host.Commands`: 命令处理器

#### 5. Simulation Layer (ZakYip.WheelDiverterSorter.Simulation)

**职责：**
- 系统仿真和测试
- 性能测试场景
- 策略实验
- **仅消费** Core + Execution + Infrastructure，不被其他层依赖

#### 6. Tools Layer (Tools/*)

**职责：**
- 离线分析工具
- 报表生成
- 性能分析
- **仅消费** Core，不被其他层依赖

## 命名规范 / Naming Conventions

### 1. 领域术语 / Domain Terminology

| 中文术语 | 英文术语 | 类型 | 说明 |
|---------|---------|------|------|
| 格口 | Chute | `int ChuteId` | 分拣目标格口，使用整型ID |
| 异常口 | Exception Chute | `int ExceptionChuteId` | 异常包裹目标格口 |
| 节点 | Node | `string NodeId` | 物理节点（摆轮/检测器等） |
| 摆轮 | Diverter | `int DiverterId` | 摆轮设备ID |
| 包裹 | Parcel | `long ParcelId` | 包裹唯一标识 |

### 2. 事件命名 / Event Naming

**规范：** `*EventArgs` （record struct 或 record class）

**位置：** `ZakYip.WheelDiverterSorter.Core.Events`

**示例：**
- `ParcelScannedEventArgs`: 包裹扫描事件
- `PathSegmentFailedEventArgs`: 路径段执行失败事件
- `ChuteChangeRequestedEventArgs`: 格口变更请求事件

### 3. 命令命名 / Command Naming

**规范：** `*Command` / `*Request`

**位置：** `ZakYip.WheelDiverterSorter.Host.Commands` 或 Execution

**示例：**
- `ChangeParcelChuteCommand`: 修改包裹格口命令

### 4. API DTO 命名 / API DTO Naming

**规范：** `*Request` / `*Response` / `*Dto`

**位置：** `ZakYip.WheelDiverterSorter.Host.Models`

**示例：**
- `SystemConfigRequest`: 系统配置请求
- `SystemConfigResponse`: 系统配置响应
- `RouteConfigRequest`: 路由配置请求

### 5. 超载原因 / Overload Reasons

使用 `OverloadReason` 枚举（位于 `ZakYip.Sorting.Core.Overload`）：

```csharp
public enum OverloadReason
{
    None = 0,               // 正常分拣
    Timeout = 1,            // 超时
    WindowMiss = 2,         // 窗口错失
    CapacityExceeded = 3,   // 产能超限
    NodeDegraded = 4,       // 节点降级
    TopologyUnreachable = 5,// 拓扑不可达
    SensorFault = 6,        // 传感器故障
    Other = 99              // 其他原因
}
```

## 事件流示意 / Event Flow Diagram

```
┌─────────────┐
│  入口光电    │ (Entry Sensor)
│  检测到包裹  │
└──────┬──────┘
       │
       ↓
┌─────────────────────────┐
│  ParcelScannedEventArgs │ ← 事件：包裹扫描
└──────┬──────────────────┘
       │
       ↓
┌──────────────────────┐
│  创建 RoutePlan      │ ← 核心域模型
│  (SortOrder)         │
└──────┬───────────────┘
       │
       ↓
┌──────────────────────┐
│  路由计划生成        │ ← ISwitchingPathGenerator
│  (Generate Path)     │
└──────┬───────────────┘
       │
       ↓
┌──────────────────────┐
│  Overload 判定       │ ← IOverloadHandlingPolicy
│  (Check Capacity)    │
└──────┬───────────────┘
       │
    ┌──┴──┐
    │ 正常 │ 拥堵/超载
    ↓     ↓
┌──────┐ ┌──────────────┐
│ 执行 │ │ 路由到异常口  │
│ 路径 │ │ (Exception)  │
└──┬───┘ └──────────────┘
   │
   ↓
┌──────────────────────┐
│  摆轮路径执行         │ ← ISwitchingPathExecutor
│  (Execute Segments)  │
└──────┬───────────────┘
       │
    ┌──┴──┐
    │ 成功 │ 失败
    ↓     ↓
┌────────┐ ┌──────────────┐
│ 落格   │ │ 重新规划      │
│ 反馈   │ │ (Reroute)    │
└────────┘ └──────────────┘
```

## 依赖规则 / Dependency Rules

### ✅ 允许的依赖 / Allowed Dependencies

- **Execution** → Core ✓
- **Infrastructure (Drivers, Communication, etc.)** → Core, Execution ✓
- **Host** → Core, Execution, Infrastructure ✓
- **Simulation** → Core, Execution, Infrastructure ✓
- **Tools** → Core ✓

### ❌ 禁止的依赖 / Forbidden Dependencies

- **Core** → Host ✗
- **Core** → Infrastructure ✗
- **Core** → Simulation ✗
- **Core** → Tools ✗
- **Execution** → Host ✗
- **Execution** → Simulation ✗
- **Execution** → Infrastructure (除 Observability 外) ✗

## 关键接口 / Key Interfaces

### 路径生成 / Path Generation
```csharp
public interface ISwitchingPathGenerator
{
    SwitchingPath? GeneratePath(int targetChuteId);
}
```

### 路径执行 / Path Execution
```csharp
public interface ISwitchingPathExecutor
{
    Task<PathExecutionResult> ExecuteAsync(
        SwitchingPath path, 
        CancellationToken cancellationToken = default);
}
```

### 路径重规划 / Path Rerouting
```csharp
public interface IPathReroutingService
{
    Task<ReroutingResult> TryRerouteAsync(
        int originalTargetChuteId,
        ReroutingContext context,
        CancellationToken cancellationToken = default);
}
```

### 超载处理 / Overload Handling
```csharp
public interface IOverloadHandlingPolicy
{
    OverloadDecision Decide(OverloadContext context);
}
```

## 配置管理 / Configuration Management

系统配置存储在 LiteDB 数据库中，通过仓储模式访问：

- `IRouteConfigurationRepository`: 路由配置
- `ISystemConfigurationRepository`: 系统配置
- `ISensorConfigurationRepository`: 传感器配置
- `ICommunicationConfigurationRepository`: 通信配置

## 并发控制 / Concurrency Control

使用 `DiverterResourceLockManager` 管理摆轮资源锁，防止多个包裹同时访问同一摆轮：

```csharp
await using var locks = await _lockManager.AcquireLocksAsync(
    requiredDiverters, 
    timeout, 
    cancellationToken);
```

## 可观测性 / Observability

- **Logging**: NLog / Microsoft.Extensions.Logging
- **Metrics**: Prometheus
- **Tracing**: 包裹追踪日志（ParcelTraceLogger）
- **Health Checks**: 节点健康监控（NodeHealthMonitorService）

## 参考文档 / Reference Documentation

- [Performance Baseline](./PERFORMANCE_BASELINE.md)
- [Strategy Experiment Guide](./STRATEGY_EXPERIMENT_GUIDE.md)
- [Reporting and Analysis](./REPORTING_OFFLINE_ANALYSIS.md)

---

**维护说明：** 本文档描述系统的架构设计和约定。修改架构时请同步更新此文档。

**Maintenance Note:** This document describes the system architecture and conventions. Please update this document when making architectural changes.
