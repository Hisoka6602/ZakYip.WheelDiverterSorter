# 提前触发、超时、丢包时间对比说明

> **文档类型**: 技术说明文档  
> **创建日期**: 2025-12-28  
> **优先级**: 🔴 **P0-Critical**  
> **相关PR**: #530 (Fix: Validate upstream ChuteId before path regeneration)

---

## 一、核心时间字段定义

系统在队列任务（`PositionQueueItem`）中定义了以下关键时间字段：

```csharp
public record class PositionQueueItem
{
    /// <summary>
    /// 任务创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// 期望到达时间（理论到达时间）
    /// </summary>
    public required DateTime ExpectedArrivalTime { get; init; }
    
    /// <summary>
    /// 最早出队时间（用于提前触发检测）
    /// </summary>
    /// <remarks>
    /// 计算公式：EarliestDequeueTime = Max(CreatedAt, ExpectedArrivalTime - TimeoutThresholdMs)
    /// </remarks>
    public DateTime? EarliestDequeueTime { get; init; }
    
    /// <summary>
    /// 超时阈值（毫秒）
    /// </summary>
    public required long TimeoutThresholdMs { get; init; }
}
```

---

## 二、提前触发检测（Early Trigger Detection）

### 2.1 时间对比

**对比公式**：
```csharp
if (currentTime < task.EarliestDequeueTime)
{
    // 提前触发
}
```

**对比内容**：
- **时间A**：`currentTime`（当前时间，传感器触发时刻）
- **时间B**：`task.EarliestDequeueTime`（最早出队时间）

**判定逻辑**：
- 如果 `currentTime < EarliestDequeueTime`，则判定为**提前触发**
- 系统记录警告日志，并根据配置决定是否执行直行动作

### 2.2 代码位置

**文件**：`src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`

**行号**：1166-1224

**关键代码**：
```csharp
// 提前触发检测：如果启用且队列任务有 EarliestDequeueTime，检查当前时间是否过早
if (enableEarlyTriggerDetection
    && peekedTask.EarliestDequeueTime is DateTime earliestDequeueTime
    && currentTime < earliestDequeueTime)  // ⬅️ 关键对比
{
    var earlyMs = (earliestDequeueTime - currentTime).TotalMilliseconds;
    
    _logger.LogWarning(
        "[提前触发检测] Position {PositionIndex} 传感器 {SensorId} 提前触发 {EarlyMs}ms，" +
        "包裹 {ParcelId}，PassThroughOnInterference={PassThroughOnInterference} | " +
        "当前时间={CurrentTime:HH:mm:ss.fff}, " +
        "最早出队时间={EarliestTime:HH:mm:ss.fff}, " +
        "期望到达时间={ExpectedTime:HH:mm:ss.fff}, " +
        "{SegmentInfo}",
        positionIndex, sensorId, earlyMs,
        peekedTask.ParcelId,
        passThroughOnInterference,
        currentTime,  // ⬅️ 时间A
        earliestDequeueTime,  // ⬅️ 时间B
        peekedTask.ExpectedArrivalTime,
        segmentInfo);
    
    // 不出队、直接返回（任务保留在队列中）
    return;
}
```

### 2.3 时间窗口图示

```
CreatedAt        EarliestDequeueTime    ExpectedArrivalTime
   |                    |                        |
   |<--- 过早区间 ----->|<---- 正常窗口 -------->|
   |                    |                        |
00:00:00            00:04:58                 00:05:00

❌ 提前触发：currentTime < EarliestDequeueTime
✅ 正常触发：currentTime >= EarliestDequeueTime
```

---

## 三、超时检测（Timeout Detection）

### 3.1 时间对比

**第一步判断（是否延迟到达）**：
```csharp
if (currentTime > task.ExpectedArrivalTime)
{
    // 延迟到达，需要进一步判断是超时还是丢失
}
```

**第二步判断（超时 vs 丢失）**：
```csharp
if (currentTime < nextTask.EarliestDequeueTime)
{
    // 超时
}
else
{
    // 丢失
}
```

**对比内容**：
- **第一步**：
  - **时间A**：`currentTime`（当前时间，传感器触发时刻）
  - **时间B**：`task.ExpectedArrivalTime`（当前包裹的期望到达时间）
- **第二步**（区分超时和丢失）：
  - **时间A**：`currentTime`（当前时间，传感器触发时刻）
  - **时间B**：`nextTask.EarliestDequeueTime`（下一个包裹的最早出队时间）

