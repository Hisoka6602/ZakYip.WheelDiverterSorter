# 上游格口分配工作流详解

> **文档类型**: 系统集成与业务流程说明  
> **优先级**: 🔴 **P0 - 核心业务流程**  
> **创建时间**: 2025-12-22  
> **适用场景**: 理解上游系统（RuleEngine）格口分配后的完整处理流程

---

## 📋 文档概述

本文档详细说明了**收到上游格口信息后的完整逻辑操作**，包括：
- 上游通信模式（Fire-and-Forget）
- Parcel-First 流程
- Position-Index 队列系统
- 瑞典（Sweden）系统集成点
- 超时处理与故障恢复

---

## 一、核心概念回顾

### 1.1 Parcel-First 原则

**规则**: 必须先在本地创建包裹实体，再向上游请求路由分配。

```
正确流程：
1. 入口传感器检测到包裹
2. 创建本地包裹实体（分配 ParcelId）
3. 向上游发送 ParcelDetectionNotification
4. 等待上游异步推送 ChuteAssignmentNotification
5. 收到格口分配后，生成路径并入队
6. IO 触发时执行摆轮动作
```

**禁止行为**:
- ❌ 先请求路由，再创建包裹
- ❌ 收到上游响应后才创建包裹（幽灵包裹）
- ❌ 没有本地包裹实体就向上游请求路由

### 1.2 Fire-and-Forget 通信模式

**特点**: 完全异步，不等待响应

```
┌──────────────────┐                      ┌──────────────────┐
│   分拣系统        │                      │   上游系统        │
│  (WheelDiverter) │                      │  (RuleEngine)    │
└────────┬─────────┘                      └────────┬─────────┘
         │                                         │
         │  1. ParcelDetectionNotification         │
         │  ─────────────────────────────────────▶ │
         │  (fire-and-forget，不等待响应)          │
         │                                         │
         │  2. ChuteAssignmentNotification         │
         │  ◀───────────────────────────────────── │
         │  (异步推送，非请求-响应)                │
         │                                         │
         │  3. 包裹分拣执行...                     │
         │                                         │
         │  4. SortingCompletedNotification        │
         │  ─────────────────────────────────────▶ │
         │  (fire-and-forget，通知完成状态)        │
         │                                         │
```

---

## 二、完整工作流程

### 2.1 阶段 1: 入口检测与包裹创建

**触发点**: 入口传感器 IO 触发

**执行位置**: `SortingOrchestrator.OnParcelDetected()`

**操作步骤**:

```csharp
// 步骤 1.1: 创建本地包裹实体（Parcel-First）
await CreateParcelEntityAsync(parcelId, sensorId);

// 记录包裹创建信息
_createdParcels[parcelId] = new ParcelCreationRecord
{
    ParcelId = parcelId,
    CreatedAt = _clock.LocalNowOffset,
    UpstreamRequestSentAt = null,
    UpstreamReplyReceivedAt = null,
    RouteBoundAt = null
};

// 步骤 1.2: 验证系统状态
var stateValidation = await ValidateSystemStateAsync(parcelId);
if (!stateValidation.IsValid)
{
    // 系统状态不允许接收包裹，拒绝处理
    CleanupParcelRecord(parcelId);
    return;
}

// 步骤 1.3: 拥堵检测
var overloadDecision = await DetectCongestionAndOverloadAsync(parcelId);
```

**关键数据结构**:
```csharp
// 包裹创建记录
class ParcelCreationRecord
{
    public long ParcelId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpstreamRequestSentAt { get; set; }
    public DateTimeOffset? UpstreamReplyReceivedAt { get; set; }
    public DateTimeOffset? RouteBoundAt { get; set; }
}
```

---

### 2.2 阶段 2: 向上游发送检测通知

**执行位置**: `SortingOrchestrator.SendUpstreamNotificationAsync()`

**操作步骤**:

