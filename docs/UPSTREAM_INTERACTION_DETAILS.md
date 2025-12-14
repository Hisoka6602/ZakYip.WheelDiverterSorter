# 上游交互详细说明 / Upstream Interaction Details

> **文档状态**: ✅ 基于实际代码实现  
> **最后更新**: 2025-12-14  
> **目标读者**: RuleEngine 上游系统开发者  
> **前置阅读**: [UPSTREAM_INTEGRATION_GUIDE.md](./UPSTREAM_INTEGRATION_GUIDE.md)

---

## 📌 文档目的

本文档详细说明**每个上游交互在什么情况下发生**，以及**上游应该如何响应**。补充集成指南中的技术细节，提供完整的业务场景和决策逻辑。

---

## 目录

1. [交互 1: 包裹检测通知](#交互-1-包裹检测通知)
2. [交互 2: 格口分配推送](#交互-2-格口分配推送)
3. [交互 3: 落格完成通知](#交互-3-落格完成通知)
4. [异常场景处理](#异常场景处理)
5. [时序关系图](#时序关系图)
6. [常见问题 FAQ](#常见问题-faq)

---

## 交互 1: 包裹检测通知

### 🔔 触发条件

**何时发生**: 当传感器检测到包裹进入分拣系统时

**详细触发链路**:
```
1. 硬件传感器 IO 触发（物理信号）
   ↓
2. Ingress 层：ParcelDetectionService 检测到传感器触发
   ↓
3. Execution 层：SortingOrchestrator.HandleParcelCreationAsync()
   ↓
4. 在本地创建包裹记录（ParcelId 生成）
   ↓
5. 发送上游通知（SendUpstreamNotificationAsync）
   ↓
6. IUpstreamRoutingClient.SendAsync(ParcelDetectedMessage)
```

**代码位置**: 
- `src/Execution/.../Orchestration/SortingOrchestrator.cs:725`
- 方法: `SendUpstreamNotificationAsync()`

### 📤 WheelDiverter 发送的数据

**消息类型**: `ParcelDetectedMessage`

**JSON 格式**:
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

**字段说明**:

| 字段 | 类型 | 必填 | 说明 | 示例值 |
|------|------|------|------|--------|
| `Type` | string | ✅ | 固定值 "ParcelDetected"，用于消息类型识别 | "ParcelDetected" |
| `ParcelId` | long | ✅ | 包裹唯一标识符，使用毫秒时间戳生成 | 1734182263000 |
| `DetectionTime` | DateTimeOffset | ✅ | 包裹检测时间，ISO 8601 格式，含时区 | "2024-12-14T18:57:43.000+08:00" |
| `Metadata` | object | ❌ | 可选的扩展信息（SensorId, LineId 等） | { "SensorId": "SENSOR-001" } |

### 🎯 上游应该做什么

#### 1. 接收并解析消息

```csharp
// 伪代码示例
public async Task OnMessageReceivedAsync(string jsonMessage)
{
    var message = JsonSerializer.Deserialize<ParcelDetectionNotification>(jsonMessage);
    
    if (message?.Type == "ParcelDetected")
    {
        await HandleParcelDetectedAsync(message);
    }
}
```

#### 2. 执行分拣规则（异步，不阻塞）

**重要**: 必须异步处理，不阻塞通信线程！

```csharp
public async Task HandleParcelDetectedAsync(ParcelDetectionNotification notification)
{
    _logger.LogInformation("收到包裹检测: ParcelId={ParcelId}", notification.ParcelId);
    
    // ⚠️ 关键：异步执行分拣规则，不阻塞通信
    _ = Task.Run(async () =>
    {
        try
        {
            // 执行分拣规则（可能需要查询数据库、调用外部服务）
            var chuteId = await CalculateTargetChuteAsync(notification.ParcelId);
            
            // 可选：查询 DWS 数据
            var dwsData = await QueryDwsDataAsync(notification.ParcelId);
            
            // 立即推送格口分配（见交互 2）
            await PushChuteAssignmentAsync(notification.ParcelId, chuteId, dwsData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理包裹 {ParcelId} 分拣规则失败", notification.ParcelId);
            // 注意：不推送格口分配，WheelDiverter 会超时并路由到异常格口
        }
    });
    
    // 立即返回，不等待规则执行完成
}
```

#### 3. 性能要求

| 指标 | 要求 | 说明 |
|------|------|------|
| **响应时间** | < 1 秒 | 从收到检测通知到推送格口分配 |
| **并发处理** | 10+ 包裹/秒 | 支持高吞吐量场景 |
| **超时时间** | 5-10 秒 | WheelDiverter 的默认超时时间（可配置） |

#### 4. 不要做的事情（❌ 禁止）

- ❌ **不要同步等待**: 不要在接收线程上执行长时间计算
- ❌ **不要返回响应**: 这不是请求-响应模式，不要试图返回数据
- ❌ **不要阻塞通信**: 必须立即返回，异步处理业务逻辑
- ❌ **不要假设顺序**: 包裹可能乱序到达，使用 ParcelId 匹配

### ⚠️ 异常处理

**场景 1: 无法发送通知（WheelDiverter 侧）**
```
原因：网络断开、上游服务不可用
行为：记录错误日志，包裹继续处理，路由到异常格口
影响：包裹不会等待上游响应，直接进入异常流程
```

**场景 2: 超时未收到格口分配（WheelDiverter 侧）**
```
原因：上游处理过慢、网络延迟、上游服务故障
行为：记录警告日志，包裹路由到异常格口（通常是 999）
影响：包裹仍然会完成分拣，但去向是异常格口
```

---

## 交互 2: 格口分配推送

### 🔔 触发条件

**何时发生**: 上游系统完成分拣规则计算后，**主动推送**格口分配

**重要**: 这不是对"包裹检测通知"的响应！这是独立的异步推送事件。

**时间窗口**: 从收到检测通知起，建议 < 1 秒内推送

### 📤 上游应该发送的数据

**消息类型**: `ChuteAssignmentNotification`

**JSON 格式（基础版）**:
```json
{
  "ParcelId": 1734182263000,
  "ChuteId": 5,
  "AssignedAt": "2024-12-14T18:57:43.500+08:00",
  "DwsPayload": null,
  "Metadata": null
}
```

**JSON 格式（完整版，含 DWS）**:
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
    "Destination": "Beijing",
    "OrderId": "ORD-2024-001"
  }
}
```

**字段说明**:

| 字段 | 类型 | 必填 | 说明 | 示例值 |
|------|------|------|------|--------|
| `ParcelId` | long | ✅ | **必须匹配**检测通知中的 ParcelId | 1734182263000 |
| `ChuteId` | long | ✅ | 目标格口 ID，**必须是数字** | 5 |
| `AssignedAt` | DateTimeOffset | ✅ | 分配时间，ISO 8601 格式 | "2024-12-14T18:57:43.500+08:00" |
| `DwsPayload` | object | ❌ | DWS（尺寸重量）数据，可选 | 见下表 |
| `Metadata` | object | ❌ | 额外元数据，可选 | { "Priority": "High" } |

**DwsPayload 字段说明**:

| 字段 | 类型 | 必填 | 说明 | 单位 | 示例值 |
|------|------|------|------|------|--------|
| `WeightGrams` | decimal | ✅ | 重量 | 克（g） | 500.0 |
| `LengthMm` | decimal | ✅ | 长度 | 毫米（mm） | 300.0 |
| `WidthMm` | decimal | ✅ | 宽度 | 毫米（mm） | 200.0 |
| `HeightMm` | decimal | ✅ | 高度 | 毫米（mm） | 100.0 |
| `VolumetricWeightGrams` | decimal | ❌ | 体积重量，可选 | 克（g） | 600.0 |
| `Barcode` | string | ❌ | 条码，可选 | - | "PKG123456789" |
| `MeasuredAt` | DateTimeOffset | ✅ | 测量时间 | - | "2024-12-14T18:57:42.000+08:00" |

### 🎯 上游必须遵守的规则

#### 1. ParcelId 匹配（必须）

```csharp
// ✅ 正确：使用检测通知中的 ParcelId
var assignment = new ChuteAssignmentNotification
{
    ParcelId = detectedParcelId,  // 必须匹配检测通知
    ChuteId = calculatedChuteId,
    AssignedAt = DateTimeOffset.Now
};

// ❌ 错误：使用自己生成的 ID
var assignment = new ChuteAssignmentNotification
{
    ParcelId = GenerateNewId(),  // ❌ WheelDiverter 无法匹配
    ChuteId = 5,
    AssignedAt = DateTimeOffset.Now
};
```

#### 2. ChuteId 格式（必须是数字）

```csharp
// ✅ 正确：数字 ID
"ChuteId": 5            // ✅
"ChuteId": 999          // ✅ 异常格口
"ChuteId": 101          // ✅

// ❌ 错误：字符串 ID
"ChuteId": "CHUTE-001"  // ❌ 会导致解析失败
"ChuteId": "A5"         // ❌ 不支持
```

#### 3. 推送时机（性能要求）

| 场景 | 推送时机 | 说明 |
|------|----------|------|
| **正常场景** | < 1 秒 | 从收到检测通知到推送格口分配 |
| **复杂规则** | < 5 秒 | 需要查询数据库或外部服务 |
| **超时阈值** | 5-10 秒 | 超过此时间，WheelDiverter 会超时 |

#### 4. 幂等性处理（必须支持）

**场景**: 网络重传可能导致同一个 ParcelId 收到多次检测通知

```csharp
// 使用内存缓存或数据库记录已处理的包裹
private readonly ConcurrentDictionary<long, bool> _processedParcels = new();

public async Task HandleParcelDetectedAsync(ParcelDetectionNotification notification)
{
    // 幂等性检查
    if (!_processedParcels.TryAdd(notification.ParcelId, true))
    {
        _logger.LogWarning("包裹 {ParcelId} 已处理，忽略重复通知", notification.ParcelId);
        return;  // 忽略重复通知
    }
    
    // 执行分拣规则...
}
```

### 📥 WheelDiverter 如何处理

**接收机制**: 通过 `IUpstreamRoutingClient.ChuteAssigned` 事件

```csharp
// WheelDiverter 侧代码（供参考）
_upstreamClient.ChuteAssigned += OnChuteAssignmentReceived;

private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentEventArgs e)
{
    // 验证包裹存在
    if (!_createdParcels.ContainsKey(e.ParcelId))
    {
        _logger.LogError("收到未知包裹 {ParcelId} 的路由响应，已丢弃", e.ParcelId);
        return;
    }
    
    // 检查是否超时
    if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
    {
        // 正常：在超时前收到
        _logger.LogDebug("收到包裹 {ParcelId} 的格口分配: {ChuteId}", e.ParcelId, e.ChuteId);
        tcs.TrySetResult(e.ChuteId);
        _pendingAssignments.TryRemove(e.ParcelId, out _);
    }
    else
    {
        // 迟到：包裹已超时并路由到异常格口
        _logger.LogInformation(
            "迟到的路由响应: ParcelId={ParcelId}, ChuteId={ChuteId}，" +
            "包裹已路由到异常格口，不再改变去向",
            e.ParcelId, e.ChuteId);
    }
}
```

### ⚠️ 异常处理

**场景 1: ParcelId 不匹配**
```
原因：上游使用了自己生成的 ID
行为：WheelDiverter 记录错误日志，丢弃该响应
影响：包裹超时，路由到异常格口
解决：确保使用检测通知中的 ParcelId
```

**场景 2: 推送超时（> 10 秒）**
```
原因：上游处理过慢
行为：WheelDiverter 已将包裹路由到异常格口
影响：迟到的分配会被记录但不会改变包裹去向
解决：优化分拣规则计算性能，使用缓存
```

**场景 3: ChuteId 无效（不存在）**
```
原因：上游返回的格口 ID 在系统中不存在
行为：WheelDiverter 记录错误日志，包裹路由到异常格口
影响：包裹不会分拣到指定格口
解决：确保 ChuteId 在 WheelDiverter 系统中已配置
```

---

## 交互 3: 落格完成通知

### 🔔 触发条件

**何时发生**: 包裹完成物理分拣，落入目标格口后

**详细触发链路**:
```
1. 包裹通过摆轮，物理落入格口
   ↓
2. 落格传感器触发（ChuteDropoff 类型传感器）
   ↓
3. Execution 层：HandleChuteDropoffSensorAsync()
   ↓
4. 构造 SortingCompletedNotification
   ↓
5. IUpstreamRoutingClient.SendAsync(SortingCompletedMessage)
```

**代码位置**: 
- `src/Execution/.../Orchestration/SortingOrchestrator.cs:815,1045,1757,1793,1962`
- 方法: 多个位置调用 `SendAsync(SortingCompletedMessage)`

### 📤 WheelDiverter 发送的数据

**消息类型**: `SortingCompletedMessage`

#### 场景 1: 成功分拣

**JSON 格式**:
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

**触发条件**: 
- 收到格口分配（ChuteId=5）
- 摆轮动作执行成功
- 包裹落入目标格口（格口5的落格传感器触发）

---

#### 场景 2: 超时（分配超时）

**JSON 格式**:
```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1734182263000,
  "ActualChuteId": 999,
  "CompletedAt": "2024-12-14T18:58:00.000+08:00",
  "IsSuccess": false,
  "FinalStatus": "Timeout",
  "FailureReason": "Chute assignment timeout - no response from RuleEngine within 10 seconds"
}
```

**触发条件**: 
- 发送检测通知后 > 10 秒未收到格口分配
- 包裹自动路由到异常格口（999）
- 所有摆轮设置为直行（PassThrough）
- 包裹落入异常格口

---

#### 场景 3: 超时（落格超时）

**JSON 格式**:
```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1734182263000,
  "ActualChuteId": 999,
  "CompletedAt": "2024-12-14T18:58:10.000+08:00",
  "IsSuccess": false,
  "FinalStatus": "Timeout",
  "FailureReason": "Sorting timeout - parcel did not reach target chute within expected time"
}
```

**触发条件**: 
- 收到格口分配（如 ChuteId=5）
- 摆轮动作执行成功
- 但包裹在预期时间内未到达目标格口
- 超时后路由到异常格口

---

#### 场景 4: 包裹丢失

**JSON 格式**:
```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1734182263000,
  "ActualChuteId": 0,
  "CompletedAt": "2024-12-14T18:58:20.000+08:00",
  "IsSuccess": false,
  "FinalStatus": "Lost",
  "FailureReason": "Parcel lost - exceeded maximum lifetime without drop-off confirmation"
}
```

**触发条件**: 
- 包裹从检测起超过最大存活时间（通常 30-60 秒）
- 仍未落格确认
- 推测包裹已物理丢失（掉落、卡住等）

**重要**: 
- `ActualChuteId` 固定为 `0`（包裹已不在输送线上，无法确定位置）
- WheelDiverter 已从缓存中清除该包裹记录

---

### 🎯 上游应该做什么

#### 1. 接收并解析消息

```csharp
public async Task OnMessageReceivedAsync(string jsonMessage)
{
    var message = JsonSerializer.Deserialize<SortingCompletedNotificationDto>(jsonMessage);
    
    if (message?.Type == "SortingCompleted")
    {
        await HandleSortingCompletedAsync(message);
    }
}
```

#### 2. 更新业务系统状态

```csharp
public async Task HandleSortingCompletedAsync(SortingCompletedNotificationDto notification)
{
    _logger.LogInformation(
        "包裹落格完成: ParcelId={ParcelId}, ActualChute={ChuteId}, Status={Status}, Success={Success}",
        notification.ParcelId,
        notification.ActualChuteId,
        notification.FinalStatus,
        notification.IsSuccess);
    
    // 根据不同状态更新业务系统
    switch (notification.FinalStatus)
    {
        case "Success":
            await UpdateParcelStatusAsync(notification.ParcelId, "Sorted", notification.ActualChuteId);
            await TriggerDownstreamProcessAsync(notification.ParcelId, notification.ActualChuteId);
            break;
            
        case "Timeout":
            await UpdateParcelStatusAsync(notification.ParcelId, "Exception", notification.ActualChuteId);
            await AlertOperatorAsync(notification.ParcelId, "分拣超时");
            await CreateExceptionWorkOrderAsync(notification.ParcelId, notification.FailureReason);
            break;
            
        case "Lost":
            await UpdateParcelStatusAsync(notification.ParcelId, "Lost", 0);
            await AlertOperatorAsync(notification.ParcelId, "包裹丢失");
            await InitiateInvestigationAsync(notification.ParcelId);
            break;
            
        case "ExecutionError":
            await UpdateParcelStatusAsync(notification.ParcelId, "Error", notification.ActualChuteId);
            await AlertOperatorAsync(notification.ParcelId, "执行错误");
            break;
    }
}
```

#### 3. 统计和监控

```csharp
// 记录统计数据
public async Task HandleSortingCompletedAsync(SortingCompletedNotificationDto notification)
{
    // 更新计数器
    _metrics.IncrementCounter($"parcel_status_{notification.FinalStatus.ToLower()}");
    
    // 记录延迟
    var latency = notification.CompletedAt - GetDetectionTime(notification.ParcelId);
    _metrics.RecordLatency("parcel_sorting_latency", latency.TotalMilliseconds);
    
    // 记录成功率
    if (notification.IsSuccess)
    {
        _metrics.IncrementCounter("parcel_success");
    }
    else
    {
        _metrics.IncrementCounter("parcel_failure");
        _metrics.IncrementCounter($"parcel_failure_reason_{notification.FinalStatus.ToLower()}");
    }
}
```

### 字段说明

| 字段 | 类型 | 必填 | 说明 | 示例值 |
|------|------|------|------|--------|
| `Type` | string | ✅ | 固定值 "SortingCompleted" | "SortingCompleted" |
| `ParcelId` | long | ✅ | 包裹 ID，匹配检测通知 | 1734182263000 |
| `ActualChuteId` | long | ✅ | 实际落格格口 ID（Lost时为0） | 5 / 999 / 0 |
| `CompletedAt` | DateTimeOffset | ✅ | 落格完成时间 | "2024-12-14T18:57:45.000+08:00" |
| `IsSuccess` | bool | ✅ | 是否成功 | true / false |
| `FinalStatus` | string | ✅ | 最终状态（枚举值） | "Success" / "Timeout" / "Lost" |
| `FailureReason` | string | ❌ | 失败原因（失败时提供） | "Chute assignment timeout..." |

### FinalStatus 枚举值

| 值 | 说明 | ActualChuteId | IsSuccess |
|----|------|---------------|-----------|
| `Success` | 成功分拣到目标格口 | 目标格口ID（如5） | true |
| `Timeout` | 超时，路由到异常格口 | 异常格口ID（通常999） | false |
| `Lost` | 包裹丢失，无法确定位置 | **固定为0** | false |
| `ExecutionError` | 执行错误 | 异常格口ID 或 0 | false |

---

## 异常场景处理

### 场景总览

| 场景 | 触发条件 | WheelDiverter 行为 | 上游应收到的通知 |
|------|----------|------------------|-----------------|
| **正常流程** | 一切正常 | 包裹分拣到目标格口 | FinalStatus=Success, ActualChuteId=目标格口 |
| **上游无响应** | 发送检测通知后 > 10秒未收到分配 | 包裹路由到异常格口（999） | FinalStatus=Timeout, ActualChuteId=999 |
| **上游响应慢** | 发送检测通知后 5-10秒才收到分配 | 可能超时，可能正常（取决于具体时间） | Success 或 Timeout |
| **网络断开** | 无法发送检测通知 | 包裹路由到异常格口（999） | （可能收不到通知） |
| **格口不存在** | 上游返回的 ChuteId 无效 | 包裹路由到异常格口（999） | FinalStatus=Timeout, ActualChuteId=999 |
| **包裹丢失** | 包裹超过最大存活时间未落格 | 从缓存清除，不再追踪 | FinalStatus=Lost, ActualChuteId=0 |

### 详细场景分析

#### 场景 1: 上游完全不响应

**原因**:
- 上游服务故障、重启
- 网络完全断开
- 上游处理逻辑异常（未推送格口分配）

**时序**:
```
T+0s:  WheelDiverter 发送 ParcelDetectedMessage
T+1s:  WheelDiverter 等待格口分配...
T+5s:  WheelDiverter 等待格口分配...
T+10s: WheelDiverter 超时，包裹路由到异常格口（999）
T+12s: 包裹落入异常格口，发送 SortingCompletedMessage (FinalStatus=Timeout)
```

**上游收到的通知**:
```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1734182263000,
  "ActualChuteId": 999,
  "FinalStatus": "Timeout",
  "FailureReason": "Chute assignment timeout - no response from RuleEngine within 10 seconds"
}
```

**上游应该做什么**:
1. 检查日志，确认是否收到检测通知
2. 如果收到但未推送分配，检查分拣规则逻辑
3. 如果未收到，检查网络连接和服务状态
4. 标记包裹为"异常处理"状态
5. 创建告警和工单

---

#### 场景 2: 上游响应延迟

**原因**:
- 分拣规则计算复杂
- 数据库查询慢
- 网络延迟

**时序（边界情况）**:
```
T+0s:    WheelDiverter 发送 ParcelDetectedMessage
T+1s:    上游收到通知，开始计算...
T+8s:    上游完成计算，推送 ChuteAssignmentNotification (ChuteId=5)
T+8.5s:  WheelDiverter 收到分配，执行摆轮动作
T+10s:   包裹落入目标格口5，发送 SortingCompletedMessage (FinalStatus=Success)
```

**时序（超时情况）**:
```
T+0s:    WheelDiverter 发送 ParcelDetectedMessage
T+1s:    上游收到通知，开始计算...
T+10s:   WheelDiverter 超时，包裹路由到异常格口（999）
T+12s:   包裹落入异常格口，发送 SortingCompletedMessage (FinalStatus=Timeout)
T+15s:   上游完成计算，推送 ChuteAssignmentNotification (ChuteId=5)  // 迟到！
         WheelDiverter 记录日志"迟到的路由响应"，不改变包裹去向
