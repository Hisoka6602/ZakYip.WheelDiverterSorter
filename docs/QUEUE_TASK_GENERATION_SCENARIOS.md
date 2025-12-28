# 队列任务生成场景说明

> **创建日期**: 2025-12-28  
> **问题**: 除了重复触发，还有哪些情况生成队列任务？

---

## 概述

系统中有 **3 种主要场景** 会调用 `GenerateQueueTasks` 生成队列任务并入队。

---

## 场景 1: 正常包裹创建和分拣

### 触发条件

**入口传感器检测到新包裹**（正常分拣流程）

### 代码位置

`SortingOrchestrator.cs:340-460` - `ProcessParcelAsync` 方法

### 完整流程

```
1. 包裹创建（Parcel-First）
   └─> CreateParcelEntityAsync(parcelId, sensorId, detectedAt)

2. 系统状态验证
   └─> ValidateSystemStateAsync(parcelId)
   
3. 拥堵检测与超载评估
   └─> DetectCongestionAndOverloadAsync(parcelId)

4. 确定目标格口
   └─> DetermineTargetChuteAsync(parcelId, overloadDecision)
   └─> 可能来自：
       - 上游路由分配（正常流程）
       - 超载检测结果（异常格口）
       - 默认异常格口（路由失败）

5. 生成队列任务并入队 ← 关键步骤
   └─> var queueTasks = _pathGenerator.GenerateQueueTasks(
           parcelId,
           targetChuteId,
           _clock.LocalNow);
   
   └─> 如果生成失败，尝试生成异常格口任务
       └─> queueTasks = _pathGenerator.GenerateQueueTasks(
               parcelId,
               exceptionChuteId,
               _clock.LocalNow);

6. 入队到各 Position 队列
   └─> foreach (var task in queueTasks)
       {
           _queueManager.EnqueueTask(task.PositionIndex, task);
       }
```

### 日志示例

```
[生命周期-创建] P1766935876325 包裹创建
[生命周期-路由] P1766935876325 目标格口=6
[队列任务生成] 开始为包裹 1766935876325 生成到格口 6 的队列任务
[生命周期-入队] P1766935876325 5任务入队 目标C6 耗时15ms
```

### 队列任务内容

对于目标格口 6（假设有 5 个摆轮）：
```
Position 1: DiverterId=1, Action=Straight
Position 2: DiverterId=2, Action=Straight
Position 3: DiverterId=3, Action=Straight
Position 4: DiverterId=4, Action=Straight
Position 5: DiverterId=5, Action=Right  ← 最后一个摆轮转向
```

### 何时生成异常格口任务？

**主任务生成失败时**：
- 拓扑配置错误，无法生成到目标格口的路径
- 目标格口不存在或无效
- 路径计算异常

**直接路由到异常格口时**：
- 超载检测触发
- 上游路由超时
- 系统状态异常

---

## 场景 2: 重复触发异常处理

### 触发条件

**传感器在防抖窗口内重复触发**（异常流程）

### 代码位置

`SortingOrchestrator.cs:1680-1750` - `OnDuplicateTriggerDetected` 方法

### 完整流程

```
1. 防抖检测
   └─> ParcelDetectionService 检测到重复触发
   └─> 触发 DuplicateTriggerDetected 事件
   └─> ParcelId = 0（占位符）

2. 创建占位符包裹实体
   └─> CreateParcelEntityAsync(0, sensorId, detectedAt)

3. 获取异常格口ID
   └─> exceptionChuteId = systemConfig.ExceptionChuteId  // 999

4. 通知上游系统
   └─> _upstreamClient.SendAsync(
           new ParcelDetectedMessage { 
               ParcelId = 0, 
               DetectedAt = ... 
           })

5. 生成队列任务并入队 ← 关键步骤
   └─> var queueTasks = _pathGenerator.GenerateQueueTasks(
           parcelId: 0,                  // ← 占位符包裹
           exceptionChuteId: 999,        // ← 固定异常格口
           _clock.LocalNow);

6. 入队到各 Position 队列
   └─> foreach (var task in queueTasks)
       {
           _queueManager.EnqueueTask(task.PositionIndex, task);
       }
```

### 日志示例

```
[重复触发异常] 检测到重复触发异常: ParcelId=0, 传感器=1
重复触发异常包裹 0 已加入队列，目标异常格口=999
```

### 队列任务内容

