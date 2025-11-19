# ZakYip.WheelDiverterSorter.Communication

通信层模块，负责与RuleEngine.Core进行通信，请求包裹的格口号分配。

## 概述

本模块实现了与规则引擎(RuleEngine.Core)的通信功能，支持多种通信协议，采用**工厂模式和策略模式实现低耦合架构**，便于扩展新的通信协议。

## 架构设计

### 核心接口

- `IRuleEngineClient`: 通信客户端接口，定义统一的通信方法
- `IRuleEngineClientFactory`: 客户端工厂接口，负责创建客户端实例

### 低耦合设计

```
┌─────────────────────────────────────────────────────────┐
│           IRuleEngineClient (接口)                       │
│   - ConnectAsync()                                      │
│   - DisconnectAsync()                                   │
│   - RequestChuteAssignmentAsync()                       │
└─────────────────────┬───────────────────────────────────┘
                      │
          ┌───────────┴───────────┐
          │ 工厂模式创建具体实现   │
          └───────────┬───────────┘
                      │
      ┌───────────────┼───────────────┬─────────────┐
      │               │               │             │
┌─────▼─────┐  ┌─────▼─────┐  ┌─────▼─────┐  ┌───▼────┐
│TcpClient  │  │SignalR    │  │MqttClient │  │Http    │
│           │  │Client     │  │           │  │Client  │
└───────────┘  └───────────┘  └───────────┘  └────────┘
```

**扩展新协议步骤**：
1. 实现 `IRuleEngineClient` 接口
2. 在 `RuleEngineClientFactory` 中添加创建逻辑
3. 在 `CommunicationMode` 枚举中添加新模式
4. 无需修改其他代码，完全解耦

## 支持的通信协议

### 1. TCP Socket（推荐生产环境）✅

**特点**：
- ✅ 低延迟（<10ms）
- ✅ 高吞吐量
- ✅ 长连接，减少握手开销
- ✅ 适合实时控制场景

**配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "TcpServer": "192.168.1.100:8000",
    "TimeoutMs": 5000,
    "RetryCount": 3
  }
}
```

### 2. SignalR（推荐生产环境）✅

**特点**：
- ✅ 实时双向通信
- ✅ 自动重连机制
- ✅ 支持组播和广播
- ✅ RuleEngine已内置支持

**配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "SignalR",
    "SignalRHub": "http://192.168.1.100:5000/sortingHub",
    "TimeoutMs": 5000,
    "RetryCount": 3
  }
}
```

### 3. MQTT（推荐生产环境）✅

**特点**：
- ✅ 轻量级IoT协议
- ✅ 支持QoS质量保证
- ✅ 解耦性强
- ✅ 支持消息持久化

**配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Mqtt",
    "MqttBroker": "mqtt://192.168.1.100:1883",
    "MqttTopic": "sorting/chute/assignment",
    "TimeoutMs": 5000,
    "RetryCount": 3
  }
}
```

### 4. HTTP REST API（仅测试用）⚠️

**特点**：
- ⚠️ 仅用于测试和调试
- ❌ 生产环境禁用
- ❌ 同步阻塞调用
- ❌ 性能较差

**配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "Http",
    "HttpApi": "http://localhost:5000/api/sorting/chute",
    "TimeoutMs": 5000,
    "RetryCount": 3
  }
}
```

## 使用方法

### 1. 在 Program.cs 中注册服务

```csharp
builder.Services.AddRuleEngineCommunication(builder.Configuration);
```

### 2. 在业务代码中使用

```csharp
public class SortingService
{
    private readonly IRuleEngineClient _client;
    
    public SortingService(IRuleEngineClient client)
    {
        _client = client;
    }
    
    public async Task ProcessParcelAsync(string parcelId)
    {
        // 连接到RuleEngine
        await _client.ConnectAsync();
        
        // 请求格口号
        var response = await _client.RequestChuteAssignmentAsync(parcelId);
        
        if (response.IsSuccess)
        {
            Console.WriteLine($"包裹 {parcelId} 分配到格口 {response.ChuteNumber}");
        }
        else
        {
            Console.WriteLine($"请求失败: {response.ErrorMessage}");
        }
    }
}
```

