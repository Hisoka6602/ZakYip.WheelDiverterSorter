# 上游接入层使用指南

本文档说明如何使用标准化的 Ingress 上游接入层与规则引擎进行通信。

## 目录

- [快速开始](#快速开始)
- [配置说明](#配置说明)
- [使用方式](#使用方式)
- [扩展新协议](#扩展新协议)
- [常见问题](#常见问题)

## 快速开始

### 1. 添加配置

在 `appsettings.json` 中添加 Ingress 配置：

```json
{
  "Ingress": {
    "UpstreamChannels": [
      {
        "Name": "RuleEngineHttp",
        "Type": "HTTP",
        "Priority": 10,
        "Enabled": true,
        "Endpoint": "http://localhost:5000",
        "TimeoutMs": 5000,
        "RetryCount": 3,
        "RetryDelayMs": 1000
      }
    ],
    "DefaultTimeoutMs": 5000,
    "EnableFallback": true,
    "FallbackChuteId": 999
  }
}
```

### 2. 注册服务

在 `Program.cs` 中注册上游服务：

```csharp
using ZakYip.WheelDiverterSorter.Ingress.Upstream;

// 添加上游服务
builder.Services.AddUpstreamServices(builder.Configuration);
```

### 3. 使用 IUpstreamFacade

#### 方式 A：在中间件中使用

```csharp
using ZakYip.WheelDiverterSorter.Host.Pipeline;

// 创建使用 IUpstreamFacade 的中间件
var upstreamFacade = serviceProvider.GetRequiredService<IUpstreamFacade>();
var middleware = new UpstreamFacadeAssignmentMiddleware(
    upstreamFacade,
    traceSink,
    logger);

// 添加到管道
pipeline.Use(middleware);
```

#### 方式 B：使用适配器兼容现有代码

```csharp
using ZakYip.WheelDiverterSorter.Host.Pipeline;

// 创建适配器
var upstreamFacade = serviceProvider.GetRequiredService<IUpstreamFacade>();
var adapter = new UpstreamAssignmentAdapter(upstreamFacade, logger);
var assignmentDelegate = adapter.CreateDelegate();

// 使用原有的中间件
var middleware = new UpstreamAssignmentMiddleware(
    assignmentDelegate,
    traceSink,
    logger);
```

#### 方式 C：直接调用 IUpstreamFacade

```csharp
using ZakYip.Sorting.Core.Contracts;
using ZakYip.WheelDiverterSorter.Ingress.Upstream;

public class MyService
{
    private readonly IUpstreamFacade _upstreamFacade;
    
    public MyService(IUpstreamFacade upstreamFacade)
    {
        _upstreamFacade = upstreamFacade;
    }
    
    public async Task<int?> GetChuteForParcel(long parcelId, string? barcode)
    {
        var request = new AssignChuteRequest
        {
            ParcelId = parcelId,
            Barcode = barcode
        };
        
        var result = await _upstreamFacade.AssignChuteAsync(request);
        
        if (result.IsSuccess && result.Data != null)
        {
            if (result.IsFallback)
            {
                // 使用了降级策略
                _logger.LogWarning("使用降级格口: {ChuteId}", result.Data.ChuteId);
            }
            return result.Data.ChuteId;
        }
        
        _logger.LogError("获取格口失败: {ErrorMessage}", result.ErrorMessage);
        return null;
    }
}
```

## 配置说明

### IngressOptions

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| `UpstreamChannels` | List<UpstreamChannelConfig> | [] | 上游通道列表 |
| `DefaultTimeoutMs` | int | 5000 | 默认超时时间（毫秒） |
| `EnableFallback` | bool | true | 是否启用降级策略 |
| `FallbackChuteId` | int | 999 | 降级格口 ID |
| `EnableCircuitBreaker` | bool | true | 是否启用熔断器 |
| `CircuitBreakerFailureThreshold` | int | 5 | 熔断器失败阈值 |
| `CircuitBreakerTimeoutSeconds` | int | 30 | 熔断器超时时间（秒） |

### UpstreamChannelConfig

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| `Name` | string | （必填） | 通道名称 |
| `Type` | string | （必填） | 通道类型（HTTP, SignalR, MQTT） |
| `Priority` | int | 100 | 优先级（数值越小优先级越高） |
| `Enabled` | bool | true | 是否启用 |
| `Endpoint` | string | null | 端点地址 |
| `TimeoutMs` | int | 5000 | 超时时间（毫秒） |
| `RetryCount` | int | 3 | 重试次数 |
| `RetryDelayMs` | int | 1000 | 重试延迟（毫秒） |
| `AdditionalConfig` | Dictionary<string, string> | null | 附加配置 |

### 多通道配置示例

```json
{
  "Ingress": {
    "UpstreamChannels": [
      {
        "Name": "PrimarySignalR",
        "Type": "SignalR",
        "Priority": 10,
        "Enabled": true,
        "Endpoint": "http://ruleengine-primary:5000/sortingHub",
        "TimeoutMs": 5000
      },
      {
        "Name": "SecondaryHttp",
        "Type": "HTTP",
        "Priority": 20,
        "Enabled": true,
        "Endpoint": "http://ruleengine-secondary:5000",
        "TimeoutMs": 8000
      },
      {
        "Name": "TertiaryMqtt",
        "Type": "MQTT",
        "Priority": 30,
        "Enabled": true,
        "Endpoint": "mqtt://mqtt-broker:1883",
        "TimeoutMs": 10000,
        "AdditionalConfig": {
          "Topic": "sorting/chute/assignment",
          "QoS": "1"
        }
      }
    ],
    "DefaultTimeoutMs": 5000,
    "EnableFallback": true,
    "FallbackChuteId": 999
  }
}
```

## 使用方式

### 调用分配格口

```csharp
var request = new AssignChuteRequest
{
    ParcelId = 123456,
    Barcode = "PKG123456",
    CandidateChuteIds = new[] { 1, 2, 3, 4, 5 },
    SensorId = "SENSOR_01"
};

var result = await upstreamFacade.AssignChuteAsync(request);

if (result.IsSuccess)
{
    Console.WriteLine($"分配成功: 格口ID={result.Data.ChuteId}");
    Console.WriteLine($"延迟: {result.LatencyMs}ms");
    Console.WriteLine($"来源: {result.Source}");
    Console.WriteLine($"是否降级: {result.IsFallback}");
}
else
{
    Console.WriteLine($"分配失败: {result.ErrorMessage}");
    Console.WriteLine($"错误代码: {result.ErrorCode}");
}
```

### 创建包裹通知

```csharp
var request = new CreateParcelRequest
{
    ParcelId = 123456,
    Barcode = "PKG123456",
    DetectedAt = DateTimeOffset.UtcNow,
    SensorId = "SENSOR_01"
};

var result = await upstreamFacade.CreateParcelAsync(request);

if (result.IsSuccess)
{
    Console.WriteLine($"创建成功: 包裹ID={result.Data.ParcelId}");
}
else
{
    Console.WriteLine($"创建失败: {result.ErrorMessage}");
}
```

### 查看可用通道

```csharp
Console.WriteLine($"当前通道: {upstreamFacade.CurrentChannelName}");
Console.WriteLine($"可用通道: {string.Join(", ", upstreamFacade.AvailableChannels)}");
```

## 扩展新协议

### 步骤 1：实现 IUpstreamChannel

```csharp
using ZakYip.WheelDiverterSorter.Ingress.Upstream;
using ZakYip.WheelDiverterSorter.Ingress.Upstream.Configuration;

public class CustomUpstreamChannel : IUpstreamChannel, IUpstreamCommandSender
{
    private readonly UpstreamChannelConfig _config;
    private readonly ILogger _logger;
    
    public CustomUpstreamChannel(
        UpstreamChannelConfig config,
        ILogger<CustomUpstreamChannel> logger)
    {
        _config = config;
        _logger = logger;
    }
    
    public string Name => _config.Name;
    public string ChannelType => "Custom";
    public bool IsConnected { get; private set; }
    
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // 实现连接逻辑
        _logger.LogInformation("连接到自定义通道: {Name}", Name);
        IsConnected = true;
    }
    
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // 实现断开逻辑
        _logger.LogInformation("断开自定义通道: {Name}", Name);
        IsConnected = false;
    }
    
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        // 实现健康检查逻辑
        return IsConnected;
    }
    
    public async Task<TResponse> SendCommandAsync<TRequest, TResponse>(
        TRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        // 实现发送命令逻辑
        _logger.LogDebug("发送命令: {RequestType}", typeof(TRequest).Name);
        
        // TODO: 实现具体的通信逻辑
        throw new NotImplementedException();
    }
    
    public async Task SendOneWayAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
    {
        // 实现单向发送逻辑
        _logger.LogDebug("发送单向命令: {RequestType}", typeof(TRequest).Name);
        
        // TODO: 实现具体的通信逻辑
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        // 清理资源
    }
}
```

### 步骤 2：注册到 DI 容器

```csharp
// 在 Program.cs 或 Startup.cs 中
services.AddSingleton<IUpstreamChannel>(sp =>
{
    var config = new UpstreamChannelConfig
    {
        Name = "CustomChannel",
        Type = "Custom",
        Endpoint = "custom://endpoint",
        TimeoutMs = 5000
    };
    var logger = sp.GetRequiredService<ILogger<CustomUpstreamChannel>>();
    return new CustomUpstreamChannel(config, logger);
});

services.AddSingleton<IUpstreamCommandSender>(sp =>
{
    var channel = sp.GetServices<IUpstreamChannel>()
        .FirstOrDefault(c => c.ChannelType == "Custom");
    return (IUpstreamCommandSender)channel;
});
```

### 步骤 3：更新配置

```json
{
  "Ingress": {
    "UpstreamChannels": [
      {
        "Name": "CustomChannel",
        "Type": "Custom",
        "Priority": 10,
        "Enabled": true,
        "Endpoint": "custom://endpoint",
        "TimeoutMs": 5000
      }
    ]
  }
}
```

## 常见问题

### Q1: 如何处理连接失败？

A: UpstreamFacade 会自动尝试所有可用的通道。如果所有通道都失败，且启用了降级策略（`EnableFallback=true`），则会返回降级格口。

```csharp
var result = await upstreamFacade.AssignChuteAsync(request);

if (result.IsFallback)
{
    _logger.LogWarning("使用降级策略，格口ID: {ChuteId}", result.Data.ChuteId);
    // 可以触发告警或其他处理
}
```

### Q2: 如何配置多个规则引擎实例？

A: 在配置中添加多个通道，设置不同的优先级：

```json
{
  "UpstreamChannels": [
    {
      "Name": "Primary",
      "Type": "HTTP",
      "Priority": 10,
      "Endpoint": "http://ruleengine1:5000"
    },
    {
      "Name": "Secondary",
      "Type": "HTTP",
      "Priority": 20,
      "Endpoint": "http://ruleengine2:5000"
    }
  ]
}
```

### Q3: 如何监控上游调用的性能？

A: OperationResult 包含延迟信息，可以记录到日志或监控系统：

```csharp
var result = await upstreamFacade.AssignChuteAsync(request);

_metrics.RecordUpstreamLatency(result.LatencyMs);
_logger.LogInformation(
    "上游调用完成: 延迟={LatencyMs}ms, 来源={Source}, 降级={IsFallback}",
    result.LatencyMs,
    result.Source,
    result.IsFallback);
```

### Q4: 如何禁用降级策略？

A: 在配置中设置 `EnableFallback=false`：

```json
{
  "Ingress": {
    "EnableFallback": false
  }
}
```

这样，当所有通道失败时，会返回失败结果而不是降级格口。

### Q5: 如何实现自定义的重试逻辑？

A: 可以在具体的 Channel 实现中添加重试逻辑，或者在 UpstreamFacade 外层包装 Polly 策略：

```csharp
using Polly;

var retryPolicy = Policy
    .Handle<Exception>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var result = await retryPolicy.ExecuteAsync(async () =>
{
    return await upstreamFacade.AssignChuteAsync(request);
});
```

## 相关文档

- [PR26_IMPLEMENTATION_SUMMARY.md](PR26_IMPLEMENTATION_SUMMARY.md) - 实施总结
- [RELATIONSHIP_WITH_RULEENGINE.md](RELATIONSHIP_WITH_RULEENGINE.md) - 与规则引擎的关系

---

**最后更新**：2025-11-19
