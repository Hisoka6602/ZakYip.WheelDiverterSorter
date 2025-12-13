# 包裹丢失检测方案

> **文档类型**: 技术方案  
> **创建时间**: 2025-12-13  
> **问题来源**: Issue - 包裹丢失导致后续包裹错分  
> **约束条件**: 传感器只能感应到有包裹经过，无法得知包裹ID

---

## 一、问题描述

### 1.1 场景重现

**初始状态**：
- 包裹P1、P2、P3依次创建，对应目标格口为1、2、3
- positionIndex 1的队列状态：`[P1-Left, P2-Right, P3-Straight]`

**正常流程**：
1. P1到达positionIndex1 → 执行Left → 到达格口1 ✓
2. P2到达positionIndex1 → 执行Right → 到达格口2 ✓  
3. P3到达positionIndex1 → 执行Straight → 继续到positionIndex2 ✓

**异常流程（P2丢失）**：
1. P1到达positionIndex1 → 执行Left → 到达格口1 ✓
2. **P2在物理线体上丢失** ❌
3. P3到达positionIndex1 → **系统认为是P2** → 执行Right → **P3错误到达格口2** ❌
4. 后续所有包裹的动作都错位执行 ❌

### 1.2 根本原因

**当前系统行为**：
- 使用纯FIFO队列机制
- 传感器触发时，直接从队首取出任务并执行
- **无法验证实际到达的包裹是否与队首任务匹配**
- 传感器只能感知"有包裹经过"，无法识别"是哪个包裹"

**问题核心**：
- 物理世界的包裹顺序（实际）≠ 队列中的任务顺序（期望）
- 系统缺乏机制来检测两者的不一致

---

## 二、技术约束分析

### 2.1 硬件约束

**传感器能力**：
```
✓ 可以检测：包裹通过（触发信号）
✓ 可以检测：触发时间戳
✗ 不能检测：包裹ID
✗ 不能检测：包裹尺寸/重量等物理特征
```

**已有传感器类型**（来自代码分析）：
- `ParcelCreation`：入口传感器，创建包裹
- `WheelFront`：摆轮前传感器，触发动作执行

### 2.2 系统约束

**现有机制**：
1. **位置索引队列（Position-Index Queue）**
   - 每个positionIndex对应一个FIFO队列
   - 队列中存储 `PositionQueueItem`（包含ParcelId、DiverterId、Action等）
   
2. **超时检测机制**
   - 每个任务有 `ExpectedArrivalTime` 和 `TimeoutThresholdMs`
   - 触发时检查是否超时：`now > ExpectedArrivalTime + TimeoutThresholdMs`
   - 超时时执行 `FallbackAction`（默认Straight）

3. **IO触发机制**
   - 所有动作基于IO触发，不主动扫描
   - 符合 `CORE_ROUTING_LOGIC.md` 的强制约束

---

## 三、检测方案设计

由于传感器无法识别包裹ID，我们需要使用**间接检测**方法，通过以下维度建立检测机制：

### 方案A：增强型超时检测（推荐 ⭐）

#### 核心思路
扩展现有超时机制，引入"严重超时"概念，当超时达到严重级别时，判定为包裹丢失。

#### 检测条件

**现有超时逻辑**：
```
正常超时 = now > ExpectedArrivalTime + TimeoutThresholdMs
→ 执行FallbackAction（Straight）
→ 在后续position插入补偿任务
```

**新增严重超时逻辑**：
```
严重超时 = now > ExpectedArrivalTime + CriticalTimeoutThresholdMs

其中：
CriticalTimeoutThresholdMs = TimeoutThresholdMs * CriticalMultiplier
推荐值：CriticalMultiplier = 2.0 ~ 3.0

例如：
- 正常超时阈值 = 2000ms
- 严重超时阈值 = 6000ms (3倍)
```

#### 判定逻辑

```csharp
var task = _queueManager.DequeueTask(positionIndex);
if (task == null)
{
    // 队列为空但传感器触发 → 可能是：
    // 1. 配置错误
    // 2. 系统状态异常
    // 3. 前序包裹全部丢失
    _logger.LogWarning("队列为空但传感器触发");
    return;
}

var currentTime = _clock.LocalNow;
var delayMs = (currentTime - task.ExpectedArrivalTime).TotalMilliseconds;

// 正常到达
if (delayMs <= task.TimeoutThresholdMs)
{
    await ExecuteNormalAction(task);
}
// 轻微超时（原有逻辑）
else if (delayMs <= task.TimeoutThresholdMs * CriticalMultiplier)
{
    await ExecuteTimeoutAction(task);
    InsertCompensationTasks(task.ParcelId, positionIndex);
}
// 严重超时 → 判定为包裹丢失
else
{
    _logger.LogError(
        "检测到严重超时 (DelayMs={DelayMs}ms, Threshold={Threshold}ms, Critical={Critical}ms), " +
        "判定包裹 {ParcelId} 丢失",
        delayMs,
        task.TimeoutThresholdMs,
        task.TimeoutThresholdMs * CriticalMultiplier,
        task.ParcelId);
    
    // 处理丢失场景
    await HandleParcelLoss(task, currentTime, positionIndex);
}
```