```

**上游应该做什么**:
1. 监控从收到检测通知到推送分配的延迟
2. 优化分拣规则计算性能（使用缓存、索引）
3. 如果延迟 > 5 秒，触发告警
4. 对于迟到的响应，上游仍会收到 Timeout 通知

---

#### 场景 3: 包裹丢失

**原因**:
- 包裹从输送线掉落
- 包裹卡在某处无法移动
- 落格传感器故障（未能检测到落格）

**时序**:
```
T+0s:    传感器检测到包裹，发送 ParcelDetectedMessage
T+1s:    上游推送 ChuteAssignmentNotification (ChuteId=5)
T+2s:    WheelDiverter 执行摆轮动作
T+3s:    包裹物理掉落（从输送线上消失）
T+30s:   WheelDiverter 包裹丢失监控检测到超时
         从缓存清除包裹记录
         发送 SortingCompletedMessage (FinalStatus=Lost, ActualChuteId=0)
```

**上游收到的通知**:
```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1734182263000,
  "ActualChuteId": 0,
  "FinalStatus": "Lost",
  "FailureReason": "Parcel lost - exceeded maximum lifetime without drop-off confirmation"
}
```

**上游应该做什么**:
1. **重要**: `ActualChuteId=0` 表示无法确定位置
2. 标记包裹为"丢失"状态
3. 创建高优先级告警
4. 触发人工介入调查
5. 检查输送线和传感器状态
6. 可能需要物理搜索包裹

**区分 Timeout vs Lost**:
| 特征 | Timeout | Lost |
|------|---------|------|
| 包裹位置 | 在输送线上，在异常格口 | 不在输送线上，位置未知 |
| ActualChuteId | 999（异常格口） | 0（无法确定） |
| 可恢复性 | 可以从异常格口取出 | 需要物理搜索 |
| 严重程度 | 中等（流程问题） | 高（物理丢失） |

---

## 时序关系图

### 正常流程

```
WheelDiverter                  RuleEngine
     │                              │
     │ ① 传感器检测                 │
     │ 创建包裹(ParcelId)           │
     │                              │
     │ ② ParcelDetectedMessage      │
     ├─────────────────────────────▶│
     │  (Fire-and-Forget)           │
     │                              │ ③ 执行分拣规则
     │                              │   (异步，不阻塞)
     │ ④ ChuteAssignmentNotification│
     │◀─────────────────────────────┤
     │  (主动推送，不是响应)         │
     │                              │
     │ ⑤ 执行摆轮动作               │
     │ ⑥ 包裹落格                   │
     │                              │
     │ ⑦ SortingCompletedMessage    │
     ├─────────────────────────────▶│
     │  (FinalStatus=Success)       │
     │                              │
