# 包裹丢失判定机制详解

**关键问题回答**：
1. **有多少种情况会判定为包裹丢失？** → **仅1种**
2. **在哪个位置判定？** → `SortingOrchestrator.cs:1257-1294`
3. **有多少种情况调用 RemoveAllTasksForParcel？** → **仅1种**

---

## 一、包裹丢失判定的唯一场景

### 场景：传感器触发时的延迟判定

**位置**：`SortingOrchestrator.cs:1257-1294`

**前置条件**：
1. 系统配置启用超时检测 (`SystemConfiguration.EnableTimeoutDetection = true`)
2. 包裹触发传感器时间 **晚于** 期望到达时间 (`currentTime > task.ExpectedArrivalTime`)
3. 队列中存在下一个包裹任务
4. 当前触发时间 **≥** 下一个包裹的最早出队时间 (`currentTime >= nextTask.EarliestDequeueTime`)

**判定代码**：
```csharp
// SortingOrchestrator.cs:1257-1284
if (enableTimeoutDetection && currentTime > task.ExpectedArrivalTime)
{
    var nextTask = _queueManager!.PeekNextTask(positionIndex);
    
    if (nextTask is { EarliestDequeueTime: not null })
    {
        if (currentTime < nextTask.EarliestDequeueTime.Value)
        {
            isTimeout = true;  // ← 仅超时，继续分拣
        }
        else
        {
            isPacketLoss = true;  // ← 判定为丢失 ⚠️
            // 触发时间 >= 下一个包裹最早出队时间
        }
    }
    else
    {
        isTimeout = true;  // 队列中无下一个包裹，判定为超时
    }
}
```

### 判定逻辑图解

```
时间轴示例：

当前包裹期望到达时间：  23:31:00.000
下一个包裹最早出队时间：23:31:10.000
实际传感器触发时间：    23:31:12.000  ← 晚于下一个包裹的时间

判定：
  currentTime (23:31:12) >= nextTask.EarliestDequeueTime (23:31:10)
  → isPacketLoss = true  ✅ 判定为丢失
  
对比 - 超时情况：
  实际触发时间：23:31:08.000  ← 早于下一个包裹的时间
  currentTime (23:31:08) < nextTask.EarliestDequeueTime (23:31:10)
  → isTimeout = true  ✅ 判定为超时（不是丢失）
```

---

## 二、RemoveAllTasksForParcel 调用场景

### 唯一调用位置

**位置**：`SortingOrchestrator.cs:1331`

**触发条件**：**仅在包裹丢失判定后调用**

```csharp
// SortingOrchestrator.cs:1314-1356
else if (isPacketLoss)
{
    // 包裹丢失处理：发送上游丢失消息 + 从所有队列删除该包裹的所有任务
    _logger.LogError(
        "[包裹丢失处理] 包裹 {ParcelId} 在 Position {PositionIndex} 判定为丢失，" +
        "发送上游丢失通知并删除所有队列中的任务",
        task.ParcelId, positionIndex);
    
    // 发送丢失通知到上游
    await NotifyUpstreamSortingCompletedAsync(
        task.ParcelId,
        NoTargetChute,
        isSuccess: false,
        failureReason: "PacketLoss",
        finalStatus: Core.Enums.Parcel.ParcelFinalStatus.Lost);
    
    // ⚠️ 唯一调用位置 ⚠️
    var removedCount = _queueManager!.RemoveAllTasksForParcel(task.ParcelId);
    
    _logger.LogWarning(
        "[包裹丢失清理] 已从所有队列删除包裹 {ParcelId} 的 {RemovedCount} 个任务",
        task.ParcelId, removedCount);
    
    RecordSortingFailure(NoTargetChute, isTimeout: false);
    
    // 清理丢失包裹的内存记录
    CleanupParcelMemory(task.ParcelId);
    
    // 递归处理下一个包裹
    RecursiveProcessNextParcelAfterLoss(boundWheelDiverterId, sensorId, positionIndex, task.ParcelId);
    return;  // 丢失包裹不执行任何摆轮动作，直接返回
}
```

### RemoveAllTasksForParcel 的影响

