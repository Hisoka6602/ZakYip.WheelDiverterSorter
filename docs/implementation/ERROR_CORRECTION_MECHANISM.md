# 异常纠错机制完整说明 (Error Correction Mechanism)

## 概述

本系统实现了完整的异常纠错机制，确保在**任何异常情况**下，包裹都能被正确路由到**异常格口**，**绝不会错分到其他格口**。

纠错机制的核心原则：
- ✅ **在包裹当前位置检测异常**
- ✅ **立即触发纠错逻辑**
- ✅ **统一路由到异常格口**
- ✅ **记录详细日志用于追踪**
- ✅ **防止包裹错分或丢失**

## 异常场景覆盖

### 1. 上游通信异常

#### 1.1 RuleEngine连接失败
**场景描述**：系统启动时或运行时无法连接到RuleEngine

**检测时机**：
```csharp
// ParcelSortingOrchestrator.StartAsync()
_isConnected = await _ruleEngineClient.ConnectAsync(cancellationToken);
if (!_isConnected)
{
    _logger.LogWarning("无法连接到RuleEngine，将在包裹检测时尝试重新连接");
}
```

**纠错策略**：
- 系统继续运行，但标记为"未连接"状态
- 每个包裹检测时，尝试发送通知
- 如果通知失败，立即路由到异常格口

**代码位置**：`ParcelSortingOrchestrator.OnParcelDetected()` 第178-187行

#### 1.2 包裹检测通知发送失败
**场景描述**：包裹到达但无法通知RuleEngine

**检测时机**：
```csharp
var notificationSent = await _ruleEngineClient.NotifyParcelDetectedAsync(parcelId);
if (!notificationSent)
{
    _logger.LogError("包裹 {ParcelId} 无法发送检测通知到RuleEngine，将发送到异常格口", parcelId);
    await ProcessSortingAsync(parcelId, exceptionChuteId);
    return;
}
```

**纠错策略**：
- 检测通知失败立即触发
- 不等待响应，直接路由到异常格口
- 记录错误日志

**代码位置**：`ParcelSortingOrchestrator.OnParcelDetected()` 第178-187行

#### 1.3 格口分配等待超时
**场景描述**：RuleEngine未在规定时间内推送格口分配

**检测时机**：
```csharp
var timeoutMs = systemConfig.ChuteAssignmentTimeoutMs; // 默认10000ms
using var cts = new CancellationTokenSource(timeoutMs);
targetChuteId = await tcs.Task.WaitAsync(cts.Token);
```

**纠错策略**：
- 超时后捕获TimeoutException或OperationCanceledException
- 自动路由到异常格口
- 记录警告日志，包含超时时间

**代码位置**：`ParcelSortingOrchestrator.OnParcelDetected()` 第207-222行

**配置参数**：
```json
{
  "ChuteAssignmentTimeoutMs": 10000  // 可在系统配置中调整
}
```

### 2. 路径规划异常

#### 2.1 格口未配置
**场景描述**：目标格口在路由配置中不存在

**检测时机**：
```csharp
var path = _pathGenerator.GeneratePath(targetChuteId);
if (path == null)
{
    _logger.LogWarning("包裹 {ParcelId} 无法生成到格口 {TargetChuteId} 的路径，将发送到异常格口", 
                       parcelId, targetChuteId);
    // 重新生成到异常格口的路径
    targetChuteId = exceptionChuteId;
    path = _pathGenerator.GeneratePath(targetChuteId);
}
```

**纠错策略**：
- 路径生成返回null时触发
- 自动切换到异常格口
- 重新生成异常格口路径
- 记录警告日志

**代码位置**：`ParcelSortingOrchestrator.ProcessSortingAsync()` 第255-271行

#### 2.2 路径生成失败
**场景描述**：格口配置存在但路径生成失败（如配置错误）

**检测时机**：路径生成器内部验证失败返回null