### 3.2 判定逻辑

**超时判定**：
- 条件1：`currentTime > task.ExpectedArrivalTime`（当前包裹已延迟）
- 条件2：`currentTime < nextTask.EarliestDequeueTime`（未到下一个包裹的时间窗口）
- 结论：**包裹超时到达，但仍在合理的延迟范围内**

**处理方式**：
- 发送上游超时通知
- **仍然执行当前包裹的摆轮动作**（包裹虽然延迟但仍在线体上）

### 3.3 代码位置

**文件**：`src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`

**行号**：1254-1321

**关键代码**：
```csharp
// 新的超时/丢失检测逻辑：
// 1. 检查触发时间是否晚于当前包裹的期望到达时间
// 2. 如果是，则查看下一个包裹的最早出队时间来区分"超时"和"丢失"

if (enableTimeoutDetection && currentTime > task.ExpectedArrivalTime)  // ⬅️ 第一步对比
{
    // 当前包裹已延迟到达，需要判断是超时还是丢失
    var nextTask = _queueManager!.PeekNextTask(positionIndex);
    
    if (nextTask != null && nextTask.EarliestDequeueTime.HasValue)
    {
        // 有下一个包裹，基于其最早出队时间判断
        if (currentTime < nextTask.EarliestDequeueTime.Value)  // ⬅️ 第二步对比（超时）
        {
            // 触发时间在下一个包裹最早出队时间之前 → 超时
            isTimeout = true;
            var delayMs = (currentTime - task.ExpectedArrivalTime).TotalMilliseconds;
            _logger.LogWarning(
                "[超时检测] 包裹 {ParcelId} 在 Position {PositionIndex} 超时 (延迟 {DelayMs}ms)，" +
                "触发时间={CurrentTime:HH:mm:ss.fff} < 下一个包裹最早出队时间={NextEarliest:HH:mm:ss.fff}",
                task.ParcelId, positionIndex, delayMs, 
                currentTime,  // ⬅️ 时间A
                nextTask.EarliestDequeueTime.Value);  // ⬅️ 时间B
        }
        // ... 丢失判定见下一节
    }
    else
    {
        // 没有下一个包裹（队列中只有当前包裹），判定为超时
        isTimeout = true;
    }
}

if (isTimeout)
{
    // 超时处理：仅发送上游超时消息
    await NotifyUpstreamSortingCompletedAsync(
        task.ParcelId,
        NoTargetChute,  // 0
        isSuccess: false,
        failureReason: "Timeout",
        finalStatus: Core.Enums.Parcel.ParcelFinalStatus.Timeout);
    
    // ✅ 仍然继续执行当前包裹的摆轮动作（代码在 line 1371 之后）
}
```

### 3.4 时间窗口图示

```
当前包裹                                         下一个包裹
ExpectedArrivalTime                      EarliestDequeueTime
        |                                        |
        |<------- 超时延迟窗口 ----------------->|
        |                                        |
    00:05:00                                 00:07:58

✅ 超时：task.ExpectedArrivalTime < currentTime < nextTask.EarliestDequeueTime
   - 当前包裹延迟到达
   - 但还未到下一个包裹的时间窗口
   - 仍然执行当前包裹的动作
```

---

## 四、丢包检测（Packet Loss Detection）

### 4.1 时间对比

**对比公式**：
```csharp
if (currentTime > task.ExpectedArrivalTime  // 第一步：当前包裹已延迟
    && currentTime >= nextTask.EarliestDequeueTime)  // 第二步：已到下一个包裹的时间窗口
{
    // 丢包
}
```

**对比内容**：
- **第一步**：
  - **时间A**：`currentTime`（当前时间，传感器触发时刻）
  - **时间B**：`task.ExpectedArrivalTime`（当前包裹的期望到达时间）
- **第二步**（确认丢失）：
  - **时间A**：`currentTime`（当前时间，传感器触发时刻）
  - **时间B**：`nextTask.EarliestDequeueTime`（下一个包裹的最早出队时间）

### 4.2 判定逻辑

**丢包判定**：
- 条件1：`currentTime > task.ExpectedArrivalTime`（当前包裹已延迟）
- 条件2：`currentTime >= nextTask.EarliestDequeueTime`（已到或超过下一个包裹的时间窗口）
- 结论：**当前包裹已丢失，传感器触发的是下一个包裹**

