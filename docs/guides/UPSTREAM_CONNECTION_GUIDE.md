# Upstream Connection Configuration Guide

> **权威文档声明**：本文档是上游协议（字段定义、示例 JSON、时序说明、超时/丢失规则）的**唯一权威位置**。
> 其他文档（如 README）只做高层引用，不再重复字段表/JSON 示例。

## Overview

系统与上游 RuleEngine 的交互采用 **Fire-and-Forget** 模式，完全异步通信。所有上游连接（到 RuleEngine）都支持客户端和服务器两种模式，并可配置重连策略。

### 通信模型

本系统**不存在**同步的"请求格口分配"操作。通信流程为：

1. **入口检测时**：向上游发送 `ParcelDetectionNotification`（fire-and-forget，仅通知）
2. **上游异步推送**：上游系统匹配格口后，**主动推送** `ChuteAssignmentNotification`（包含 DWS 数据）
3. **落格完成时**：向上游发送 `SortingCompletedNotification`（fire-and-forget，含 FinalStatus）

```
┌──────────────────┐                      ┌──────────────────┐
│   分拣系统        │                      │   RuleEngine     │
│  (WheelDiverter) │                      │   (上游系统)      │
└────────┬─────────┘                      └────────┬─────────┘
         │                                         │
         │  1. ParcelDetectionNotification         │
         │  ─────────────────────────────────────▶ │
         │  (检测通知: ParcelId, DetectionTime)   │
         │                                         │
         │  2. ChuteAssignmentNotification         │
         │  ◀───────────────────────────────────── │
         │  (格口分配: ParcelId, ChuteId, DWS 数据)│
         │                                         │
         │  3. SortingCompletedNotification        │
         │  ─────────────────────────────────────▶ │
         │  (落格完成: ParcelId, ActualChuteId,    │
         │   FinalStatus=Success/Timeout/Lost)     │
         │                                         │
```

> **重要**：系统发送检测通知后**不等待**格口分配，继续执行后续逻辑。格口分配通过事件异步接收。

## Connection Modes

### 1. Client Mode
The WheelDiverterSorter actively connects to an upstream RuleEngine server.

**Features**:
- Automatic reconnection with exponential backoff
- Maximum backoff time: 2 seconds (hardcoded)
- Infinite retry attempts
- Suitable for: Connecting to centralized RuleEngine server

**Configuration Example**:
```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "ConnectionMode": "Client",
    "TcpServer": "ruleengine.example.com:5000",
    "EnableAutoReconnect": true,
    "MaxBackoffSeconds": 2,
    "TimeoutMs": 5000,
    "RetryCount": 3,
    "RetryDelayMs": 1000
  }
}
```

### 2. Server Mode
The WheelDiverterSorter listens for incoming connections from RuleEngine.

**Features**:
- Listens on configured port
- Accepts incoming connections
- No reconnection logic needed (server waits for clients)
- Suitable for: Distributed architectures where RuleEngine connects to multiple sorters

**Configuration Example**:
```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "ConnectionMode": "Server",
    "TcpServer": "0.0.0.0:5000",
    "TimeoutMs": 5000
  }
}
```

## 上游通信数据结构

### ParcelDetectionNotification（包裹检测通知）

当系统检测到包裹时，发送此通知给 RuleEngine（fire-and-forget）。

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

### ChuteAssignmentNotification（格口分配通知）

上游 RuleEngine **主动推送**的格口分配结果。这是异步事件，不是请求的响应。