#### 丢失处理流程

```csharp
private async Task HandleParcelLoss(
    PositionQueueItem lostTask, 
    DateTime detectedAt, 
    int positionIndex)
{
    // 1. 记录丢失包裹
    _logger.LogError(
        "[包裹丢失] ParcelId={ParcelId}, Position={Position}, " +
        "ExpectedArrival={Expected:HH:mm:ss.fff}, ActualTrigger={Actual:HH:mm:ss.fff}, " +
        "Delay={DelayMs}ms",
        lostTask.ParcelId,
        positionIndex,
        lostTask.ExpectedArrivalTime,
        detectedAt,
        (detectedAt - lostTask.ExpectedArrivalTime).TotalMilliseconds);
    
    // 2. 从所有队列中移除该包裹的任务
    int removedCount = RemoveAllTasksForParcel(lostTask.ParcelId);
    
    _logger.LogWarning(
        "[队列清理] 已从所有队列移除包裹 {ParcelId} 的 {Count} 个任务",
        lostTask.ParcelId,
        removedCount);
    
    // 3. 发送包裹丢失事件
    OnParcelLossDetected(new ParcelLossDetectedEventArgs
    {
        LostParcelId = lostTask.ParcelId,
        PositionIndex = positionIndex,
        DetectedAt = detectedAt,
        TotalTasksRemoved = removedCount,
        DelayMilliseconds = (detectedAt - lostTask.ExpectedArrivalTime).TotalMilliseconds
    });
    
    // 4. 记录指标
    _metrics?.RecordParcelLoss(positionIndex);
    
    // 5. 通知上游系统（如果需要）
    // await NotifyUpstreamParcelLoss(lostTask.ParcelId);
    
    // 6. 重新处理当前触发
    // 当前触发的包裹应该是队列中的下一个包裹
    // 递归调用处理逻辑，使用更新后的队列
    _logger.LogInformation(
        "[重新处理] 丢失包裹已清理，重新处理当前传感器触发");
    
    await ExecuteWheelFrontSortingAsync(
        lostTask.DiverterId, 
        /* sensorId */ 0, // 需要从上下文获取
        positionIndex);
}
```

#### 优点
- ✅ 最小代码改动，基于现有超时机制扩展
- ✅ 符合系统架构约束（IO触发、FIFO队列）
- ✅ 不依赖硬件升级
- ✅ 可配置的阈值，适应不同线体速度

#### 缺点
- ⚠️ 检测延迟：需要等到严重超时才能确认丢失
- ⚠️ 可能误判：线体临时停止可能被误判为丢失
- ⚠️ 只能被动检测，无法预测性警告

#### 适用场景
- 包裹在线体上完全消失（掉落、卡住等）
- 包裹流速相对稳定
- 超时阈值配置合理

---

### 方案B：跨位置序列一致性检测

#### 核心思路
利用多个位置的传感器触发序列，交叉验证包裹流动的一致性。

#### 检测机制

**数据结构**：
```csharp
// 全局包裹追踪记录
private readonly ConcurrentDictionary<long, ParcelTrackingRecord> _parcelTracking = new();

public class ParcelTrackingRecord
{
    public long ParcelId { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<PositionCheckpoint> Checkpoints { get; init; } = new();
    public ParcelStatus Status { get; set; } = ParcelStatus.Active;
}

public class PositionCheckpoint
{
    public int PositionIndex { get; init; }
    public DateTime ExpectedArrivalTime { get; init; }
    public DateTime? ActualArrivalTime { get; set; }
    public bool IsConfirmed { get; set; }
}

public enum ParcelStatus
{
    Active,      // 正常流动中
    Lost,        // 已判定丢失
    Completed    // 已到达目标格口
}
```

**检测逻辑**：