```

### 超时流程

```
WheelDiverter                  RuleEngine
     │                              │
     │ ① ParcelDetectedMessage      │
     ├─────────────────────────────▶│
     │                              │ ② 处理过慢...
     │ ③ 等待...                    │
     │ (10秒超时)                   │
     │                              │
     │ ④ 超时，路由到异常格口(999)  │
     │                              │
     │ ⑤ 包裹落入异常格口           │
     │                              │
     │ ⑥ SortingCompletedMessage    │
     ├─────────────────────────────▶│
     │  (FinalStatus=Timeout)       │
     │                              │
     │                              │ ⑦ 迟到的分配(可选)
     │ ChuteAssignmentNotification  │
     │◀─────────────────────────────┤
     │  (已忽略，包裹已完成)        │
     │                              │
```

### 丢失流程

```
WheelDiverter                  RuleEngine
     │                              │
     │ ① ParcelDetectedMessage      │
     ├─────────────────────────────▶│
     │                              │
     │ ② ChuteAssignmentNotification│
     │◀─────────────────────────────┤
     │                              │
     │ ③ 执行摆轮动作               │
     │ ④ 包裹物理丢失❌             │
     │ (从输送线掉落)                │
     │                              │
     │ ⑤ 等待落格确认...            │
     │ (30秒无响应)                 │
     │                              │
     │ ⑥ 包裹丢失监控触发           │
     │ 从缓存清除                   │
     │                              │
     │ ⑦ SortingCompletedMessage    │
     ├─────────────────────────────▶│
     │  (FinalStatus=Lost, ActualChuteId=0)
     │                              │