**处理方式**：
- 发送上游丢失通知
- **从所有队列中删除当前包裹的所有任务**（包裹已不在线体上）
- **递归处理下一个包裹**（触发的传感器应该对应下一个包裹）

### 4.3 代码位置

**文件**：`src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`

**行号**：1254-1364

**关键代码**：
```csharp
if (enableTimeoutDetection && currentTime > task.ExpectedArrivalTime)  // ⬅️ 第一步对比
{
    var nextTask = _queueManager!.PeekNextTask(positionIndex);
    
    if (nextTask != null && nextTask.EarliestDequeueTime.HasValue)
    {
        // ... 超时判定（见上一节）
        else
        {
            // 触发时间在下一个包裹最早出队时间之后或相等 → 丢失
            isPacketLoss = true;  // ⬅️ 第二步对比（丢失）
            var delayMs = (currentTime - task.ExpectedArrivalTime).TotalMilliseconds;
            _logger.LogError(
                "[包裹丢失] 包裹 {ParcelId} 在 Position {PositionIndex} 判定为丢失 (延迟 {DelayMs}ms)，" +
                "触发时间={CurrentTime:HH:mm:ss.fff} >= 下一个包裹最早出队时间={NextEarliest:HH:mm:ss.fff}",
                task.ParcelId, positionIndex, delayMs, 
                currentTime,  // ⬅️ 时间A
                nextTask.EarliestDequeueTime.Value);  // ⬅️ 时间B
        }
    }
}

if (isPacketLoss)
{
    // 包裹丢失处理：发送上游丢失消息 + 从所有队列删除该包裹的所有任务
    await NotifyUpstreamSortingCompletedAsync(
        task.ParcelId,
        NoTargetChute,  // 0
        isSuccess: false,
        failureReason: "PacketLoss",
        finalStatus: Core.Enums.Parcel.ParcelFinalStatus.Lost);
    
    // 从所有队列中删除该包裹的所有任务
    var removedCount = _queueManager!.RemoveAllTasksForParcel(task.ParcelId);
    
    // 清理丢失包裹的内存记录
    CleanupParcelMemory(task.ParcelId);
    
    // ❌ 不执行当前丢失包裹的动作
    // ✅ 递归处理下一个包裹（触发的传感器对应的是下一个包裹）
    RecursiveProcessNextParcelAfterLoss(boundWheelDiverterId, sensorId, positionIndex, task.ParcelId);
    return;
}
```

### 4.4 时间窗口图示

```
当前包裹                                         下一个包裹
ExpectedArrivalTime                      EarliestDequeueTime
        |                                        |
        |<------- 超时延迟窗口 ----------------->|<--- 丢失判定区 --->
        |                                        |
    00:05:00                                 00:07:58

❌ 丢包：currentTime >= nextTask.EarliestDequeueTime
   - 当前包裹严重延迟
   - 已经到达或超过下一个包裹的时间窗口
   - 判定当前包裹丢失，触发的传感器对应下一个包裹
   - 删除当前包裹的所有队列任务
   - 递归处理下一个包裹
```

---

## 五、完整时间线示例

假设有两个包裹 P1 和 P2 在同一个队列中：

```
包裹P1：
- CreatedAt = 00:00:00
- ExpectedArrivalTime = 00:05:00
- EarliestDequeueTime = 00:04:58 (= 00:05:00 - 2000ms)
- TimeoutThresholdMs = 2000ms

包裹P2（下一个包裹）：
- CreatedAt = 00:03:00
- ExpectedArrivalTime = 00:08:00
- EarliestDequeueTime = 00:07:58 (= 00:08:00 - 2000ms)
- TimeoutThresholdMs = 2000ms
```

### 时间线图示