```csharp
// 在每次传感器触发时
private async Task OnSensorTriggered(long sensorId, int positionIndex)
{
    // 1. 从队列取出期望的任务
    var expectedTask = _queueManager.PeekTask(positionIndex);
    
    if (expectedTask == null)
    {
        // 队列为空但有触发 → 异常情况
        await HandleUnexpectedTrigger(sensorId, positionIndex);
        return;
    }
    
    // 2. 更新追踪记录
    if (_parcelTracking.TryGetValue(expectedTask.ParcelId, out var tracking))
    {
        var checkpoint = tracking.Checkpoints
            .FirstOrDefault(c => c.PositionIndex == positionIndex);
        
        if (checkpoint != null)
        {
            checkpoint.ActualArrivalTime = _clock.LocalNow;
            checkpoint.IsConfirmed = true;
        }
    }
    
    // 3. 检查前序位置的未确认包裹
    var lostParcels = DetectLostParcelsBySequence(positionIndex);
    
    if (lostParcels.Any())
    {
        foreach (var lostParcelId in lostParcels)
        {
            await HandleParcelLoss(lostParcelId, positionIndex);
        }
    }
    
    // 4. 执行正常动作
    var task = _queueManager.DequeueTask(positionIndex);
    await ExecuteAction(task);
}

// 通过序列检测丢失包裹
private List<long> DetectLostParcelsBySequence(int currentPosition)
{
    var lostParcels = new List<long>();
    var currentTime = _clock.LocalNow;
    
    foreach (var (parcelId, tracking) in _parcelTracking)
    {
        if (tracking.Status != ParcelStatus.Active)
            continue;
        
        // 查找当前位置之前的所有检查点
        var priorCheckpoints = tracking.Checkpoints
            .Where(c => c.PositionIndex < currentPosition)
            .OrderBy(c => c.PositionIndex);
        
        foreach (var checkpoint in priorCheckpoints)
        {
            // 如果前序位置未确认，且严重超时
            if (!checkpoint.IsConfirmed)
            {
                var delayMs = (currentTime - checkpoint.ExpectedArrivalTime).TotalMilliseconds;
                var criticalThreshold = GetCriticalThreshold(checkpoint.PositionIndex);
                
                if (delayMs > criticalThreshold)
                {
                    // 后续包裹已到达当前位置，但该包裹在前序位置未确认
                    // → 该包裹在前序位置与当前位置之间丢失
                    _logger.LogError(
                        "[序列检测] 包裹 {ParcelId} 在 Position {Position} 丢失 " +
                        "(后续包裹已到达 Position {CurrentPosition})",
                        parcelId,
                        checkpoint.PositionIndex,
                        currentPosition);
                    
                    lostParcels.Add(parcelId);
                    tracking.Status = ParcelStatus.Lost;
                    break; // 每个包裹只判定一次
                }
            }
        }
    }
    
    return lostParcels;
}
```

#### 判定规则

**规则1：时间窗口一致性**
```
如果包裹P1的期望到达时间早于包裹P2，
但在后续位置观察到：
- P2已确认到达
- P1仍未确认到达，且已严重超时
→ 判定P1丢失
```

**规则2：顺序一致性**
```
如果包裹序列为 [P1, P2, P3]，
在positionIndex N观察到：
- P3已触发执行
- P2在前序位置未确认
→ 判定P2在前序位置丢失
```

#### 优点
- ✅ 更高的检测准确性（跨位置交叉验证）
- ✅ 可以提前检测（无需等到严重超时）
- ✅ 可定位丢失发生的区间

#### 缺点
- ⚠️ 复杂度高，需要维护全局追踪状态
- ⚠️ 内存开销（需要保留所有活跃包裹的记录）
- ⚠️ 需要确保状态同步的线程安全性

#### 适用场景
- 多摆轮线体（≥3个position）
- 需要精确定位丢失位置
- 系统资源充足

---

### 方案C：统计模式识别（长期方案）

#### 核心思路
通过统计历史触发模式，建立正常行为基线，识别异常模式。

#### 检测维度

1. **触发频率分析**
   ```
   正常模式：传感器触发间隔 = 平均包裹间距 / 线体速度
   异常模式：连续多次触发间隔异常 → 可能包裹丢失或堆积
   ```

2. **队列深度监控**
   ```
   正常模式：各position队列深度大致相等
   异常模式：某position队列深度异常增长 → 前序position可能有包裹丢失
   ```

3. **完成率对比**
   ```
   正常模式：创建包裹数 ≈ 完成包裹数
   异常模式：长期不平衡 → 存在丢失或卡住
   ```

#### 实现框架

```csharp
public class ParcelFlowAnalyzer
{
    // 滑动窗口统计
    private readonly Queue<TriggerEvent> _triggerHistory = new();
    private readonly TimeSpan _windowSize = TimeSpan.FromMinutes(5);
    
    // 正常基线
    private double _baselineIntervalMs = 2000; // 从历史数据学习
    private double _baselineStdDeviation = 500;
    
    public ParcelLossAlert? AnalyzeTriggerPattern(
        long sensorId, 
        int positionIndex, 
        DateTime triggerTime)
    {
        // 1. 添加新触发记录
        _triggerHistory.Enqueue(new TriggerEvent
        {
            SensorId = sensorId,
            PositionIndex = positionIndex,
            TriggerTime = triggerTime
        });
        
        // 2. 清理过期数据
        CleanupOldHistory();
        
        // 3. 计算当前间隔
        var recentTriggers = GetRecentTriggersForPosition(positionIndex);
        if (recentTriggers.Count < 2)
            return null;
        
        var intervals = CalculateIntervals(recentTriggers);
        var currentInterval = intervals.Last();
        
        // 4. 异常检测
        if (currentInterval > _baselineIntervalMs + 3 * _baselineStdDeviation)
        {
            // 当前间隔异常长 → 可能有包裹丢失
            return new ParcelLossAlert
            {
                AlertType = AlertType.LongInterval,
                PositionIndex = positionIndex,
                ExpectedIntervalMs = _baselineIntervalMs,
                ActualIntervalMs = currentInterval,
                ConfidenceLevel = CalculateConfidence(currentInterval)
            };
        }
        
        // 5. 更新基线（自适应学习）
        UpdateBaseline(intervals);
        
        return null;
    }
}
```

#### 优点
- ✅ 可自适应学习正常模式
- ✅ 可提供预警（不仅是事后检测）
- ✅ 可识别多种异常模式

