# PR-26 实施总结：Ingress 上游接入层标准化

## 概述

本 PR 将与上游规则引擎的交互（HTTP / SignalR / MQTT / 其他）收敛为统一的 Ingress Adapter 模型，便于未来接入更多上游系统或厂商规则引擎，减少 Host/Execution 对具体协议、序列化细节的感知。

## 核心改动

### 1. 定义 Ingress 抽象接口

在 `ZakYip.WheelDiverterSorter.Ingress/Upstream` 下定义了以下核心接口：

#### IUpstreamChannel
```csharp
public interface IUpstreamChannel : IDisposable
{
    string Name { get; }
    string ChannelType { get; }
    bool IsConnected { get; }
    
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
```

**职责**：管理与上游系统的连接生命周期（连接、重连、关闭）。

#### IUpstreamCommandSender
```csharp
public interface IUpstreamCommandSender
{
    Task<TResponse> SendCommandAsync<TRequest, TResponse>(
        TRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;
    
    Task SendOneWayAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class;
}
```

**职责**：发送请求/命令到上游系统并接收响应。

#### IUpstreamEventListener
```csharp
public interface IUpstreamEventListener
{
    Task SubscribeAsync<TEvent>(
        string eventName,
        Func<TEvent, Task> handler,
        CancellationToken cancellationToken = default)
        where TEvent : class;
    
    Task UnsubscribeAsync(
        string eventName,
        CancellationToken cancellationToken = default);
}
```

**职责**：订阅上游事件（如配置变更通知）。

#### IUpstreamFacade
```csharp
public interface IUpstreamFacade
{
    Task<OperationResult<AssignChuteResponse>> AssignChuteAsync(
        AssignChuteRequest request,
        CancellationToken cancellationToken = default);
    
    Task<OperationResult<CreateParcelResponse>> CreateParcelAsync(
        CreateParcelRequest request,
        CancellationToken cancellationToken = default);
    
    string CurrentChannelName { get; }
    IReadOnlyList<string> AvailableChannels { get; }
}
```

**职责**：提供统一的上游系统访问入口，屏蔽底层通道差异。

### 2. 统一请求/响应契约模型

在 `ZakYip.WheelDiverterSorter.Core/Sorting/Contracts` 下定义了与上游规则引擎交互的契约：

#### AssignChuteRequest / AssignChuteResponse
```csharp
public record AssignChuteRequest
{
    public required long ParcelId { get; init; }
    public string? Barcode { get; init; }
    public DateTimeOffset RequestTime { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<int>? CandidateChuteIds { get; init; }
    public string? SensorId { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public record AssignChuteResponse
{
    public required long ParcelId { get; init; }
    public required int ChuteId { get; init; }
    public bool IsSuccess { get; init; } = true;
    public DateTimeOffset ResponseTime { get; init; } = DateTimeOffset.UtcNow;
    public string? Source { get; init; }
    public string? ReasonCode { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
```

#### CreateParcelRequest / CreateParcelResponse
```csharp
public record CreateParcelRequest
{
    public required long ParcelId { get; init; }
    public string? Barcode { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? SensorId { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public record CreateParcelResponse
{
    public required long ParcelId { get; init; }
    public bool IsSuccess { get; init; } = true;
    public DateTimeOffset ResponseTime { get; init; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
```

### 3. 实现具体协议适配器

#### HttpUpstreamChannel
实现了基于 HTTP 的上游通道：

- 位置：`ZakYip.WheelDiverterSorter.Ingress/Upstream/Http/HttpUpstreamChannel.cs`
- 特性：
  - 实现 `IUpstreamChannel` 和 `IUpstreamCommandSender` 接口
  - 使用 `HttpClient` 进行 HTTP 通信
  - 支持配置端点、超时等参数
  - 根据请求类型自动映射 API 端点

### 4. 上游多通道支持

