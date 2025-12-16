# 包裹超时处理机制说明文档

> **文档类型**: 技术说明文档  
> **创建日期**: 2025-12-15  
> **维护团队**: ZakYip Development Team

---

## 一、问题定义

**问题**：如果创建包裹后一直接收不到上游返回的目标格口信息，系统会如何处理？

**答案**：系统已实现完整的超时兜底机制，会在超时后自动将包裹路由到异常格口，并通知上游系统。

---

## 二、超时处理完整流程

### 2.1 流程概览

```
┌─────────────────────────────────────────────────────────────────────┐
│  步骤1：包裹创建与上游通知（Parcel-First 原则）                      │
├─────────────────────────────────────────────────────────────────────┤
│  1. 创建本地包裹实体（CreateParcelEntityAsync）                      │
│  2. 向上游发送包裹检测通知（Fire-and-Forget）                       │
│  3. 不等待上游响应，继续执行后续逻辑                                │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│  步骤2：格口分配等待（仅Formal模式）                                 │
├─────────────────────────────────────────────────────────────────────┤
│  1. 注册TaskCompletionSource等待上游事件                            │
│  2. 计算动态超时时间（基于输送线物理参数）                          │
│  3. 等待上游ChuteAssigned事件或超时                                 │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
              ┌──────────────┴──────────────┐
              │                             │
         ✅ 正常收到                    ❌ 超时/取消
              │                             │
              ↓                             ↓
┌─────────────────────────┐  ┌──────────────────────────────────────┐
│  步骤3A：正常路由        │  │  步骤3B：超时兜底处理                │
├─────────────────────────┤  ├──────────────────────────────────────┤
│  1. 使用上游分配的格口ID │  │  1. 记录警告日志                     │
│  2. TargetChuteId =      │  │  2. 写入超时追踪事件                 │
│     上游分配的格口       │  │  3. 立即通知上游（FinalStatus=Timeout）│
│                          │  │  4. TargetChuteId = 异常格口ID（999）│
└─────────────────────────┘  └──────────────────────────────────────┘
              │                             │
              └──────────────┬──────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│  步骤4：拓扑路径生成与队列任务创建（正常和超时都会执行）             │
├─────────────────────────────────────────────────────────────────────┤
│  1. 调用 _pathGenerator.GenerateQueueTasks(parcelId, TargetChuteId) │
│  2. 基于拓扑配置计算到目标格口（正常格口或异常格口）的摆轮路径      │
│  3. 为路径上的每个摆轮节点创建队列任务                              │
│  4. 任务包含：ParcelId、DiverterAction、PositionIndex、超时容差     │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│  步骤5：加入Position Index队列（正常和超时都会执行）                 │
├─────────────────────────────────────────────────────────────────────┤
│  1. 遍历所有队列任务                                                │
│  2. 根据任务的PositionIndex加入对应队列（FIFO顺序）                 │
│  3. 记录目标格口ID到_parcelTargetChutes                             │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│  步骤6：IO触发执行（基于CORE_ROUTING_LOGIC.md机制）                 │
├─────────────────────────────────────────────────────────────────────┤
│  1. 传感器IO点触发                                                  │
│  2. 从positionIndex队列取出任务（FIFO）                             │
│  3. 执行摆轮动作（Left/Right/Straight）                            │
│  4. 包裹到达目标格口（正常格口或异常格口999）                       │
└─────────────────────────────────────────────────────────────────────┘
```

**关键说明**：
- ✅ **步骤4-6对超时包裹和正常包裹完全一致**
- ✅ 超时包裹会生成到异常格口的完整拓扑路径
- ✅ 超时包裹的任务会加入positionIndex队列
- ✅ 超时包裹通过相同的IO触发机制执行摆轮动作
- 🔸 唯一区别：TargetChuteId不同（异常格口 vs 正常格口）

---

## 三、源码实现分析

### 3.1 核心方法：GetChuteFromUpstreamAsync

**位置**：`src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`

