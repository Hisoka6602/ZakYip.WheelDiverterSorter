# 防抖机制行为说明

> **文档类型**: 技术说明文档  
> **创建日期**: 2025-12-26  
> **适用版本**: v1.0+

---

## 问题说明

**问题**：假设当前防抖时间是200ms，在触发之后200ms，传感器仍处于高电平状态，系统是否会再次触发?

**答案**：**不会**。系统采用**边沿触发 + 防抖**机制，信号保持高电平时不会重复触发。这是正确的设计行为。

---

## 防抖机制详解

### 1. 触发模式：边沿触发 vs 电平触发

系统采用**边沿触发**模式，而非电平触发模式：

| 触发模式 | 行为说明 | 系统采用情况 |
|---------|---------|-------------|
| **边沿触发** | 仅在信号状态**变化时**触发（LOW → HIGH 或 HIGH → LOW） | ✅ **当前系统** |
| 电平触发 | 只要信号保持特定电平（如 HIGH）就持续触发 | ❌ 不采用 |

### 2. 实现机制

#### 传感器层（LeadshineSensor.cs）

传感器层只检测**状态变化**：

```csharp
// Line 158: LeadshineSensor.cs
// 检测状态变化
if (currentState != _lastState) {
    // 只有在状态发生变化时才触发事件
    // 如果信号保持 HIGH，currentState == _lastState，不会触发
    _lastState = currentState;
    OnSensorTriggered(sensorEvent);
}
```

**关键点**：
- 传感器首次检测到 HIGH（上升沿 LOW → HIGH）时触发一次
- 如果信号保持 HIGH，`currentState == _lastState`，不会再次触发
- 只有当信号变为 LOW 再变回 HIGH 时，才会产生新的上升沿触发

#### 防抖层（ParcelDetectionService.cs）

防抖层在收到传感器事件后，进一步过滤重复触发：

```csharp
// Line 432-455: ParcelDetectionService.cs
private (bool IsDuplicate, double TimeSinceLastTriggerMs) CheckForDuplicateTrigger(SensorEvent sensorEvent)
{
    if (!_lastTriggerTimes.TryGetValue(sensorEvent.SensorId, out var lastTime))
    {
        return (false, 0);  // 首次触发
    }

    var timeSinceLastTrigger = sensorEvent.TriggerTime - lastTime;
    var timeSinceLastTriggerMs = timeSinceLastTrigger.TotalMilliseconds;
    var deduplicationWindowMs = GetDeduplicationWindowForSensor(sensorEvent.SensorId);

    if (timeSinceLastTriggerMs < deduplicationWindowMs)
    {
        return (true, timeSinceLastTriggerMs);  // 重复触发，被过滤
    }

    return (false, timeSinceLastTriggerMs);  // 允许触发
}
```

**防抖策略**：
1. 记录每个传感器的最后触发时间
2. 如果新触发距离上次触发 **< 防抖时间窗口**（默认 400ms），判定为重复触发并忽略
3. 如果新触发距离上次触发 **≥ 防抖时间窗口**，允许触发

### 3. 完整触发流程

```
包裹经过传感器的完整流程：

时间 | 传感器状态 | 传感器层行为 | 防抖层行为 | 结果
-----|----------|------------|-----------|------
0ms  | LOW      | 无事件     | -          | 无触发
10ms | HIGH ↑   | 触发事件   | 首次触发   | ✅ 创建包裹
20ms | HIGH     | 无事件     | -          | 无触发（状态未变化）
50ms | HIGH     | 无事件     | -          | 无触发（状态未变化）
100ms| HIGH     | 无事件     | -          | 无触发（状态未变化）
200ms| HIGH     | 无事件     | -          | 无触发（状态未变化）
300ms| HIGH     | 无事件     | -          | 无触发（状态未变化）
400ms| HIGH     | 无事件     | -          | 无触发（状态未变化）
500ms| LOW  ↓   | 触发事件   | （不处理下降沿）| 无触发
600ms| HIGH ↑   | 触发事件   | 超过防抖窗口 | ✅ 创建新包裹
```

**关键说明**：
- 即使防抖窗口（200ms 或 400ms）过期，如果信号保持 HIGH，传感器层不会产生新事件
- 只有在信号产生新的上升沿时（LOW → HIGH），才会触发新的包裹检测

