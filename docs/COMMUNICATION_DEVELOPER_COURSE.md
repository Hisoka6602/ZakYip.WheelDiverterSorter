# Communication 开发者课程

## 目录

1. [课程概述](#课程概述)
2. [通信层架构](#通信层架构)
3. [新增协议客户端](#新增协议客户端)
4. [本地联调流程](#本地联调流程)
5. [高并发与高延迟场景](#高并发与高延迟场景)
6. [故障排查指南](#故障排查指南)
7. [测试与验证](#测试与验证)
8. [最佳实践](#最佳实践)

---

## 课程概述

本课程面向需要扩展或维护 Communication 层的开发者，涵盖通信协议客户端的开发、调试和故障排除。

### 学习目标

- 理解通信层与其他模块（Drivers、Execution、Ingress）的边界和调用关系
- 掌握新增协议客户端的完整步骤
- 熟悉本地联调和调试工具
- 了解高并发和高延迟场景的处理方法
- 掌握故障排查技巧

### 前置知识

- C# 和 .NET 8 基础
- 异步编程（async/await）
- 依赖注入（DI）基础
- TCP、HTTP、SignalR、MQTT 等通信协议基础

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
│  └──┬──────────┬──────────┬──────────┬─────────────┘       │
│     │          │          │          │                     │
│  ┌──▼──┐   ┌──▼──┐   ┌──▼──┐   ┌──▼──┐                   │
│  │ TCP │   │HTTP │   │MQTT │   │S.R. │                   │
│  └─────┘   └─────┘   └─────┘   └─────┘                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                    Drivers Layer                            │
│  (硬件驱动、轮分机、信号塔等)                                 │
└─────────────────────────────────────────────────────────────┘
```

### 核心接口与抽象

#### IRuleEngineClient

所有通信客户端必须实现此接口：

```csharp
namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

public interface IRuleEngineClient : IDisposable
{
    /// <summary>
    /// 连接到规则引擎服务器
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开连接
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求格口分配
    /// </summary>
    Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        ChuteAssignmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 连接状态
    /// </summary>
    bool IsConnected { get; }
}
```

#### 通信层职责

- **职责**：封装与规则引擎的通信细节
- **不负责**：分拣逻辑、路径规划、硬件控制
- **边界**：仅提供统一的通信抽象，具体协议实现对上层透明

---

## 新增协议客户端

### Step 1: 创建协议客户端类

在 `ZakYip.WheelDiverterSorter.Communication/Clients/` 目录下创建新的客户端类：

```csharp
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 基于 gRPC 的规则引擎客户端（示例）
/// </summary>
public class GrpcRuleEngineClient : IRuleEngineClient
{
    private readonly ILogger<GrpcRuleEngineClient> _logger;
    private readonly string _serverAddress;
    private bool _isConnected;

    public GrpcRuleEngineClient(
        ILogger<GrpcRuleEngineClient> logger,
        string serverAddress)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serverAddress = serverAddress ?? throw new ArgumentNullException(nameof(serverAddress));
    }

    public bool IsConnected => _isConnected;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("正在连接到 gRPC 服务器: {ServerAddress}", _serverAddress);
            
            // TODO: 实现 gRPC 连接逻辑
            // var channel = GrpcChannel.ForAddress(_serverAddress);
            // var client = new RuleEngine.RuleEngineClient(channel);
            
            _isConnected = true;
            _logger.LogInformation("成功连接到 gRPC 服务器");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接 gRPC 服务器失败");
            _isConnected = false;
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在断开 gRPC 连接");
        _isConnected = false;
        await Task.CompletedTask;
    }

    public async Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        ChuteAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("客户端未连接");
        }

        try
        {
            _logger.LogDebug("请求格口分配: ParcelId={ParcelId}", request.ParcelId);
            
            // TODO: 实现 gRPC 调用
            // var grpcRequest = new ChuteAssignmentGrpcRequest { ... };
            // var grpcResponse = await client.AssignChuteAsync(grpcRequest, cancellationToken: cancellationToken);
            
            // 示例返回
            return new ChuteAssignmentResponse
            {
                Success = true,
                ChuteId = "C001",
                ParcelId = request.ParcelId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "请求格口分配失败");
            return new ChuteAssignmentResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }
}
```

### Step 2: 在 DI 容器中注册

在 `CommunicationServiceExtensions.cs` 中添加注册逻辑：

```csharp
public static class CommunicationServiceExtensions
{
    public static IServiceCollection AddCommunicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ... 现有代码 ...

        // 添加新协议客户端
        var mode = configuration.GetValue<string>("RuleEngineConnection:Mode");
        
        if (mode == "Grpc")
        {
            services.AddSingleton<IRuleEngineClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<GrpcRuleEngineClient>>();
                var serverAddress = configuration.GetValue<string>("RuleEngineConnection:GrpcServer")
                    ?? throw new InvalidOperationException("未配置 GrpcServer");
                
                return new GrpcRuleEngineClient(logger, serverAddress);
            });
        }

        return services;
    }
}
```

### Step 3: 更新配置模型

在 `Configuration/RuleEngineConnectionOptions.cs` 中添加新配置项（如果需要）：

```csharp
public class RuleEngineConnectionOptions
{
    // ... 现有属性 ...

    /// <summary>
    /// gRPC 服务器地址（例如：https://192.168.1.100:5001）
    /// </summary>
    public string? GrpcServer { get; set; }
}
```

### Step 4: 被 Drivers/Execution 使用

通信客户端会被注入到 `Execution` 层的分拣服务中：

```csharp
public class SortingCoordinator
{
    private readonly IRuleEngineClient _ruleEngineClient;

    public SortingCoordinator(IRuleEngineClient ruleEngineClient)
    {
        _ruleEngineClient = ruleEngineClient;
    }

    public async Task<string> AssignChuteAsync(string parcelId)
    {
        var request = new ChuteAssignmentRequest { ParcelId = parcelId };
        var response = await _ruleEngineClient.RequestChuteAssignmentAsync(request);
        return response.ChuteId;
    }
}
```

---

## 本地联调流程

### 1. 启动/模拟上游或设备

#### 选项 A: 使用内置的 InMemoryRuleEngineClient

在 `appsettings.Development.json` 中配置：

```json
{
  "RuleEngineConnection": {
    "Mode": "InMemory"
  }
}
```

这会使用内存中的模拟规则引擎，无需外部依赖。

#### 选项 B: 启动 Mock RuleEngine 服务器

使用 `ZakYip.WheelDiverterSorter.Simulation` 项目中的 Mock Server：

```bash
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run --mock-rule-engine
```

或使用 Docker Compose 启动：

```bash
docker-compose -f docker-compose.mock.yml up rule-engine-mock
```

### 2. 启动本项目

```bash
cd ZakYip.WheelDiverterSorter.Host
dotnet run
```

或使用 Visual Studio / Rider 的调试功能。

### 3. 验证连接

#### 方法 1: 查看日志

启动后查看日志输出：

```
[INF] 正在连接到 TCP 服务器: 192.168.1.100:8000
[INF] 成功连接到 TCP 服务器
```

#### 方法 2: 使用 Health Check API

```bash
curl http://localhost:5000/health/line
```

响应示例：

```json
{
  "systemState": "Ready",
  "isSelfTestSuccess": true,
  "upstreams": [
    {
      "endpointName": "RuleEngine",
      "isHealthy": true,
      "checkedAt": "2025-11-19T10:30:00Z"
    }
  ]
}
```

#### 方法 3: 使用 Swagger UI

访问 `http://localhost:5000/swagger`，找到 `/api/parcels/sort` 端点，发送测试请求。

### 4. 抓包分析（高级调试）

#### 使用 Wireshark

1. 启动 Wireshark
2. 选择网络接口（例如 `lo0` 或 `eth0`）
3. 过滤器输入：`tcp.port == 8000`
4. 发送分拣请求，观察数据包

#### 使用 Fiddler（HTTP/HTTPS）

1. 启动 Fiddler
2. 配置代理：`http://localhost:8888`
3. 观察 HTTP 请求和响应

---

## 高并发与高延迟场景

### 高并发场景建议

#### 1. 连接池管理

对于 TCP 或 HTTP 客户端，使用连接池避免频繁创建连接：

```csharp
public class PooledTcpRuleEngineClient : IRuleEngineClient
{
    private readonly ObjectPool<TcpClient> _connectionPool;

    public PooledTcpRuleEngineClient()
    {
        _connectionPool = ObjectPool.Create(new TcpClientPoolPolicy());
    }

    public async Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(...)
    {
        var client = _connectionPool.Get();
        try
        {
            // 使用客户端发送请求
            return await SendRequestAsync(client, request);
        }
        finally
        {
            _connectionPool.Return(client);
        }
    }
}
```

#### 2. 批量请求优化

如果规则引擎支持批量请求，优先使用批量接口：

```csharp
public interface IRuleEngineClient
{
    Task<List<ChuteAssignmentResponse>> RequestBatchChuteAssignmentAsync(
        List<ChuteAssignmentRequest> requests,
        CancellationToken cancellationToken = default);
}
```

#### 3. 异步非阻塞

避免同步阻塞调用：

```csharp
// ❌ 错误示例
var result = _ruleEngineClient.RequestChuteAssignmentAsync(request).Result;

// ✅ 正确示例
var result = await _ruleEngineClient.RequestChuteAssignmentAsync(request, cancellationToken);
```

### 高延迟场景处理

#### 1. 超时配置

在配置中设置合理的超时时间：

```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "TcpServer": "192.168.1.100:8000",
    "TimeoutMs": 5000,
    "RetryCount": 3,
    "RetryDelayMs": 1000
  }
}
```

#### 2. 重试策略

使用 Polly 库实现重试：

```csharp
using Polly;

var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var response = await retryPolicy.ExecuteAsync(async () =>
{
    return await _ruleEngineClient.RequestChuteAssignmentAsync(request);
});
```

#### 3. 熔断器（Circuit Breaker）

防止级联故障：

```csharp
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30));
```

### 常见坑

1. **忘记释放资源**：使用 `using` 或实现 `IDisposable`
2. **线程安全问题**：使用 `lock` 或 `SemaphoreSlim` 保护共享状态
3. **无限重试**：设置最大重试次数和退避策略
4. **死锁**：避免在同步上下文中等待异步方法
5. **未处理超时**：使用 `CancellationToken` 和 `Task.WhenAny` 实现超时

---

## 故障排查指南

### Checklist

#### 1. 检查日志

查看 `logs/` 目录下的日志文件：

```bash
tail -f logs/app-20251119.log
```

关键日志标记：

- `[ERR]`：错误日志
- `[WRN]`：警告日志
- `[ALERT]`：告警日志

#### 2. 开启诊断开关

在 `appsettings.json` 中开启详细日志：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ZakYip.WheelDiverterSorter.Communication": "Debug"
    }
  },
  "Diagnostics": {
    "Level": "Verbose",
    "EnableMetrics": true,
    "EnableTracing": true
  }
}
```

#### 3. 检查网络连通性

```bash
# 测试 TCP 连接
telnet 192.168.1.100 8000