#### IngressOptions 配置类
```csharp
public class IngressOptions
{
    public const string SectionName = "Ingress";
    
    public List<UpstreamChannelConfig> UpstreamChannels { get; init; } = new();
    public int DefaultTimeoutMs { get; init; } = 5000;
    public bool EnableFallback { get; init; } = true;
    public int FallbackChuteId { get; init; } = 999;
    public bool EnableCircuitBreaker { get; init; } = true;
    public int CircuitBreakerFailureThreshold { get; init; } = 5;
    public int CircuitBreakerTimeoutSeconds { get; init; } = 30;
}

public class UpstreamChannelConfig
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public int Priority { get; init; } = 100;
    public bool Enabled { get; init; } = true;
    public string? Endpoint { get; init; }
    public int TimeoutMs { get; init; } = 5000;
    public int RetryCount { get; init; } = 3;
    public int RetryDelayMs { get; init; } = 1000;
    public Dictionary<string, string>? AdditionalConfig { get; init; }
}
```

#### UpstreamFacade 实现
- 位置：`ZakYip.WheelDiverterSorter.Ingress/Upstream/UpstreamFacade.cs`
- 特性：
  - 管理多个上游通道
  - 按优先级尝试通道
  - 支持降级策略
  - 封装 `OperationResult` 统一返回格式

### 5. 超时、重试、降级策略

#### OperationResult 模型
```csharp
public record OperationResult<T>
{
    public required bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public double LatencyMs { get; init; }
    public string? Source { get; init; }
    public bool IsFallback { get; init; }
    
    public static OperationResult<T> Success(T data, double latencyMs, string? source = null);
    public static OperationResult<T> Failure(string errorMessage, string? errorCode = null, double latencyMs = 0, string? source = null);
    public static OperationResult<T> Fallback(T data, double latencyMs, string? source = null);
}
```

**特性**：
- 封装操作结果，包含成功/失败状态
- 记录延迟信息
- 标识是否为降级结果
- 提供便捷的工厂方法

### 6. Execution/Host 层集成

#### UpstreamFacadeAssignmentMiddleware
- 位置：`ZakYip.WheelDiverterSorter.Host/Pipeline/UpstreamFacadeAssignmentMiddleware.cs`
- 职责：替代原有的 `UpstreamAssignmentMiddleware`，直接使用 `IUpstreamFacade` 进行上游通信
- 特性：
  - 无需关心具体协议实现
  - 自动处理降级场景
  - 记录详细的追踪日志

#### UpstreamAssignmentAdapter
- 位置：`ZakYip.WheelDiverterSorter.Host/Pipeline/UpstreamAssignmentAdapter.cs`
- 职责：将 `IUpstreamFacade` 适配到现有的 `UpstreamAssignmentDelegate`
- 用途：提供向后兼容，使得现有代码无需改动即可使用新的上游抽象

### 7. 依赖注入配置

#### UpstreamServiceExtensions
```csharp
public static class UpstreamServiceExtensions
{
    public static IServiceCollection AddUpstreamServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册配置
        services.Configure<IngressOptions>(configuration.GetSection(IngressOptions.SectionName));
        
        // 注册 HttpClient
        services.AddHttpClient();
        
        // 注册通道和命令发送器
        services.AddSingleton<IUpstreamChannel, HttpUpstreamChannel>(...);
        services.AddSingleton<IUpstreamCommandSender>(...);
        
        // 注册门面
        services.AddSingleton<IUpstreamFacade, UpstreamFacade>();
        
        return services;
    }
}
```

## 配置示例

### appsettings.json
```json
{
  "Ingress": {
    "UpstreamChannels": [
      {
        "Name": "PrimaryHttp",
        "Type": "HTTP",
        "Priority": 10,
        "Enabled": true,
        "Endpoint": "http://ruleengine:5000",
        "TimeoutMs": 5000,
        "RetryCount": 3,
        "RetryDelayMs": 1000
      }
    ],
    "DefaultTimeoutMs": 5000,
    "EnableFallback": true,
    "FallbackChuteId": 999,
    "EnableCircuitBreaker": true,
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerTimeoutSeconds": 30
  }
}
```

## 使用方式

### 方式一：使用新的 UpstreamFacadeAssignmentMiddleware

