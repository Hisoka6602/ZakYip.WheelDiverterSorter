# 上游系统集成指南 / Upstream System Integration Guide

> **文档状态**: ✅ 基于实际代码实现（Version 1.0）  
> **最后更新**: 2025-12-14  
> **目标读者**: RuleEngine 上游系统开发者  
> **代码版本**: 基于 `src/Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Upstream/IUpstreamRoutingClient.cs` 实际实现

---

## 📋 目录 / Table of Contents

1. [快速开始 / Quick Start](#快速开始--quick-start)
2. [通信架构 / Communication Architecture](#通信架构--communication-architecture)
3. [核心接口 / Core Interface](#核心接口--core-interface)
4. [数据结构 / Data Structures](#数据结构--data-structures)
5. [消息流程 / Message Flow](#消息流程--message-flow)
6. [JSON 示例 / JSON Examples](#json-示例--json-examples)
7. [连接模式 / Connection Modes](#连接模式--connection-modes)
8. [超时与丢失 / Timeout and Loss](#超时与丢失--timeout-and-loss)
9. [错误处理 / Error Handling](#错误处理--error-handling)
10. [实现检查清单 / Implementation Checklist](#实现检查清单--implementation-checklist)

---

## 快速开始 / Quick Start

### 核心概念 / Core Concepts

WheelDiverterSorter（摆轮分拣系统）通过 `IUpstreamRoutingClient` 接口与上游 RuleEngine 通信。

**关键特性**:
- ✅ **Fire-and-Forget**: 所有消息都是异步通知，不等待响应
- ✅ **事件驱动**: 格口分配通过事件推送，不是请求-响应
- ✅ **自动重连**: 连接失败时自动重试（指数退避，最大2秒）
- ✅ **三种协议**: 支持 TCP / SignalR / MQTT
- ✅ **双向模式**: Client（主动连接）或 Server（被动监听）

### 三步集成 / Three-Step Integration

```
1️⃣ 接收包裹检测通知（ParcelDetectedMessage）
   ↓
2️⃣ 推送格口分配（通过 ChuteAssigned 事件）
   ↓
3️⃣ 接收落格完成通知（SortingCompletedMessage）
```

---

## 通信架构 / Communication Architecture

### IUpstreamRoutingClient 接口

这是分拣系统与上游通信的**唯一接口**，定义在 Core 层。

**接口签名**:
```csharp
public interface IUpstreamRoutingClient : IDisposable
{
    // 连接状态
    bool IsConnected { get; }
    
    // 1个事件：接收格口分配
    event EventHandler<ChuteAssignmentEventArgs>? ChuteAssigned;
    
    // 2个核心方法
    Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    
    // 1个扩展方法：热更新配置
    Task UpdateOptionsAsync(UpstreamConnectionOptions options);
}
```

**设计原则**:
- **统一发送接口**: `SendAsync` 支持两种消息类型（ParcelDetected / SortingCompleted）
- **事件接收**: 格口分配通过 `ChuteAssigned` 事件推送（不是响应）
- **连接管理**: 自动重连由实现类内部处理，调用方无需关心连接状态

---

## 核心接口 / Core Interface

### 1. IUpstreamMessage（消息基接口）

所有发送到上游的消息都实现此接口：

```csharp
public interface IUpstreamMessage
{
    UpstreamMessageType MessageType { get; }
}

public enum UpstreamMessageType
{
    ParcelDetected = 1,      // 包裹检测通知
    SortingCompleted = 2     // 落格完成通知
}
```

### 2. ParcelDetectedMessage（包裹检测消息）

```csharp
public sealed record ParcelDetectedMessage : IUpstreamMessage
{
    public required long ParcelId { get; init; }           // 包裹ID（毫秒时间戳）
    public required DateTimeOffset DetectedAt { get; init; } // 检测时间
    public UpstreamMessageType MessageType => UpstreamMessageType.ParcelDetected;
}
```

**调用示例**:
```csharp
var message = new ParcelDetectedMessage 
{ 
    ParcelId = 1734182263000, 
    DetectedAt = DateTimeOffset.Now 
};
bool sent = await _upstreamClient.SendAsync(message, cancellationToken);
```

### 3. SortingCompletedMessage（落格完成消息）

```csharp
public sealed record SortingCompletedMessage : IUpstreamMessage
{
    public required SortingCompletedNotification Notification { get; init; }
    public UpstreamMessageType MessageType => UpstreamMessageType.SortingCompleted;
}
```

**Notification 结构**:
```csharp
public record SortingCompletedNotification
{
    public required long ParcelId { get; init; }
    public required long ActualChuteId { get; init; }          // Lost时为0
    public required DateTimeOffset CompletedAt { get; init; }
    public bool IsSuccess { get; init; } = true;
    public string? FailureReason { get; init; }
    public ParcelFinalStatus FinalStatus { get; init; } = ParcelFinalStatus.Success;
}
```

**FinalStatus 枚举**:
```csharp
public enum ParcelFinalStatus
{
    Success = 0,         // 成功分拣到目标格口
    Timeout = 1,         // 超时，路由到异常格口（仍在输送线上）
    Lost = 2,            // 丢失，无法确定位置（已从缓存清除）
    ExecutionError = 3   // 执行错误
}
```

### 4. ChuteAssignmentEventArgs（格口分配事件）

RuleEngine 推送格口分配时触发此事件：

```csharp
public record ChuteAssignmentEventArgs
{
    public required long ParcelId { get; init; }
    public required long ChuteId { get; init; }
    public required DateTimeOffset AssignedAt { get; init; }
    public DwsMeasurement? DwsPayload { get; init; }           // 可选的DWS数据
    public Dictionary<string, string>? Metadata { get; init; } // 可选的元数据
}
```

**DWS数据结构**:
```csharp
public readonly record struct DwsMeasurement
{
    public decimal WeightGrams { get; init; }           // 重量（克）
    public decimal LengthMm { get; init; }              // 长度（毫米）
    public decimal WidthMm { get; init; }               // 宽度（毫米）
    public decimal HeightMm { get; init; }              // 高度（毫米）
    public decimal? VolumetricWeightGrams { get; init; } // 体积重量（可选）
    public string? Barcode { get; init; }               // 条码（可选）
    public DateTimeOffset MeasuredAt { get; init; }     // 测量时间
}
```

---

## 数据结构 / Data Structures

### 传输层 DTO（Communication.Models）

实际 JSON 传输使用以下 DTO 类型：

#### ParcelDetectionNotification（传输层）

```csharp
// 位置：src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/
public record ParcelDetectionNotification
{
    public string Type { get; init; } = "ParcelDetected";  // 固定值
    public required long ParcelId { get; init; }
    public required DateTimeOffset DetectionTime { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
```

#### ChuteAssignmentNotification（传输层）

```csharp
public record ChuteAssignmentNotification
{
    public required long ParcelId { get; init; }
    public required long ChuteId { get; init; }
    public required DateTimeOffset AssignedAt { get; init; }
    public DwsMeasurementDto? DwsPayload { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
```

#### SortingCompletedNotificationDto（传输层）

```csharp
public sealed record SortingCompletedNotificationDto
{
    public string Type { get; init; } = "SortingCompleted";  // 固定值
    public required long ParcelId { get; init; }
    public required long ActualChuteId { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public bool IsSuccess { get; init; } = true;
    public string? FailureReason { get; init; }
    public ParcelFinalStatus FinalStatus { get; init; } = ParcelFinalStatus.Success;
}
```

### 映射关系 / Mapping

| Core 层（业务模型） | Communication 层（传输 DTO） |
|-------------------|---------------------------|
| `ParcelDetectedMessage` → | `ParcelDetectionNotification` |
| `ChuteAssignmentEventArgs` ← | `ChuteAssignmentNotification` |
| `SortingCompletedNotification` → | `SortingCompletedNotificationDto` |

**映射由 `IUpstreamContractMapper` 完成，调用方无需关心。**

---

## 消息流程 / Message Flow

### 完整时序图

```
┌────────────────┐                             ┌───────────────┐
│ WheelDiverter  │                             │  RuleEngine   │
│ (分拣系统)     │                             │  (上游系统)   │
└───────┬────────┘                             └───────┬───────┘
        │                                              │
        │ ① 传感器检测到包裹                            │
        │    创建包裹记录（ParcelId: 1734182263000）    │
        │                                              │
        │ ② SendAsync(ParcelDetectedMessage)           │
        │  ────────────────────────────────────────▶   │
        │     Fire-and-Forget（不等待响应）            │
        │                                              │
        │                                              │ ③ 执行分拣规则
        │                                              │    匹配目标格口
        │                                              │
        │ ④ 触发 ChuteAssigned 事件                     │
        │  ◀────────────────────────────────────────   │
        │     （上游主动推送，不是响应）                 │
        │     ChuteId: 5, DwsPayload: {...}           │
        │                                              │
        │ ⑤ 执行摆轮动作，包裹物理分拣                   │
        │                                              │
        │ ⑥ 包裹落格确认                                │
        │                                              │
        │ ⑦ SendAsync(SortingCompletedMessage)         │
        │  ────────────────────────────────────────▶   │
        │     FinalStatus: Success                    │
        │     ActualChuteId: 5                        │
        │                                              │
```

### 代码实现示例

#### 分拣系统侧（WheelDiverter）

```csharp
public class SortingOrchestrator
{
    private readonly IUpstreamRoutingClient _upstreamClient;
    
    public async Task HandleParcelCreationAsync(long parcelId)
    {
        // ① 先创建本地包裹记录（Parcel-First原则）
        _createdParcels[parcelId] = new ParcelCreationRecord 
        { 
            ParcelId = parcelId, 
            CreatedAt = _clock.LocalNowOffset 
        };
        
        // ② 发送检测通知（Fire-and-Forget）
        var message = new ParcelDetectedMessage 
        { 
            ParcelId = parcelId, 
            DetectedAt = _clock.LocalNowOffset 
        };
        
        bool sent = await _upstreamClient.SendAsync(message, CancellationToken.None);
        
        if (!sent)
        {
            _logger.LogError("无法发送检测通知，包裹将路由到异常格口");
            // 继续执行，路由到异常格口
        }
        
        // ③ 不等待格口分配，继续后续处理
        // 格口分配会通过 ChuteAssigned 事件异步到达
    }
    
    // ④ 订阅格口分配事件
    public void Initialize()
    {
        _upstreamClient.ChuteAssigned += OnChuteAssignmentReceived;
    }
    
    private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentEventArgs e)
    {
        _logger.LogInformation(
            "收到格口分配: ParcelId={ParcelId}, ChuteId={ChuteId}, DWS={HasDws}",
            e.ParcelId, e.ChuteId, e.DwsPayload != null);
        
        // 更新包裹路由
        _parcelTargetChutes[e.ParcelId] = e.ChuteId;
    }
    
    // ⑦ 落格完成后发送通知
    public async Task NotifySortingCompletedAsync(long parcelId, long actualChuteId, bool isSuccess)
    {
        var notification = new SortingCompletedNotification
        {
            ParcelId = parcelId,
            ActualChuteId = actualChuteId,
            CompletedAt = _clock.LocalNowOffset,
            IsSuccess = isSuccess,
            FinalStatus = isSuccess ? ParcelFinalStatus.Success : ParcelFinalStatus.Timeout
        };
        
        var message = new SortingCompletedMessage { Notification = notification };
        await _upstreamClient.SendAsync(message, CancellationToken.None);
    }
}
```

#### 上游系统侧（RuleEngine）

```csharp
public class RuleEngineHandler : IRuleEngineHandler
{
    // ② 接收包裹检测通知
    public async Task OnParcelDetectedAsync(ParcelDetectionNotification notification)
    {
        _logger.LogInformation("收到包裹检测: ParcelId={ParcelId}", notification.ParcelId);
        
        // ③ 执行分拣规则（异步，不阻塞通信线程）
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // 模拟规则计算
            
            var targetChuteId = await CalculateTargetChute(notification.ParcelId);
            
            // ④ 主动推送格口分配
            var assignment = new ChuteAssignmentNotification
            {
                ParcelId = notification.ParcelId,
                ChuteId = targetChuteId,
                AssignedAt = DateTimeOffset.Now,
                DwsPayload = await GetDwsData(notification.ParcelId) // 可选
            };
            
            await SendChuteAssignmentAsync(assignment);
        });
    }
    
    // ⑦ 接收落格完成通知
    public async Task OnSortingCompletedAsync(SortingCompletedNotificationDto notification)
    {
        _logger.LogInformation(
            "包裹落格完成: ParcelId={ParcelId}, ActualChute={ChuteId}, Status={Status}",
            notification.ParcelId, notification.ActualChuteId, notification.FinalStatus);
        
        // 更新业务系统状态
        await UpdateParcelStatus(notification);
    }
}
```

---

## JSON 示例 / JSON Examples

### 示例 1: 包裹检测通知

**方向**: WheelDiverter → RuleEngine

```json
{
  "Type": "ParcelDetected",
  "ParcelId": 1734182263000,
  "DetectionTime": "2024-12-14T18:57:43.000+08:00",
  "Metadata": {
    "SensorId": "SENSOR-001",
    "LineId": "LINE-01"
  }
}
```

### 示例 2: 格口分配通知（无 DWS）

**方向**: RuleEngine → WheelDiverter

```json
{
  "ParcelId": 1734182263000,
  "ChuteId": 5,
  "AssignedAt": "2024-12-14T18:57:43.500+08:00",
  "DwsPayload": null,
  "Metadata": null
}
```

### 示例 3: 格口分配通知（含完整 DWS）

**方向**: RuleEngine → WheelDiverter

```json
{
  "ParcelId": 1734182263000,
  "ChuteId": 5,
  "AssignedAt": "2024-12-14T18:57:43.500+08:00",
  "DwsPayload": {
    "WeightGrams": 500.0,
    "LengthMm": 300.0,
    "WidthMm": 200.0,
    "HeightMm": 100.0,
    "VolumetricWeightGrams": 600.0,
    "Barcode": "PKG123456789",
    "MeasuredAt": "2024-12-14T18:57:42.000+08:00"
  },
  "Metadata": {
    "Priority": "High",
    "Destination": "Beijing"
  }
}
```

### 示例 4: 落格完成通知（成功）

**方向**: WheelDiverter → RuleEngine

```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1734182263000,
  "ActualChuteId": 5,
  "CompletedAt": "2024-12-14T18:57:45.000+08:00",
  "IsSuccess": true,
  "FinalStatus": "Success",
  "FailureReason": null
}
```

### 示例 5: 落格完成通知（超时）

**方向**: WheelDiverter → RuleEngine

```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1734182263000,
  "ActualChuteId": 999,
  "CompletedAt": "2024-12-14T18:58:00.000+08:00",
  "IsSuccess": false,
  "FinalStatus": "Timeout",
  "FailureReason": "Chute assignment timeout - no response within 10 seconds"
}
```

### 示例 6: 落格完成通知（丢失）

**方向**: WheelDiverter → RuleEngine

```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1734182263000,
  "ActualChuteId": 0,
  "CompletedAt": "2024-12-14T18:58:20.000+08:00",
  "IsSuccess": false,
  "FinalStatus": "Lost",
  "FailureReason": "Parcel lost - exceeded maximum lifetime without confirmation"
}
```

**注意**: `Lost` 状态时 `ActualChuteId` 固定为 `0`，因为包裹已不在输送线上，无法确定位置。

---

## 连接模式 / Connection Modes

### Client 模式（分拣系统主动连接）

**配置示例** (WheelDiverter 侧):
```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "ConnectionMode": "Client",
    "TcpServer": "192.168.1.100:5000",
    "EnableAutoReconnect": true,
    "TimeoutMs": 5000
  }
}
```

**连接行为**:
- WheelDiverter 主动连接到 RuleEngine 的监听端口
- 连接失败时自动重试（200ms → 400ms → 800ms → ... → 最大2秒）
- 无限重试，直到连接成功
- 连接管理完全内部化，调用方无需关心

**RuleEngine 实现要求**:
```csharp
// 伪代码示例
var server = new TcpListener(IPAddress.Any, 5000);
server.Start();

while (true)
{
    var client = await server.AcceptTcpClientAsync();
    _ = HandleClientAsync(client); // 异步处理
}

async Task HandleClientAsync(TcpClient client)
{
    using var stream = client.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8);
    using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    
    while (true)
    {
        var json = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(json)) break;
        
        var message = JsonSerializer.Deserialize<ParcelDetectionNotification>(json);
        if (message?.Type == "ParcelDetected")
        {
            await OnParcelDetectedAsync(message);
        }
        else if (message?.Type == "SortingCompleted")
        {
            var completed = JsonSerializer.Deserialize<SortingCompletedNotificationDto>(json);
            await OnSortingCompletedAsync(completed);
        }
    }
}
```

### Server 模式（分拣系统被动监听）

**配置示例** (WheelDiverter 侧):
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

**连接行为**:
- WheelDiverter 监听指定端口
- RuleEngine 主动连接到 WheelDiverter
- RuleEngine 需要实现自己的重连逻辑

**RuleEngine 实现要求**:
```csharp
// 伪代码示例
async Task ConnectToWheelDiverterAsync(string host, int port)
{
    int backoffMs = 200;
    
    while (true)
    {
        try
        {
            var client = new TcpClient();
            await client.ConnectAsync(host, port);
            
            _logger.LogInformation("已连接到 WheelDiverter: {Host}:{Port}", host, port);
            
            await CommunicateAsync(client);
            break;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("连接失败: {Error}, {Backoff}ms 后重试", ex.Message, backoffMs);
            await Task.Delay(backoffMs);
            backoffMs = Math.Min(backoffMs * 2, 2000); // 最大2秒
        }
    }
}
```

---

## 超时与丢失 / Timeout and Loss

### 时间参数计算

WheelDiverter 基于输送线物理参数动态计算超时时间：

```
分配超时 (Assignment Timeout) = (入口到首个决策点距离 / 线速) × 安全系数(0.9)
落格超时 (Sorting Timeout) = 路径总长度 / 线速
丢失判定 (Lost Detection) = (输送线总长度 / 线速) × 丢失检测安全系数(1.5)
```

**典型值**:
- 安全系数: `0.9`
- 丢失检测安全系数: `1.5`
- 降级超时: `5` 秒（无法动态计算时使用）

### 状态转换

```
[Detected] ─────────▶ [AssignmentReceived] ─────────▶ [Success]
     │                        │                            ▲
     │                        │                            │
     │ 超时 (10s)             │ 落格超时                    │
     ▼                        ▼                            │
[Timeout] ───▶ [Route to Exception Chute (999)] ─────────┘
     │
     │ 丢失判定 (30s)
     ▼
[Lost] ───▶ [Removed from Cache]
             ActualChuteId = 0
```

### 超时处理最佳实践

**RuleEngine 建议**:
1. **快速响应**: 收到检测通知后 < 1 秒内推送格口分配
2. **异步计算**: 不要在接收线程上执行长时间计算
3. **缓存规则**: 预先计算并缓存常见的路由规则
4. **监控延迟**: 记录通知到分配的时间，优化瓶颈

**超时后的处理**:
- 分拣系统会自动路由到异常格口（通常是 999）
- 发送 `FinalStatus=Timeout` 的完成通知
- RuleEngine 应该记录超时事件，用于后续分析

---

## 错误处理 / Error Handling

### 连接失败

**Client 模式**: 自动重试，无限重试直到连接成功  
**Server 模式**: 等待 RuleEngine 重新连接

### 发送失败

```csharp
bool sent = await _upstreamClient.SendAsync(message, cancellationToken);

if (!sent)
{
    // 发送失败只记录日志，不重试
    _logger.LogError("发送失败，包裹将路由到异常格口");
    // 系统继续运行，包裹路由到异常格口
}
```

**设计原则**: Fire-and-Forget，失败不重试，避免阻塞分拣流程

### 格口分配超时

**触发条件**: 发送检测通知后超过配置的超时时间未收到格口分配

**系统行为**:
1. 记录警告日志
2. 包裹路由到异常格口（999）
3. 发送 `FinalStatus=Timeout` 的完成通知

### 包裹丢失

**触发条件**: 包裹超过最大存活时间仍未落格

**系统行为**:
1. 标记为 `Lost` 状态
2. **从缓存中清除包裹记录**（防止队列错乱）
3. 发送 `FinalStatus=Lost, ActualChuteId=0` 的完成通知

**重要**: `Timeout` vs `Lost` 的区别:
- **Timeout**: 包裹仍在输送线上，可导向异常口
- **Lost**: 包裹已不在输送线上（可能掉落、卡住），无法导向异常口

---

## 实现检查清单 / Implementation Checklist

### RuleEngine 必须实现

#### 消息接收
- [ ] 接收并解析 `ParcelDetectionNotification`（JSON 格式）
- [ ] 接收并解析 `SortingCompletedNotificationDto`（JSON 格式）
- [ ] 字段类型匹配（`ParcelId` 为 `long`，`ChuteId` 为 `long`）
- [ ] 处理 `Lost` 状态（`ActualChuteId=0`）

#### 消息发送
- [ ] 主动推送 `ChuteAssignmentNotification`（JSON 格式）
- [ ] 正确设置 `ParcelId` 匹配检测通知
- [ ] 正确设置 `ChuteId`（必须是数字ID，如 1, 2, 3, 999）
- [ ] 可选：填充 `DwsPayload` 字段（尺寸重量数据）
- [ ] 响应时间 < 1 秒

#### 连接管理
- [ ] **Client 模式**: 监听端口，接受来自 WheelDiverter 的连接
- [ ] **Server 模式**: 主动连接到 WheelDiverter，失败时重试（指数退避）
- [ ] 处理连接断开和重连
- [ ] 处理网络超时和错误

#### 业务逻辑
- [ ] 异步执行分拣规则（不阻塞通信线程）
- [ ] 记录超时事件（用于分析和优化）
- [ ] 处理重复消息（幂等性）
- [ ] 记录所有消息用于审计

### 测试检查清单

#### 功能测试
- [ ] 正常流程：检测 → 分配 → 完成
- [ ] 超时流程：检测 → 超时 → 异常口完成
- [ ] 丢失流程：检测 → 丢失 → Lost 状态（ActualChuteId=0）
- [ ] DWS 数据传递完整性

#### 性能测试
- [ ] 格口分配响应时间 < 1 秒
- [ ] 并发包裹处理（每秒 10+ 个包裹）
- [ ] 网络延迟 < 100ms

#### 异常测试
- [ ] 连接断开后重连
- [ ] 消息乱序处理
- [ ] 重复消息处理（幂等性）
- [ ] 超大消息（> 1MB）处理

---

## 📚 参考资料 / References

### 相关文档
- **详细协议说明**: [UPSTREAM_CONNECTION_GUIDE.md](./guides/UPSTREAM_CONNECTION_GUIDE.md)
- **系统配置指南**: [SYSTEM_CONFIG_GUIDE.md](./guides/SYSTEM_CONFIG_GUIDE.md)
- **编码规范**: [../.github/copilot-instructions.md](../.github/copilot-instructions.md)

### 源码位置
| 类型 | 位置 |
|------|------|
| `IUpstreamRoutingClient` | `src/Core/.../Abstractions/Upstream/` |
| `ParcelDetectionNotification` | `src/Infrastructure/.../Communication/Models/` |
| `ChuteAssignmentNotification` | `src/Infrastructure/.../Communication/Models/` |
| `SortingCompletedNotificationDto` | `src/Infrastructure/.../Communication/Models/` |
| TCP 客户端实现 | `src/Infrastructure/.../Clients/TouchSocketTcpRuleEngineClient.cs` |

### 技术支持
- **GitHub**: https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter
- **Email**: support@example.com

---

**文档版本历史**:
- v1.0 (2025-12-14): 基于实际代码实现创建，100% 准确反映当前系统行为