```

---

## 常见问题 FAQ

### Q1: 为什么发送检测通知后不等待响应？

**A**: 这是 Fire-and-Forget 设计，原因：
1. **性能**: 不阻塞分拣流程，支持高吞吐量
2. **容错**: 上游故障不影响系统运行（包裹路由到异常格口）
3. **解耦**: 分拣系统和上游系统独立运行

### Q2: 如何保证包裹和格口分配的匹配？

**A**: 通过 ParcelId 匹配：
1. WheelDiverter 在检测通知中发送 ParcelId
2. 上游必须在格口分配中使用**相同的 ParcelId**
3. WheelDiverter 收到分配后，通过 ParcelId 找到对应包裹

### Q3: 超时时间是固定的还是可配置的？

**A**: 动态计算 + 可配置：
- **动态计算**: 基于输送线长度和速度：`超时时间 = 距离 / 速度 × 安全系数(0.9)`
- **降级值**: 无法动态计算时使用固定值（默认 5-10 秒）
- **可配置**: 通过 `ChuteAssignmentTimeoutOptions` 配置安全系数和降级值

### Q4: 上游如何知道哪些格口可用？

**A**: 两种方式：
1. **配置同步**: 在系统初始化时，从 WheelDiverter 读取格口配置
2. **健康检查**: 定期查询 `/api/health` 端点获取系统状态和可用格口列表

### Q5: 如果格口分配的 ChuteId 不存在怎么办？

**A**: WheelDiverter 会：
1. 记录错误日志："格口 X 不存在于拓扑配置中"
2. 包裹路由到异常格口（999）
3. 发送 `FinalStatus=Timeout` 的完成通知

**建议**: 上游在推送前验证 ChuteId 的有效性

### Q6: 包裹丢失时为什么 ActualChuteId=0？

**A**: 因为：
1. 包裹已不在输送线上（物理丢失）
2. 无法确定包裹最终位置
3. 0 表示"位置未知"，区别于正常格口 ID（1, 2, 3...）和异常格口（999）

### Q7: 上游推送格口分配时可以使用字符串 ID 吗？

**A**: ❌ **不可以**，必须使用数字 ID（long 类型）
- ✅ 正确: `"ChuteId": 5`
- ❌ 错误: `"ChuteId": "CHUTE-001"`

如果上游系统使用字符串 ID，需要维护映射关系：
```csharp
var mapping = new Dictionary<string, long>
{
    ["CHUTE-001"] = 1,
    ["CHUTE-002"] = 2,
    ["EXCEPTION"] = 999
};