```csharp
// 在 Program.cs 中注册服务
builder.Services.AddUpstreamServices(builder.Configuration);

// 构建排序管道
var pipeline = new SortingPipeline();
pipeline.Use(new TracingMiddleware(...));
pipeline.Use(new OverloadEvaluationMiddleware(...));
pipeline.Use(new UpstreamFacadeAssignmentMiddleware(upstreamFacade, ...)); // 新的中间件
pipeline.Use(new RoutePlanningMiddleware(...));
pipeline.Use(new PathExecutionMiddleware(...));
```

### 方式二：使用适配器兼容现有代码

```csharp
// 在 Program.cs 中注册服务
builder.Services.AddUpstreamServices(builder.Configuration);

// 创建适配器
var adapter = new UpstreamAssignmentAdapter(upstreamFacade, logger);
var assignmentDelegate = adapter.CreateDelegate();

// 使用原有的 UpstreamAssignmentMiddleware
var pipeline = new SortingPipeline();
pipeline.Use(new UpstreamAssignmentMiddleware(assignmentDelegate, ...));
```

## 扩展新协议

### 添加 SignalR 支持示例

1. 创建 SignalRUpstreamChannel 类：

```csharp
public class SignalRUpstreamChannel : IUpstreamChannel, IUpstreamCommandSender
{
    private readonly HubConnection _connection;
    
    public SignalRUpstreamChannel(UpstreamChannelConfig config, ...)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(config.Endpoint!)
            .Build();
    }
    
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connection.StartAsync(cancellationToken);
    }
    
    // 实现其他接口方法...
}
```

2. 在配置中添加 SignalR 通道：

```json
{
  "Ingress": {
    "UpstreamChannels": [
      {
        "Name": "PrimarySignalR",
        "Type": "SignalR",
        "Priority": 10,
        "Enabled": true,
        "Endpoint": "http://ruleengine:5000/sortingHub",
        "TimeoutMs": 5000
      },
      {
        "Name": "FallbackHttp",
        "Type": "HTTP",
        "Priority": 20,
        "Enabled": true,
        "Endpoint": "http://ruleengine:5000",
        "TimeoutMs": 5000
      }
    ]
  }
}
```

3. 在 DI 容器中注册：

```csharp
services.AddSingleton<IUpstreamChannel, SignalRUpstreamChannel>(...);
services.AddSingleton<IUpstreamCommandSender>(...);
```

## 测试

### 单元测试
- `HttpUpstreamChannelTests.cs`：测试 HTTP 通道的基本功能
- `UpstreamFacadeTests.cs`：测试门面的多通道管理、降级策略

运行测试：
```bash
dotnet test ZakYip.WheelDiverterSorter.Ingress.Tests
```

所有测试通过（16/16）。

## 验收标准

✅ 所有与上游通信相关的调用入口统一经过 IUpstreamFacade / IUpstreamChannel

✅ 新增一个上游厂商/协议时，不需要修改 Execution，仅在 Ingress 增加一个 Adapter + 配置

✅ 本 PR 涉及的所有 .cs 文件命名空间与目录结构一致

✅ 构建成功，无错误

✅ 所有测试通过

## 后续工作

1. **添加更多协议支持**：
   - SignalR（实时双向通信）
   - MQTT（IoT 消息队列）
   - gRPC（高性能 RPC）

2. **完善熔断器实现**：
   - 当前降级策略已实现
   - 可以进一步集成 Polly 库实现更完善的熔断、重试策略

3. **性能优化**：
   - 连接池管理
   - 请求批处理
   - 缓存策略

4. **监控和可观测性**：
   - 集成 OpenTelemetry
   - 记录上游调用的详细指标
   - 添加 Grafana Dashboard

## 相关文档

- [RELATIONSHIP_WITH_RULEENGINE.md](../RELATIONSHIP_WITH_RULEENGINE.md) - 与规则引擎的关系说明
- [UPSTREAM_CONNECTION_GUIDE.md](../UPSTREAM_CONNECTION_GUIDE.md) - 上游连接配置指南

---

**完成日期**：2025-11-19  
**PR 编号**：PR-26