对于异常格口 999：
```
Position 1: DiverterId=1, Action=Straight
Position 2: DiverterId=2, Action=Straight
...
Position N: DiverterId=N, Action=Right  ← 转向到异常格口
```

### 特殊性

- **ParcelId = 0**（占位符，非真实包裹）
- **固定路由到异常格口 999**
- **仍会经历正常分拣流程**（超时检测、摆轮执行）

---

## 场景 3: 上游路由更新（路径重生成）

### 触发条件

**上游系统发送新的格口分配**（路由变更）

### 代码位置

`SortingOrchestrator.cs:2090-2220` - `OnChuteAssigned` 方法中的路径重生成逻辑

### 完整流程

```
1. 接收上游格口分配
   └─> OnChuteAssigned(sender, ChuteAssignmentNotification)
   └─> ParcelId = e.ParcelId
   └─> NewChuteId = e.ChuteId

2. 检查包裹状态
   └─> 如果包裹已完成分拣或已清理，忽略
   
3. 检查是否需要路径重生成
   └─> 如果新格口 != 当前目标格口，需要重生成

4. 重新生成队列任务 ← 关键步骤
   └─> var newTasks = _pathGenerator.GenerateQueueTasks(
           parcelId,
           newTargetChuteId,    // ← 上游分配的新格口
           _clock.LocalNow);

5. 原地替换队列任务（保持队列顺序）
   └─> _queueManager.ReplaceTasksInPlace(parcelId, newTasks);
   └─> 不是简单的入队，而是替换现有任务

6. 更新目标格口映射
   └─> _parcelTargetChutes[parcelId] = newTargetChuteId;
```

### 日志示例

```
[TD-088-上游响应] 包裹 1766935876325 收到格口分配 ChuteId=6 (延迟=150ms)，开始异步更新路径
[TD-088-路径重生成] 开始为包裹 1766935876325 重新生成到格口 6 的路径
[TD-088-替换成功] 包裹 1766935876325 队列任务已更新: 5个新任务已替换，耗时 2.5ms
```

### 队列任务内容

**原任务**（假设初始目标格口 3）：
```
Position 1: DiverterId=1, Action=Straight
Position 2: DiverterId=2, Action=Straight
Position 3: DiverterId=3, Action=Left  ← 转向到格口 3
```

**新任务**（上游更新为格口 6）：
```
Position 1: DiverterId=1, Action=Straight
Position 2: DiverterId=2, Action=Straight
Position 3: DiverterId=3, Action=Straight
Position 4: DiverterId=4, Action=Straight
Position 5: DiverterId=5, Action=Right  ← 转向到格口 6
```

### 特殊性

- **不是新增入队，而是替换现有任务**
- **保持队列顺序，不影响后续包裹**
- **仅替换尚未执行的任务**（已执行的不会回滚）
- **TD-088 上游超时路由优化功能**

### 何时触发？

**典型场景**：
1. 上游路由系统延迟响应
2. 包裹已入队但上游路由结果才到达
3. 需要动态调整包裹目标格口

**不触发场景**：
- 包裹已完成分拣
- 包裹已被清理（丢失/超时）
- 新格口 == 当前目标格口（无需更新）

---

## 场景对比总结

### 对比表

| 场景 | 触发事件 | ParcelId | 目标格口 | 操作类型 | 是否真实包裹 |
|------|---------|----------|---------|---------|-------------|
| **1. 正常包裹分拣** | 入口传感器触发 | 时间戳 (> 0) | 上游分配或异常格口 | **入队** | ✅ 是 |
| **2. 重复触发异常** | 防抖窗口内重复触发 | 固定值 0 | 固定异常格口 999 | **入队** | ❌ 否（占位符） |
| **3. 路径重生成** | 上游格口分配更新 | 时间戳 (> 0) | 上游新分配格口 | **替换** | ✅ 是 |

### 频率对比

| 场景 | 正常频率 | 异常频率 | 影响 |
|------|---------|---------|------|
| **正常包裹分拣** | 每个包裹 1 次 | - | 核心流程 |
| **重复触发异常** | 0（无抖动） | 取决于传感器质量 | 可能造成混淆 |
| **路径重生成** | 0（上游及时响应） | 取决于上游延迟 | 性能影响小 |

---

## 队列任务的生命周期

### 完整流程