```csharp
// 步骤 2.1: Invariant 检查 - 确保本地包裹已存在
if (!_createdParcels.ContainsKey(parcelId))
{
    _logger.LogError(
        "[Invariant Violation] 尝试为不存在的包裹 {ParcelId} 发送上游通知。" +
        "通知已阻止，不发送到上游。",
        parcelId);
    return;
}

// 步骤 2.2: 记录发送时间
var upstreamRequestSentAt = _clock.LocalNowOffset;
_createdParcels[parcelId].UpstreamRequestSentAt = upstreamRequestSentAt;

// 步骤 2.3: 发送包裹检测通知（fire-and-forget）
var notificationSent = await _upstreamClient.SendAsync(
    new ParcelDetectedMessage 
    { 
        ParcelId = parcelId, 
        DetectedAt = _clock.LocalNowOffset 
    }, 
    CancellationToken.None);

// 步骤 2.4: 记录发送结果（仅日志，不阻塞流程）
if (!notificationSent)
{
    _logger.LogError(
        "包裹 {ParcelId} 无法发送检测通知到上游系统。连接失败或上游不可用。",
        parcelId);
}
```

**数据结构**:
```json
// ParcelDetectionNotification
{
  "ParcelId": 1701446263000,
  "DetectionTime": "2024-12-01T18:57:43+08:00",
  "Metadata": {
    "SensorId": "Sensor001",
    "LineId": "Line01"
  }
}
```

**瑞典集成点 🇸🇪**:
- **集成接口**: `IUpstreamRoutingClient.SendAsync(ParcelDetectedMessage)`
- **瑞典系统角色**: 作为上游 RuleEngine，接收包裹检测通知
- **集成协议**: TCP/SignalR/MQTT（根据配置选择）
- **数据交换**: JSON 序列化的 `ParcelDetectionNotification`

---

### 2.3 阶段 3: 等待格口分配（含超时处理）

**执行位置**: `SortingOrchestrator.GetChuteFromUpstreamAsync()`

**操作步骤**:

```csharp
// 步骤 3.1: 创建等待任务
var tcs = new TaskCompletionSource<long>();
_pendingAssignments[parcelId] = tcs;

// 步骤 3.2: 计算超时时间
var timeout = _timeoutCalculator?.CalculateAssignmentTimeout(parcelId) 
              ?? TimeSpan.FromMilliseconds(_options.ChuteAssignmentTimeoutMs);

// 步骤 3.3: 等待格口分配（带超时）
var delayTask = Task.Delay(timeout, cancellationToken);
var completedTask = await Task.WhenAny(tcs.Task, delayTask);

// 步骤 3.4: 判断结果
if (completedTask == tcs.Task)
{
    // 正常收到格口分配
    var chuteId = await tcs.Task;
    _logger.LogInformation(
        "包裹 {ParcelId} 收到上游格口分配: {ChuteId}, 延迟: {LatencyMs}ms",
        parcelId, chuteId, 
        (_clock.LocalNow - _createdParcels[parcelId].UpstreamRequestSentAt.Value).TotalMilliseconds);
    return chuteId;
}
else
{
    // 超时，使用异常格口
    _logger.LogWarning(
        "包裹 {ParcelId} 等待上游格口分配超时 ({TimeoutMs}ms)，将使用异常格口",
        parcelId, timeout.TotalMilliseconds);
    
    _pendingAssignments.TryRemove(parcelId, out _);
    return exceptionChuteId;
}
```

**超时计算公式**:
```
超时时间 = (入口到首个决策点距离 / 线速) × SafetyFactor

例如：
- 距离: 10 米
- 线速: 1 m/s
- SafetyFactor: 0.9
- 超时时间 = 10 / 1 × 0.9 = 9 秒
```

---

### 2.4 阶段 4: 接收格口分配（异步回调）

**触发点**: `IUpstreamRoutingClient.ChuteAssigned` 事件

**执行位置**: `SortingOrchestrator.OnChuteAssignmentReceived()`