**纠错策略**：
- 与"格口未配置"相同
- 尝试生成异常格口路径
- 如果异常格口路径也失败，记录错误并终止分拣

**代码位置**：`ParcelSortingOrchestrator.ProcessSortingAsync()` 第266-270行

### 3. 路径执行异常

#### 3.1 摆轮控制失败
**场景描述**：摆轮硬件故障或通信失败

**检测时机**：
```csharp
var executionResult = await _pathExecutor.ExecuteAsync(path);
if (!executionResult.IsSuccess)
{
    _logger.LogError("包裹 {ParcelId} 分拣失败: {FailureReason}，实际到达格口: {ActualChuteId}",
                     parcelId, executionResult.FailureReason, executionResult.ActualChuteId);
}
```

**纠错策略**：
- 执行失败后触发PathFailureHandler
- 记录失败段信息（哪个摆轮失败）
- 计算备用路径到异常格口
- 触发事件通知监控系统

**代码位置**：`ParcelSortingOrchestrator.ProcessSortingAsync()` 第280-306行

**相关组件**：
- `IPathFailureHandler` - 失败处理器接口
- `PathFailureHandler` - 实现类
- `PathSegmentExecutionFailedEventArgs` - 段失败事件
- `PathExecutionFailedEventArgs` - 路径失败事件

#### 3.2 路径段执行超时
**场景描述**：包裹在TTL时间内未到达下一个摆轮

**检测时机**：路径执行器内部TTL检查

**纠错策略**：
- 与摆轮控制失败相同
- 失败原因标记为"执行超时"
- 触发PathFailureHandler处理

**代码位置**：路径执行器内部实现

### 4. 传感器异常

#### 4.1 传感器故障
**场景描述**：传感器健康监控检测到异常

**检测时机**：`ISensorHealthMonitor` 持续监控传感器状态

**纠错策略**：
- 记录告警日志
- 通知运维人员
- 传感器故障不直接影响包裹分拣（依赖其他传感器）

**相关组件**：
- `ISensorHealthMonitor` - 传感器健康监控接口
- `SensorHealthMonitor` - 实现类

#### 4.2 重复触发异常
**场景描述**：包裹在短时间内多次触发同一传感器

**检测时机**：
```csharp
// ParcelSortingOrchestrator.OnDuplicateTriggerDetected()
_parcelDetectionService.DuplicateTriggerDetected += OnDuplicateTriggerDetected;
```

**纠错策略**：
- 检测到重复触发事件
- 记录警告日志，包含触发间隔
- 尝试通知RuleEngine（但不等待响应）
- **直接路由到异常格口**

**代码位置**：`ParcelSortingOrchestrator.OnDuplicateTriggerDetected()` 第127-161行

### 5. 设备异常

#### 5.1 硬件驱动初始化失败
**场景描述**：系统启动时硬件驱动无法初始化

**检测时机**：驱动配置加载和初始化阶段

**纠错策略**：
- 记录错误日志
- 系统可能无法启动或进入降级模式
- 需要人工干预修复

**相关配置**：`DriverConfiguration` 中的 `UseHardwareDriver` 标志

#### 5.2 设备通信中断
**场景描述**：运行时设备连接丢失

**检测时机**：路径执行时检测通信失败

**纠错策略**：
- 与摆轮控制失败相同
- 触发PathFailureHandler
- 路由到异常格口

## 纠错机制实现细节

### PathFailureHandler组件

**核心职责**：
1. 监听和处理路径执行失败
2. 记录失败详情（原因、位置、时间）
3. 计算备用路径到异常格口
4. 触发事件通知

**关键接口**：
```csharp
public interface IPathFailureHandler
{
    // 事件：路径段执行失败
    event EventHandler<PathSegmentExecutionFailedEventArgs>? SegmentExecutionFailed;
    
    // 事件：路径执行失败
    event EventHandler<PathExecutionFailedEventArgs>? PathExecutionFailed;
    
    // 事件：路径切换到异常格口
    event EventHandler<PathSwitchedEventArgs>? PathSwitched;
    
    // 处理路径失败
    void HandlePathFailure(long parcelId, SwitchingPath originalPath, 
                          string failureReason, SwitchingPathSegment? failedSegment = null);
    
    // 计算备用路径
    SwitchingPath? CalculateBackupPath(SwitchingPath originalPath);
}
```