#### 缺点
- ⚠️ 需要学习周期，初期可能不准确
- ⚠️ 实现复杂度最高
- ⚠️ 需要大量历史数据

#### 适用场景
- 长期运行的生产系统
- 有稳定的包裹流模式
- 需要预测性维护

---

## 四、方案对比与推荐

### 4.1 方案对比矩阵

| 维度 | 方案A：增强超时 | 方案B：序列一致性 | 方案C：统计识别 |
|------|----------------|------------------|----------------|
| **实现难度** | ⭐ 简单 | ⭐⭐⭐ 中等 | ⭐⭐⭐⭐ 复杂 |
| **代码改动量** | 小 | 中 | 大 |
| **检测延迟** | 高（需严重超时） | 中（跨位置验证） | 低（实时分析） |
| **检测准确性** | ⭐⭐⭐ 中等 | ⭐⭐⭐⭐ 高 | ⭐⭐⭐⭐⭐ 很高 |
| **误判风险** | 中（线体停止误判） | 低 | 很低（有置信度） |
| **资源消耗** | 低 | 中（内存） | 高（计算+存储） |
| **配置复杂度** | 低（1个参数） | 中 | 高 |
| **可维护性** | ⭐⭐⭐⭐ 高 | ⭐⭐⭐ 中等 | ⭐⭐ 较低 |
| **即时可用性** | 立即 | 立即 | 需学习期 |

### 4.2 推荐策略

#### 阶段1：立即实施（方案A）

**推荐理由**：
1. 最小改动，快速上线
2. 基于现有超时机制，风险低
3. 可解决大部分明显丢失场景

**实施要点**：
```
1. 添加 CriticalTimeoutMultiplier 配置项（推荐值：2.5）
2. 修改 ExecuteWheelFrontSortingAsync 方法
3. 实现 RemoveAllTasksForParcel 方法
4. 添加包裹丢失事件和指标
5. 编写单元测试和E2E测试
```

**配置示例**：
```json
{
  "SortingConfiguration": {
    "TimeoutThresholdMs": 2000,
    "CriticalTimeoutMultiplier": 2.5,  // 新增
    "EnableParcelLossDetection": true   // 新增
  }
}
```

#### 阶段2：增强检测（方案B，可选）

**实施时机**：
- 方案A运行3个月后
- 如果出现以下情况：
  - 方案A检测延迟过高
  - 需要精确定位丢失位置
  - 误判率不可接受

**实施要点**：
```
1. 实现 ParcelTrackingRecord 全局追踪
2. 在 OnParcelDetected 中记录检查点
3. 在每次触发时执行序列检测
4. 与方案A并行运行，交叉验证
```

#### 阶段3：智能优化（方案C，长期）

**实施时机**：
- 系统稳定运行1年后
- 积累足够历史数据
- 需要预测性维护

---

## 五、具体实施方案（方案A详细设计）

### 5.1 配置模型变更

**新增配置项**：

```csharp
// 位置：src/Core/.../LineModel/Configuration/Models/SystemConfiguration.cs
public class SystemConfiguration
{
    // ... 现有字段 ...
    
    /// <summary>
    /// 严重超时倍数（用于包裹丢失检测）
    /// </summary>
    /// <remarks>
    /// 当包裹延迟超过 TimeoutThreshold * CriticalTimeoutMultiplier 时，
    /// 判定为包裹丢失而非普通超时。
    /// 推荐值：2.5 ~ 3.0
    /// </remarks>
    public double CriticalTimeoutMultiplier { get; set; } = 2.5;
    
    /// <summary>
    /// 是否启用包裹丢失检测
    /// </summary>
    public bool EnableParcelLossDetection { get; set; } = true;
}
```

### 5.2 事件模型定义

**文件路径**：`src/Core/.../Events/Sorting/ParcelLossDetectedEventArgs.cs`

```csharp
namespace ZakYip.WheelDiverterSorter.Core.Events.Sorting;

/// <summary>
/// 包裹丢失检测事件参数
/// </summary>
public sealed record class ParcelLossDetectedEventArgs
{
    /// <summary>丢失的包裹ID</summary>
    public required long LostParcelId { get; init; }
    
    /// <summary>检测到丢失的位置索引</summary>
    public required int PositionIndex { get; init; }
    
    /// <summary>传感器ID</summary>
    public required long SensorId { get; init; }
    
    /// <summary>检测时间</summary>
    public required DateTime DetectedAt { get; init; }
    
    /// <summary>实际延迟（毫秒）</summary>
    public required double DelayMilliseconds { get; init; }
    
    /// <summary>从所有队列移除的任务总数</summary>
    public required int TotalTasksRemoved { get; init; }
}
```

### 5.3 队列管理器接口扩展

**文件路径**：`src/Execution/.../Queues/IPositionIndexQueueManager.cs`

```csharp
public interface IPositionIndexQueueManager
{
    // ... 现有方法 ...
    
    /// <summary>
    /// 从所有队列中移除指定包裹的所有任务
    /// </summary>
    /// <param name="parcelId">要移除的包裹ID</param>
    /// <returns>移除的任务总数</returns>
    int RemoveAllTasksForParcel(long parcelId);
}
```