**操作步骤**:

```csharp
// 步骤 4.1: Invariant 检查 - 确保本地包裹存在
if (!_createdParcels.ContainsKey(e.ParcelId))
{
    _logger.LogError(
        "[Invariant Violation] 收到未知包裹 {ParcelId} 的路由响应 (ChuteId={ChuteId})，" +
        "本地不存在此包裹实体。响应已丢弃，不创建幽灵包裹。",
        e.ParcelId, e.ChuteId);
    return; // 🔴 防止幽灵包裹
}

// 步骤 4.2: 记录响应接收时间
_createdParcels[e.ParcelId].UpstreamReplyReceivedAt = _clock.LocalNowOffset;

// 步骤 4.3: 尝试完成等待任务
if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
{
    // 正常情况：在超时前收到响应
    _logger.LogDebug("收到包裹 {ParcelId} 的格口分配: {ChuteId}", e.ParcelId, e.ChuteId);
    
    // 记录路由绑定时间
    _createdParcels[e.ParcelId].RouteBoundAt = _clock.LocalNowOffset;
    
    // 完成等待任务
    tcs.TrySetResult(e.ChuteId);
    _pendingAssignments.TryRemove(e.ParcelId, out _);
}
else
{
    // 迟到的响应：包裹已经超时并被路由到异常口
    _logger.LogInformation(
        "【迟到路由响应】收到包裹 {ParcelId} 的格口分配 (ChuteId={ChuteId})，" +
        "但该包裹已因超时被路由到异常口，不再改变去向。",
        e.ParcelId, e.ChuteId);
}
```

**数据结构**:
```json
// ChuteAssignmentNotification
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
  }
}
```

**瑞典集成点 🇸🇪**:
- **集成接口**: `IUpstreamRoutingClient.ChuteAssigned` 事件
- **瑞典系统角色**: 作为上游 RuleEngine，推送格口分配结果
- **集成协议**: TCP/SignalR/MQTT
- **数据交换**: JSON 序列化的 `ChuteAssignmentNotification`
- **DWS 数据**: 可选携带包裹尺寸重量数据（来自瑞典系统的 DWS 设备）

---

### 2.5 阶段 5: 生成路径并入队

**执行位置**: `SortingOrchestrator.ProcessParcelAsync()` 后半部分

**操作步骤**:

```csharp
// 步骤 5.1: 确定目标格口（来自上游或本地策略）
var targetChuteId = await DetermineTargetChuteAsync(parcelId, overloadDecision);

// 步骤 5.2: 生成队列任务
var queueTasks = _pathGenerator.GenerateQueueTasks(
    parcelId,
    targetChuteId,
    _clock.LocalNow);

// 步骤 5.3: 记录包裹目标格口
_parcelTargetChutes[parcelId] = targetChuteId;

// 步骤 5.4: 将任务加入对应的 Position-Index 队列
foreach (var task in queueTasks)
{
    _queueManager.EnqueueTask(task.PositionIndex, task);
}

_logger.LogInformation(
    "[生命周期-入队] P{ParcelId} {TaskCount}任务入队 目标C{TargetChuteId}",
    parcelId,
    queueTasks.Count,
    targetChuteId);
```

**队列任务结构**:
```csharp
public record PositionIndexTask
{
    public required long ParcelId { get; init; }
    public required int PositionIndex { get; init; }
    public required long DiverterId { get; init; }
    public required DiverterDirection Action { get; init; }
    public required DateTimeOffset ExpectedArrivalTime { get; init; }
    public required int TimeoutToleranceMs { get; init; }
    public DiverterDirection FallbackAction { get; init; } = DiverterDirection.Straight;
}
```