---

## 为什么这是正确的行为？

### 1. 符合工业控制最佳实践

边沿触发 + 防抖是工业控制系统的标准做法：

✅ **优点**：
- 每个包裹只触发一次，避免重复检测
- 防止物理抖动导致的误触发
- 与实际业务逻辑一致（包裹"进入检测区域"时触发，而非"停留在检测区域"时持续触发）

❌ **如果采用电平触发**：
- 一个包裹可能被检测为多个包裹
- 包裹停留在传感器上方时会持续触发
- 无法区分"一个长包裹"和"多个连续包裹"

### 2. 防止误触发场景

#### 场景1：包裹停留在传感器上方
```
包裹卡在传感器上方，信号保持 HIGH：
- 边沿触发：只触发一次 ✅
- 电平触发：持续触发，产生大量虚假包裹 ❌
```

#### 场景2：传感器物理抖动
```
传感器抖动产生多次 HIGH/LOW 切换（100ms 内）：
- 边沿触发 + 防抖：只触发一次，后续抖动被过滤 ✅
- 纯边沿触发（无防抖）：每次抖动都触发，产生多个包裹 ❌
```

#### 场景3：包裹边缘触发
```
包裹边缘经过传感器，可能产生短暂的 HIGH → LOW → HIGH 切换：
- 边沿触发 + 防抖：第一次上升沿触发，后续切换在防抖窗口内被过滤 ✅
- 纯边沿触发（无防抖）：多次触发，产生多个包裹 ❌
```

---

## 配置说明

### 防抖时间配置

系统支持两级防抖配置：

#### 1. 传感器级别防抖

**位置**：`Core/LineModel/Configuration/Models/SensorConfiguration.cs`

**配置项**：`SensorIoEntry.DeduplicationWindowMs`

**默认值**：400ms

**建议范围**：100ms - 5000ms

```json
{
  "Sensors": [
    {
      "SensorId": 1,
      "SensorName": "创建包裹感应IO",
      "IoType": "ParcelCreation",
      "BitNumber": 0,
      "DeduplicationWindowMs": 400,  // 防抖时间
      "IsEnabled": true
    }
  ]
}
```

#### 2. 面板按钮防抖

**位置**：`Core/LineModel/Configuration/Models/PanelConfiguration.cs`

**配置项**：`PanelConfiguration.DebounceMs`

**默认值**：50ms

**建议范围**：10ms - 5000ms

```json
{
  "PollingIntervalMs": 100,
  "DebounceMs": 50  // 面板按钮防抖时间
}
```

### 防抖时间选择指南

| 场景 | 建议防抖时间 | 说明 |
|------|------------|------|
| 高速分拣（包裹间隔 < 1秒） | 100-400ms | 快速响应，过滤传感器抖动 |
| 标准速度（包裹间隔 1-3秒） | 400-1000ms | 平衡防抖和灵敏度（推荐） |
| 低速场景（包裹间隔 > 3秒） | 1000-2000ms | 充分过滤抖动，适用于机械抖动明显的传感器 |
| 面板按钮 | 50-100ms | 防止误触，保持良好操作体验 |

---

## 测试验证

### 测试用例

系统包含完整的测试用例验证防抖行为：

**测试文件**：`tests/ZakYip.WheelDiverterSorter.Ingress.Tests/DebounceBehaviorTests.cs`

#### 测试1：信号保持高电平不会重复触发
```csharp
[Fact]
public async Task ContinuousHighSignal_ShouldNotRetrigger_AfterDebounceWindow()
{
    // 验证：信号保持 HIGH 时，即使超过防抖窗口也不会重复触发
    // 因为传感器层只检测状态变化，没有状态变化就不会产生新事件
}
```

#### 测试2：多个上升沿在防抖窗口外正常触发
```csharp
[Fact]
public async Task EdgeTriggering_MultipleRisingEdges_OutsideDebounceWindow_ShouldTriggerMultipleTimes()
{
    // 验证：多个包裹依次经过（每个产生新的上升沿），且间隔超过防抖窗口
    // 每个包裹都能正常触发
}
```

#### 测试3：防抖窗口内的重复触发被过滤
```csharp
[Fact]
public async Task EdgeTriggering_RisingEdgeWithinDebounceWindow_ShouldNotRetrigger()
{
    // 验证：防抖窗口内的多次上升沿（传感器抖动）被正确过滤
    // 只有第一次上升沿有效
}
```

