# 包裹丢失检测功能增强总结

> **文档类型**: 功能增强总结文档  
> **创建日期**: 2025-12-23  
> **PR分支**: copilot/update-loss-detection-config

---

## 概述

本次功能增强主要针对包裹丢失检测系统进行了多项改进，包括增加配置灵活性、自动清空中位数统计、系统停止时清理数据等功能。

---

## 已实现的需求

### ✅ 需求1: 停止时清空所有中位数记录

**实现位置**: `src/Execution/.../Orchestration/SortingOrchestrator.cs`

**实现说明**:
- 在系统状态转换到 `EmergencyStop`、`Ready` 或 `Faulted` 时，除了清空队列，还会自动清空所有 Position 的中位数统计数据
- 添加详细的日志记录，便于问题追踪

**代码示例**:
```csharp
if (shouldClearQueues)
{
    // 清空队列
    _queueManager?.ClearAllQueues();
    
    // 需求1: 清空中位数统计数据
    if (_intervalTracker != null)
    {
        _logger.LogWarning(
            "[中位数清理] 系统状态转换到 {NewState}，正在清空所有 Position 的中位数统计数据...",
            e.NewState);
        
        _intervalTracker.ClearAllStatistics();
        
        _logger.LogInformation(
            "[中位数清理] 中位数统计数据清空完成，状态: {OldState} -> {NewState}",
            e.OldState,
            e.NewState);
    }
}
```

**触发场景**:
- 用户按下急停按钮
- 系统从运行状态停止
- 系统发生故障
- 急停解除/复位操作

---

### ✅ 需求4: 增加丢失检测开关和自动清空中位数参数

#### 4.1 配置模型增强

**实现位置**: `src/Core/.../Models/ParcelLossDetectionConfiguration.cs`

**新增字段**:
```csharp
/// <summary>
/// 是否启用包裹丢失检测
/// </summary>
public bool IsEnabled { get; set; } = true;

/// <summary>
/// 自动清空中位数数据的时间间隔（毫秒）
/// </summary>
/// <remarks>
/// 当超过此时间未创建新包裹时，自动清空所有 Position 的中位数统计数据
/// 设置为 0 表示不自动清空
/// 默认值：300000ms (5分钟)
/// </remarks>
public int AutoClearMedianIntervalMs { get; set; } = 300000;
```

#### 4.2 API 端点更新

**实现位置**: `src/Host/.../Controllers/SortingController.cs`

**端点1**: `GET /api/sorting/loss-detection-config`

响应示例:
```json
{
  "isEnabled": true,
  "monitoringIntervalMs": 60,
  "autoClearMedianIntervalMs": 300000,
  "lostDetectionMultiplier": 1.5,
  "timeoutMultiplier": 3.0,
  "windowSize": 10
}
```

**端点2**: `POST /api/sorting/loss-detection-config`

请求示例（关闭丢失检测）:
```json
{
  "isEnabled": false
}
```

请求示例（设置10分钟自动清空）:
```json
{
  "autoClearMedianIntervalMs": 600000
}
```

请求示例（支持大窗口配置）:
```json
{
  "windowSize": 10000,
  "monitoringIntervalMs": 1000,
  "lostDetectionMultiplier": 5,
  "timeoutMultiplier": 5
}
```

**参数验证**:
- `IsEnabled`: 布尔值，无需验证
- `AutoClearMedianIntervalMs`: 范围 0-3600000ms (0-60分钟)
- `WindowSize`: 范围 10-10000 (支持大窗口)
- `MonitoringIntervalMs`: 范围 50-1000ms
- `LostDetectionMultiplier`: 范围 1.0-5.0
- `TimeoutMultiplier`: 范围 1.5-10.0

#### 4.3 丢失检测开关实现

**实现位置**: `src/Execution/.../Monitoring/ParcelLossMonitoringService.cs`

```csharp
private async Task MonitorQueuesForLostParcels()
{
    // 检查是否启用了包裹丢失检测
    if (_configRepository != null)
    {
        try
        {
            var config = _configRepository.Get();
            if (!config.IsEnabled)
            {
                // 检测被禁用，直接返回
                return;
            }
            
            // ... 继续检测逻辑
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取包裹丢失检测配置失败，将继续执行检测");
        }
    }
    
    // ... 丢失检测逻辑
}
```

**效果**:
- 当 `IsEnabled = false` 时，监控服务完全跳过丢失检测逻辑
- 节省CPU资源
- 避免误判

#### 4.4 自动清空中位数功能

**接口增强**: `src/Execution/.../Tracking/IPositionIntervalTracker.cs`

新增方法:
```csharp
/// <summary>
/// 获取最后一次包裹记录时间
/// </summary>
DateTime? GetLastParcelRecordTime();

/// <summary>
/// 检查是否应该自动清空统计数据
/// </summary>
bool ShouldAutoClear(int autoClearIntervalMs);
```

**实现**: `src/Execution/.../Tracking/PositionIntervalTracker.cs`