```json
{
  "ParcelId": 1701446263000,
  "ChuteId": 101,
  "AssignedAt": "2024-12-01T18:57:43.500+08:00",
  "DwsPayload": {
    "WeightGrams": 500.0,
    "LengthMm": 300.0,
    "WidthMm": 200.0,
    "HeightMm": 100.0,
    "Barcode": "PKG123456"
  },
  "Metadata": null
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `ParcelId` | long | ✅ | 包裹ID（毫秒时间戳） |
| `ChuteId` | long | ✅ | 目标格口ID（数字ID） |
| `AssignedAt` | DateTimeOffset | ✅ | 分配时间 |
| `DwsPayload` | DwsMeasurementDto | ❌ | DWS（尺寸重量扫描）数据（可选） |
| `Metadata` | Dictionary<string, string> | ❌ | 额外的元数据（可选） |

### SortingCompletedNotification（落格完成通知）

包裹落格后发送给上游的通知（fire-and-forget）。

```json
{
  "ParcelId": 1701446263000,
  "ActualChuteId": 101,
  "CompletedAt": "2024-12-01T18:57:45.000+08:00",
  "IsSuccess": true,
  "FinalStatus": "Success",
  "FailureReason": null
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `ParcelId` | long | ✅ | 包裹ID |
| `ActualChuteId` | long | ✅ | 实际落格格口ID（Lost 时为 0） |
| `CompletedAt` | DateTimeOffset | ✅ | 落格完成时间 |
| `IsSuccess` | bool | ✅ | 是否成功 |
| `FinalStatus` | ParcelFinalStatus | ✅ | 最终状态（Success/Timeout/Lost） |
| `FailureReason` | string | ❌ | 失败原因（如果失败） |

### FinalStatus 枚举值

| 值 | 说明 |
|----|------|
| `Success` | 包裹成功分拣到目标格口 |
| `Timeout` | 分配超时或落格超时，路由到异常格口 |
| `Lost` | 包裹丢失，无法确定位置，已从缓存清除 |

## 包裹超时与丢失判定

### 超时配置与协议字段关系

系统基于输送线长度和速度自动计算超时时间，配置字段位于 `ChuteAssignmentTimeout` 节点：

```json
{
  "ChuteAssignmentTimeout": {
    "SafetyFactor": 0.9,
    "FallbackTimeoutSeconds": 5,
    "LostDetectionSafetyFactor": 1.5
  }
}
```

| 配置字段 | 类型 | 说明 | 对应协议行为 |
|----------|------|------|-------------|
| `SafetyFactor` | double | 分配超时安全系数（默认 0.9） | 计算：`入口到首个决策点距离 / 线速 × SafetyFactor` |
| `FallbackTimeoutSeconds` | double | 降级超时秒数（默认 5） | 当无法动态计算时使用的固定超时 |
| `LostDetectionSafetyFactor` | double | 丢失检测安全系数（默认 1.5） | 计算：`输送线总长度 / 线速 × LostDetectionSafetyFactor` |

### 分配超时（AssignmentTimeout）

**条件**：包裹检测后超过动态计算的超时时间未收到 `ChuteAssignmentNotification`

**计算公式**：`超时时间 = 入口到首个决策点距离 / 线速 × SafetyFactor`

**处理动作**：
1. 标记为 `Timeout` 状态
2. 路由到异常格口
3. 发送 `SortingCompletedNotification`（FinalStatus=Timeout）

### 落格超时（SortingTimeout）

**条件**：收到格口分配后超过理论通过时间未完成落格确认

**计算公式**：`超时时间 = 路径总长度 / 线速`

**处理动作**：
1. 标记为 `Timeout` 状态
2. 路由到异常格口
3. 发送 `SortingCompletedNotification`（FinalStatus=Timeout）

### 包裹丢失判定（Lost）

**条件**：从首次检测时间起，超过最大存活时间仍未完成落格，且无法确定位置

**计算公式**：`最大存活时间 = 输送线总长度 / 线速 × LostDetectionSafetyFactor`

**处理动作**：
1. 标记为 `Lost` 状态
2. **从缓存中清除包裹记录**（避免队列错分）
3. 发送 `SortingCompletedNotification`（FinalStatus=Lost, ActualChuteId=0）

> **超时 vs 丢失的区别**：
> - **超时**：包裹仍在输送线上，可以导向异常口，ActualChuteId 为异常格口 ID
> - **丢失**：包裹已不在输送线上，无法导向异常口，ActualChuteId=0，必须从缓存清除

## Hot Reload Support

Configuration changes can be applied without restarting the application.

### Implementation
The application uses `IOptionsMonitor<RuleEngineConnectionOptions>` to detect configuration changes at runtime.

### Supported Changes
- Connection mode switching (Client ↔ Server)
- Server address changes
- Timeout and retry parameters
- Communication protocol changes (TCP, SignalR, MQTT)
- Protocol-specific options (buffer sizes, keep-alive settings, etc.)

### How to Hot Reload

#### Option 1: Update appsettings.json
1. Edit `appsettings.json` or `appsettings.{Environment}.json`
2. Save the file
3. The application automatically detects and applies changes
4. Existing connections are gracefully closed and re-established with new settings

#### Option 2: Configuration API (if enabled)
Use the Configuration API to update settings programmatically:
```http
PUT /api/configuration/ruleengine
Content-Type: application/json

{
  "ConnectionMode": "Server",
  "TcpServer": "0.0.0.0:6000"
}
```

## Connection Modes by Communication Protocol

### TCP Socket (默认)
- **Client Mode**: Connect to TCP server at `TcpServer` address
- **Server Mode**: Listen on TCP port specified in `TcpServer`
- **Reconnection**: Exponential backoff up to 2 seconds (hardcoded)
- **特点**: 高性能、低延迟

### SignalR
- **Client Mode**: Connect to SignalR Hub URL
- **Server Mode**: Host SignalR Hub (requires additional configuration)
- **Reconnection**: Built-in SignalR reconnection with custom intervals
- **特点**: 支持实时双向通信

### MQTT
- **Client Mode**: Connect to MQTT Broker
- **Server Mode**: Not applicable (MQTT requires a broker)
- **Reconnection**: MQTT client library handles reconnection
- **特点**: 适用于物联网场景

> **注意**：HTTP 协议支持已在 PR-UPSTREAM01 中移除，当前默认使用 TCP 协议。

## Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Mode` | CommunicationMode | Tcp | Protocol: Tcp, SignalR, Mqtt |
| `ConnectionMode` | ConnectionMode | Client | Client or Server mode |
| `EnableAutoReconnect` | bool | true | Enable automatic reconnection (Client mode) |
| `MaxBackoffSeconds` | int | 2 | Maximum backoff time between reconnection attempts (hardcoded) |
| `TimeoutMs` | int | 5000 | Connection/request timeout |
| `RetryCount` | int | 3 | Number of retries per operation |
| `RetryDelayMs` | int | 1000 | Initial delay between retries |
| `ChuteAssignmentTimeoutMs` | int | 10000 | Max wait time for chute assignment |

## Protocol-Specific Configuration

### TCP Options
```json
{
  "Tcp": {
    "ReceiveBufferSize": 8192,
    "SendBufferSize": 8192,
    "NoDelay": true,
    "KeepAliveInterval": 60
  }
}
```

### MQTT Options
```json
{
  "Mqtt": {
    "QualityOfServiceLevel": 1,
    "CleanSession": true,
    "SessionExpiryInterval": 3600,
    "MessageExpiryInterval": 0,
    "ClientIdPrefix": "WheelDiverter"
  }
}
```

### SignalR Options
```json
{
  "SignalR": {
    "HandshakeTimeout": 15,
    "KeepAliveInterval": 30,
    "ServerTimeout": 60,
    "ReconnectIntervals": [0, 2000, 5000, 10000],
    "SkipNegotiation": false
  }
}
```

## Reconnection Strategy (Client Mode)

The reconnection logic follows an exponential backoff strategy:

1. **Initial Attempt**: Immediate connection attempt
2. **First Retry**: Wait 200ms
3. **Subsequent Retries**: Exponential backoff (200ms → 400ms → 800ms → ...)
4. **Maximum Backoff**: 2 seconds (hardcoded)
5. **Continue**: Retry indefinitely with max backoff time

### Reconnection Triggers
- Initial connection failure
- Network disconnection
- Server timeout
- Read/write errors
- Manual disconnect followed by reconnect

## 源码位置

| 数据结构 | 位置 |
|---------|------|
| `ParcelDetectionNotification` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/` |
| `ChuteAssignmentNotification` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/` |
| `SortingCompletedNotificationDto` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/` |
| `ChuteAssignmentEventArgs` | `src/Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Upstream/` |
| `SortingCompletedNotification` | `src/Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Upstream/` |
| `IUpstreamRoutingClient` | `src/Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Upstream/` |

## 相关文档

- **时序图详解**: [docs/UPSTREAM_SEQUENCE_FIREFORGET.md](../UPSTREAM_SEQUENCE_FIREFORGET.md)
- **系统配置指南**: [docs/guides/SYSTEM_CONFIG_GUIDE.md](SYSTEM_CONFIG_GUIDE.md)

## Implementation Status

### ✅ Completed
- ConnectionMode enum (Client/Server)
- MaxBackoffSeconds configuration property (hardcoded to 2 seconds)
- Configuration model with hot reload support (IOptionsMonitor)
- Separate configuration files per protocol
- Fire-and-forget communication model
- Async chute assignment via events
- Documentation consolidation (TD-031)

### 🔄 To Be Implemented
- Server mode implementation for SignalR
- Connection state monitoring and event notification
- Metrics and logging for connection state changes

## Notes
- Server mode requires appropriate firewall and network configuration
- Client mode is recommended for most deployments
- Use Server mode when RuleEngine needs to connect to multiple distributed sorters
- Hot reload may cause brief connection interruptions
- 连接失败采用无限重试策略，最大退避时间为 2 秒（硬编码）