**实现**：`src/Execution/.../Queues/PositionIndexQueueManager.cs`

```csharp
public int RemoveAllTasksForParcel(long parcelId)
{
    int totalRemoved = 0;
    
    foreach (var (positionIndex, queue) in _queues)
    {
        // 临时存储需要保留的任务
        var tasksToKeep = new List<PositionQueueItem>();
        
        // 遍历队列，筛选任务
        while (queue.TryDequeue(out var task))
        {
            if (task.ParcelId == parcelId)
            {
                totalRemoved++;
                _logger.LogDebug(
                    "移除包裹 {ParcelId} 在 Position {Position} 的任务",
                    parcelId, positionIndex);
            }
            else
            {
                tasksToKeep.Add(task);
            }
        }
        
        // 将保留的任务重新入队
        foreach (var task in tasksToKeep)
        {
            queue.Enqueue(task);
        }
    }
    
    _logger.LogInformation(
        "已从所有队列移除包裹 {ParcelId} 的 {Count} 个任务",
        parcelId, totalRemoved);
    
    return totalRemoved;
}
```

### 5.4 核心检测逻辑

**文件路径**：`src/Execution/.../Orchestration/SortingOrchestrator.cs`

**修改方法**：`ExecuteWheelFrontSortingAsync`

```csharp
private async Task ExecuteWheelFrontSortingAsync(
    long boundWheelDiverterId, 
    long sensorId, 
    int positionIndex)
{
    _logger.LogDebug(
        "Position {PositionIndex} 传感器 {SensorId} 触发，从队列取出任务",
        positionIndex, sensorId);

    // 从 Position-Index 队列取出任务
    var task = _queueManager!.DequeueTask(positionIndex);
    
    if (task == null)
    {
        _logger.LogWarning(
            "Position {PositionIndex} 队列为空，但传感器 {SensorId} 被触发",
            positionIndex, sensorId);
        _metrics?.RecordSortingFailure(0);
        return;
    }
    
    // 获取系统配置
    var systemConfig = _systemConfigRepository.Get();
    var enableLossDetection = systemConfig.EnableParcelLossDetection;
    var criticalMultiplier = systemConfig.CriticalTimeoutMultiplier;
    
    // 检查超时
    var currentTime = _clock.LocalNow;
    var delayMs = (currentTime - task.ExpectedArrivalTime).TotalMilliseconds;
    var normalThreshold = task.TimeoutThresholdMs;
    var criticalThreshold = normalThreshold * criticalMultiplier;
    
    DiverterDirection actionToExecute;
    
    // 判定超时级别
    if (delayMs <= normalThreshold)
    {
        // 正常到达
        actionToExecute = task.DiverterAction;
        
        _logger.LogDebug(
            "包裹 {ParcelId} 正常到达 Position {Position} (延迟 {DelayMs}ms)",
            task.ParcelId, positionIndex, delayMs);
    }
    else if (delayMs <= criticalThreshold)
    {
        // 轻微超时（现有逻辑）
        actionToExecute = task.FallbackAction;
        
        _logger.LogWarning(
            "包裹 {ParcelId} 在 Position {Position} 超时 (延迟 {DelayMs}ms)，使用回退动作 {FallbackAction}",
            task.ParcelId, positionIndex, delayMs, task.FallbackAction);
        
        // 插入后续补偿任务
        InsertCompensationTasksForTimeout(task, positionIndex);
        
        _metrics?.RecordSortingFailure(0);
    }
    else if (enableLossDetection)
    {
        // 严重超时 → 判定为包裹丢失
        _logger.LogError(
            "检测到严重超时，判定包裹 {ParcelId} 丢失: " +
            "DelayMs={DelayMs}ms, NormalThreshold={Normal}ms, CriticalThreshold={Critical}ms, " +
            "Position={Position}, Sensor={SensorId}",
            task.ParcelId, delayMs, normalThreshold, criticalThreshold, 
            positionIndex, sensorId);
        
        // 处理包裹丢失
        await HandleParcelLossAsync(task, currentTime, positionIndex, sensorId);
        
        // 重新从队列取任务（处理当前触发）
        return; // 递归会在 HandleParcelLossAsync 中处理
    }
    else
    {
        // 未启用丢失检测，降级为超时处理
        actionToExecute = task.FallbackAction;
        
        _logger.LogWarning(
            "包裹 {ParcelId} 严重超时 (延迟 {DelayMs}ms)，但未启用丢失检测，降级为超时处理",
            task.ParcelId, delayMs);
        
        InsertCompensationTasksForTimeout(task, positionIndex);
        _metrics?.RecordSortingFailure(0);
    }
    
    // 执行摆轮动作
    await ExecuteDiverterActionAsync(task, actionToExecute, positionIndex);
    
    if (delayMs <= normalThreshold)
    {
        _metrics?.RecordSortingSuccess(0);
    }
}

/// <summary>
/// 处理包裹丢失场景
/// </summary>
private async Task HandleParcelLossAsync(
    PositionQueueItem lostTask,
    DateTime detectedAt,
    int positionIndex,
    long sensorId)
{
    var delayMs = (detectedAt - lostTask.ExpectedArrivalTime).TotalMilliseconds;
    
    // 1. 记录丢失事件
    _logger.LogError(
        "[包裹丢失] ParcelId={ParcelId}, Position={Position}, " +
        "ExpectedArrival={Expected:HH:mm:ss.fff}, ActualDetection={Actual:HH:mm:ss.fff}, " +
        "Delay={DelayMs}ms, Sensor={SensorId}",
        lostTask.ParcelId, positionIndex,
        lostTask.ExpectedArrivalTime, detectedAt, delayMs, sensorId);
    
    // 2. 从所有队列移除该包裹的任务
    int removedCount = _queueManager!.RemoveAllTasksForParcel(lostTask.ParcelId);
    
    _logger.LogWarning(
        "[队列清理] 已从所有队列移除包裹 {ParcelId} 的 {Count} 个任务",
        lostTask.ParcelId, removedCount);
    
    // 3. 触发包裹丢失事件
    OnParcelLossDetected(new ParcelLossDetectedEventArgs
    {
        LostParcelId = lostTask.ParcelId,
        PositionIndex = positionIndex,
        SensorId = sensorId,
        DetectedAt = detectedAt,
        DelayMilliseconds = delayMs,
        TotalTasksRemoved = removedCount
    });
    
    // 4. 记录指标
    _metrics?.RecordParcelLoss(positionIndex);
    
    // 5. 通知上游系统（可选）
    if (_upstreamClient != null)
    {
        try
        {
            // 发送包裹丢失通知（如果上游协议支持）
            // await _upstreamClient.NotifyParcelLostAsync(lostTask.ParcelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知上游系统包裹丢失失败");
        }
    }
    
    // 6. 重新处理当前触发
    // 当前触发应该对应队列中的下一个包裹
    _logger.LogInformation(
        "[重新处理] 丢失包裹 {LostParcelId} 已清理，重新处理 Position {Position} 的当前触发",
        lostTask.ParcelId, positionIndex);
    
    // 递归调用，处理更新后的队列头部任务
    await ExecuteWheelFrontSortingAsync(
        lostTask.DiverterId,
        sensorId,
        positionIndex);
}

/// <summary>
/// 包裹丢失检测事件
/// </summary>
public event EventHandler<ParcelLossDetectedEventArgs>? ParcelLossDetected;

/// <summary>
/// 触发包裹丢失事件
/// </summary>
private void OnParcelLossDetected(ParcelLossDetectedEventArgs e)
{
    try
    {
        ParcelLossDetected?.Invoke(this, e);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "触发包裹丢失事件时发生异常");
    }
}
```