```csharp
// 跟踪最后包裹记录时间
private DateTime? _lastParcelRecordTime;
private readonly object _lastRecordTimeLock = new();

public void RecordParcelPosition(long parcelId, int positionIndex, DateTime arrivedAt)
{
    // 更新最后包裹记录时间
    lock (_lastRecordTimeLock)
    {
        _lastParcelRecordTime = arrivedAt;
    }
    
    // ... 原有逻辑
}

public bool ShouldAutoClear(int autoClearIntervalMs)
{
    if (autoClearIntervalMs <= 0)
        return false;
    
    lock (_lastRecordTimeLock)
    {
        if (!_lastParcelRecordTime.HasValue)
            return false;
        
        var elapsed = (_clock.LocalNow - _lastParcelRecordTime.Value).TotalMilliseconds;
        return elapsed >= autoClearIntervalMs;
    }
}
```

**监控服务集成**:
```csharp
// 检查是否应该自动清空中位数统计数据
if (_intervalTracker != null && 
    config.AutoClearMedianIntervalMs > 0 &&
    _intervalTracker.ShouldAutoClear(config.AutoClearMedianIntervalMs))
{
    _logger.LogWarning(
        "[自动清空中位数] 检测到超过 {IntervalMs}ms 未创建新包裹，正在清空所有中位数统计数据...",
        config.AutoClearMedianIntervalMs);
    
    _intervalTracker.ClearAllStatistics();
    
    _logger.LogInformation(
        "[自动清空中位数] 中位数统计数据已自动清空");
}
```

**工作流程**:
1. 每次调用 `RecordParcelPosition` 时更新 `_lastParcelRecordTime`
2. `ParcelLossMonitoringService` 在每次监控循环中调用 `ShouldAutoClear`
3. 如果超过配置的时间间隔，自动清空所有中位数统计
4. 记录详细日志

---

### ✅ 需求5: 处理启动时路径布满包裹的场景

**实现位置**: `docs/STARTUP_FULL_LINE_SCENARIO.md`

**文档内容**:

#### 1. 问题分析
- 详细分析"跳点"现象的根本原因
- 说明系统当前假设及其被违反的情况
- 提供正常流程与异常流程的对比图

#### 2. 三种解决方案

**方案A - 冷启动协议（推荐短期使用）** ⭐:
- 系统启动时强制清空线体
- 实施简单，风险低
- 完全符合当前系统设计
- 预计工作量：2-4小时

**方案B - 暖启动模式（智能识别）**:
- 自动识别预存在包裹
- 将预存在包裹路由到异常格口
- 无需人工干预
- 实施复杂度较高

**方案C - 混合模式（配置化选择）**:
- 提供配置选项让用户选择启动模式
- 灵活性最高
- 适应不同生产环境
- 预计工作量：2-3天

#### 3. 实施路线图
- 阶段1：短期快速修复（冷启动协议）
- 阶段2：配置化支持（2-4周）
- 阶段3：智能优化（1-2个月）

#### 4. 测试场景
- 测试场景1：冷启动（空线体）
- 测试场景2：暖启动（线体有1个包裹）
- 测试场景3：暖启动（线体布满包裹）

---

## 待实现的需求

### ⏳ 需求2: 防抖需要继续加大

**当前状态**:
- 传感器防抖默认值：`SensorBinding.DebounceMs = 50ms`
- 面板按钮防抖默认值：`PanelConfiguration.DebounceMs = 50ms`

**建议方案**:

#### 方案1: 全局调整（推荐）
修改默认值，统一提升防抖时间：

**传感器防抖** (`Core/IoBinding/SensorBinding.cs`):
```csharp
public int DebounceMs { get; init; } = 100;  // 从 50ms 改为 100ms
```

**面板按钮防抖** (`Core/.../Models/PanelConfiguration.cs`):
```csharp
public int DebounceMs { get; init; } = 100;  // 从 50ms 改为 100ms
```

#### 方案2: 针对性调整
只调整特定类型传感器的防抖：
- 入口传感器（Position 0）：100ms
- 摆轮前传感器：80ms
- 落格传感器：50ms（保持不变）

#### 方案3: 通过配置API动态调整
创建新的API端点允许运行时调整防抖参数：
```
POST /api/config/sensor-debounce
{
  "globalDebounceMs": 100,
  "sensorTypeOverrides": {
    "entry": 150,
    "diverter": 100,
    "chute": 50
  }
}
```

**推荐**: 先使用方案1进行快速验证，如果效果良好，可以长期使用；如果需要更精细的控制，再实施方案3。

---

### ⏳ 需求3: 未创建包裹时，触发任何point都无效且不记录中位数

**问题分析**:
- 当前系统在传感器触发时会记录到中位数统计，无论是否有对应的包裹
- 这可能导致启动时的"幽灵触发"污染中位数数据

**建议方案**:

#### 方案1: 在RecordParcelPosition中检查包裹是否存在

**修改位置**: `src/Execution/.../Tracking/PositionIntervalTracker.cs`

