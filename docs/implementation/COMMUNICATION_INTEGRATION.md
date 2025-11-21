# 通信层集成说明

## 概述

本文档说明如何使用Communication层实现与RuleEngine的通信，以及如何将通信层集成到包裹分拣流程中。

## 架构概览

```
┌──────────────────────────────────────────────────────────────┐
│                     包裹分拣完整流程                          │
└──────────────────────────────────────────────────────────────┘

┌─────────────────────┐
│  1. 传感器检测包裹   │
│  (Ingress Layer)    │
└──────────┬──────────┘
           │
           │ ParcelDetected Event
           ▼
┌─────────────────────┐
│  2. 请求格口号       │
│  (Communication)    │
│  → RuleEngine       │
└──────────┬──────────┘
           │
           │ ChuteAssignmentResponse
           ▼
┌─────────────────────┐
│  3. 生成摆轮路径     │
│  (Core Layer)       │
└──────────┬──────────┘
           │
           │ SwitchingPath
           ▼
┌─────────────────────┐
│  4. 执行分拣动作     │
│  (Execution Layer)  │
└─────────────────────┘
```

## 核心组件

### 1. IRuleEngineClient 接口

定义了与RuleEngine通信的标准接口：

```csharp
public interface IRuleEngineClient : IDisposable
{
    bool IsConnected { get; }
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        string parcelId,
        CancellationToken cancellationToken = default);
}
```

### 2. 客户端实现

系统提供了四种通信协议实现：

| 协议 | 实现类 | 生产环境推荐 | 说明 |
|------|-------|------------|------|
| TCP | `TcpRuleEngineClient` | ✅ 推荐 | 低延迟、高吞吐量 |
| SignalR | `SignalRRuleEngineClient` | ✅ 推荐 | 实时双向通信、自动重连 |
| MQTT | `MqttRuleEngineClient` | ✅ 推荐 | 轻量级IoT协议、QoS保证 |
| HTTP | `HttpRuleEngineClient` | ❌ 禁用 | 仅用于测试，性能不足 |

### 3. ParcelSortingOrchestrator 编排服务

负责协调整个分拣流程：

1. 监听传感器检测到的包裹事件
2. 向RuleEngine请求格口号
3. 生成摆轮路径
4. 执行分拣动作

## 配置说明

### 基本配置结构

在 `appsettings.json` 中添加以下配置：

```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "TcpServer": "192.168.1.100:8000",
    "SignalRHub": "http://192.168.1.100:5000/sortingHub",
    "MqttBroker": "mqtt://192.168.1.100:1883",
    "MqttTopic": "sorting/chute/assignment",
    "HttpApi": "http://localhost:5000/api/sorting/chute",
    "TimeoutMs": 5000,
    "RetryCount": 3,
    "RetryDelayMs": 1000,
    "EnableAutoReconnect": true,
    "Tcp": {
      "ReceiveBufferSize": 8192,
      "SendBufferSize": 8192,
      "NoDelay": true,
      "KeepAliveInterval": 60
    },
    "Http": {
      "MaxConnectionsPerServer": 10,
      "PooledConnectionIdleTimeout": 60,
      "PooledConnectionLifetime": 0,
      "UseHttp2": false
    },
    "Mqtt": {
      "QualityOfServiceLevel": 1,
      "CleanSession": true,
      "SessionExpiryInterval": 3600,
      "MessageExpiryInterval": 0,
      "ClientIdPrefix": "WheelDiverter"
    },
    "SignalR": {
      "HandshakeTimeout": 15,
      "KeepAliveInterval": 30,
      "ServerTimeout": 60,
      "ReconnectIntervals": null,
      "SkipNegotiation": false
    }
  }
}
```

### 配置项说明

#### 通用配置

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| Mode | enum | Http | 通信模式：Tcp/SignalR/Mqtt/Http |
| TcpServer | string | - | TCP服务器地址（host:port） |
| SignalRHub | string | - | SignalR Hub URL |
| MqttBroker | string | - | MQTT Broker地址 |
| MqttTopic | string | sorting/chute/assignment | MQTT主题 |
| HttpApi | string | - | HTTP API URL |
| TimeoutMs | int | 5000 | 请求超时时间（毫秒） |
| RetryCount | int | 3 | 重试次数 |
| RetryDelayMs | int | 1000 | 重试延迟（毫秒） |
| EnableAutoReconnect | bool | true | 是否启用自动重连 |

#### TCP协议配置 (Tcp)

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| ReceiveBufferSize | int | 8192 | TCP接收缓冲区大小（字节）。根据消息大小调整：小消息<1KB用4096，中等消息1-4KB用8192，大消息>4KB用16384或更大 |
| SendBufferSize | int | 8192 | TCP发送缓冲区大小（字节） |
| NoDelay | bool | true | 是否禁用Nagle算法。true降低延迟（推荐），false提高网络利用率但增加延迟 |
| KeepAliveInterval | int | 60 | TCP KeepAlive时间间隔（秒）。0表示禁用KeepAlive |