## 配置选项

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| Mode | enum | Http | 通信模式（Tcp/SignalR/Mqtt/Http） |
| TcpServer | string | - | TCP服务器地址（格式：host:port） |
| SignalRHub | string | - | SignalR Hub URL |
| MqttBroker | string | - | MQTT Broker地址 |
| MqttTopic | string | sorting/chute/assignment | MQTT主题 |
| HttpApi | string | - | HTTP API URL |
| TimeoutMs | int | 5000 | 请求超时时间（毫秒） |
| RetryCount | int | 3 | 重试次数 |
| RetryDelayMs | int | 1000 | 重试延迟（毫秒） |
| EnableAutoReconnect | bool | true | 是否启用自动重连 |

## 错误处理

所有客户端实现都包含：
- ✅ 自动重试机制
- ✅ 超时控制
- ✅ 异常捕获
- ✅ 连接管理
- ✅ 失败时返回异常格口

## 扩展新协议

### 步骤1: 实现客户端接口

```csharp
public class WebSocketRuleEngineClient : IRuleEngineClient
{
    public bool IsConnected { get; }
    
    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // 实现WebSocket连接逻辑
    }
    
    public Task DisconnectAsync()
    {
        // 实现断开连接逻辑
    }
    
    public Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        string parcelId, 
        CancellationToken cancellationToken = default)
    {
        // 实现请求逻辑
    }
    
    public void Dispose()
    {
        // 实现资源释放
    }
}
```

### 步骤2: 添加到枚举

```csharp
public enum CommunicationMode
{
    Http,
    Tcp,
    SignalR,
    Mqtt,
    WebSocket  // 新增
}
```

### 步骤3: 更新工厂

```csharp
public class RuleEngineClientFactory : IRuleEngineClientFactory
{
    public IRuleEngineClient CreateClient()
    {
        return _options.Mode switch
        {
            CommunicationMode.Tcp => new TcpRuleEngineClient(...),
            CommunicationMode.SignalR => new SignalRRuleEngineClient(...),
            CommunicationMode.Mqtt => new MqttRuleEngineClient(...),
            CommunicationMode.Http => new HttpRuleEngineClient(...),
            CommunicationMode.WebSocket => new WebSocketRuleEngineClient(...), // 新增
            _ => throw new NotSupportedException($"不支持的通信模式: {_options.Mode}")
        };
    }
}
```

### 步骤4: 添加配置验证（可选）

在 `CommunicationServiceExtensions.ValidateOptions` 中添加验证逻辑。

## 性能指标

| 协议 | 延迟 | 吞吐量 | 适用场景 |
|------|------|--------|----------|
| TCP | <10ms | 10000+/s | 高性能实时控制 |
| SignalR | <20ms | 5000+/s | 实时双向通信 |
| MQTT | <30ms | 3000+/s | IoT设备集成 |
| HTTP | <100ms | 500/s | 测试调试 |

## 依赖项

- Microsoft.AspNetCore.SignalR.Client (8.0.0)
- MQTTnet (4.3.3.952)
- Microsoft.Extensions.Logging.Abstractions (8.0.0)
- Microsoft.Extensions.Configuration.Abstractions (8.0.0)
- Microsoft.Extensions.DependencyInjection.Abstractions (8.0.0)

## 注意事项

1. **生产环境禁用HTTP**：HTTP仅用于测试，生产环境必须使用TCP/SignalR/MQTT
2. **配置验证**：系统会在启动时验证配置的有效性
3. **自动重连**：TCP/SignalR/MQTT都支持自动重连机制
4. **异常处理**：所有请求失败都会返回异常格口（CHUTE_EXCEPTION）
5. **超时控制**：所有请求都有超时控制，避免无限等待

## 未来扩展

- [ ] 支持WebSocket协议
- [ ] 支持gRPC协议
- [ ] 支持消息队列（RabbitMQ/Kafka）
- [ ] 支持负载均衡
- [ ] 支持连接池
- [ ] 支持请求批处理

## 参考文档

- [与规则引擎的关系文档](../RELATIONSHIP_WITH_RULEENGINE.md)
- [RuleEngine.Core项目](https://github.com/Hisoka6602/ZakYip.Sorting.RuleEngine.Core)