**删除范围**：
- 从 **所有 Position 队列** 中删除该包裹的 **所有任务**
- 包括尚未到达的后续 Position（例如包裹在 Position 2 丢失，Position 3/4/5 的任务也会被删除）

**队列影响**：
- ✅ **FIFO 顺序破坏**：后续包裹任务前移
- ✅ **队列错位风险**：如果误判为丢失，会导致包裹与队列任务不匹配

**代码位置**：`PositionIndexQueueManager.cs:271-327`

---

## 三、ChuteId=0 触发队列错位的完整因果链

### 问题链路

```
1. 上游系统发送 ChuteId=0（无效格口分配）
   ↓
2. 包裹继续使用异常格口 999，但状态已被修改：
   parcelRecord.UpstreamReplyReceivedAt = receivedAt  ← ⚠️ 关键问题
   parcelRecord.RouteBoundAt = receivedAt
   ↓
3. 包裹因等待有效上游响应而物理延迟
   ↓
4. 包裹触发传感器时间晚于期望到达时间
   ↓
5. 系统比较：currentTime >= nextTask.EarliestDequeueTime
   ↓
6. **误判为包裹丢失**（实际是延迟，不是真丢失）
   ↓
7. 调用 RemoveAllTasksForParcel(parcelId)  ← 队列任务被删除
   ↓
8. 后续包裹任务前移 → **队列 FIFO 顺序被破坏**
   ↓
9. 队列错位显现：包裹与队列任务不匹配
```

### 为什么不是直接原因？

**ChuteId=0 代码路径**：
```csharp
// SortingOrchestrator.cs:2006-2016
if (e.ChuteId <= 0)
{
    _logger.LogWarning("[格口分配-无效] ...");
    return;  // ✅ 直接返回，不做任何队列操作
}
```

**证据**：
- ❌ ChuteId=0 分支 **不调用** `RemoveAllTasksForParcel`
- ❌ ChuteId=0 分支 **不修改** 队列结构
- ❌ ChuteId=0 分支 **不影响** 队列顺序

**真正原因**：
- ⚠️ **时间戳在验证前就被修改** (行 1977-1978)
- ⚠️ 包裹延迟 + 误判为丢失 → 调用 `RemoveAllTasksForParcel`
- ⚠️ 队列任务删除 → FIFO 顺序破坏

---

## 四、超时 vs 丢失的区别

| 对比项 | 超时 (Timeout) | 丢失 (PacketLoss) |
|--------|----------------|-------------------|
| **判定条件** | `currentTime < nextTask.EarliestDequeueTime` | `currentTime >= nextTask.EarliestDequeueTime` |
| **上游通知** | ✅ 发送失败通知 | ✅ 发送失败通知 |
| **队列操作** | ❌ **不删除**队列任务 | ✅ **删除所有**队列任务 |
| **摆轮动作** | ✅ **继续执行**计划动作 | ❌ **跳过**所有动作 |
| **递归处理** | ❌ 不触发递归 | ✅ 递归处理下一个包裹 |
| **队列影响** | ✅ **FIFO 顺序保持** | ⚠️ **FIFO 顺序破坏** |

---

## 五、修复建议

### 方案 1：移动时间戳设置到验证之后 ⭐

**问题**：时间戳在 ChuteId 验证前就被修改

```csharp
// ❌ 当前代码（SortingOrchestrator.cs:1977-2016）
parcelRecord.UpstreamReplyReceivedAt = new DateTimeOffset(receivedAt);  // ← 先修改
parcelRecord.RouteBoundAt = new DateTimeOffset(receivedAt);

if (e.ChuteId <= 0)
{
    return;  // 但时间戳已被修改！
}
```

**修复**：
```csharp
// ✅ 修复：先验证，再修改
if (e.ChuteId <= 0)
{
    _logger.LogWarning("[格口分配-无效] ...");
    return;  // ← 在修改任何状态前退出
}

// 只有验证通过后才修改时间戳
parcelRecord.UpstreamReplyReceivedAt = new DateTimeOffset(receivedAt);
parcelRecord.RouteBoundAt = new DateTimeOffset(receivedAt);
```

