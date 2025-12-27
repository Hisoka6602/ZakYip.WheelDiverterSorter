# 早到包裹处理机制说明

> **文档类型**: 业务逻辑说明  
> **优先级**: 🟡 **P1 - 高优先级**  
> **关联文档**: `CORE_ROUTING_LOGIC.md`  
> **生效日期**: 2025-12-27

---

## 问题场景

**场景描述**: 当一个包裹应该在 00:05 的时候到达某个传感器位置，但是在 00:01 的时候就收到了传感器信号，系统会怎么处理？队列的逻辑是怎么样的？

---

## 一、核心概念回顾

### 1.1 Position-Index 队列机制

系统使用基于位置索引（Position Index）的队列机制来管理包裹的分拣流程：

- **每个 positionIndex** 对应一个独立的 FIFO（先进先出）队列
- **队列中的任务** 包含：包裹ID、摆轮动作、期望到达时间、超时容差、异常动作等
- **触发机制**: 传感器触发时从对应 positionIndex 的队列中取出第一个任务执行

### 1.2 时间字段说明

每个队列任务 (`PositionQueueItem`) 包含以下关键时间字段：

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
    /// <remarks>
    /// 基于包裹创建时间和前序线段的理论传输时间计算
    /// </remarks>
    public required DateTime ExpectedArrivalTime { get; init; }
    
    /// <summary>
    /// 超时阈值（毫秒）
    /// </summary>
    /// <remarks>
    /// 超时判定：当前时间 > ExpectedArrivalTime + TimeoutThresholdMs
    /// </remarks>
    public required long TimeoutThresholdMs { get; init; }
    
    /// <summary>
    /// 最早出队时间（用于提前触发检测）
    /// </summary>
    /// <remarks>
    /// 计算公式：EarliestDequeueTime = Max(CreatedAt, ExpectedArrivalTime - TimeoutThresholdMs)
    /// 确保任务不会在包裹创建之前或期望时间窗口之前被出队
    /// </remarks>
    public DateTime? EarliestDequeueTime { get; init; }
    
    /// <summary>
    /// 丢失判定截止时间
    /// </summary>
    /// <remarks>
    /// = ExpectedArrivalTime + LostDetectionTimeoutMs
    /// 用于判断包裹是否丢失
    /// </remarks>
    public DateTime? LostDetectionDeadline { get; init; }
}
```

---

## 二、早到包裹处理流程

### 2.1 时间窗口划分

对于一个期望在 00:05 到达的包裹任务，系统定义了以下时间窗口：

假设：
- `ExpectedArrivalTime` = 00:05:00
- `TimeoutThresholdMs` = 2000ms (2秒)
- `CreatedAt` = 00:00:00

则：
- `EarliestDequeueTime` = Max(00:00:00, 00:05:00 - 2000ms) = Max(00:00:00, 00:04:58) = **00:04:58**
- `LatestArrivalTime` = 00:05:00 + 2000ms = **00:05:02**

**时间窗口划分**：
```
00:00:00         00:04:58         00:05:00         00:05:02         00:06:30
   |                |                |                |                |
   |                |                |                |                |
创建时间       最早出队时间      期望到达时间      超时判定线      丢失判定线
   |                |                |                |                |
   |<--- 过早区间 -->|<--- 正常窗口 -->|<--- 延迟窗口 -->|<--- 丢失检测 -->|
```

### 2.2 传感器触发时的判断逻辑

当传感器在某个时刻触发时，系统会执行以下判断流程：

#### 步骤1: 窥视队列头部任务（不出队）

```csharp
var peekedTask = _queueManager.PeekTask(positionIndex);
if (peekedTask == null)
{
    // 队列为空 - 可能是输送线残留包裹（干扰信号）
    _logger.LogInformation("队列为空，传感器触发，可能是残留包裹");
    return;  // 不执行任何动作
}
```

#### 步骤2: 检查是否启用提前触发检测

```csharp
var systemConfig = _systemConfigRepository.Get();
var enableEarlyTriggerDetection = systemConfig?.EnableEarlyTriggerDetection ?? false;
```

#### 步骤3: 提前触发检测（如果启用）

```csharp
if (enableEarlyTriggerDetection
    && peekedTask.EarliestDequeueTime is DateTime earliestDequeueTime
    && currentTime < earliestDequeueTime)
{
    var earlyMs = (earliestDequeueTime - currentTime).TotalMilliseconds;
    
    _logger.LogWarning(
        "[提前触发检测] Position {PositionIndex} 提前触发 {EarlyMs}ms，" +
        "包裹 {ParcelId} 不出队、不执行动作",
        positionIndex, earlyMs, peekedTask.ParcelId);
    
    // 记录为分拣异常（用于监控）
    _alarmService?.RecordSortingFailure();
    
    // ⚠️ 不出队、不执行动作，直接返回
    return;
}
```

#### 步骤4: 正常流程 - 从队列取出任务

```csharp
var task = _queueManager.DequeueTask(positionIndex);