var numericChuteId = mapping[stringChuteId];
```

### Q8: DWS 数据是必填的吗？

**A**: ❌ **不是必填**，完全可选
- 如果有 DWS 设备，可以在格口分配时附带
- 如果没有，设置 `"DwsPayload": null` 即可
- WheelDiverter 会存储但不强制使用 DWS 数据

### Q9: 如何测试上游集成？

**A**: 推荐步骤：
1. **使用 Simulation.Cli**: 模拟包裹检测和落格
2. **检查日志**: 观察通知发送和接收
3. **监控延迟**: 测量从检测到分配的时间
4. **异常测试**: 故意延迟响应，触发超时流程
5. **压力测试**: 并发发送多个包裹，验证吞吐量

### Q10: 上游系统重启时会发生什么？

**A**: 
1. **正在处理的包裹**: 会超时并路由到异常格口
2. **连接状态**: WheelDiverter 会自动重连（指数退避，最大2秒）
3. **数据丢失**: 上游需要自己实现持久化，WheelDiverter 不会重发历史通知

**建议**: 
- 上游实现持久化（数据库、消息队列）
- 记录所有收到的通知
- 系统重启后可以查询历史数据

---

## 📚 参考资料

- **技术集成指南**: [UPSTREAM_INTEGRATION_GUIDE.md](./UPSTREAM_INTEGRATION_GUIDE.md)
- **协议配置说明**: [guides/UPSTREAM_CONNECTION_GUIDE.md](./guides/UPSTREAM_CONNECTION_GUIDE.md)
- **系统配置指南**: [guides/SYSTEM_CONFIG_GUIDE.md](./guides/SYSTEM_CONFIG_GUIDE.md)

---

**文档版本**: 1.0  
**创建日期**: 2025-12-14  
**作者**: ZakYip Development Team