**使用示例**：
```csharp
// 在ParcelSortingOrchestrator中使用
if (!executionResult.IsSuccess)
{
    // 触发失败处理
    _pathFailureHandler?.HandlePathFailure(
        parcelId,
        path,
        executionResult.FailureReason ?? "未知错误",
        executionResult.FailedSegment);
}
```

### 异常格口配置

**配置位置**：`SystemConfiguration.ExceptionChuteId`

**默认值**：999

**验证规则**：
```csharp
if (ExceptionChuteId <= 0)
{
    return (false, "异常格口ID必须大于0");
}
```

**配置示例**：
```json
{
  "SystemConfiguration": {
    "ExceptionChuteId": 999,
    "ChuteAssignmentTimeoutMs": 10000
  }
}
```

### FallbackChuteId机制

每个生成的路径都包含 `FallbackChuteId` 属性：

```csharp
public class SwitchingPath
{
    public int TargetChuteId { get; init; }      // 目标格口
    public int FallbackChuteId { get; init; }    // 异常格口
    // ...
}
```

**用途**：
- 路径执行失败时，使用FallbackChuteId重新生成路径
- 确保所有失败场景都有明确的异常格口

## 日志记录

系统在不同异常场景下记录不同级别的日志：

### 警告日志 (Warning)
- RuleEngine连接失败
- 格口分配超时
- 格口未配置
- 路径段执行失败
- 传感器重复触发

**示例**：
```
[Warning] 包裹 12345 等待格口分配超时（10000ms），将发送到异常格口
[Warning] 路径段执行失败: ParcelId=12345, 段序号=1, 摆轮=D1, 原因=执行超时
```

### 错误日志 (Error)
- 包裹检测通知发送失败
- 路径执行失败
- 异常格口路径也无法生成

**示例**：
```
[Error] 包裹 12345 无法发送检测通知到RuleEngine，将发送到异常格口
[Error] 路径执行失败: ParcelId=12345, 原始目标格口=101, 失败原因=段1执行超时, 将切换到异常格口=999
[Error] 包裹 12345 连异常格口路径都无法生成，分拣失败
```

### 信息日志 (Information)
- 正常分拣流程
- 备用路径计算成功

**示例**：
```
[Information] 检测到包裹 12345，开始处理分拣流程
[Information] 包裹 12345 分配到格口 101
[Information] 包裹 12345 成功分拣到格口 101
[Information] 已计算备用路径: ParcelId=12345, 目标格口=999, 路径段数=2
```

## 事件通知机制

系统通过事件机制通知监控系统：

### 1. SegmentExecutionFailed 事件
**触发时机**：路径段执行失败

**事件参数**：
```csharp
public class PathSegmentExecutionFailedEventArgs
{
    public long ParcelId { get; init; }
    public SwitchingPathSegment FailedSegment { get; init; }
    public int OriginalTargetChuteId { get; init; }
    public string FailureReason { get; init; }
    public DateTimeOffset FailureTime { get; init; }
}
```

### 2. PathExecutionFailed 事件
**触发时机**：完整路径执行失败

**事件参数**：
```csharp
public class PathExecutionFailedEventArgs
{
    public long ParcelId { get; init; }
    public SwitchingPath OriginalPath { get; init; }
    public SwitchingPathSegment? FailedSegment { get; init; }
    public string FailureReason { get; init; }
    public DateTimeOffset FailureTime { get; init; }
    public int ActualChuteId { get; init; }
}
```

### 3. PathSwitched 事件
**触发时机**：路径切换到异常格口