**方法签名**：
```csharp
private async Task<long> GetChuteFromUpstreamAsync(long parcelId, SystemConfiguration systemConfig)
```

**实现逻辑**：
```csharp
// 1. 创建TaskCompletionSource等待上游事件
var tcs = new TaskCompletionSource<long>();
_pendingAssignments[parcelId] = tcs;

// 2. 计算动态超时时间
var timeoutSeconds = CalculateChuteAssignmentTimeout(systemConfig);
var timeoutMs = (int)(timeoutSeconds * 1000);

// 3. 等待上游推送格口分配（带超时）
using var cts = new CancellationTokenSource(timeoutMs);
var targetChuteId = await tcs.Task.WaitAsync(cts.Token);

// 4. 超时捕获与兜底处理
catch (TimeoutException)
{
    return await HandleRoutingTimeoutAsync(parcelId, systemConfig, exceptionChuteId, "Timeout");
}
catch (OperationCanceledException)
{
    return await HandleRoutingTimeoutAsync(parcelId, systemConfig, exceptionChuteId, "Cancelled");
}
finally
{
    _pendingAssignments.TryRemove(parcelId, out _);
}
```

### 3.2 超时处理方法：HandleRoutingTimeoutAsync

**位置**：同上

**方法签名**：
```csharp
private async Task<long> HandleRoutingTimeoutAsync(
    long parcelId, 
    SystemConfiguration systemConfig, 
    long exceptionChuteId, 
    string status)
```

**处理步骤**：
1. **记录警告日志**：
   ```csharp
   _logger.LogWarning(
       "【路由超时兜底】包裹 {ParcelId} 等待格口分配超时（超时限制：{TimeoutMs}ms），已分拣至异常口。" +
       "异常口ChuteId={ExceptionChuteId}, " +
       "发生时间={OccurredAt:yyyy-MM-dd HH:mm:ss.fff}",
       parcelId, timeoutMs, exceptionChuteId, _clock.LocalNow);
   ```

2. **写入追踪事件**：
   ```csharp
   await WriteTraceAsync(new ParcelTraceEventArgs
   {
       ItemId = parcelId,
       Stage = "RoutingTimeout",
       Source = "Upstream",
       Details = $"TimeoutMs={timeoutMs}, Status={status}, RoutedToException={exceptionChuteId}"
   });
   ```

3. **立即通知上游系统**：
   ```csharp
   await NotifyUpstreamSortingCompletedAsync(
       parcelId,
       exceptionChuteId,
       isSuccess: false,
       failureReason: $"AssignmentTimeout: {status}",
       finalStatus: ParcelFinalStatus.Timeout);
   ```

4. **返回异常格口ID**：
   ```csharp
   return exceptionChuteId;
   ```

**重要说明**：`HandleRoutingTimeoutAsync` 返回异常格口ID后，流程会继续执行：
- **步骤4**：调用 `_pathGenerator.GenerateQueueTasks(parcelId, exceptionChuteId, _clock.LocalNow)` 生成到异常格口的拓扑路径任务
- **步骤5**：将生成的任务加入对应的 `positionIndex` 队列
- **步骤6**：等待IO触发，从队列取出任务，执行摆轮动作，包裹最终到达异常格口

**因此，超时后的包裹与正常包裹一样，都会生成完整的拓扑路径并加入位置索引队列。唯一的区别是目标格口ID不同（异常格口 vs 正常格口）。**

### 3.3 超时时间计算：CalculateChuteAssignmentTimeout

**计算公式**：

**动态计算**（有超时计算器时）：
```
超时时间（秒） = 入口到首个决策点距离（mm） / 线速（mm/s） × SafetyFactor
```

**降级固定超时**（无法动态计算时）：
```
超时时间（秒） = FallbackTimeoutSeconds（默认5秒）
```