**示例**:
```json
// 包裹 P1 目标格口 101，需经过 2 个摆轮
[
  {
    "ParcelId": 1701446263000,
    "PositionIndex": 1,
    "DiverterId": 1,
    "Action": "Straight",
    "ExpectedArrivalTime": "2024-12-01T18:57:45.000+08:00",
    "TimeoutToleranceMs": 2000,
    "FallbackAction": "Straight"
  },
  {
    "ParcelId": 1701446263000,
    "PositionIndex": 2,
    "DiverterId": 2,
    "Action": "Left",
    "ExpectedArrivalTime": "2024-12-01T18:57:50.000+08:00",
    "TimeoutToleranceMs": 2000,
    "FallbackAction": "Straight"
  }
]
```

---

### 2.6 阶段 6: IO 触发执行摆轮动作

**触发点**: 摆轮前传感器（WheelFront）IO 触发

**执行位置**: `SortingOrchestrator.HandleWheelFrontSensorAsync()`

**操作步骤**:

```csharp
// 步骤 6.1: 从 Position-Index 队列取出任务
var task = _queueManager.DequeueTask(positionIndex);

if (task == null)
{
    _logger.LogWarning(
        "Position {PositionIndex} 队列为空，但传感器 {SensorId} 被触发",
        positionIndex, sensorId);
    return;
}

// 步骤 6.2: 检查是否超时
var now = _clock.LocalNow;
var isTimeout = now > (task.ExpectedArrivalTime + TimeSpan.FromMilliseconds(task.TimeoutToleranceMs));

// 步骤 6.3: 确定执行动作
var actionToExecute = isTimeout ? task.FallbackAction : task.Action;

// 步骤 6.4: 执行摆轮动作
var command = new WheelCommand
{
    DiverterId = task.DiverterId,
    Direction = actionToExecute,
    ParcelId = task.ParcelId,
    TimeoutMs = DefaultSingleActionTimeoutMs
};

var result = await _pathExecutor.ExecuteAsync(command);

// 步骤 6.5: 超时补偿（如需要）
if (isTimeout && actionToExecute == DiverterDirection.Straight)
{
    // 在后续节点插入补偿任务
    InsertTimeoutCompensationTasks(task.ParcelId, positionIndex);
}

// 步骤 6.6: 判断是否为最后一个摆轮
if (IsLastDiverterInTopology(positionIndex))
{
    // 最后一个摆轮，发送落格完成通知
    var actualChuteId = await DetermineActualChuteIdAsync(task.ParcelId, actionToExecute, task.DiverterId);
    await NotifyUpstreamSortingCompletedAsync(
        task.ParcelId,
        actualChuteId,
        isSuccess: !isTimeout,
        failureReason: isTimeout ? "SortingTimeout" : null);
    
    CleanupParcelMemory(task.ParcelId);
}
```

---

### 2.7 阶段 7: 发送落格完成通知

**触发点**: 
1. 最后一个摆轮执行完成
2. 或落格传感器触发（如配置）

**执行位置**: `SortingOrchestrator.NotifyUpstreamSortingCompletedAsync()`

**操作步骤**:

```csharp
// 步骤 7.1: 构建通知消息
var notification = new SortingCompletedNotification
{
    ParcelId = parcelId,
    ActualChuteId = actualChuteId,
    CompletedAt = _clock.LocalNowOffset,
    IsSuccess = isSuccess,
    FailureReason = failureReason,
    FinalStatus = finalStatus ?? (isSuccess 
        ? ParcelFinalStatus.Success 
        : ParcelFinalStatus.ExecutionError)
};

// 步骤 7.2: 发送通知（fire-and-forget）
var notificationSent = await _upstreamClient.SendAsync(
    new SortingCompletedMessage { Notification = notification }, 
    CancellationToken.None);

// 步骤 7.3: 记录结果
if (notificationSent)
{
    _logger.LogInformation(
        "包裹 {ParcelId} 已成功发送分拣完成通知到上游系统: " +
        "ActualChuteId={ActualChuteId}, IsSuccess={IsSuccess}",
        parcelId, actualChuteId, isSuccess);
}
else
{
    _logger.LogError(
        "包裹 {ParcelId} 无法发送分拣完成通知到上游系统。连接失败或上游不可用。",
        parcelId);
}

// 步骤 7.4: 清理本地记录
CleanupParcelMemory(parcelId);
```