**事件参数**：
```csharp
public class PathSwitchedEventArgs
{
    public long ParcelId { get; init; }
    public SwitchingPath OriginalPath { get; init; }
    public SwitchingPath BackupPath { get; init; }
    public string SwitchReason { get; init; }
    public DateTimeOffset SwitchTime { get; init; }
}
```

## 异常场景测试

系统提供了完整的测试用例验证纠错机制：

### 单元测试
- `PathFailureHandlerTests` - PathFailureHandler功能测试
- 共10个测试，覆盖所有失败场景

### 集成测试
- `PathFailureIntegrationTests` - 端到端失败场景测试
- 共3个测试，验证完整纠错流程

**运行测试**：
```bash
dotnet test --filter "FullyQualifiedName~PathFailure"
```

## 配置示例

### 完整的系统配置
```json
{
  "SystemConfiguration": {
    "ExceptionChuteId": 999,
    "ChuteAssignmentTimeoutMs": 10000,
    "MaxConcurrentParcels": 10
  },
  "RouteConfigurations": [
    {
      "ChuteId": 999,
      "ChuteName": "异常格口（末端）",
      "DiverterConfigurations": [
        {
          "DiverterId": "D1",
          "TargetDirection": "Straight",
          "SequenceNumber": 1
        },
        {
          "DiverterId": "D2",
          "TargetDirection": "Straight",
          "SequenceNumber": 2
        },
        {
          "DiverterId": "D3",
          "TargetDirection": "Straight",
          "SequenceNumber": 3
        }
      ],
      "BeltSpeedMeterPerSecond": 1.5,
      "BeltLengthMeter": 15.0,
      "ToleranceTimeMs": 2000,
      "IsEnabled": true
    }
  ]
}
```

## 最佳实践

### 1. 异常格口配置
- ✅ **必须配置**：异常格口配置永远不能删除
- ✅ **简单路径**：异常格口应配置最简单的路径（通常是所有摆轮直行）
- ✅ **末端位置**：异常格口应位于输送线末端

### 2. 超时时间配置
- ✅ **合理设置**：超时时间应根据实际网络延迟和RuleEngine响应时间设置
- ✅ **不宜过短**：避免正常包裹因超时被错误路由
- ✅ **不宜过长**：避免包裹积压影响吞吐量

**推荐值**：
- 开发环境：5000ms
- 测试环境：10000ms
- 生产环境：8000-12000ms

### 3. 监控和告警
- ✅ **监控异常率**：异常格口包裹数量/总包裹数
- ✅ **设置告警**：异常率超过5%时触发告警
- ✅ **分析日志**：定期分析异常原因，优化系统

### 4. 故障恢复
- ✅ **RuleEngine断线**：自动重连机制（已实现）
- ✅ **传感器故障**：健康监控和告警（已实现）
- ✅ **摆轮故障**：需要人工维修，系统记录日志

## 系统保障

### 防止错分的机制
1. **统一异常出口**：所有异常都路由到同一个异常格口
2. **早期检测**：在路径生成和执行的每个阶段都有检测
3. **立即纠错**：检测到异常立即触发纠错逻辑
4. **完整日志**：记录每个异常的详细信息用于追溯

### 包裹安全保障
1. **不会丢失**：所有包裹都有明确的去向（目标格口或异常格口）
2. **不会堵塞**：异常格口确保包裹能够离开系统
3. **可追溯**：完整的日志记录可追踪每个包裹的流向

## 相关文档

- [PATH_FAILURE_DETECTION_GUIDE.md](PATH_FAILURE_DETECTION_GUIDE.md) - 路径失败检测详细指南
- [SYSTEM_CONFIG_GUIDE.md](SYSTEM_CONFIG_GUIDE.md) - 系统配置指南
- [API_USAGE_GUIDE.md](API_USAGE_GUIDE.md) - API使用教程
- [TESTING.md](TESTING.md) - 测试文档

---

**文档版本：** v1.0  
**最后更新：** 2025-11-14  
**维护团队：** ZakYip Development Team