**效果**：防止无效响应修改包裹状态，避免触发后续误判逻辑

---

### 方案 2：改进丢失判定逻辑 ⭐⭐ **推荐**

**问题**：当前逻辑无法区分"延迟但已收到响应"和"真正丢失"

```csharp
// ❌ 当前代码（SortingOrchestrator.cs:1275-1278）
if (currentTime >= nextTask.EarliestDequeueTime.Value)
{
    isPacketLoss = true;  // ← 延迟也误判为丢失
}
```

**修复**：检查是否已收到上游响应
```csharp
// ✅ 修复：检查上游响应状态
if (currentTime >= nextTask.EarliestDequeueTime.Value)
{
    // 查询包裹记录，检查是否已收到上游响应
    var parcelRecord = await _parcelRepository!.GetByIdAsync(task.ParcelId);
    
    if (parcelRecord?.UpstreamReplyReceivedAt.HasValue == true)
    {
        // 已收到响应（即使是无效的 ChuteId=0），不是真丢失
        isTimeout = true;  // ← 改判为超时，继续分拣
        
        _logger.LogWarning(
            "[超时检测-修正] 包裹 {ParcelId} 延迟但已收到上游响应，判定为超时而非丢失",
            task.ParcelId);
    }
    else
    {
        // 未收到任何响应，真正丢失
        isPacketLoss = true;
        
        _logger.LogError(
            "[包裹丢失] 包裹 {ParcelId} 未收到上游响应且严重延迟，判定为丢失",
            task.ParcelId);
    }
}
```

**效果**：
- ✅ 真正丢失（无响应）→ 删除队列任务（正确）
- ✅ 延迟但有响应（包括 ChuteId=0）→ 判定为超时，继续分拣（避免误删）

---

### 方案 3：组合修复 ⭐⭐⭐ **最优**

结合方案 1 和方案 2：

1. **立即修复**：移动时间戳设置到验证后（防止状态污染）
2. **根本修复**：改进丢失判定逻辑（检查上游响应状态）

**综合效果**：
- ✅ 防止无效响应污染包裹状态
- ✅ 准确区分"真丢失"和"延迟"
- ✅ 避免误删队列任务
- ✅ 保持队列 FIFO 顺序

---

## 六、验证检查清单

当怀疑包裹被误判为丢失时，检查以下日志：

```bash
# 1. 查看包裹是否被判定为丢失
grep "\[包裹丢失\].*ParcelId" log.txt

# 2. 查看是否有队列任务被删除
grep "RemoveAllTasksForParcel\|包裹丢失清理" log.txt

# 3. 查看包裹是否收到 ChuteId=0
grep "ChuteId=0.*ParcelId" log.txt

# 4. 查看包裹的完整时间线
grep "ParcelId=<包裹ID>" log.txt | grep -E "期望到达|最早出队|触发时间|UpstreamReply"

# 5. 查看下一个包裹的最早出队时间
grep "下一个包裹最早出队时间" log.txt
```

**关键时间点对比**：
```
包裹 A 期望到达时间：     23:31:00.000
包裹 A 实际触发时间：     23:31:12.000  ← 延迟 12 秒
包裹 B 最早出队时间：     23:31:10.000
判定：12.000 >= 10.000 → 丢失 ⚠️

如果包裹 A 收到过上游响应（包括 ChuteId=0）：
  → 应判定为"超时"，不应删除队列任务
  → 当前代码误判为"丢失"，删除队列任务 → 队列错位
```

---

## 七、相关文档

- **队列错位根因分析**：`docs/CHUTE_ZERO_ROOT_CAUSE_FOUND.md`
- **无效格口影响分析**：`docs/INVALID_CHUTE_ASSIGNMENT_IMPACT.md`
- **队列任务时间计算**：`docs/QUEUE_TASK_TIME_CALCULATION.md`
- **ChuteId=0 与队列错位关联**：`docs/CHUTE_ZERO_QUEUE_CORRUPTION_CORRELATION.md`

---

**文档创建时间**：2025-12-28  
**最后更新**：2025-12-28  
**维护者**：@copilot