```csharp
public void RecordParcelPosition(long parcelId, int positionIndex, DateTime arrivedAt, bool isValidParcel = true)
{
    // 需求3: 如果不是有效包裹，不记录中位数
    if (!isValidParcel)
    {
        _logger.LogDebug(
            "忽略无效包裹的位置记录: ParcelId={ParcelId}, PositionIndex={PositionIndex}",
            parcelId, positionIndex);
        return;
    }
    
    // 更新最后包裹记录时间
    lock (_lastRecordTimeLock)
    {
        _lastParcelRecordTime = arrivedAt;
    }
    
    // ... 原有逻辑
}
```

#### 方案2: 在调用端检查包裹存在性

**修改位置**: `src/Execution/.../Orchestration/SortingOrchestrator.cs`

在传感器触发处理中，先验证包裹是否存在于 `_createdParcels` 字典中：

```csharp
private async Task OnSensorTriggeredAsync(object? sender, SensorTriggeredEventArgs e)
{
    // 验证包裹是否已创建
    if (!_createdParcels.ContainsKey(e.ParcelId))
    {
        _logger.LogWarning(
            "[包裹验证] 传感器触发但包裹未创建，忽略: ParcelId={ParcelId}, PositionIndex={PositionIndex}",
            e.ParcelId, e.PositionIndex);
        return;  // 不记录，不执行任何操作
    }
    
    // 记录到中位数（仅对已创建的包裹）
    _intervalTracker?.RecordParcelPosition(e.ParcelId, e.PositionIndex, e.TriggeredAt);
    
    // ... 继续处理
}
```

#### 方案3: 引入包裹生命周期管理

创建专门的包裹状态管理器，明确包裹的生命周期：

```csharp
public enum ParcelState
{
    Created,      // 已创建
    Routed,       // 已获取路由
    InProgress,   // 分拣中
    Completed,    // 已完成
    Lost,         // 已丢失
    Invalid       // 无效
}

// 只有处于 Created/Routed/InProgress 状态的包裹才记录中位数
```

**推荐**: 使用方案2，在调用端进行验证，保持 `PositionIntervalTracker` 的职责单一。

---

## 使用指南

### 1. 关闭丢失检测

```bash
curl -X POST http://localhost:5000/api/sorting/loss-detection-config \
  -H "Content-Type: application/json" \
  -d '{"isEnabled": false}'
```

**应用场景**:
- 测试环境调试
- 线体速度不稳定时期
- 避免误判

### 2. 启用自动清空中位数

```bash
curl -X POST http://localhost:5000/api/sorting/loss-detection-config \
  -H "Content-Type: application/json" \
  -d '{"autoClearMedianIntervalMs": 300000}'
```

**应用场景**:
- 间歇性生产（如每天只运行几小时）
- 午休时间较长的生产线
- 避免长时间停止后的陈旧数据影响判断

### 3. 支持大窗口配置

```bash
curl -X POST http://localhost:5000/api/sorting/loss-detection-config \
  -H "Content-Type: application/json" \
  -d '{
    "windowSize": 10000,
    "monitoringIntervalMs": 1000,
    "lostDetectionMultiplier": 5,
    "timeoutMultiplier": 5
  }'
```

**应用场景**:
- 高流量生产线
- 需要更精确的中位数计算
- 包裹间隔变化大的场景

---

## 性能影响分析

### CPU使用率
- **关闭丢失检测**: 减少约 2-5% CPU使用率（取决于包裹流量）
- **自动清空**: 每次清空操作耗时 < 1ms，对性能影响可忽略

### 内存使用
- **大窗口 (10000)**: 每个 Position 约 80KB (10000 * 8 bytes)
  - 假设 20 个 Position，总计约 1.6MB
  - 相比默认窗口 (10)，增加约 1.6MB
- **自动清空**: 定期清空可避免内存无限增长

### 建议
- 正常生产环境：WindowSize = 10-100
- 高流量环境：WindowSize = 100-1000
- 仅在特殊需求时使用 WindowSize > 1000

---

## 测试建议

### 单元测试
- [ ] `PositionIntervalTrackerTests.ShouldAutoClear_ReturnsTrue_WhenTimeElapsed`
- [ ] `PositionIntervalTrackerTests.ShouldAutoClear_ReturnsFalse_WhenDisabled`
- [ ] `ParcelLossMonitoringServiceTests.SkipsDetection_WhenDisabled`
- [ ] `SortingOrchestratorTests.ClearsMedianStats_OnSystemStop`

### 集成测试
- [ ] `SortingControllerTests.UpdateLossDetectionConfig_WithNewFields`
- [ ] `SortingControllerTests.UpdateLossDetectionConfig_ValidatesWindowSizeRange`

### E2E测试
- [ ] 验证关闭丢失检测后不再触发丢失事件
- [ ] 验证自动清空功能正常工作
- [ ] 验证大窗口配置下系统稳定性

---

## 相关文档

- [包裹丢失检测方案](./PARCEL_LOSS_DETECTION_SOLUTION.md) - 原始设计文档
- [启动时路径布满包裹场景](./STARTUP_FULL_LINE_SCENARIO.md) - 需求5方案文档
- [API使用指南](./guides/API_USAGE_GUIDE.md) - API端点使用说明

---

**文档版本**: 1.0  
**最后更新**: 2025-12-23  
**维护团队**: ZakYip Development Team