**数据结构**:
```json
// SortingCompletedNotification
{
  "ParcelId": 1701446263000,
  "ActualChuteId": 101,
  "CompletedAt": "2024-12-01T18:57:52.000+08:00",
  "IsSuccess": true,
  "FinalStatus": "Success",
  "FailureReason": null
}
```

**FinalStatus 枚举**:
| 值 | 说明 |
|----|------|
| `Success` | 包裹成功分拣到目标格口 |
| `Timeout` | 分配超时或落格超时，路由到异常格口 |
| `Lost` | 包裹丢失，无法确定位置，已从缓存清除 |

**瑞典集成点 🇸🇪**:
- **集成接口**: `IUpstreamRoutingClient.SendAsync(SortingCompletedMessage)`
- **瑞典系统角色**: 作为上游 RuleEngine，接收分拣完成通知
- **集成协议**: TCP/SignalR/MQTT
- **数据交换**: JSON 序列化的 `SortingCompletedNotification`
- **业务价值**: 瑞典系统可根据此通知更新包裹状态、生成报表、触发下游流程

---

## 三、瑞典系统集成说明 🇸🇪

### 3.1 集成架构

```
┌─────────────────────────────────────────────────────┐
│                 瑞典上游系统 (Sweden)                │
│                    (RuleEngine)                     │
│  ┌───────────────────────────────────────────────┐ │
│  │  • 包裹路由决策引擎                            │ │
│  │  • DWS 数据管理                                │ │
│  │  • 分拣任务协调                                │ │
│  │  • 报表与统计                                  │ │
│  └───────────────────────────────────────────────┘ │
└──────────────────┬──────────────────┬───────────────┘
                   │ TCP/SignalR/MQTT │
                   │                  │
        ┌──────────▼──────────┐  ┌───▼──────────────┐
        │ ParcelDetection     │  │ ChuteAssignment  │
        │ Notification        │  │ Notification     │
        │ (上报)              │  │ (下发)           │
        └──────────┬──────────┘  └───┬──────────────┘
                   │                 │
                   │                 │
        ┌──────────▼─────────────────▼──────────────┐
        │   中国分拣系统 (China Sorter)              │
        │   ZakYip.WheelDiverterSorter              │
        │  ┌─────────────────────────────────────┐  │
        │  │  IUpstreamRoutingClient             │  │
        │  │  • SendAsync() - 发送通知           │  │
        │  │  • ChuteAssigned - 接收分配         │  │
        │  └─────────────────────────────────────┘  │
        └───────────────────────────────────────────┘
```

### 3.2 集成模式

**支持的通信协议**:
1. **TCP Socket** (推荐)
   - 高性能、低延迟
   - 支持 Client/Server 双模式
   - 自动重连，最大退避 2 秒

2. **SignalR**
   - 实时双向通信
   - 内置心跳检测
   - 适合 Web 场景

3. **MQTT**
   - 物联网标准协议
   - 支持 QoS 保证
   - 适合分布式部署

**连接模式**:
- **Client 模式**: 分拣系统主动连接到瑞典 RuleEngine
- **Server 模式**: 分拣系统作为服务器，瑞典 RuleEngine 连接过来

### 3.3 集成数据流

#### 数据流 1: 包裹检测上报
```
中国分拣系统                               瑞典系统
    │                                         │
    │  ParcelDetectionNotification            │
    │  ─────────────────────────────────────▶ │
    │  {                                      │
    │    ParcelId: 1701446263000,             │
    │    DetectionTime: "2024-12-01T18:57:43" │ ── 记录入库
    │  }                                      │ ── 触发路由决策
    │                                         │
```