// 检查是否超时（延迟到达）
var enableTimeoutDetection = systemConfig?.EnableTimeoutDetection ?? false;
var isTimeout = enableTimeoutDetection && 
               currentTime > task.ExpectedArrivalTime.AddMilliseconds(task.TimeoutThresholdMs);

DiverterDirection actionToExecute;

if (isTimeout)
{
    // 超时处理：使用回退动作，插入补偿任务
    actionToExecute = task.FallbackAction;
    InsertCompensationTasks(task);
}
else
{
    // 正常处理：执行计划动作
    actionToExecute = task.DiverterAction;
}

// 执行摆轮动作
await ExecuteDiverterAction(task.DiverterId, actionToExecute);

// 动态更新下一个position的期望时间（避免误差累积）
if (enableEarlyTriggerDetection)
{
    UpdateNextPositionExpectedTime(task, positionIndex, currentTime);
}
```

---

## 三、具体场景示例

### 场景1: 包裹在 00:01 到达，期望 00:05 到达（提前 4 分钟）

**假设条件**：
- 包裹创建时间 `CreatedAt` = 00:00:00
- 期望到达时间 `ExpectedArrivalTime` = 00:05:00
- 超时阈值 `TimeoutThresholdMs` = 2000ms (2秒)
- 最早出队时间 `EarliestDequeueTime` = 00:04:58
- 提前触发检测 `EnableEarlyTriggerDetection` = **true** (启用)

**时间线**：
```
00:00:00 - 包裹创建，任务入队
00:01:00 - 传感器触发（实际到达时间）❌ 提前触发
00:04:58 - 最早出队时间
00:05:00 - 期望到达时间
00:05:02 - 超时判定线
```

**处理流程**：

1. **窥视队列**：
   ```
   peekedTask.ParcelId = P001
   peekedTask.EarliestDequeueTime = 00:04:58
   currentTime = 00:01:00
   ```

2. **提前触发检测**：
   ```
   currentTime (00:01:00) < EarliestDequeueTime (00:04:58) ？ YES ✅
   earlyMs = (00:04:58 - 00:01:00).TotalMilliseconds = 238000ms = 3分58秒
   ```

3. **判定结果**：**提前触发**

4. **系统动作**：
   - ⚠️ **不出队**：任务保留在队列头部
   - ⚠️ **不执行摆轮动作**：摆轮保持当前状态
   - 📝 记录告警日志：
     ```
     [提前触发检测] Position 1 传感器 2 提前触发 238000ms，
     包裹 P001 不出队、不执行动作
     ```
   - 📊 记录分拣异常统计：`_alarmService?.RecordSortingFailure()`

5. **后续影响**：
   - 包裹 P001 的任务仍在队列中
   - 如果传感器再次触发（例如包裹往复运动），会再次执行相同判断
   - 只有当传感器触发时间 >= 00:04:58 时，才会正常出队并执行动作

**关键要点**：
- ✅ 防止错位：避免队列头部的包裹提前被其他物理包裹触发
- ✅ 保持 FIFO 顺序：队列中的包裹顺序不受影响
- ✅ 监控告警：提前触发会被记录，便于排查输送线速度异常

---

### 场景2: 包裹在 00:05:01 到达（在正常窗口内）

**假设条件**（同上）：
- `EarliestDequeueTime` = 00:04:58
- `ExpectedArrivalTime` = 00:05:00
- `TimeoutThresholdMs` = 2000ms
- 提前触发检测 `EnableEarlyTriggerDetection` = **true**

**时间线**：
```
00:04:58 - 最早出队时间
00:05:00 - 期望到达时间
00:05:01 - 传感器触发（实际到达时间）✅ 正常
00:05:02 - 超时判定线
```

**处理流程**：

1. **提前触发检测**：
   ```
   currentTime (00:05:01) < EarliestDequeueTime (00:04:58) ？ NO ❌
   ```

2. **判定结果**：**正常到达**（在正常窗口内）

3. **系统动作**：
   - ✅ **从队列取出任务**：`DequeueTask(positionIndex)`
   - ✅ **执行计划动作**：`actionToExecute = task.DiverterAction` (例如：Left)
   - ✅ **执行摆轮动作**：`await ExecuteDiverterAction(task.DiverterId, DiverterDirection.Left)`
   - 📝 记录正常执行日志

4. **动态时间更新**：
   - 基于实际到达时间 00:05:01，更新下一个 position 的期望时间
   - 避免误差累积

---

### 场景3: 包裹在 00:05:03 到达（超时）

**假设条件**（同上）：
- `ExpectedArrivalTime` = 00:05:00
- `TimeoutThresholdMs` = 2000ms
- 超时检测 `EnableTimeoutDetection` = **true**

**时间线**：
```
00:05:00 - 期望到达时间
00:05:02 - 超时判定线
00:05:03 - 传感器触发（实际到达时间）❌ 超时
```

**处理流程**：

1. **提前触发检测**：
   ```
   currentTime (00:05:03) < EarliestDequeueTime (00:04:58) ？ NO ❌
   通过提前触发检测
   ```

2. **超时检测**：
   ```
   currentTime (00:05:03) > ExpectedArrivalTime (00:05:00) + TimeoutThresholdMs (2000ms) ？ YES ✅
   delayMs = (00:05:03 - 00:05:00).TotalMilliseconds = 3000ms
   ```

3. **判定结果**：**超时到达**

4. **系统动作**：
   - ✅ **从队列取出任务**：`DequeueTask(positionIndex)`
   - ⚠️ **执行回退动作**：`actionToExecute = task.FallbackAction` (通常是 Straight)
   - ⚠️ **插入补偿任务**：在后续所有 position 的队列头部插入 Straight 任务
   - 📝 记录超时告警日志：
     ```
     包裹 P001 在 Position 1 超时 (延迟 3000ms)，使用回退动作 Straight
     ```
   - 📊 记录分拣异常统计

---

## 四、功能开关与配置

### 4.1 提前触发检测开关

**配置路径**: `SystemConfiguration.EnableEarlyTriggerDetection`

**默认值**: `false` (禁用)

**启用时的行为**：
- ✅ 检查 `currentTime < EarliestDequeueTime`
- ✅ 提前触发时：不出队、不执行动作、记录告警
- ✅ 动态更新下一个 position 的期望时间

**禁用时的行为**：
- ❌ 不检查提前触发
- ✅ 所有传感器触发都正常出队并执行动作
- ❌ 不动态更新期望时间

### 4.2 超时检测开关

**配置路径**: `SystemConfiguration.EnableTimeoutDetection`

**默认值**: `false` (禁用)

**启用时的行为**：
- ✅ 检查 `currentTime > ExpectedArrivalTime + TimeoutThresholdMs`
- ✅ 超时时：执行回退动作、插入补偿任务、记录告警

**禁用时的行为**：
- ❌ 不检查超时
- ✅ 所有包裹都按计划动作执行，无论延迟多久

---

## 五、三种异常检测机制对比

系统设计了三种互补的异常检测机制，覆盖不同的异常场景：

| 机制 | 检测条件 | 触发时机 | 系统动作 | 配置开关 |
|------|---------|---------|---------|---------|
| **提前触发检测** | `currentTime < EarliestDequeueTime` | 传感器触发时 | 不出队、不执行、记录告警 | `EnableEarlyTriggerDetection` |
| **超时处理** | `currentTime > ExpectedArrivalTime + TimeoutThreshold` | 传感器触发时 | 执行回退动作、插入补偿任务 | `EnableTimeoutDetection` |
| **丢失检测** | `currentTime > LostDetectionDeadline` | 后台定时扫描 | 从队列移除任务、修改后续包裹动作 | 自动启用 |

**时间关系**：
```
EarliestDequeueTime < ExpectedArrivalTime < TimeoutDeadline < LostDetectionDeadline
   |                         |                      |                    |
   |<------- 正常窗口 ------->|<--- 延迟窗口 ------>|<--- 丢失检测 ---->|