#### HTTP协议配置 (Http)

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| MaxConnectionsPerServer | int | 10 | HTTP连接池最大连接数。根据并发需求调整 |
| PooledConnectionIdleTimeout | int | 60 | 连接空闲超时时间（秒）。连接空闲超过此时间会被回收 |
| PooledConnectionLifetime | int | 0 | 连接生存时间（秒）。0表示无限制。强制回收长期连接以应对DNS变化 |
| UseHttp2 | bool | false | 是否使用HTTP/2协议。仅当服务端支持时启用 |

#### MQTT协议配置 (Mqtt)

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| QualityOfServiceLevel | int | 1 | MQTT服务质量等级。0=最多一次（可能丢失），1=至少一次（可能重复，推荐），2=恰好一次（最可靠但最慢） |
| CleanSession | bool | true | 是否使用Clean Session。true=不保留会话状态（推荐），false=保留会话状态和订阅 |
| SessionExpiryInterval | int | 3600 | 会话保持时间（秒）。0表示连接断开后立即清理会话 |
| MessageExpiryInterval | int | 0 | 消息保留时间（秒）。0表示不保留。用于保留最后一条消息给新订阅者 |
| ClientIdPrefix | string | WheelDiverter | MQTT客户端ID前缀。完整ID为：前缀_GUID |

#### SignalR协议配置 (SignalR)

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| HandshakeTimeout | int | 15 | 握手超时时间（秒） |
| KeepAliveInterval | int | 30 | 保持连接超时时间（秒）。服务端发送心跳包的间隔 |
| ServerTimeout | int | 60 | 服务端超时时间（秒）。未收到服务端心跳的最大等待时间 |
| ReconnectIntervals | int[] | null | 重连间隔（毫秒）。null使用默认策略，可设置固定重连间隔如 [0, 2000, 5000, 10000] |
| SkipNegotiation | bool | false | 是否跳过协商（直接使用WebSocket）。true可减少连接延迟，但需要服务端支持 |

## 使用步骤

### 步骤1：注册服务

在 `Program.cs` 中注册通信服务：

```csharp
// 注册RuleEngine通信服务
builder.Services.AddRuleEngineCommunication(builder.Configuration);

// 注册包裹分拣编排服务
builder.Services.AddSingleton<ParcelSortingOrchestrator>();

// 注册后台服务（可选）
builder.Services.AddHostedService<ParcelSortingWorker>();
```

### 步骤2：启用后台服务

取消注释 `Program.cs` 中的后台服务注册：

```csharp
// 启用传感器监听和分拣编排
builder.Services.AddHostedService<SensorMonitoringWorker>();
builder.Services.AddHostedService<ParcelSortingWorker>();
```

### 步骤3：配置通信模式与环境切换

系统支持通过环境变量和配置文件切换不同的通信协议。ASP.NET Core会根据 `ASPNETCORE_ENVIRONMENT` 环境变量自动加载对应的配置文件：

- **开发环境**：`ASPNETCORE_ENVIRONMENT=Development` → 加载 `appsettings.Development.json`
- **生产环境**：`ASPNETCORE_ENVIRONMENT=Production` → 加载 `appsettings.Production.json`
- **自定义环境**：可使用 `appsettings.{EnvironmentName}.json` 格式

#### 方式1：使用环境特定配置文件（推荐）

项目已提供了针对不同环境和协议的配置文件：

- `appsettings.Development.json` - 开发环境（HTTP）
- `appsettings.Production.TCP.json` - 生产环境TCP协议
- `appsettings.Production.SignalR.json` - 生产环境SignalR协议
- `appsettings.Production.MQTT.json` - 生产环境MQTT协议

**使用方法**：

1. 复制对应的配置文件内容到 `appsettings.Production.json` 或通过命令行参数指定：
```bash
# Linux/Mac
export ASPNETCORE_ENVIRONMENT=Production
dotnet run --launch-profile Production

# Windows
set ASPNETCORE_ENVIRONMENT=Production
dotnet run --launch-profile Production

# Docker
docker run -e ASPNETCORE_ENVIRONMENT=Production myapp
```

2. 或在 `launchSettings.json` 中配置：
```json
{
  "profiles": {
    "Production-TCP": {
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Production.TCP"
      }
    }
  }
}
```

#### 方式2：直接修改配置文件

根据部署环境选择合适的通信模式：