```
1. 生成（GenerateQueueTasks）
   ├─> 场景 1: 正常分拣（新增）
   ├─> 场景 2: 重复触发（新增）
   └─> 场景 3: 路径重生成（替换）

2. 入队（EnqueueTask）
   └─> 按 PositionIndex 分配到不同队列
   └─> FIFO 顺序

3. 等待执行
   └─> 队列中等待对应 Position 的传感器触发

4. 触发执行（IO 触发）
   └─> Position 传感器触发
   └─> 从队列取出任务（DequeueTask）
   └─> 执行摆轮动作

5. 完成或移除
   ├─> 正常完成：摆轮动作执行完成
   ├─> 超时：发送上游通知，继续执行
   ├─> 丢失：发送上游通知，从所有队列删除
   └─> 清理：包裹完成分拣后清理内存记录
```

---

## 代码追踪路径

### GenerateQueueTasks 实现

**接口定义**：`ISwitchingPathGenerator.cs`
```csharp
List<PositionQueueItem> GenerateQueueTasks(
    long parcelId,
    long targetChuteId,
    DateTime createdAt);
```

**核心实现**：`DefaultSwitchingPathGenerator.cs:615`
```csharp
public List<PositionQueueItem> GenerateQueueTasks(...)
{
    // 1. 根据目标格口查找路径
    var path = GeneratePath(targetChuteId);
    
    // 2. 将路径转换为队列任务列表
    var tasks = new List<PositionQueueItem>();
    foreach (var segment in path.Segments)
    {
        tasks.Add(new PositionQueueItem
        {
            ParcelId = parcelId,
            PositionIndex = GetPositionIndex(segment.DiverterId),
            DiverterId = segment.DiverterId,
            DiverterAction = segment.TargetDirection,
            ExpectedArrivalTime = CalculateExpectedTime(...),
            CreatedAt = createdAt
        });
    }
    
    return tasks;
}
```

**缓存包装**：`CachedSwitchingPathGenerator.cs:135`
```csharp
public List<PositionQueueItem> GenerateQueueTasks(...)
{
    // 路径缓存逻辑
    return _innerGenerator.GenerateQueueTasks(parcelId, targetChuteId, createdAt);
}
```

### EnqueueTask 实现

**位置**：`PositionIndexQueueManager.cs`
```csharp
public void EnqueueTask(int positionIndex, PositionQueueItem task)
{
    // 获取对应 Position 的队列
    var queue = _queues.GetOrAdd(positionIndex, _ => new ConcurrentQueue<PositionQueueItem>());
    
    // 入队（FIFO）
    queue.Enqueue(task);
    
    _logger.LogDebug(
        "任务已加入 Position {PositionIndex} 队列: ParcelId={ParcelId}, Action={Action}",
        positionIndex, task.ParcelId, task.DiverterAction);
}
```

---

## 常见问题

### Q1: 为什么重复触发要生成队列任务？

**答**: 设计目的是记录和处理异常事件，但可能造成混淆。详见 `DUPLICATE_TRIGGER_PARCELID_ZERO.md`。

### Q2: 路径重生成会导致包裹丢失吗？

**答**: 不会。替换操作是原子的，且只替换尚未执行的任务。已执行的任务不受影响。

### Q3: 如果生成失败会怎样？

**答**: 
- **场景 1（正常分拣）**: 尝试生成异常格口任务，如果仍失败则返回分拣失败结果
- **场景 2（重复触发）**: 记录错误日志，不入队
- **场景 3（路径重生成）**: 记录错误日志，保留原任务不变

### Q4: 队列任务可以被取消吗？

**答**: 可以。当包裹丢失时，会调用 `RemoveAllTasksForParcel(parcelId)` 从所有队列删除该包裹的所有任务。

---

## 总结

### 三种场景

1. ✅ **正常包裹分拣**: 每个包裹都会生成队列任务（核心流程）
2. ⚠️ **重复触发异常**: ParcelId=0 占位符包裹入队（可能造成混淆）
3. ✅ **路径重生成**: 上游路由更新时替换队列任务（性能优化）

### 关键区别

- **场景 1 和 2**: 新增入队（`EnqueueTask`）
- **场景 3**: 原地替换（`ReplaceTasksInPlace`）

### 设计考虑

- **可追溯性**: 所有事件都有记录
- **灵活性**: 支持动态路由更新
- **异常处理**: 重复触发也被妥善处理

---

**维护团队**: ZakYip Development Team  
**文档维护**: 请确保代码修改时同步更新本文档