```

**示例时间线**（假设 ExpectedArrivalTime = 00:05:00, TimeoutThreshold = 2000ms, LostDetectionTimeout = 3000ms）：
```
00:04:58         00:05:00         00:05:02         00:05:03
   |                |                |                |
最早出队时间      期望到达         超时判定线       丢失判定线
   |                |                |                |
   |<-- 提前触发 -->|<-- 正常窗口 -->|<-- 超时处理 -->|<-- 包裹丢失 -->
```

---

## 六、实施要点

### 6.1 为什么需要提前触发检测？

**问题场景**：
- 输送线速度不稳定（加速/减速）
- 包裹间距不均匀
- 传感器抖动或干扰信号

**不检测的风险**：
- 队列头部的包裹可能被错误的物理包裹触发
- 导致包裹分拣到错误的格口
- 破坏 FIFO 队列顺序

**检测的好处**：
- ✅ 防止错位：确保队列任务与物理包裹一一对应
- ✅ 保持顺序：维护 FIFO 队列的准确性
- ✅ 监控告警：识别输送线速度异常

### 6.2 动态时间更新机制

**目的**: 避免误差累积

**原理**：
1. 每次传感器触发时，记录包裹的**实际到达时间**
2. 基于实际到达时间，重新计算下一个 position 的**期望到达时间**
3. 更新下一个 position 队列中对应任务的时间字段

**计算公式**：
```csharp
// 1. 获取下一个输送段的理论传输时间
var transitTimeMs = GetSegmentTransitTime(nextPosition);