**开发/测试环境（HTTP）**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Http",
    "HttpApi": "http://localhost:5000/api/sorting/chute",
    "TimeoutMs": 10000,
    "Http": {
      "MaxConnectionsPerServer": 10,
      "UseHttp2": false
    }
  }
}
```

**生产环境（TCP - 高性能优化）**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "TcpServer": "192.168.1.100:8000",
    "TimeoutMs": 5000,
    "RetryCount": 3,
    "Tcp": {
      "ReceiveBufferSize": 16384,
      "SendBufferSize": 16384,
      "NoDelay": true,
      "KeepAliveInterval": 60
    }
  }
}
```

**生产环境（SignalR - 自动重连优化）**：
```json
{
  "RuleEngineConnection": {
    "Mode": "SignalR",
    "SignalRHub": "http://192.168.1.100:5000/sortingHub",
    "EnableAutoReconnect": true,
    "SignalR": {
      "HandshakeTimeout": 15,
      "KeepAliveInterval": 15,
      "ServerTimeout": 30,
      "ReconnectIntervals": [0, 2000, 5000, 10000, 30000]
    }
  }
}
```

**生产环境（MQTT - QoS优化）**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Mqtt",
    "MqttBroker": "mqtt://192.168.1.100:1883",
    "MqttTopic": "sorting/chute/assignment",
    "Mqtt": {
      "QualityOfServiceLevel": 1,
      "CleanSession": true,
      "SessionExpiryInterval": 3600
    }
  }
}
```

## 工作流程

### 自动分拣流程

当启用后台服务后，系统会自动处理包裹分拣：

```
1. SensorMonitoringWorker 启动传感器监听
   ↓
2. 传感器检测到包裹 → ParcelDetected 事件
   ↓
3. ParcelSortingOrchestrator 接收事件
   ↓
4. 向 RuleEngine 请求格口号
   ↓
5. 生成摆轮路径
   ↓
6. 执行分拣动作
   ↓
7. 记录结果日志
```

### 手动测试流程

如果不启用后台服务，可以通过调试API手动测试：

```bash
# 手动触发分拣（不通过RuleEngine）
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG001", "targetChuteId": "CHUTE_A"}'
```

## 错误处理

### 连接失败处理

如果无法连接到RuleEngine，系统会：

1. 尝试重新连接（根据 `RetryCount` 配置）
2. 如果所有重试都失败，返回异常格口号 `CHUTE_EXCEPTION`
3. 包裹会被发送到异常格口等待人工处理

示例日志：

```
[Warning] 无法连接到RuleEngine SignalR Hub
[Information] 包裹 PKG001 分配到格口 CHUTE_EXCEPTION（连接失败）
```

### 请求超时处理

如果请求超时，系统会：

1. 自动重试（根据 `RetryCount` 和 `RetryDelayMs` 配置）
2. 记录警告日志
3. 最终失败后返回异常格口号

示例日志：

```
[Warning] 请求格口号失败（第1次尝试）: 请求超时
[Warning] 请求格口号失败（第2次尝试）: 请求超时
[Error] 请求格口号失败，已达到最大重试次数
```

### 路径生成失败处理

如果无法生成到指定格口的路径，系统会：

1. 尝试生成到异常格口的路径
2. 如果连异常格口路径都无法生成，记录错误并跳过该包裹

示例日志：

```
[Warning] 包裹 PKG001 无法生成到格口 CHUTE_UNKNOWN 的路径，将发送到异常格口
[Information] 包裹 PKG001 成功分拣到格口 CHUTE_EXCEPTION
```

## 性能考虑

### 通信协议性能对比

| 协议 | 平均延迟 | 最大吞吐量 | 连接开销 | 适用场景 |
|------|---------|-----------|---------|----------|
| TCP | <10ms | 10000+/s | 低 | 高性能实时控制 |
| SignalR | <20ms | 5000+/s | 中 | 实时双向通信 |
| MQTT | <30ms | 3000+/s | 低 | IoT设备集成 |
| HTTP | <100ms | 500/s | 高 | 仅测试环境 |

### 优化建议

1. **生产环境使用TCP或SignalR**：避免使用HTTP，延迟高且连接开销大
2. **调整超时时间**：根据网络环境调整 `TimeoutMs`，建议3-10秒
3. **合理设置重试次数**：`RetryCount` 建议3-5次，避免过多重试阻塞处理
4. **启用自动重连**：保持 `EnableAutoReconnect: true`，增强系统鲁棒性
5. **监控连接状态**：定期检查 `IRuleEngineClient.IsConnected` 状态

### 协议参数优化建议

#### TCP优化
- **缓冲区大小**：根据消息大小调整 `ReceiveBufferSize` 和 `SendBufferSize`
  - 小消息（<1KB）：4096字节
  - 中等消息（1-4KB）：8192字节（默认）
  - 大消息（>4KB）：16384字节或更大
- **NoDelay**：保持 `true`（禁用Nagle算法）以降低延迟
- **KeepAlive**：设置为60秒，确保长连接稳定性

#### HTTP优化
- **连接池**：根据并发需求调整 `MaxConnectionsPerServer`（默认10）
- **连接复用**：调整 `PooledConnectionIdleTimeout`（默认60秒）
- **HTTP/2**：如果服务端支持，启用 `UseHttp2` 以提高性能
- **注意**：仅用于测试环境，生产环境禁用

#### MQTT优化
- **QoS等级**：
  - QoS 0：最快但可能丢失消息
  - QoS 1：推荐，至少传输一次，平衡性能和可靠性
  - QoS 2：最可靠但最慢，仅在必须保证消息不重复时使用
- **Clean Session**：保持 `true` 以避免会话状态累积
- **Session Expiry**：设置合理的过期时间（默认3600秒）

#### SignalR优化
- **重连策略**：配置 `ReconnectIntervals` 如 `[0, 2000, 5000, 10000, 30000]`
- **超时设置**：
  - `HandshakeTimeout`：15秒（默认）
  - `KeepAliveInterval`：15-30秒，根据网络稳定性调整
  - `ServerTimeout`：30-60秒，为 KeepAliveInterval 的 2倍
- **WebSocket直连**：网络稳定时可启用 `SkipNegotiation: true` 减少延迟

## 监控与调试

### 日志级别

系统使用结构化日志记录关键事件：

- **Information**：正常操作（连接成功、包裹分拣成功）
- **Warning**：可恢复错误（请求失败、重试中）
- **Error**：严重错误（连接失败、路径生成失败）

### 关键日志示例

```
# 连接成功
[Information] 成功连接到RuleEngine TCP服务器 192.168.1.100:8000