#### 数据流 2: 格口分配下发
```
瑞典系统                                   中国分拣系统
    │                                         │
    │  ChuteAssignmentNotification            │
    │  ◀───────────────────────────────────── │
    │  {                                      │
    │    ParcelId: 1701446263000,             │
    │    ChuteId: 101,                        │ ── 生成路径
    │    DwsPayload: { ... }                  │ ── 入队等待
    │  }                                      │
    │                                         │
```

#### 数据流 3: 分拣完成上报
```
中国分拣系统                               瑞典系统
    │                                         │
    │  SortingCompletedNotification           │
    │  ─────────────────────────────────────▶ │
    │  {                                      │
    │    ParcelId: 1701446263000,             │
    │    ActualChuteId: 101,                  │ ── 更新包裹状态
    │    FinalStatus: "Success"               │ ── 生成报表
    │  }                                      │ ── 触发下游流程
    │                                         │
```

### 3.4 DWS 数据集成

**DWS (Dimensioning, Weighing, Scanning)** - 尺寸重量扫描系统

瑞典系统可在 `ChuteAssignmentNotification` 中携带 DWS 数据：

```json
{
  "ParcelId": 1701446263000,
  "ChuteId": 101,
  "AssignedAt": "2024-12-01T18:57:43.500+08:00",
  "DwsPayload": {
    "WeightGrams": 500.0,      // 重量（克）
    "LengthMm": 300.0,         // 长度（毫米）
    "WidthMm": 200.0,          // 宽度（毫米）
    "HeightMm": 100.0,         // 高度（毫米）
    "Barcode": "PKG123456"     // 条码
  }
}
```

**用途**:
- 包裹信息完整性验证
- 分拣决策优化（根据尺寸重量）
- 后续报表与统计

---

## 四、故障处理与容错

### 4.1 超时处理

**分配超时** (AssignmentTimeout):
```csharp
// 超时时间 = (入口到首个决策点距离 / 线速) × SafetyFactor
var timeout = CalculateAssignmentTimeout(parcelId);

// 超时后自动使用异常格口
if (await Task.WhenAny(tcs.Task, Task.Delay(timeout)) != tcs.Task)
{
    _logger.LogWarning(
        "包裹 {ParcelId} 等待上游格口分配超时，使用异常格口",
        parcelId);
    return exceptionChuteId;
}
```

**执行超时** (SortingTimeout):
```csharp
// 包裹到达摆轮时间超过预期
var isTimeout = now > (task.ExpectedArrivalTime + task.TimeoutTolerance);

if (isTimeout)
{
    // 执行异常动作（直通）
    actionToExecute = task.FallbackAction; // Straight
    
    // 插入后续节点的补偿任务
    InsertTimeoutCompensationTasks(parcelId, positionIndex);
}
```

### 4.2 包裹丢失处理

**丢失判定**:
```
最大存活时间 = (输送线总长度 / 线速) × LostDetectionSafetyFactor
```

**处理流程**:
```csharp
// 1. 从所有队列删除丢失包裹的任务
_queueManager.RemoveAllTasksForParcel(lostParcelId);

// 2. 将受影响包裹的任务改为直行
foreach (var affectedParcel in affectedParcels)
{
    _queueManager.ChangeAllTasksToStraight(affectedParcel.ParcelId);
}

// 3. 通知上游包裹丢失
await _upstreamClient.SendAsync(new SortingCompletedMessage
{
    Notification = new SortingCompletedNotification
    {
        ParcelId = lostParcelId,
        ActualChuteId = 0,  // 丢失，无格口
        FinalStatus = ParcelFinalStatus.Lost
    }
});

// 4. 清理本地记录
CleanupParcelMemory(lostParcelId);
```

### 4.3 迟到响应处理

**场景**: 包裹已超时并路由到异常口，此时收到上游格口分配