# 测试 HTTP 连接
curl -v http://192.168.1.100:5000/health

# 测试 MQTT 连接
mosquitto_sub -h 192.168.1.100 -p 1883 -t "sorting/#" -v
```

#### 4. 查看健康检查状态

```bash
curl http://localhost:5000/health/line | jq '.upstreams'
```

#### 5. 检查告警历史

```bash
cat logs/alerts-20251119.log | jq 'select(.severity == "Critical")'
```

### 常见问题与解决方案

#### 问题 1: "连接超时"

**症状**：

```
[ERR] 连接 TCP 服务器失败: System.TimeoutException: 连接超时
```

**排查步骤**：

1. 检查网络连通性：`ping 192.168.1.100`
2. 检查防火墙规则
3. 检查服务器是否启动：`netstat -an | grep 8000`
4. 增加超时时间配置

#### 问题 2: "请求返回错误码 500"

**症状**：

```
[ERR] 请求格口分配失败: HTTP 500 Internal Server Error
```

**排查步骤**：

1. 查看规则引擎服务器日志
2. 检查请求参数是否正确
3. 使用 Postman 手动测试规则引擎 API

#### 问题 3: "内存泄漏"

**症状**：

```
[WRN] 内存使用率: 85%, 可能存在内存泄漏
```

**排查步骤**：

1. 使用 `dotnet-dump` 分析内存快照
2. 检查是否有未释放的客户端连接
3. 查看是否有事件订阅未取消

---

## 测试与验证

### 单元测试示例

在 `ZakYip.WheelDiverterSorter.Communication.Tests` 中添加测试：

```csharp
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Clients;