// 2. 基于实际到达时间计算下一个期望时间
var nextExpectedArrival = actualArrivalTime.AddMilliseconds(transitTimeMs);

// 3. 重新计算最早出队时间
var nextEarliestDequeue = nextExpectedArrival.AddMilliseconds(-TimeoutThresholdMs);
if (nextEarliestDequeue < task.CreatedAt)
{
    nextEarliestDequeue = task.CreatedAt;
}

// 4. 更新下一个position队列中的任务
UpdateTaskTiming(nextPosition, task.ParcelId, nextExpectedArrival, nextEarliestDequeue);
```

---

## 七、常见问题

### Q1: 如果包裹在 00:01 触发，提前了 4 分钟，会发生什么？

**A**: 如果启用了提前触发检测（`EnableEarlyTriggerDetection = true`）：
- ⚠️ 系统会检测到 `currentTime (00:01) < EarliestDequeueTime (00:04:58)`
- ⚠️ **不会出队**，任务保留在队列头部
- ⚠️ **不会执行摆轮动作**
- 📝 记录提前触发告警日志
- 📊 记录分拣异常统计

包裹的任务仍在队列中等待，下次触发时会再次判断。只有当触发时间 >= 00:04:58 时，才会正常出队并执行。

### Q2: 如果禁用提前触发检测会怎样？

**A**: 如果 `EnableEarlyTriggerDetection = false`（默认值）：
- ✅ 系统会直接从队列取出任务
- ✅ 执行计划的摆轮动作
- ⚠️ **可能导致错位**：队列头部的包裹被错误的物理包裹触发

**建议**: 在输送线速度不稳定或包裹间距不均匀的场景下，建议启用提前触发检测。

### Q3: 提前触发检测会不会导致包裹一直不出队？

**A**: 不会。提前触发检测只是延迟出队，而不是永久阻止：
- 当传感器触发时间 >= `EarliestDequeueTime` 时，会正常出队
- 如果包裹真的物理到达了传感器，最终会在正常窗口内触发
- 如果包裹长时间不到达，会被丢失检测机制处理

### Q4: 提前触发和超时处理的区别是什么？

**A**: 两者处理不同的异常场景：

| 对比项 | 提前触发检测 | 超时处理 |
|-------|------------|---------|
| **检测条件** | `currentTime < EarliestDequeueTime` | `currentTime > ExpectedArrivalTime + Timeout` |
| **触发原因** | 包裹提前到达 | 包裹延迟到达 |
| **系统动作** | 不出队、不执行 | 执行回退动作、插入补偿任务 |
| **物理包裹** | 可能是其他包裹（错位） | 确实是该包裹（延迟） |

### Q5: 为什么要动态更新下一个position的期望时间？

**A**: 避免误差累积：
- 初始期望时间基于**理论传输时间**计算
- 实际传输时间可能与理论值有偏差（输送线速度波动）
- 如果不更新，误差会在多个 position 之间累积
- 动态更新基于**实际到达时间**重新计算，减少误差

---

## 八、相关文档

- **`docs/CORE_ROUTING_LOGIC.md`** - 核心路由逻辑（必读）
- **`docs/RepositoryStructure.md`** - 仓库结构
- **`.github/copilot-instructions.md`** - 编码规范
- **`docs/guides/SYSTEM_CONFIG_GUIDE.md`** - 系统配置指南

---

## 九、技术实现参考

### 源代码位置

**队列管理**：
- `src/Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Execution/PositionQueueItem.cs` - 队列任务模型
- `src/Execution/ZakYip.WheelDiverterSorter.Execution/Queues/IPositionIndexQueueManager.cs` - 队列管理接口
- `src/Execution/ZakYip.WheelDiverterSorter.Execution/Queues/PositionIndexQueueManager.cs` - 队列管理实现

**传感器触发处理**：
- `src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs` (行 1260-1520)
  - `HandleWheelFrontSensorAsync()` - 传感器触发入口
  - `ExecuteWheelFrontSortingAsync()` - 具体处理逻辑
  - `UpdateNextPositionExpectedTime()` - 动态时间更新

**配置模型**：
- `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Models/SystemConfiguration.cs`
  - `EnableEarlyTriggerDetection` - 提前触发检测开关
  - `EnableTimeoutDetection` - 超时检测开关

---

**文档版本**: 1.0  
**最后更新**: 2025-12-27  
**维护团队**: ZakYip Development Team