#### 测试4：防抖窗口边界行为
```csharp
[Fact]
public async Task DebounceWindow_ExactlyAtBoundary_ShouldBeFiltered()
{
    // 验证：恰好在防抖窗口边界（200ms）的触发行为
    // 当前实现：timeSinceLastTriggerMs < deduplicationWindowMs
    // 因此 200ms 边界上的触发应该被允许（< 而非 <=）
}
```

### 运行测试

```bash
cd /path/to/ZakYip.WheelDiverterSorter
dotnet test tests/ZakYip.WheelDiverterSorter.Ingress.Tests/ \
    --filter "FullyQualifiedName~DebounceBehaviorTests"
```

**预期结果**：所有测试通过 ✅

---

## 常见问题 (FAQ)

### Q1：为什么防抖窗口过期后，信号还是HIGH，不会重复触发？

**A**：因为系统采用**边沿触发**模式，只有在信号状态**发生变化**时（LOW → HIGH）才会触发。信号保持 HIGH 没有状态变化，传感器层不会产生新的事件，防抖层也就收不到新事件。

这是正确的设计，防止一个包裹被重复检测为多个包裹。

### Q2：如果需要检测"包裹停留在传感器上方"的时长，应该怎么做？

**A**：这需要额外的逻辑，不是防抖机制的职责：

1. **方案1**：在传感器层记录下降沿时间，计算 HIGH 持续时长
2. **方案2**：使用独立的计时器，在上升沿启动，在下降沿停止
3. **方案3**：在业务层添加包裹停留时长监控

但这与防抖无关，防抖只负责过滤短时间内的重复触发。

### Q3：我的包裹间隔只有 150ms，会不会被防抖过滤掉第二个包裹？

**A**：不会。关键在于理解触发时机：

```
场景：两个包裹间隔 150ms

时间  | 包裹1 | 包裹2 | 传感器状态 | 系统行为
------|-------|-------|-----------|----------
0ms   | ↓进入 | -     | LOW → HIGH| ✅ 触发包裹1
50ms  | 经过  | -     | HIGH      | 无事件
100ms | 经过  | -     | HIGH      | 无事件
120ms | ↓离开 | -     | HIGH → LOW| 无事件（下降沿不处理）
150ms | -     | ↓进入 | LOW → HIGH| ❌ 被防抖过滤（< 400ms）
```

在这种情况下，第二个包裹**确实会被防抖过滤**，因为两次上升沿间隔只有 150ms。

**解决方案**：
1. 减小防抖时间（如改为 100ms），但要注意可能增加误触发
2. 调整传送带速度，增大包裹间隔
3. 使用更精确的传感器，减少抖动，从而可以使用更小的防抖时间

### Q4：防抖时间是从"上升沿"开始计时，还是从"信号变为HIGH"开始计时？

**A**：防抖时间从**上升沿**（LOW → HIGH）开始计时。

具体来说：
- 第一次上升沿触发时，记录触发时间 `_lastTriggerTimes[sensorId] = now`
- 后续上升沿到来时，计算距离上次触发的时间间隔
- 如果间隔 < 防抖时间，判定为重复触发并忽略
- 如果间隔 ≥ 防抖时间，允许触发并更新 `_lastTriggerTimes`

信号保持 HIGH 的时间**不计入**防抖时间，因为没有新的上升沿事件产生。

---

## 相关文档

- **系统能力分析**：[SYSTEM_CAPABILITIES_ANALYSIS.md](./SYSTEM_CAPABILITIES_ANALYSIS.md) - 第一章节详细说明防抖能力
- **包裹检测服务**：`src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Services/ParcelDetectionService.cs`
- **传感器实现**：`src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Sensors/LeadshineSensor.cs`
- **防抖测试**：`tests/ZakYip.WheelDiverterSorter.Ingress.Tests/DebounceBehaviorTests.cs`
- **传感器配置**：[guides/SENSOR_IO_POLLING_CONFIGURATION.md](./guides/SENSOR_IO_POLLING_CONFIGURATION.md)

---

**文档版本**: 1.0  
**最后更新**: 2025-12-26  
**维护团队**: ZakYip Development Team