**处理策略**:
```csharp
if (_pendingAssignments.TryGetValue(parcelId, out var tcs))
{
    // 正常：在超时前收到
    tcs.TrySetResult(chuteId);
}
else
{
    // 迟到：包裹已超时
    _logger.LogInformation(
        "【迟到路由响应】包裹 {ParcelId} 的格口分配 (ChuteId={ChuteId}) 已迟到，" +
        "包裹已被路由到异常口，不再改变去向。",
        parcelId, chuteId);
    // 不做任何处理，保持包裹当前路径
}
```

### 4.4 幽灵包裹防护

**Invariant 1**: 上游请求必须引用已存在的本地包裹
```csharp
// 发送检测通知前检查
if (!_createdParcels.ContainsKey(parcelId))
{
    _logger.LogError(
        "[Invariant Violation] 尝试为不存在的包裹 {ParcelId} 发送上游通知。" +
        "通知已阻止，不发送到上游。",
        parcelId);
    return; // 阻止发送
}
```

**Invariant 2**: 上游响应必须匹配已存在的本地包裹
```csharp
// 收到格口分配前检查
if (!_createdParcels.ContainsKey(e.ParcelId))
{
    _logger.LogError(
        "[Invariant Violation] 收到未知包裹 {ParcelId} 的路由响应，" +
        "本地不存在此包裹实体。响应已丢弃，不创建幽灵包裹。",
        e.ParcelId);
    return; // 丢弃响应
}
```

---

## 五、系统各层职责

### 5.1 Core 层

**位置**: `src/Core/ZakYip.WheelDiverterSorter.Core/`

**职责**:
- 定义 `IUpstreamRoutingClient` 接口
- 定义事件模型（`ChuteAssignmentEventArgs` 等）
- 定义通信消息模型（`ParcelDetectionNotification` 等）
- 定义路径生成接口（`ISwitchingPathGenerator`）
- 定义枚举和常量

**关键接口**:
```csharp
public interface IUpstreamRoutingClient
{
    // 事件：收到格口分配
    event EventHandler<ChuteAssignmentEventArgs> ChuteAssigned;
    
    // 方法：发送消息（fire-and-forget）
    Task<bool> SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken);
    
    // 属性：连接状态
    bool IsConnected { get; }
}
```

### 5.2 Communication 层

**位置**: `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/`

**职责**:
- 实现 `IUpstreamRoutingClient` 接口
- 管理上游连接（TCP/SignalR/MQTT）
- 处理消息序列化/反序列化
- 实现重连机制
- 实现 Client/Server 模式

**关键实现**:
- `TouchSocketTcpRuleEngineClient` - TCP 客户端
- `TouchSocketTcpRuleEngineServer` - TCP 服务器
- `SignalRRuleEngineClient` - SignalR 客户端
- `MqttRuleEngineClient` - MQTT 客户端

### 5.3 Execution 层

**位置**: `src/Execution/ZakYip.WheelDiverterSorter.Execution/`

**职责**:
- 实现 `ISortingOrchestrator` 业务编排
- 管理包裹生命周期
- 订阅上游事件（`ChuteAssigned`）
- 生成队列任务并入队
- 处理超时和故障

**关键类**:
- `SortingOrchestrator` - 核心编排服务
- `UpstreamAssignmentMiddleware` - 上游分配中间件

### 5.4 Host 层

**位置**: `src/Host/ZakYip.WheelDiverterSorter.Host/`

**职责**:
- 启动和停止服务
- DI 容器配置
- API 端点暴露
- 配置文件管理

---

## 六、配置示例

### 6.1 Client 模式配置

```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "ConnectionMode": "Client",
    "TcpServer": "sweden.ruleengine.com:5000",
    "EnableAutoReconnect": true,
    "TimeoutMs": 5000,
    "RetryCount": 3,
    "RetryDelayMs": 1000
  },
  "ChuteAssignmentTimeout": {
    "SafetyFactor": 0.9,
    "FallbackTimeoutSeconds": 5,
    "LostDetectionSafetyFactor": 1.5
  }
}
```