**配置参数**：
```csharp
public class ChuteAssignmentTimeoutOptions
{
    /// <summary>安全系数（0.1 ~ 1.0，默认：0.9）</summary>
    public decimal SafetyFactor { get; set; } = 0.9m;

    /// <summary>降级超时（秒，默认：5）</summary>
    public decimal FallbackTimeoutSeconds { get; set; } = 5m;

    /// <summary>丢失判定安全系数（1.0 ~ 3.0，默认：1.5）</summary>
    public decimal LostDetectionSafetyFactor { get; set; } = 1.5m;
}
```

**实现代码**：
```csharp
private decimal CalculateChuteAssignmentTimeout(SystemConfiguration systemConfig)
{
    // 如果有超时计算器，使用动态计算
    if (_timeoutCalculator != null)
    {
        var context = new ChuteAssignmentTimeoutContext(
            LineId: 1,
            SafetyFactor: systemConfig.ChuteAssignmentTimeout?.SafetyFactor ?? 0.9m
        );
        
        return _timeoutCalculator.CalculateTimeoutSeconds(context);
    }
    
    // 降级：使用配置的固定超时时间
    return systemConfig.ChuteAssignmentTimeout?.FallbackTimeoutSeconds ?? _options.FallbackTimeoutSeconds;
}
```

---

## 四、上游通信协议

### 4.1 包裹检测通知（Fire-and-Forget）

**时机**：包裹创建后立即发送（不等待响应）

**消息结构**：`ParcelDetectionNotification`
```json
{
  "ParcelId": 1701446263000,
  "DetectionTime": "2024-12-01T18:57:43+08:00",
  "Metadata": {
    "SensorId": "Sensor001",
    "LineId": "Line01"
  }
}
```

### 4.2 格口分配通知（上游主动推送）

**时机**：上游系统匹配格口后主动推送

**消息结构**：`ChuteAssignmentNotification`
```json
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

**事件处理**：`OnChuteAssignmentReceived`
```csharp
private void OnChuteAssignmentReceived(object? sender, ChuteAssignmentEventArgs e)
{
    // Invariant检查：上游响应必须匹配已存在的本地包裹
    if (!_createdParcels.ContainsKey(e.ParcelId))
    {
        _logger.LogError("[Invariant Violation] 收到未知包裹的路由响应，响应已丢弃");
        return;
    }

    if (_pendingAssignments.TryGetValue(e.ParcelId, out var tcs))
    {
        // 正常情况：在超时前收到响应
        tcs.TrySetResult(e.ChuteId);
        _pendingAssignments.TryRemove(e.ParcelId, out _);
    }
    else
    {
        // 迟到的响应：包裹已经超时并被路由到异常口
        _logger.LogInformation("【迟到路由响应】包裹已因超时被路由到异常口，不再改变去向");
    }
}
```

### 4.3 分拣完成通知（超时后立即发送）

**时机**：超时处理后立即发送

**消息结构**：`SortingCompletedNotification`
```json
{
  "ParcelId": 1701446263000,
  "ActualChuteId": 999,
  "CompletedAt": "2024-12-01T18:57:45.000+08:00",
  "IsSuccess": false,
  "FinalStatus": "Timeout",
  "FailureReason": "AssignmentTimeout: Timeout"
}
```

**FinalStatus枚举**：
- `Success`：成功分拣到目标格口
- `Timeout`：**分配超时或落格超时，路由到异常格口**
- `Lost`：包裹丢失（超过最大存活时间）
- `ExecutionError`：执行错误

---

## 五、典型场景示例

### 5.1 正常场景（有上游响应）

```
时刻 T0（20:52:34.123）：入口传感器触发
↓
[生命周期-创建] P1234 入口传感器1触发 T=20:52:34.123
[Parcel-First] 本地创建包裹: ParcelId=1234, CreatedAt=20:52:34.123
[Parcel-First] 发送上游包裹检测通知: ParcelId=1234, SentAt=20:52:34.125
↓
时刻 T1（20:52:34.500）：上游响应到达（延迟375ms）
↓
[生命周期-路由] P1234 目标格口=101（上游分配）
[生命周期-入队] P1234 3任务入队 目标C101 耗时400ms
↓
时刻 T2（20:52:36.000）：第一个摆轮传感器触发
↓
[队列执行] positionIndex1 触发，取出任务 P1234
[摆轮动作] 摆轮D1 执行 Left 动作
↓
时刻 T3（20:52:38.000）：包裹到达格口101
↓
[生命周期-完成] P1234 成功落格 C101
```

### 5.2 超时场景（无上游响应）

```
时刻 T0（20:52:34.123）：入口传感器触发
↓
[生命周期-创建] P1234 入口传感器1触发 T=20:52:34.123
[Parcel-First] 本地创建包裹: ParcelId=1234, CreatedAt=20:52:34.123
[Parcel-First] 发送上游包裹检测通知: ParcelId=1234, SentAt=20:52:34.125
↓
时刻 T1（20:52:38.625）：等待超时（4500ms后）
↓
【路由超时兜底】包裹 1234 等待格口分配超时（超时限制：4500ms）
  异常口ChuteId=999, 发生时间=2024-12-15 20:52:38.625
