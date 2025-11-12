# 通信协议配置切换指南

## 概述

本指南说明如何通过配置文件切换不同的通信协议，以及如何针对不同环境（测试、生产）优化协议参数。

## 快速开始

### 1. 环境切换

系统支持通过 `ASPNETCORE_ENVIRONMENT` 环境变量自动切换配置：

```bash
# 开发环境（使用HTTP）
export ASPNETCORE_ENVIRONMENT=Development
dotnet run

# 生产环境TCP
export ASPNETCORE_ENVIRONMENT=Production.TCP
dotnet run

# 生产环境SignalR
export ASPNETCORE_ENVIRONMENT=Production.SignalR
dotnet run

# 生产环境MQTT
export ASPNETCORE_ENVIRONMENT=Production.MQTT
dotnet run
```

### 2. 配置文件说明

| 配置文件 | 环境 | 协议 | 用途 |
|---------|------|------|------|
| `appsettings.Development.json` | 开发/测试 | HTTP | 简单易用，适合本地开发调试 |
| `appsettings.Production.TCP.json` | 生产 | TCP | 高性能，低延迟，适合实时控制 |
| `appsettings.Production.SignalR.json` | 生产 | SignalR | 双向通信，自动重连，适合Web集成 |
| `appsettings.Production.MQTT.json` | 生产 | MQTT | 轻量级IoT协议，适合设备集成 |

## 协议配置详解

### HTTP协议（测试环境）

**特点**：
- ✅ 简单易用，无需维护长连接
- ✅ 方便调试，可用浏览器/Postman测试
- ❌ 性能较低，连接开销大
- ❌ 不支持服务端推送

**配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Http",
    "HttpApi": "http://localhost:5000/api/sorting/chute",
    "TimeoutMs": 10000,
    "Http": {
      "MaxConnectionsPerServer": 10,
      "PooledConnectionIdleTimeout": 60,
      "PooledConnectionLifetime": 0,
      "UseHttp2": false
    }
  }
}
```

**参数说明**：
- `MaxConnectionsPerServer`: 连接池大小，根据并发需求调整
- `PooledConnectionIdleTimeout`: 空闲连接超时（秒）
- `PooledConnectionLifetime`: 连接最大生存时间（秒），0=无限制
- `UseHttp2`: 是否使用HTTP/2，需服务端支持

### TCP协议（生产环境-高性能）

**特点**：
- ✅ 超低延迟（<10ms）
- ✅ 高吞吐量（10000+/s）
- ✅ 连接开销小
- ❌ 需要自己实现协议

**配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "TcpServer": "192.168.1.100:8000",
    "TimeoutMs": 5000,
    "Tcp": {
      "ReceiveBufferSize": 16384,
      "SendBufferSize": 16384,
      "NoDelay": true,
      "KeepAliveInterval": 60
    }
  }
}
```

**参数说明**：
- `ReceiveBufferSize`: 接收缓冲区大小（字节）
  - 小消息（<1KB）：4096
  - 中等消息（1-4KB）：8192（默认）
  - 大消息（>4KB）：16384或更大
- `SendBufferSize`: 发送缓冲区大小（字节）
- `NoDelay`: 是否禁用Nagle算法（true=降低延迟）
- `KeepAliveInterval`: TCP保活间隔（秒），0=禁用

**优化建议**：
- 高性能场景：增大缓冲区到16KB-32KB
- 低延迟场景：保持NoDelay=true
- 长连接场景：启用KeepAlive（60秒）

### SignalR协议（生产环境-实时双向通信）

**特点**：
- ✅ 自动重连机制
- ✅ 支持服务端推送
- ✅ 集成简单
- ⚠️ 延迟稍高于TCP（<20ms）

**配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "SignalR",
    "SignalRHub": "http://192.168.1.100:5000/sortingHub",
    "SignalR": {
      "HandshakeTimeout": 15,
      "KeepAliveInterval": 15,
      "ServerTimeout": 30,
      "ReconnectIntervals": [0, 2000, 5000, 10000, 30000],
      "SkipNegotiation": false
    }
  }
}
```

**参数说明**：
- `HandshakeTimeout`: 握手超时（秒）
- `KeepAliveInterval`: 心跳间隔（秒）
- `ServerTimeout`: 服务端超时（秒），应为KeepAlive的2倍
- `ReconnectIntervals`: 重连间隔数组（毫秒），null=使用默认策略
- `SkipNegotiation`: 跳过协商直接用WebSocket（需服务端支持）

**优化建议**：
- 网络稳定：KeepAlive=30秒，ServerTimeout=60秒
- 网络不稳定：KeepAlive=15秒，ServerTimeout=30秒
- 重连策略：使用递增间隔 `[0, 2000, 5000, 10000, 30000]`

### MQTT协议（生产环境-IoT集成）

**特点**：
- ✅ 轻量级，适合IoT设备
- ✅ QoS保证消息可靠性
- ✅ 支持发布/订阅模式
- ⚠️ 延迟稍高（<30ms）

**配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Mqtt",
    "MqttBroker": "mqtt://192.168.1.100:1883",
    "MqttTopic": "sorting/chute/assignment",
    "Mqtt": {
      "QualityOfServiceLevel": 1,
      "CleanSession": true,
      "SessionExpiryInterval": 3600,
      "MessageExpiryInterval": 0,
      "ClientIdPrefix": "WheelDiverter"
    }
  }
}
```