### 6.2 Server 模式配置

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

---

## 七、关键日志示例

### 7.1 正常流程日志

```
[2024-12-01 18:57:43.000] [INFO] [生命周期-创建] P1701446263000 入口传感器1触发
[2024-12-01 18:57:43.010] [INFO] [Parcel-First] 发送上游包裹检测通知: ParcelId=1701446263000
[2024-12-01 18:57:43.100] [INFO] 包裹 1701446263000 收到上游格口分配: 101, 延迟: 90ms
[2024-12-01 18:57:43.110] [INFO] [生命周期-路由] P1701446263000 目标格口=101
[2024-12-01 18:57:43.120] [INFO] [生命周期-入队] P1701446263000 2任务入队 目标C101
[2024-12-01 18:57:45.000] [INFO] [WheelFront触发] SensorId=2, DiverterId=1, PositionIndex=1
[2024-12-01 18:57:45.010] [INFO] 包裹 1701446263000 在 Position 1 执行动作: Straight
[2024-12-01 18:57:50.000] [INFO] [WheelFront触发] SensorId=4, DiverterId=2, PositionIndex=2
[2024-12-01 18:57:50.010] [INFO] 包裹 1701446263000 在 Position 2 执行动作: Left
[2024-12-01 18:57:52.000] [INFO] 包裹 1701446263000 已成功发送分拣完成通知: ActualChuteId=101, IsSuccess=True
```

### 7.2 超时流程日志

```
[2024-12-01 18:57:43.000] [INFO] [生命周期-创建] P1701446263001 入口传感器1触发
[2024-12-01 18:57:43.010] [INFO] [Parcel-First] 发送上游包裹检测通知: ParcelId=1701446263001
[2024-12-01 18:57:52.000] [WARN] 包裹 1701446263001 等待上游格口分配超时 (9000ms)，将使用异常格口
[2024-12-01 18:57:52.010] [INFO] [生命周期-路由] P1701446263001 目标格口=999 (异常格口)
[2024-12-01 18:57:52.020] [INFO] [生命周期-入队] P1701446263001 2任务入队 目标C999
[2024-12-01 18:58:00.000] [INFO] 【迟到路由响应】收到包裹 1701446263001 的格口分配 (ChuteId=101)，但该包裹已因超时被路由到异常口
```

---

## 八、相关文档

- **核心路由逻辑**: [docs/CORE_ROUTING_LOGIC.md](CORE_ROUTING_LOGIC.md)
- **上游连接配置**: [docs/guides/UPSTREAM_CONNECTION_GUIDE.md](guides/UPSTREAM_CONNECTION_GUIDE.md)
- **仓库结构**: [docs/RepositoryStructure.md](RepositoryStructure.md)
- **编码规范**: [.github/copilot-instructions.md](../.github/copilot-instructions.md)

---

## 九、总结

### 核心要点

1. **Parcel-First**: 先创建本地包裹，再请求上游路由
2. **Fire-and-Forget**: 完全异步，不等待响应
3. **Position-Index 队列**: 每个摆轮位置对应一个 FIFO 队列
4. **IO 触发执行**: 仅在传感器触发时执行动作，不主动扫描
5. **超时容错**: 分配超时、执行超时、丢失检测三重保护
6. **瑞典集成**: 通过 TCP/SignalR/MQTT 与瑞典上游系统通信

### 系统特性

- ✅ 完全异步通信，不阻塞业务流程
- ✅ 自动重连，最大退避 2 秒
- ✅ 支持 Client/Server 双模式
- ✅ 多协议支持（TCP/SignalR/MQTT）
- ✅ DWS 数据集成
- ✅ 完善的超时和故障处理
- ✅ 幽灵包裹防护
- ✅ 包裹丢失检测与补偿

---

**文档版本**: 1.0  
**最后更新**: 2025-12-22  
**维护团队**: ZakYip Development Team  
**联系方式**: Hisoka6602