[生命周期-路由] P1234 目标格口=999（异常格口）

**拓扑路径生成**（基于拓扑配置）：
[队列任务生成] 开始为包裹 1234 生成到格口 999 的队列任务
  拓扑路径: D1(Straight) -> D2(Straight) -> D3(Right) -> 格口999
  生成任务: 
    - PositionIndex1: {ParcelId=1234, Action=Straight, DiverterId=D1}
    - PositionIndex2: {ParcelId=1234, Action=Straight, DiverterId=D2}
    - PositionIndex3: {ParcelId=1234, Action=Right, DiverterId=D3}

[生命周期-入队] P1234 3任务入队 目标C999 耗时4502ms
↓
立即发送超时通知到上游：
{
  "ParcelId": 1234,
  "ActualChuteId": 999,
  "IsSuccess": false,
  "FinalStatus": "Timeout",
  "FailureReason": "AssignmentTimeout: Timeout"
}
↓
时刻 T2（20:52:40.000）：第一个摆轮传感器触发
↓
[队列执行] positionIndex1 触发，取出任务 P1234
[摆轮动作] 摆轮D1 执行 Straight 动作（导向异常格口）
↓
时刻 T3（20:52:44.000）：包裹到达异常格口999
↓
[生命周期-完成] P1234 路由到异常格口 C999
```

### 5.3 迟到响应场景（超时后上游才响应）

```
时刻 T0（20:52:34.123）：入口传感器触发
↓
（省略包裹创建和上游通知步骤）
↓
时刻 T1（20:52:38.625）：等待超时（4500ms后）
↓
【路由超时兜底】包裹 1234 等待格口分配超时
[生命周期-路由] P1234 目标格口=999（异常格口）
（超时通知已发送到上游）
↓
时刻 T2（20:52:40.000）：上游迟到的响应到达（延迟5877ms）
↓
【迟到路由响应】收到包裹 1234 的格口分配 (ChuteId=101)，
  但该包裹已因超时被路由到异常口，不再改变去向。
  接收时间=2024-12-15 20:52:40.000