**参数说明**：
- `QualityOfServiceLevel`: QoS等级
  - 0: 最多一次（可能丢失，最快）
  - 1: 至少一次（可能重复，推荐）
  - 2: 恰好一次（最可靠，最慢）
- `CleanSession`: 是否清理会话（true=推荐）
- `SessionExpiryInterval`: 会话过期时间（秒）
- `MessageExpiryInterval`: 消息过期时间（秒），0=不过期
- `ClientIdPrefix`: 客户端ID前缀

**优化建议**：
- 可靠性优先：QoS=1或2
- 性能优先：QoS=0
- 生产环境：CleanSession=true，避免会话累积

## 性能对比

| 协议 | 平均延迟 | 最大吞吐量 | 连接开销 | 适用场景 |
|------|---------|-----------|---------|----------|
| TCP | <10ms | 10000+/s | 低 | 高性能实时控制 |
| SignalR | <20ms | 5000+/s | 中 | 实时双向通信 |
| MQTT | <30ms | 3000+/s | 低 | IoT设备集成 |
| HTTP | <100ms | 500/s | 高 | 仅测试环境 |

## 环境切换实践

### 方式1：使用环境变量（推荐）

**Linux/Mac**:
```bash
# 开发环境
export ASPNETCORE_ENVIRONMENT=Development
dotnet run

# 生产TCP
export ASPNETCORE_ENVIRONMENT=Production.TCP
dotnet run
```

**Windows**:
```cmd
set ASPNETCORE_ENVIRONMENT=Production.TCP
dotnet run
```

**Docker**:
```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Production.TCP
```

### 方式2：使用launchSettings.json

在 `Properties/launchSettings.json` 中定义启动配置：

```json
{
  "profiles": {
    "Development": {
      "commandName": "Project",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "Production-TCP": {
      "commandName": "Project",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Production.TCP"
      }
    },
    "Production-SignalR": {
      "commandName": "Project",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Production.SignalR"
      }
    }
  }
}
```

使用：
```bash
dotnet run --launch-profile Production-TCP
```

### 方式3：复制配置文件

将对应的配置文件内容复制到 `appsettings.Production.json`：

```bash
# 使用TCP配置
cp appsettings.Production.TCP.json appsettings.Production.json

# 部署
export ASPNETCORE_ENVIRONMENT=Production
dotnet run
```

## 故障排查

### 问题1：无法连接到服务器

**检查清单**：
1. 确认服务器地址和端口正确
2. 检查防火墙设置
3. 测试网络连通性（ping/telnet）
4. 查看应用日志中的连接错误信息

### 问题2：性能不足

**优化步骤**：
1. 确认使用生产级协议（TCP/SignalR/MQTT）
2. 调整协议参数（缓冲区、超时等）
3. 检查网络延迟和带宽
4. 监控应用性能指标

### 问题3：频繁断连

**解决方案**：
1. 启用自动重连：`EnableAutoReconnect: true`
2. 调整超时参数：增大 `TimeoutMs`
3. 配置重试策略：增加 `RetryCount`
4. 启用KeepAlive机制（TCP/SignalR）

## 最佳实践

1. **开发环境使用HTTP**
   - 方便调试和测试
   - 不用担心连接管理

2. **生产环境选择合适协议**
   - 高性能场景：TCP
   - Web集成场景：SignalR
   - IoT设备场景：MQTT

3. **参数优化**
   - 根据消息大小调整缓冲区
   - 根据网络状况调整超时时间
   - 配置合理的重连策略

4. **监控与日志**
   - 记录连接状态变化
   - 监控性能指标
   - 及时处理异常

## 相关文档

- [通信层集成说明](COMMUNICATION_INTEGRATION.md)
- [与规则引擎的关系](RELATIONSHIP_WITH_RULEENGINE.md)
- [配置管理API](CONFIGURATION_API.md)

## 版本历史

- **v1.1** (2025-11-12): 添加协议参数配置支持
  - TCP: 缓冲区大小、NoDelay、KeepAlive
  - HTTP: 连接池、HTTP/2支持
  - MQTT: QoS等级、会话管理
  - SignalR: 超时、重连策略
- **v1.0** (2025-11-12): 初始版本，支持多协议切换