```
00:00:00    00:03:00    00:04:58    00:05:00    00:07:58    00:08:00
   |           |           |           |           |           |
   P1创建     P2创建    P1最早     P1期望     P2最早     P2期望
                        出队时间   到达时间   出队时间   到达时间
   |           |           |           |           |           |
   |<-- P1过早区 -->|<-- P1正常窗口 -->|<-- P1超时窗口 -->|
                                                   |<-- P2正常窗口 -->|

传感器触发时间判定：
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
触发时间      判定结果                      时间对比
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
00:04:00  →  ❌ 提前触发（P1）           currentTime < P1.EarliestDequeueTime
                                          (00:04:00 < 00:04:58)

00:04:58  →  ✅ 正常触发（P1）           currentTime >= P1.EarliestDequeueTime
                                          (00:04:58 >= 00:04:58)

00:05:30  →  ⚠️ 超时触发（P1）           currentTime > P1.ExpectedArrivalTime
                                          (00:05:30 > 00:05:00)
                                       && currentTime < P2.EarliestDequeueTime
                                          (00:05:30 < 00:07:58)

00:08:30  →  🔴 丢包判定（P1丢失）       currentTime > P1.ExpectedArrivalTime
                                          (00:08:30 > 00:05:00)
                                       && currentTime >= P2.EarliestDequeueTime
                                          (00:08:30 >= 00:07:58)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 六、总结表格

| 场景 | 第一步对比 | 第二步对比 | 判定结果 | 处理方式 |
|------|-----------|-----------|---------|---------|
| **提前触发** | `currentTime < task.EarliestDequeueTime` | - | ❌ 提前触发 | 记录警告，不出队，可选执行直行 |
| **正常触发** | `currentTime >= task.EarliestDequeueTime`<br>`&& currentTime <= task.ExpectedArrivalTime` | - | ✅ 正常触发 | 出队并执行计划动作 |
| **超时触发** | `currentTime > task.ExpectedArrivalTime` | `currentTime < nextTask.EarliestDequeueTime` | ⚠️ 超时 | 发送超时通知，仍执行动作 |
| **丢包判定** | `currentTime > task.ExpectedArrivalTime` | `currentTime >= nextTask.EarliestDequeueTime` | 🔴 丢包 | 发送丢失通知，删除任务，递归处理下一个包裹 |

---

## 七、关键时间字段计算公式

### 7.1 EarliestDequeueTime 计算

```csharp
// 最早出队时间 = Max(任务创建时间, 期望到达时间 - 超时阈值)
var earliestDequeueTime = expectedArrivalTime.AddMilliseconds(-timeoutThresholdMs);
if (earliestDequeueTime < createdAt)
{
    earliestDequeueTime = createdAt;
}
```

**目的**：
- 确保任务不会在包裹创建之前被出队
- 为提前触发检测提供时间窗口下界

### 7.2 延迟时间计算

```csharp
// 提前触发的提前时间
var earlyMs = (task.EarliestDequeueTime - currentTime).TotalMilliseconds;

// 超时或丢失的延迟时间
var delayMs = (currentTime - task.ExpectedArrivalTime).TotalMilliseconds;
```

---

## 八、相关文档

- **`docs/EARLY_ARRIVAL_HANDLING.md`** - 早到包裹处理机制详解
- **`docs/TIMEOUT_HANDLING_MECHANISM.md`** - 包裹超时处理机制详解
- **`docs/CORE_ROUTING_LOGIC.md`** - 核心路由逻辑和队列机制

---

## 九、常见问题

### Q1: 为什么超时包裹仍然执行动作，而丢失包裹不执行？

**A**: 
- **超时包裹**：虽然延迟，但仍在线体上，只是比预期慢，所以仍需执行摆轮动作完成分拣
- **丢失包裹**：传感器触发时间已经到达或超过下一个包裹的时间窗口，说明当前包裹已不在线体上，触发的是下一个包裹的传感器，所以删除当前包裹的任务，改为处理下一个包裹

### Q2: EarliestDequeueTime 为什么要与 CreatedAt 比较取最大值？

**A**: 
- 避免出现 `EarliestDequeueTime < CreatedAt` 的不合理情况
- 例如：如果 `ExpectedArrivalTime = CreatedAt + 500ms`，`TimeoutThresholdMs = 2000ms`，则直接计算会得到 `EarliestDequeueTime = CreatedAt - 1500ms`（负值），这是不合理的
- 取最大值确保：`EarliestDequeueTime >= CreatedAt`

### Q3: 为什么需要下一个包裹的 EarliestDequeueTime 来区分超时和丢失？

**A**: 
- 仅凭 `currentTime > ExpectedArrivalTime` 无法判断包裹是"延迟到达"还是"已经丢失"
- 通过查看下一个包裹的时间窗口，可以推断：
  - 如果还未到下一个包裹的时间窗口，说明当前包裹虽然延迟但仍在线体上（**超时**）
  - 如果已经到达或超过下一个包裹的时间窗口，说明当前包裹已经不在线体上，触发的是下一个包裹（**丢失**）

---

**文档维护**: ZakYip Development Team  
**最后更新**: 2025-12-28