public class GrpcRuleEngineClientTests
{
    [Fact]
    public async Task ConnectAsync_ShouldReturnTrue_WhenConnectionSucceeds()
    {
        // Arrange
        var logger = new Mock<ILogger<GrpcRuleEngineClient>>();
        var client = new GrpcRuleEngineClient(logger.Object, "localhost:5001");

        // Act
        var result = await client.ConnectAsync();

        // Assert
        Assert.True(result);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task RequestChuteAssignmentAsync_ShouldReturnResponse_WhenConnected()
    {
        // Arrange
        var logger = new Mock<ILogger<GrpcRuleEngineClient>>();
        var client = new GrpcRuleEngineClient(logger.Object, "localhost:5001");
        await client.ConnectAsync();

        var request = new ChuteAssignmentRequest
        {
            ParcelId = "PKG001",
            DestinationCode = "BJ001"
        };

        // Act
        var response = await client.RequestChuteAssignmentAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotEmpty(response.ChuteId);
    }
}
```

### 回环测试（Echo 服务）

创建简单的回环测试，模拟上游响应：

```csharp
using Xunit;

namespace ZakYip.WheelDiverterSorter.Communication.Tests;

public class EchoServerTests
{
    [Fact]
    public async Task TcpEchoServer_ShouldRespondWithSameMessage()
    {
        // Arrange
        var echoServer = new TcpEchoServer(port: 9000);
        await echoServer.StartAsync();

        var logger = new Mock<ILogger<TcpRuleEngineClient>>();
        var client = new TcpRuleEngineClient(logger.Object, "localhost:9000");

        await client.ConnectAsync();

        // Act
        var request = new ChuteAssignmentRequest { ParcelId = "TEST001" };
        var response = await client.RequestChuteAssignmentAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("TEST001", response.ParcelId);

        // Cleanup
        await client.DisconnectAsync();
        await echoServer.StopAsync();
    }
}
```

---

## 最佳实践

### 1. 日志记录

- **结构化日志**：使用 JSON 格式便于解析
- **关键路径日志**：记录请求ID、耗时、结果
- **避免敏感信息**：不记录密码、令牌等

### 2. 错误处理

- **区分可重试错误和不可重试错误**
- **提供清晰的错误消息**
- **使用异常类型表达语义**

### 3. 性能优化

- **使用异步 I/O**
- **避免不必要的序列化**
- **缓存静态配置**

### 4. 监控与可观测性

- **记录 Prometheus 指标**：请求延迟、成功率、错误率
- **集成分布式追踪**：使用 OpenTelemetry
- **设置告警阈值**：响应时间 > 5s 触发告警

### 5. 向后兼容性

- **使用版本化的协议**：在消息中包含版本号
- **优雅降级**：当新功能不可用时回退到旧逻辑

---

## 参考资料

### 项目内文档

- [Communication README](../ZakYip.WheelDiverterSorter.Communication/README.md)
- [ARCHITECTURE_OVERVIEW.md](ARCHITECTURE_OVERVIEW.md)
- [API_USAGE_GUIDE.md](../API_USAGE_GUIDE.md)

### 外部资源

- [.NET 异步编程最佳实践](https://docs.microsoft.com/en-us/dotnet/standard/async)
- [Polly 重试库文档](https://github.com/App-vNext/Polly)
- [SignalR 官方文档](https://docs.microsoft.com/en-us/aspnet/core/signalr/introduction)
- [MQTTnet 库文档](https://github.com/dotnet/MQTTnet)

---

## 总结

通过本课程，你应该掌握了：

✅ 通信层架构和各层职责边界  
✅ 新增协议客户端的完整流程  
✅ 本地联调和调试技巧  
✅ 高并发和高延迟场景的应对策略  
✅ 故障排查的系统化方法  
✅ 测试和验证的最佳实践

如有疑问，请查阅项目 Wiki 或联系团队成员。

**祝你开发愉快！** 🚀