### 5.5 指标收集

**文件路径**：`src/Observability/.../PrometheusMetrics.cs`

```csharp
public class PrometheusMetrics
{
    // ... 现有指标 ...
    
    /// <summary>
    /// 包裹丢失计数器
    /// </summary>
    private readonly Counter _parcelLossCounter = Metrics.CreateCounter(
        "wds_parcel_loss_total",
        "包裹丢失总数",
        new CounterConfiguration
        {
            LabelNames = new[] { "position_index" }
        });
    
    /// <summary>
    /// 记录包裹丢失
    /// </summary>
    public void RecordParcelLoss(int positionIndex)
    {
        _parcelLossCounter.WithLabels(positionIndex.ToString()).Inc();
    }
}
```

### 5.6 测试用例

#### 单元测试

**文件路径**：`tests/.../Execution.Tests/Orchestration/ParcelLossDetectionTests.cs`

```csharp
public class ParcelLossDetectionTests
{
    [Fact]
    public async Task Should_Detect_Parcel_Loss_When_Critical_Timeout_Exceeded()
    {
        // Arrange
        var clock = new MockSystemClock();
        var queueManager = new PositionIndexQueueManager(...);
        var orchestrator = new SortingOrchestrator(...);
        
        // 创建3个包裹：P1、P2、P3
        await orchestrator.ProcessParcelAsync(parcelId: 1, sensorId: 1);
        await orchestrator.ProcessParcelAsync(parcelId: 2, sensorId: 1);
        await orchestrator.ProcessParcelAsync(parcelId: 3, sensorId: 1);
        
        // positionIndex 1 队列：[P1, P2, P3]
        
        // Act：P1正常触发
        await TriggerSensor(sensorId: 2, positionIndex: 1);
        // 队列变为：[P2, P3]
        
        // Act：P2丢失，P3到达（时间超过严重超时阈值）
        clock.Advance(TimeSpan.FromSeconds(10)); // 超过严重超时
        
        ParcelLossDetectedEventArgs? lossEvent = null;
        orchestrator.ParcelLossDetected += (s, e) => lossEvent = e;
        
        await TriggerSensor(sensorId: 2, positionIndex: 1);
        
        // Assert
        Assert.NotNull(lossEvent);
        Assert.Equal(2, lossEvent.LostParcelId);
        Assert.Equal(1, lossEvent.PositionIndex);
        Assert.True(lossEvent.TotalTasksRemoved > 0);
        
        // 验证P2的任务已从所有队列移除
        var remainingTasks = queueManager.GetAllQueueStatuses();
        Assert.DoesNotContain(
            remainingTasks.Values.SelectMany(q => q.Tasks),
            t => t.ParcelId == 2);
    }
    
    [Theory]
    [InlineData(1000, false)]  // 正常延迟
    [InlineData(2500, false)]  // 轻微超时
    [InlineData(6000, true)]   // 严重超时 → 丢失
    public async Task Should_Handle_Different_Timeout_Levels(
        int delayMs,
        bool shouldDetectLoss)
    {
        // ... 测试不同超时级别的处理 ...
    }
}
```