↓
（继续按异常格口999执行，上游响应被忽略）
↓
时刻 T3（20:52:44.000）：包裹到达异常格口999
```

---

## 六、配置示例

### 6.1 超时配置（appsettings.json）

```json
{
  "ChuteAssignmentTimeout": {
    "SafetyFactor": 0.9,
    "FallbackTimeoutSeconds": 5,
    "LostDetectionSafetyFactor": 1.5
  },
  "SystemConfiguration": {
    "ExceptionChuteId": 999,
    "SortingMode": "Formal"
  }
}
```

### 6.2 输送线物理参数（用于动态超时计算）

```json
{
  "ChutePathTopology": {
    "EntryToFirstDiverterDistanceMm": 5000,
    "LineSpeedMmPerSecond": 1000
  }
}
```

**动态超时计算**：
```
超时时间 = 5000mm / 1000mm/s × 0.9 = 4.5秒
```

---

## 七、监控与故障排查

### 7.1 关键日志关键字

**正常流程**：
- `[生命周期-创建]`：包裹创建
- `[Parcel-First]`：Parcel-First原则相关日志
- `[生命周期-路由]`：格口分配完成
- `[生命周期-入队]`：任务入队完成
- `[队列执行]`：IO触发执行

**超时流程**：
- `【路由超时兜底】`：超时兜底处理
- `RoutingTimeout`：追踪事件阶段
- `AssignmentTimeout`：失败原因类型

**异常情况**：
- `[Invariant Violation]`：不变式违反
- `【迟到路由响应】`：上游响应迟到

### 7.2 诊断步骤

**步骤1：检查上游连接状态**
```bash
# 查看日志中的上游连接状态
grep "IsConnected" /path/to/logs/*.log
```

**步骤2：检查超时配置**
```bash
# 查看超时限制日志
grep "超时限制" /path/to/logs/*.log
```

**预期输出**：
```
【路由超时兜底】包裹 1234 等待格口分配超时（超时限制：4500ms）
```

**步骤3：检查异常格口配置**
```bash
# 查看异常格口ID
grep "异常口ChuteId" /path/to/logs/*.log
```

**预期输出**：
```
异常口ChuteId=999
```

**步骤4：验证包裹最终状态**
```bash
# 查看分拣完成通知
grep "SortingCompletedNotification" /path/to/logs/*.log
```

**预期输出**：
```json
{
  "ParcelId": 1234,
  "ActualChuteId": 999,
  "FinalStatus": "Timeout"
}
```

---

## 八、常见问题FAQ

### Q1：超时时间如何确定？

**A**：超时时间有两种计算方式：

1. **动态计算**（推荐）：
   - 基于输送线物理参数（入口到首个决策点距离、线速）
   - 公式：`超时时间 = 距离 / 线速 × SafetyFactor`
   - 例如：5000mm / 1000mm/s × 0.9 = 4.5秒

2. **降级固定超时**（兜底）：
   - 当无法获取物理参数时使用
   - 默认值：5秒
   - 配置项：`FallbackTimeoutSeconds`

### Q2：超时后包裹会去哪里？

**A**：超时后包裹会被路由到异常格口：
- 异常格口ID由系统配置决定（`ExceptionChuteId`）
- 默认值：999
- 可通过API或配置文件修改

### Q3：超时后还会收到上游响应吗？

**A**：可能会收到迟到的上游响应：
- 系统会记录日志：`【迟到路由响应】`
- 但不会改变包裹去向（已经路由到异常格口）
- 上游响应会被忽略

### Q4：超时是否会通知上游系统？

**A**：是的，超时后会立即通知上游：
- 发送`SortingCompletedNotification`
- `FinalStatus = Timeout`
- `FailureReason = "AssignmentTimeout: Timeout"`
- 让上游系统知道包裹已经被路由到异常格口

### Q5：如何区分"超时"和"丢失"？

**A**：两者有明确区分：

**Timeout（超时）**：
- 包裹仍在输送线上
- 可以导向异常格口
- `ActualChuteId = 异常格口ID`（如999）
- `FinalStatus = Timeout`

**Lost（丢失）**：
- 包裹已不在输送线上
- 无法导向异常格口
- `ActualChuteId = 0`
- `FinalStatus = Lost`
- 从缓存中清除包裹记录

### Q6：超时分配目标格口是否也会生成拓扑路径和加入position index队列？

**A**：✅ **是的，超时包裹与正常包裹的处理流程完全一致**

**超时后的处理流程**：
1. `HandleRoutingTimeoutAsync` 返回异常格口ID（默认999）
2. `ProcessParcelAsync` 继续执行：
   ```csharp
   // 步骤4: 使用异常格口ID生成拓扑路径任务
   var queueTasks = _pathGenerator.GenerateQueueTasks(
       parcelId, 
       exceptionChuteId,  // 使用异常格口ID
       _clock.LocalNow);
   
   // 步骤5: 将任务加入positionIndex队列
   foreach (var task in queueTasks)
   {
       _queueManager.EnqueueTask(task.PositionIndex, task);
   }
   ```
3. IO触发时从队列取出任务，执行摆轮动作
4. 包裹最终到达异常格口

**关键点**：
- ✅ 生成拓扑路径（基于拓扑配置计算到异常格口的路径）
- ✅ 创建队列任务（每个positionIndex对应的摆轮动作）
- ✅ 加入positionIndex队列（FIFO顺序）
- ✅ IO触发执行（与正常包裹相同的机制）

**唯一区别**：目标格口ID不同（异常格口999 vs 上游分配的正常格口）

### Q7：如何测试超时处理机制？

**A**：测试步骤：

1. **关闭上游RuleEngine**（模拟无响应）
2. **触发包裹创建**：
   ```bash
   curl -X POST http://localhost:5000/api/simulation/parcels \
     -H "Content-Type: application/json" \
     -d '{"parcelId": 1234, "sensorId": 1}'
   ```
3. **观察日志输出**：
   ```bash
   tail -f /path/to/logs/sorting.log | grep -E "超时兜底|RoutingTimeout"
   ```
4. **验证包裹路由到异常格口**：
   ```bash
   curl http://localhost:5000/api/parcels/1234/status
   ```
5. **预期结果**：
   - 日志显示：`【路由超时兜底】`
   - 包裹状态：`FinalStatus = Timeout`
   - 实际格口：`ActualChuteId = 999`

---

## 九、相关文档

### 9.1 核心文档

- **[CORE_ROUTING_LOGIC.md](./CORE_ROUTING_LOGIC.md)** - 核心路由逻辑与队列机制
- **[UPSTREAM_CONNECTION_GUIDE.md](./guides/UPSTREAM_CONNECTION_GUIDE.md)** - 上游通信协议说明
- **[ARCHITECTURE_PRINCIPLES.md](./ARCHITECTURE_PRINCIPLES.md)** - 架构原则

### 9.2 代码位置

- **超时处理核心逻辑**：
  - `src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`
    - `GetChuteFromUpstreamAsync()` - 等待上游响应
    - `HandleRoutingTimeoutAsync()` - 超时兜底处理
    - `CalculateChuteAssignmentTimeout()` - 超时时间计算
    - `OnChuteAssignmentReceived()` - 上游事件处理

- **超时配置模型**：
  - `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Models/ChuteAssignmentTimeoutOptions.cs`

- **包裹最终状态枚举**：
  - `src/Core/ZakYip.WheelDiverterSorter.Core/Enums/Parcel/ParcelFinalStatus.cs`

- **上游通信协议**：
  - `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/ParcelDetectionNotification.cs`
  - `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/ChuteAssignmentNotification.cs`
  - `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/SortingCompletedNotification.cs`

---

## 十、总结

### 10.1 核心要点

✅ **系统已完整实现超时处理机制**，不需要额外开发

✅ **超时兜底机制完善**：
1. 动态计算超时时间（基于物理参数）
2. 超时后自动路由到异常格口
3. 立即通知上游系统（FinalStatus=Timeout）
4. 完整的日志追踪

✅ **符合核心路由逻辑**（`CORE_ROUTING_LOGIC.md`）：
- 遵循Parcel-First原则
- 基于positionIndex队列机制
- IO触发为操作起点

✅ **上游协议规范**（`UPSTREAM_CONNECTION_GUIDE.md`）：
- Fire-and-Forget通信模式
- 异步事件推送
- 完整的超时通知

### 10.2 验证建议

建议在以下场景进行验证测试：

1. **正常场景**：上游响应正常（延迟<1秒）
2. **慢响应场景**：上游响应较慢（延迟2-4秒）
3. **超时场景**：上游无响应（延迟>5秒）
4. **迟到响应场景**：超时后上游才响应

---

**文档版本**: 1.0  
**最后更新**: 2025-12-15  
**维护团队**: ZakYip Development Team
