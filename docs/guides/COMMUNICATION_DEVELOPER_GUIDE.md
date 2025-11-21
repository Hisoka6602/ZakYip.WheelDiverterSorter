# Communication Layer 开发者课程

## 目录

1. [课程概述](#课程概述)
2. [通信层架构](#通信层架构)
3. [推送模型详解](#推送模型详解)
4. [新增协议客户端](#新增协议客户端)
5. [统一基础设施使用](#统一基础设施使用)
6. [契约测试编写](#契约测试编写)
7. [本地联调流程](#本地联调流程)
8. [高并发与高延迟场景](#高并发与高延迟场景)
9. [故障排查指南](#故障排查指南)
10. [最佳实践](#最佳实践)

---

## 课程概述

本课程面向需要扩展或维护 Communication 层的开发者，涵盖通信协议客户端的开发、调试和故障排除。

### 学习目标

- 理解通信层与其他模块（Drivers、Execution、Ingress）的边界和调用关系
- 掌握推送模型的工作原理和实现方式
- 掌握新增协议客户端的完整步骤
- 学会使用统一的基础设施工具（重试、熔断、日志、序列化）
- 编写符合契约的测试用例
- 熟悉本地联调和调试工具
- 了解高并发和高延迟场景的处理方法
- 掌握故障排查技巧

### 前置知识

- C# 和 .NET 8 基础
- 异步编程（async/await）
- 依赖注入（DI）基础
- TCP、HTTP、SignalR、MQTT 等通信协议基础
- 事件驱动架构基础

---

## 通信层架构

### 系统边界与调用关系

```
┌─────────────────────────────────────────────────────────────┐
│                     Ingress Layer                           │
│  (接收包裹信息，触发分拣请求)                                 │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                   Execution Layer                           │
│  (分拣协调逻辑、路径规划、节点健康监控)                        │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                 Communication Layer                         │
│  (与RuleEngine通信，获取格口分配)                             │
│                                                             │
│  ┌─────────────────────────────────────────────────┐       │
│  │    IRuleEngineClient (统一接口)                  │       │
│  │    - NotifyParcelDetectedAsync()                │       │
│  │    - ChuteAssignmentReceived (event)            │       │
│  │    - ConnectAsync() / DisconnectAsync()         │       │
│  └──┬──────────┬──────────┬──────────┬─────────────┘       │
│     │          │          │          │                     │
│  ┌──▼──┐   ┌──▼──┐   ┌──▼──┐   ┌──▼──┐                   │
│  │ TCP │   │HTTP │   │MQTT │   │S.R. │                   │
│  └─────┘   └─────┘   └─────┘   └─────┘                   │
│                                                             │
│  ┌─────────────────────────────────────────────────┐       │
│  │   ICommunicationInfrastructure (统一工具)        │       │
│  │   - RetryPolicy (重试策略)                      │       │
│  │   - CircuitBreaker (熔断器)                     │       │
│  │   - Serializer (序列化器)                       │       │
│  │   - Logger (日志记录器)                         │       │
│  └─────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────┘
```

### 核心接口

#### IRuleEngineClient

所有通信客户端必须实现此接口：

```csharp
namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// 规则引擎通信客户端接口
/// </summary>
public interface IRuleEngineClient : IDisposable
{
    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 格口分配通知事件（推送模型）
    /// </summary>
    event EventHandler<ChuteAssignmentNotificationEventArgs>? ChuteAssignmentReceived;

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 通知RuleEngine包裹已到达（不等待响应）
    /// </summary>
    Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default);
}
```

#### IRuleEngineHandler

处理RuleEngine推送消息的标准回调接口：

```csharp
public interface IRuleEngineHandler
{
    /// <summary>
    /// 处理格口分配通知
    /// </summary>
    Task HandleChuteAssignmentAsync(ChuteAssignmentNotificationEventArgs notification);

    /// <summary>
    /// 处理连接状态变化
    /// </summary>
    Task HandleConnectionStateChangedAsync(bool isConnected, string? reason = null);

    /// <summary>
    /// 处理错误
    /// </summary>
    Task HandleErrorAsync(string error, Exception? exception = null);

    /// <summary>
    /// 处理心跳响应
    /// </summary>
    Task HandleHeartbeatAsync(DateTime timestamp);
}
```

#### ICommunicationInfrastructure

统一的基础设施工具入口点：

```csharp
public interface ICommunicationInfrastructure
{
    /// <summary>
    /// 重试策略
    /// </summary>
    IRetryPolicy RetryPolicy { get; }

    /// <summary>
    /// 熔断器
    /// </summary>
    ICircuitBreaker CircuitBreaker { get; }

    /// <summary>
    /// 序列化器
    /// </summary>
    IMessageSerializer Serializer { get; }

    /// <summary>
    /// 日志记录器
    /// </summary>
    ICommunicationLogger Logger { get; }
}
```

---

## 推送模型详解

### 推送模型 vs 请求/响应模型

**传统请求/响应模型：**
```
WheelDiverter: 检测到包裹 → 请求格口号 → 等待响应 → 收到格口号 → 分拣
```

**新推送模型：**
```
WheelDiverter: 检测到包裹 → 通知RuleEngine → 启动TTL计时器
RuleEngine: 收到通知 → 查询DWS → 决策 → 推送格口号
WheelDiverter: 收到推送 → 停止TTL → 分拣
           或：TTL超时 → 使用异常格口
```

### 推送模型的优势

1. **更符合业务逻辑**：格口号由上游决定和推送
2. **更好的解耦**：WheelDiverter不需要等待同步响应
3. **支持异步处理**：RuleEngine可以异步查询多个数据源
4. **更好的容错**：通过TTL超时自动降级到异常格口

### 推送模型实现示例

#### 1. 发送通知

```csharp
// WheelDiverter端
var parcelId = DateTime.Now.Ticks; // 毫秒时间戳
var notified = await _ruleEngineClient.NotifyParcelDetectedAsync(parcelId);

if (!notified)
{
    _logger.LogWarning("Failed to notify RuleEngine about parcel {ParcelId}", parcelId);
}
```

#### 2. 等待推送（带超时）

```csharp
// 使用TaskCompletionSource等待推送
var tcs = new TaskCompletionSource<string>();
_pendingAssignments[parcelId] = tcs;

// 订阅推送事件
_ruleEngineClient.ChuteAssignmentReceived += (sender, args) =>
{
    if (_pendingAssignments.TryGetValue(args.ParcelId, out var pendingTcs))
    {
        pendingTcs.TrySetResult(args.ChuteNumber);
        _pendingAssignments.Remove(args.ParcelId);
    }
};

// 等待推送，带超时
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
    var chuteNumber = await tcs.Task.WaitAsync(cts.Token);
    _logger.LogInformation("Received chute assignment: {ChuteNumber}", chuteNumber);
}
catch (OperationCanceledException)
{
    _logger.LogWarning("Chute assignment timeout for parcel {ParcelId}", parcelId);
    // 使用异常格口
    chuteNumber = WellKnownChuteIds.Exception;
}
```

#### 3. 服务端推送实现（RuleEngine端示例）

**SignalR Hub示例：**
```csharp
public class SortingHub : Hub
{
    public async Task NotifyParcelDetected(long parcelId)
    {
        // 1. 接收包裹检测通知
        _logger.LogInformation("Received parcel detection: {ParcelId}", parcelId);
        
        // 2. 查询DWS获取包裹信息
        var parcelInfo = await _dwsService.GetParcelInfoAsync(parcelId);
        
        // 3. 规则引擎决策格口号
        var chuteNumber = await _ruleEngine.EvaluateAsync(parcelInfo);
        
        // 4. 推送格口分配给调用者
        await Clients.Caller.SendAsync("ReceiveChuteAssignment", new
        {
            ParcelId = parcelId,
            ChuteNumber = chuteNumber,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

**MQTT主题示例：**
```
# WheelDiverter → RuleEngine
Topic: sorting/chute/detection
Message: { "parcelId": 1234567890, "timestamp": "2025-11-19T10:00:00Z" }

# RuleEngine → WheelDiverter
Topic: sorting/chute/assignment
Message: { "parcelId": 1234567890, "chuteNumber": "CHUTE_A", "timestamp": "2025-11-19T10:00:01Z" }
```

---

## 新增协议客户端

### Step 1: 创建协议客户端类

在 `ZakYip.WheelDiverterSorter.Communication/Clients/` 目录下创建新的客户端类：

```csharp
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于 WebSocket 的规则引擎客户端（示例）
/// </summary>
public class WebSocketRuleEngineClient : IRuleEngineClient
{
    private readonly ICommunicationInfrastructure _infrastructure;
    private readonly RuleEngineConnectionOptions _options;
    private bool _isConnected;
    private ClientWebSocket? _webSocket;

    public WebSocketRuleEngineClient(
        ICommunicationInfrastructure infrastructure,
        RuleEngineConnectionOptions options)
    {
        _infrastructure = infrastructure ?? throw new ArgumentNullException(nameof(infrastructure));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsConnected => _isConnected;

    public event EventHandler<ChuteAssignmentNotificationEventArgs>? ChuteAssignmentReceived;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        return await _infrastructure.RetryPolicy.ExecuteAsync(async () =>
        {
            return await _infrastructure.CircuitBreaker.ExecuteAsync(async () =>
            {
                try
                {
                    _infrastructure.Logger.LogInformation(
                        "Connecting to WebSocket server: {Server}", 
                        _options.WebSocketUrl);
                    
                    _webSocket = new ClientWebSocket();
                    await _webSocket.ConnectAsync(
                        new Uri(_options.WebSocketUrl), 
                        cancellationToken);
                    
                    _isConnected = true;
                    _infrastructure.Logger.LogInformation("Successfully connected to WebSocket server");
                    
                    // 启动接收循环
                    _ = Task.Run(() => ReceiveLoopAsync(cancellationToken));
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _infrastructure.Logger.LogError(ex, "Failed to connect to WebSocket server");
                    _isConnected = false;
                    return false;
                }
            }, cancellationToken);
        }, cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        _infrastructure.Logger.LogInformation("Disconnecting from WebSocket server");
        
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure, 
                "Client closing", 
                CancellationToken.None);
        }
        
        _webSocket?.Dispose();
        _isConnected = false;
    }

    public async Task<bool> NotifyParcelDetectedAsync(
        long parcelId, 
        CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _webSocket == null)
        {
            _infrastructure.Logger.LogWarning("Cannot notify: client not connected");
            return false;
        }

        try
        {
            var notification = new ParcelDetectionNotification
            {
                ParcelId = parcelId,
                DetectedAt = DateTime.UtcNow
            };

            var message = _infrastructure.Serializer.Serialize(notification);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(message),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);

            _infrastructure.Logger.LogDebug(
                "Notified RuleEngine about parcel {ParcelId}", 
                parcelId);
            
            return true;
        }
        catch (Exception ex)
        {
            _infrastructure.Logger.LogError(
                ex, 
                "Failed to notify parcel detection: {ParcelId}", 
                parcelId);
            return false;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        
        while (_isConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _webSocket!.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _isConnected = false;
                    break;
                }

                var data = new byte[result.Count];
                Array.Copy(buffer, data, result.Count);

                // 反序列化并触发事件
                var notification = _infrastructure.Serializer.Deserialize<ChuteAssignmentNotificationEventArgs>(data);
                if (notification != null)
                {
                    ChuteAssignmentReceived?.Invoke(this, notification);
                }
            }
            catch (Exception ex)
            {
                _infrastructure.Logger.LogError(ex, "Error in receive loop");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _webSocket?.Dispose();
    }
}
```

### Step 2: 在配置选项中添加新协议配置

在 `Configuration/RuleEngineConnectionOptions.cs` 中：

```csharp
public class RuleEngineConnectionOptions
{
    // ... 现有属性 ...

    /// <summary>
    /// WebSocket 服务器URL（例如：ws://192.168.1.100:8080/sorting）
    /// </summary>
    public string WebSocketUrl { get; set; } = string.Empty;
}
```

### Step 3: 在通信模式枚举中添加新协议

在 `Configuration/CommunicationMode.cs` 中：

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

### Step 4: 在服务扩展中注册新客户端

在 `CommunicationServiceExtensions.cs` 中：

```csharp
public static IServiceCollection AddRuleEngineCommunication(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // ... 现有代码 ...

    var mode = options.Mode;
    
    services.AddSingleton<IRuleEngineClient>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<WebSocketRuleEngineClient>>();
        var infrastructure = new DefaultCommunicationInfrastructure(options, logger);
        
        return mode switch
        {
            CommunicationMode.Tcp => new TcpRuleEngineClient(infrastructure, options),
            CommunicationMode.SignalR => new SignalRRuleEngineClient(infrastructure, options),
            CommunicationMode.Mqtt => new MqttRuleEngineClient(infrastructure, options),
            CommunicationMode.Http => new HttpRuleEngineClient(infrastructure, options),
            CommunicationMode.WebSocket => new WebSocketRuleEngineClient(infrastructure, options), // 新增
            _ => throw new NotSupportedException($"Communication mode {mode} is not supported")
        };
    });

    return services;
}
```

### Step 5: 在配置文件中配置新协议

在 `appsettings.json` 中：

```json
{
  "RuleEngineConnection": {
    "Mode": "WebSocket",
    "WebSocketUrl": "ws://192.168.1.100:8080/sorting",
    "TimeoutMs": 5000,
    "RetryCount": 3,
    "RetryDelayMs": 1000,
    "EnableAutoReconnect": true
  }
}
```

---

## 统一基础设施使用

### 为什么使用统一基础设施？

1. **避免重复代码**：重试、熔断、日志、序列化逻辑在每个客户端中都需要
2. **一致性**：确保所有协议实现行为一致
3. **易于维护**：修改基础设施只需要一个地方
4. **易于测试**：可以Mock基础设施接口进行测试

### 使用重试策略

```csharp
// 自动重试失败的操作
var result = await _infrastructure.RetryPolicy.ExecuteAsync(async () =>
{
    // 可能失败的操作
    return await SomeRiskyOperationAsync();
}, cancellationToken);
```

### 使用熔断器

```csharp
// 保护系统免受级联故障
var result = await _infrastructure.CircuitBreaker.ExecuteAsync(async () =>
{
    // 可能导致系统过载的操作
    return await ConnectToRemoteServiceAsync();
}, cancellationToken);
```

### 使用序列化器

```csharp
// 序列化对象为字节数组
var message = new ParcelDetectionNotification { ParcelId = 123 };
var bytes = _infrastructure.Serializer.Serialize(message);

// 反序列化
var notification = _infrastructure.Serializer.Deserialize<ChuteAssignmentNotificationEventArgs>(bytes);
```

### 使用日志记录器

```csharp
// 记录不同级别的日志
_infrastructure.Logger.LogInformation("Connected to server {Server}", serverAddress);
_infrastructure.Logger.LogWarning("Connection attempt {Attempt} failed", attemptCount);
_infrastructure.Logger.LogError(exception, "Critical error occurred");
_infrastructure.Logger.LogDebug("Received message: {Message}", messageContent);
```

---

## 契约测试编写

### 什么是契约测试？

契约测试确保所有协议实现都遵守相同的行为契约。无论使用TCP、SignalR、MQTT还是HTTP，它们都应该：

1. 能够成功连接到可用的服务器
2. 在服务器不可用时返回失败
3. 能够发送包裹检测通知
4. 能够接收推送的格口分配
5. 在TTL超时时正确处理
6. 能够在连接断开后重连

### 编写契约测试

所有协议客户端测试都应该继承 `RuleEngineClientContractTestsBase`：

```csharp
public class WebSocketRuleEngineClientContractTests : RuleEngineClientContractTestsBase
{
    private WebSocketTestServer? _testServer;
    private const int TestPort = 9876;

    protected override IRuleEngineClient CreateClient()
    {
        var options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.WebSocket,
            WebSocketUrl = $"ws://localhost:{TestPort}/sorting",
            TimeoutMs = 5000,
            RetryCount = 3
        };

        var logger = new Mock<ILogger>().Object;
        var infrastructure = new DefaultCommunicationInfrastructure(options, logger);
        
        return new WebSocketRuleEngineClient(infrastructure, options);
    }

    protected override async Task StartMockServerAsync()
    {
        _testServer = new WebSocketTestServer(TestPort);
        await _testServer.StartAsync();
    }

    protected override async Task StopMockServerAsync()
    {
        if (_testServer != null)
        {
            await _testServer.StopAsync();
            _testServer = null;
        }
    }

    protected override async Task ConfigureMockServerBehaviorAsync(MockServerBehavior behavior)
    {
        if (_testServer != null)
        {
            _testServer.Behavior = behavior;
        }
        await Task.CompletedTask;
    }
}
```

### 运行契约测试

```bash
# 运行所有契约测试
dotnet test --filter "FullyQualifiedName~ContractTests"

# 运行特定协议的契约测试
dotnet test --filter "FullyQualifiedName~WebSocketRuleEngineClientContractTests"
```

---

## 本地联调流程

### 方案1: 使用InMemory客户端

最简单的方案，无需外部依赖：

```json
{
  "RuleEngineConnection": {
    "Mode": "InMemory"
  }
}
```

### 方案2: 使用Docker Compose

创建 `docker-compose.mock.yml`：

```yaml
version: '3.8'

services:
  rule-engine-mock:
    image: mockserver/mockserver:latest
    ports:
      - "8000:8000"
    environment:
      MOCKSERVER_INITIALIZATION_JSON_PATH: /config/mock-expectations.json
    volumes:
      - ./mock-config:/config

  mqtt-broker:
    image: eclipse-mosquitto:latest
    ports:
      - "1883:1883"
      - "9001:9001"
    volumes:
      - ./mosquitto.conf:/mosquitto/config/mosquitto.conf
```

启动Mock服务：

```bash
docker-compose -f docker-compose.mock.yml up -d
```

### 方案3: 本地模拟服务器

在测试项目中创建简单的模拟服务器：

```csharp
public class SimpleTcpMockServer
{
    private TcpListener? _listener;
    private bool _isRunning;

    public async Task StartAsync(int port = 8000)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _isRunning = true;

        while (_isRunning)
        {
            var client = await _listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var stream = client.GetStream();
        var buffer = new byte[8192];

        while (client.Connected)
        {
            var bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0) break;

            // 解析请求并发送响应
            var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var response = CreateMockResponse(request);
            var responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes);
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _listener?.Stop();
        await Task.CompletedTask;
    }
}
```

### 使用Postman测试HTTP端点

1. 导入 Postman Collection
2. 配置环境变量
3. 发送测试请求

```http
POST http://localhost:5000/api/parcels/sort
Content-Type: application/json

{
  "parcelId": 1234567890,
  "targetChuteId": "CHUTE_A"
}
```

### 查看日志

```bash
# 实时查看日志
tail -f logs/app-$(date +%Y%m%d).log

# 过滤错误日志
grep -i "error\|exception" logs/app-*.log

# 查看通信层日志
grep "Communication" logs/app-*.log
```

---

## 高并发与高延迟场景

### 高并发场景处理

#### 1. 使用连接池

```csharp
public class PooledTcpRuleEngineClient : IRuleEngineClient
{
    private readonly ConcurrentBag<TcpClient> _connectionPool;
    private readonly int _maxPoolSize;

    public PooledTcpRuleEngineClient(int maxPoolSize = 10)
    {
        _maxPoolSize = maxPoolSize;
        _connectionPool = new ConcurrentBag<TcpClient>();
    }

    private async Task<TcpClient> GetConnectionAsync()
    {
        if (_connectionPool.TryTake(out var client) && client.Connected)
        {
            return client;
        }

        // 创建新连接
        return await CreateNewConnectionAsync();
    }

    private void ReturnConnection(TcpClient client)
    {
        if (_connectionPool.Count < _maxPoolSize && client.Connected)
        {
            _connectionPool.Add(client);
        }
        else
        {
            client.Dispose();
        }
    }
}
```

#### 2. 批量处理

```csharp
public class BatchingRuleEngineClient : IRuleEngineClient
{
    private readonly Channel<ParcelDetectionRequest> _requestChannel;
    private readonly TimeSpan _batchInterval = TimeSpan.FromMilliseconds(100);
    private readonly int _batchSize = 50;

    public async Task StartBatchProcessingAsync(CancellationToken cancellationToken)
    {
        var batch = new List<ParcelDetectionRequest>();
        var timer = Stopwatch.StartNew();

        await foreach (var request in _requestChannel.Reader.ReadAllAsync(cancellationToken))
        {
            batch.Add(request);

            if (batch.Count >= _batchSize || timer.Elapsed >= _batchInterval)
            {
                await ProcessBatchAsync(batch);
                batch.Clear();
                timer.Restart();
            }
        }
    }

    private async Task ProcessBatchAsync(List<ParcelDetectionRequest> batch)
    {
        // 批量发送请求
        _logger.LogInformation("Processing batch of {Count} requests", batch.Count);
        // ... 批量发送逻辑
    }
}
```

#### 3. 限流保护

```csharp
public class RateLimitedRuleEngineClient : IRuleEngineClient
{
    private readonly SemaphoreSlim _rateLimiter;
    private readonly int _maxConcurrency;

    public RateLimitedRuleEngineClient(int maxConcurrency = 100)
    {
        _maxConcurrency = maxConcurrency;
        _rateLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async Task<bool> NotifyParcelDetectedAsync(
        long parcelId, 
        CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            return await SendNotificationAsync(parcelId, cancellationToken);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}
```

### 高延迟场景处理

#### 1. 自适应超时

```csharp
public class AdaptiveTimeoutClient : IRuleEngineClient
{
    private readonly List<double> _latencyHistory = new();
    private readonly int _historySize = 100;
    private TimeSpan _currentTimeout = TimeSpan.FromSeconds(5);

    private void AdjustTimeout(TimeSpan actualLatency)
    {
        _latencyHistory.Add(actualLatency.TotalMilliseconds);
        
        if (_latencyHistory.Count > _historySize)
        {
            _latencyHistory.RemoveAt(0);
        }

        // 使用P95延迟作为超时
        var p95 = _latencyHistory.OrderBy(x => x).ElementAt((int)(_latencyHistory.Count * 0.95));
        _currentTimeout = TimeSpan.FromMilliseconds(p95 * 1.5);
        
        _logger.LogDebug("Adjusted timeout to {Timeout}ms based on P95 latency", _currentTimeout.TotalMilliseconds);
    }
}
```

#### 2. 断路器模式

已通过 `ICircuitBreaker` 接口提供：

```csharp
var result = await _infrastructure.CircuitBreaker.ExecuteAsync(async () =>
{
    // 高延迟操作
    return await HighLatencyOperationAsync();
}, cancellationToken);
```

#### 3. 超时降级

```csharp
public async Task<string> GetChuteAssignmentWithFallbackAsync(long parcelId)
{
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await GetChuteAssignmentAsync(parcelId, cts.Token);
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Chute assignment timeout, using fallback");
        return WellKnownChuteIds.Exception; // 降级到异常格口
    }
}
```

---

## 故障排查指南

### 常见问题排查

#### 问题1: "连接超时"

**症状：**
```
[ERR] Failed to connect to TCP server: System.TimeoutException: Connection timeout
```

**排查步骤：**

1. 检查网络连通性：
```bash
ping 192.168.1.100
telnet 192.168.1.100 8000
```

2. 检查服务器状态：
```bash
# Linux
netstat -an | grep 8000

# Windows
netstat -an | findstr 8000
```

3. 检查防火墙规则：
```bash
# Linux
sudo iptables -L -n | grep 8000

# Windows
netsh advfirewall firewall show rule name=all | findstr 8000
```

4. 增加超时时间：
```json
{
  "RuleEngineConnection": {
    "TimeoutMs": 10000
  }
}
```

#### 问题2: "推送未收到"

**症状：**
```
[WRN] Chute assignment timeout for parcel 1234567890
```

**排查步骤：**

1. 检查RuleEngine端日志
2. 验证推送主题/Hub方法名称是否匹配
3. 使用Wireshark抓包查看网络流量
4. 检查序列化/反序列化是否正确

```csharp
// 启用详细日志
_logger.LogDebug("Waiting for push notification for parcel {ParcelId}", parcelId);
```

#### 问题3: "熔断器打开"

**症状：**
```
[WRN] Circuit breaker opened after 10 consecutive failures
```

**排查步骤：**

1. 查看失败原因：
```bash
grep "Circuit breaker" logs/app-*.log
```

2. 手动重置熔断器：
```csharp
_infrastructure.CircuitBreaker.Reset();
```

3. 检查上游服务健康状态

#### 问题4: "内存泄漏"

**症状：**
```
[WRN] Memory usage: 85%, possible memory leak
```

**排查步骤：**

1. 使用 `dotnet-dump` 分析：
```bash
dotnet-dump collect -p <pid>
dotnet-dump analyze dump.dmp
> dumpheap -stat
> gcroot <address>
```

2. 检查未释放的资源：
- 未取消的事件订阅
- 未释放的TCP连接
- 未完成的TaskCompletionSource

3. 确保Dispose正确实现：
```csharp
public void Dispose()
{
    ChuteAssignmentReceived = null; // 取消事件订阅
    DisconnectAsync().GetAwaiter().GetResult();
    _webSocket?.Dispose();
}
```

### 使用诊断工具

#### 1. 启用详细日志

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ZakYip.WheelDiverterSorter.Communication": "Debug"
    }
  }
}
```

#### 2. 使用Performance Counter

```csharp
var counter = new PerformanceCounter("Communication", "Requests/sec", true);
counter.Increment();
```

#### 3. 使用OpenTelemetry追踪

```csharp
using var activity = ActivitySource.StartActivity("NotifyParcelDetected");
activity?.SetTag("parcelId", parcelId);
activity?.SetTag("protocol", "WebSocket");
```

---

## 最佳实践

### 1. 错误处理

```csharp
// ✅ 正确：区分可重试和不可重试错误
try
{
    await client.ConnectAsync();
}
catch (NetworkException ex)
{
    // 可重试错误
    _logger.LogWarning(ex, "Network error, will retry");
    await Task.Delay(1000);
    await client.ConnectAsync();
}
catch (AuthenticationException ex)
{
    // 不可重试错误
    _logger.LogError(ex, "Authentication failed, cannot retry");
    throw;
}

// ❌ 错误：捕获所有异常并重试
catch (Exception ex)
{
    await client.ConnectAsync(); // 可能无限重试
}
```

### 2. 资源管理

```csharp
// ✅ 正确：使用using确保资源释放
using var client = CreateClient();
await client.ConnectAsync();

// ✅ 正确：实现IDisposable
public void Dispose()
{
    Dispose(true);
    GC.SuppressFinalize(this);
}

protected virtual void Dispose(bool disposing)
{
    if (disposing)
    {
        _webSocket?.Dispose();
        _cts?.Dispose();
    }
}
```

### 3. 并发控制

```csharp
// ✅ 正确：使用lock保护共享状态
private readonly object _lock = new();

public void UpdateState()
{
    lock (_lock)
    {
        _state = newState;
    }
}

// ✅ 正确：使用SemaphoreSlim控制并发
private readonly SemaphoreSlim _semaphore = new(1, 1);

public async Task UpdateAsync()
{
    await _semaphore.WaitAsync();
    try
    {
        // 临界区代码
    }
    finally
    {
        _semaphore.Release();
    }
}
```

### 4. 日志记录

```csharp
// ✅ 正确：使用结构化日志
_logger.LogInformation(
    "Connected to {Protocol} server at {Address} with timeout {Timeout}ms",
    protocol, address, timeout);

// ✅ 正确：记录关键路径
_logger.LogInformation("Notifying parcel {ParcelId} at {Timestamp}", parcelId, DateTime.UtcNow);

// ❌ 错误：记录敏感信息
_logger.LogInformation("User password: {Password}", password);
```

### 5. 配置管理

```csharp
// ✅ 正确：使用配置验证
public class RuleEngineConnectionOptions
{
    [Required]
    public string TcpServer { get; set; } = string.Empty;

    [Range(100, 60000)]
    public int TimeoutMs { get; set; } = 5000;
}

// ✅ 正确：提供默认值
public int RetryCount { get; set; } = 3;
public int RetryDelayMs { get; set; } = 1000;
```

---

## 总结

通过本课程，你应该掌握了：

✅ 通信层架构和各层职责边界  
✅ 推送模型的工作原理和实现方式  
✅ 新增协议客户端的完整流程  
✅ 统一基础设施的使用方法  
✅ 契约测试的编写和运行  
✅ 本地联调和调试技巧  
✅ 高并发和高延迟场景的应对策略  
✅ 故障排查的系统化方法  
✅ 测试和验证的最佳实践

## 相关文档

- [Communication README](../src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/README.md)
- [COMMUNICATION_INTEGRATION.md](../COMMUNICATION_INTEGRATION.md)
- [IMPLEMENTATION_SUMMARY_PUSH_MODEL.md](../IMPLEMENTATION_SUMMARY_PUSH_MODEL.md)
- [API_USAGE_GUIDE.md](../API_USAGE_GUIDE.md)

如有疑问，请查阅项目 Wiki 或提交 Issue。

**祝你开发愉快！** 🚀