#### 集成测试

**文件路径**：`tests/.../E2ETests/ParcelLossScenarioTests.cs`

```csharp
public class ParcelLossScenarioTests
{
    [Fact]
    public async Task Full_Scenario_P2_Lost_Between_P1_And_P3()
    {
        // Arrange：3摆轮6格口配置
        var testHarness = new E2ETestHarness();
        await testHarness.StartSystemAsync();
        
        // Act：创建3个包裹
        var p1 = await testHarness.CreateParcel(targetChute: 1); // D1左转
        var p2 = await testHarness.CreateParcel(targetChute: 2); // D1右转
        var p3 = await testHarness.CreateParcel(targetChute: 3); // D1直通→D2左转
        
        // P1正常通过
        await testHarness.TriggerSensor(frontSensorId: 2);
        await Task.Delay(100);
        Assert.Equal(1, testHarness.GetParcelChute(p1));
        
        // P2丢失（模拟：不触发传感器，但时间流逝）
        testHarness.Clock.Advance(TimeSpan.FromSeconds(10));
        
        // P3到达（应检测到P2丢失）
        ParcelLossDetectedEventArgs? lossEvent = null;
        testHarness.Orchestrator.ParcelLossDetected += (s, e) => lossEvent = e;
        
        await testHarness.TriggerSensor(frontSensorId: 2);
        await Task.Delay(100);
        
        // Assert
        Assert.NotNull(lossEvent);
        Assert.Equal(p2, lossEvent.LostParcelId);
        
        // P3应执行自己的动作（直通），而非P2的动作（右转）
        Assert.Equal(3, testHarness.GetParcelChute(p3)); // 最终到达格口3
        
        // P2的状态应标记为丢失
        var p2Status = await testHarness.GetParcelStatus(p2);
        Assert.Equal(ParcelStatus.Lost, p2Status);
    }
}
```

---

## 六、配置与部署

### 6.1 配置文件示例

**appsettings.json**：
```json
{
  "SystemConfiguration": {
    "ExceptionChuteId": 999,
    "CriticalTimeoutMultiplier": 2.5,
    "EnableParcelLossDetection": true
  },
  "Logging": {
    "LogLevel": {
      "ZakYip.WheelDiverterSorter.Execution.Orchestration.SortingOrchestrator": "Information"
    }
  }
}
```

### 6.2 监控与告警

**Prometheus查询**：
```promql
# 包裹丢失率
rate(wds_parcel_loss_total[5m])

# 按位置统计丢失
sum(rate(wds_parcel_loss_total[5m])) by (position_index)

# 告警规则
alert: HighParcelLossRate
expr: rate(wds_parcel_loss_total[5m]) > 0.01  # 每秒>0.01次
for: 5m
labels:
  severity: critical
annotations:
  summary: "包裹丢失率过高"
```

**Grafana面板**：
- 包裹丢失趋势图
- 各位置丢失热力图
- 丢失包裹详情列表

### 6.3 日志示例

**正常场景**：
```
[INFO] 包裹 20251213001 正常到达 Position 1 (延迟 150ms)
[INFO] 包裹 20251213002 正常到达 Position 1 (延迟 200ms)
[INFO] 包裹 20251213003 正常到达 Position 1 (延迟 180ms)
```

**丢失场景**：
```
[INFO] 包裹 20251213001 正常到达 Position 1 (延迟 150ms)
[ERROR] 检测到严重超时，判定包裹 20251213002 丢失: DelayMs=6500ms, NormalThreshold=2000ms, CriticalThreshold=5000ms, Position=1, Sensor=2
[ERROR] [包裹丢失] ParcelId=20251213002, Position=1, ExpectedArrival=12:30:00.000, ActualDetection=12:30:06.500, Delay=6500ms, Sensor=2
[WARN] [队列清理] 已从所有队列移除包裹 20251213002 的 2 个任务
[INFO] [重新处理] 丢失包裹 20251213002 已清理，重新处理 Position 1 的当前触发
[INFO] 包裹 20251213003 正常到达 Position 1 (延迟 200ms)
```

---

## 七、风险与限制

### 7.1 已知限制

1. **检测延迟**
   - 需要等待严重超时才能确认丢失
   - 检测时间 = 正常超时阈值 × 严重超时倍数
   - 例如：2000ms × 2.5 = 5000ms

2. **误判可能性**
   - 线体临时停止可能被误判为丢失
   - 建议：合理设置严重超时倍数（2.5~3.0）

3. **无法预测**
   - 只能事后检测，无法预防
   - 无法定位丢失的具体位置（仅知道在哪个position未到达）

### 7.2 不适用场景