# 包裹处理
[Information] 检测到包裹 PKG001，开始处理分拣流程
[Information] 包裹 PKG001 分配到格口 CHUTE_A
[Information] 包裹 PKG001 成功分拣到格口 CHUTE_A

# 错误处理
[Warning] 向RuleEngine请求包裹 PKG001 的格口号失败（第1次尝试）
[Error] 包裹 PKG001 分拣失败: 路径执行超时
```

## 故障排查

### 问题1：无法连接到RuleEngine

**症状**：日志显示连接失败

**排查步骤**：
1. 检查配置中的地址是否正确
2. 确认RuleEngine服务是否运行
3. 检查网络连接和防火墙设置
4. 使用 telnet/curl 测试连接

### 问题2：请求频繁超时

**症状**：日志显示多次重试失败

**排查步骤**：
1. 检查网络延迟和带宽
2. 增加 `TimeoutMs` 配置值
3. 检查RuleEngine服务性能
4. 确认是否存在网络抖动

### 问题3：包裹都分拣到异常格口

**症状**：所有包裹的 ActualChuteId 都是 CHUTE_EXCEPTION

**排查步骤**：
1. 检查RuleEngine是否返回有效格口号
2. 确认格口配置是否正确
3. 查看日志中的错误原因
4. 测试路径生成功能

## 扩展开发

### 添加新的通信协议

如需支持新的通信协议（如WebSocket、gRPC等）：

1. 实现 `IRuleEngineClient` 接口
2. 在 `CommunicationMode` 枚举中添加新模式
3. 在 `RuleEngineClientFactory` 中添加创建逻辑
4. 在 `CommunicationServiceExtensions.ValidateOptions` 中添加验证

示例：

```csharp
// 1. 实现接口
public class WebSocketRuleEngineClient : IRuleEngineClient
{
    // 实现所有接口方法
}

// 2. 添加枚举值
public enum CommunicationMode
{
    Http, Tcp, SignalR, Mqtt,
    WebSocket  // 新增
}

// 3. 更新工厂
public IRuleEngineClient CreateClient()
{
    return _options.Mode switch
    {
        // ... 其他协议
        CommunicationMode.WebSocket => new WebSocketRuleEngineClient(...),
        _ => throw new NotSupportedException()
    };
}
```

## 相关文档

- [Communication层README](../ZakYip.WheelDiverterSorter.Communication/README.md)
- [与规则引擎的关系文档](../RELATIONSHIP_WITH_RULEENGINE.md)
- [配置管理API文档](../CONFIGURATION_API.md)
- [传感器实现总结](../SENSOR_IMPLEMENTATION_SUMMARY.md)

## 版本历史

- **v1.0** (2025-11-12): 初始版本，支持TCP/SignalR/MQTT/HTTP通信
- 包含 ParcelSortingOrchestrator 集成服务
- 支持自动分拣流程和错误处理