1. **高频丢失场景**
   - 如果连续多个包裹丢失，检测可能失效
   - 建议：先解决根本的硬件/机械问题

2. **极短间隔场景**
   - 包裹间隔 < 500ms时，时间窗口判断可能不准确
   - 建议：调整线体速度或增加包裹间距

### 7.3 应对措施

1. **配置优化**
   ```
   - 根据实际线体速度调整超时阈值
   - 根据误判率调整严重超时倍数
   - 在生产环境中逐步启用（先观察后启用）
   ```

2. **监控与反馈**
   ```
   - 密切监控包裹丢失率
   - 分析丢失模式（时间、位置、频率）
   - 定期审查误判案例
   ```

3. **硬件改进（长期）**
   ```
   - 考虑升级到支持包裹识别的传感器（RFID/条码扫描）
   - 增加冗余传感器提高检测准确性
   ```

---

## 八、实施计划

### 8.1 Phase 1：核心实现（1-2天）

- [ ] 添加配置模型（CriticalTimeoutMultiplier、EnableParcelLossDetection）
- [ ] 创建ParcelLossDetectedEventArgs事件类
- [ ] 扩展IPositionIndexQueueManager接口（RemoveAllTasksForParcel）
- [ ] 实现RemoveAllTasksForParcel方法
- [ ] 修改ExecuteWheelFrontSortingAsync方法添加严重超时检测
- [ ] 实现HandleParcelLossAsync方法
- [ ] 添加PrometheusMetrics支持

### 8.2 Phase 2：测试验证（1-2天）

- [ ] 编写单元测试（ParcelLossDetectionTests）
- [ ] 编写集成测试（ParcelLossScenarioTests）
- [ ] 编写E2E测试（完整丢失场景）
- [ ] 性能测试（确保无性能退化）
- [ ] 边界条件测试（连续丢失、误判等）

### 8.3 Phase 3：部署与监控（1天）

- [ ] 更新配置文档
- [ ] 配置Prometheus告警规则
- [ ] 创建Grafana监控面板
- [ ] 灰度发布（先禁用检测，仅记录日志）
- [ ] 观察1周，分析日志和指标
- [ ] 正式启用检测功能

### 8.4 Phase 4：优化与维护（持续）

- [ ] 收集实际运行数据
- [ ] 优化严重超时倍数配置
- [ ] 根据反馈改进算法
- [ ] 评估是否需要实施方案B或方案C

---

## 九、FAQ

### Q1: 为什么不能直接通过包裹ID识别？

**A**: 当前传感器硬件限制，只能检测到有包裹通过，无法识别具体是哪个包裹。升级到RFID或条码扫描器需要较大的硬件投资。

### Q2: 如果连续两个包裹都丢失怎么办？

**A**: 方案A在连续丢失场景下仍能检测：
```
场景：P1、P2、P3，其中P1和P2都丢失

执行流程：
1. P3触发 → 队首是P1 → P1严重超时 → 检测到P1丢失
2. 从队列移除P1的所有任务
3. 递归处理：队首变为P2 → P2也严重超时 → 检测到P2丢失
4. 从队列移除P2的所有任务
5. 再次递归：队首变为P3 → P3正常执行
```

### Q3: 严重超时倍数如何选择？

**A**: 建议根据线体特性选择：

| 线体类型 | 正常超时 | 严重超时倍数 | 严重超时阈值 |
|---------|---------|-------------|-------------|
| 高速线体 | 1000ms | 2.0 | 2000ms |
| 标准线体 | 2000ms | 2.5 | 5000ms |
| 慢速线体 | 3000ms | 3.0 | 9000ms |

原则：严重超时阈值 > 正常到达时间的最大偏差

### Q4: 误判如何处理？

**A**: 
1. 短期：通过日志分析误判案例，调整配置
2. 中期：实施方案B增加跨位置验证，降低误判率
3. 长期：考虑硬件升级或方案C的统计学习

### Q5: 对系统性能有什么影响？

**A**: 
- CPU开销：每次触发增加1次超时计算 + 1次队列遍历（仅在丢失时）
- 内存开销：无额外常驻内存（不维护全局状态）
- 延迟影响：正常情况无影响；丢失时增加递归处理延迟（<10ms）
- 结论：**性能影响极小，可忽略**

---

## 十、总结

### 推荐方案：方案A - 增强型超时检测

**选择理由**：
1. ✅ 最小改动，低风险
2. ✅ 基于现有机制，易于理解和维护
3. ✅ 可立即部署，快速见效
4. ✅ 后续可扩展到方案B或方案C

**核心思想**：
```
正常超时（2000ms）  → 包裹延迟，但可能还在线体上 → 执行Fallback
严重超时（5000ms）  → 包裹大概率丢失          → 清理队列并警告
```

**期望效果**：
- 检测率：>95%（单个包裹丢失场景）
- 误判率：<5%（合理配置下）
- 响应时间：5秒内（严重超时阈值）

**下一步行动**：
1. 审查本方案
2. 决定是否实施
3. 如批准，按照第八章实施计划执行

---

**文档版本**: 1.0  
**最后更新**: 2025-12-13  
**作者**: GitHub Copilot  
**审批状态**: 待审批
