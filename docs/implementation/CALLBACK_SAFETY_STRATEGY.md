# Callback and Event Safety Strategy

## Overview

本文档说明 ZakYip.WheelDiverterSorter 系统中事件回调和异常处理的安全策略。

This document explains the callback and event exception handling safety strategy in the ZakYip.WheelDiverterSorter system.

## Architecture

### 三层防护 / Three-Layer Protection

#### 1. BackgroundService 层（外层防护）/ BackgroundService Layer (Outer Protection)

所有后台服务的 `ExecuteAsync` 方法都通过 `ISafeExecutionService` 包裹，确保未捕获的异常不会导致进程崩溃。

All `BackgroundService.ExecuteAsync` methods are wrapped with `ISafeExecutionService` to ensure uncaught exceptions don't crash the process.

**已实现 / Implemented:**
- ✅ Worker
- ✅ RuntimePerformanceCollector
- ✅ NodeHealthMonitorService
- ✅ ParcelSortingWorker
- ✅ SensorMonitoringWorker
- ✅ AlarmMonitoringWorker
- ✅ LogCleanupHostedService

**示例 / Example:**
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await _safeExecutor.ExecuteAsync(
        async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Your business logic
            }
        },
        operationName: "WorkerLoop",
        cancellationToken: stoppingToken
    );
}
```

#### 2. 事件发布者层（中层防护）/ Event Publisher Layer (Middle Protection)

事件发布者使用 `EventHandlerExtensions.SafeInvoke` 确保单个订阅者的异常不会影响其他订阅者。

Event publishers use `EventHandlerExtensions.SafeInvoke` to ensure one subscriber's exception doesn't affect other subscribers.

**可选使用 / Optional Usage:**

当事件有多个订阅者且需要确保所有订阅者都能收到事件时使用。

Use when an event has multiple subscribers and you need to ensure all subscribers receive the event.

**示例 / Example:**
```csharp
// ✅ Safe invocation - all subscribers are protected
ChuteAssignmentReceived.SafeInvoke(
    this, 
    notification, 
    _logger, 
    nameof(ChuteAssignmentReceived));

// ❌ Unsafe invocation - one subscriber exception breaks all others
ChuteAssignmentReceived?.Invoke(this, notification);
```

**适用场景 / Use Cases:**
- 事件有多个订阅者 / Event has multiple subscribers
- 订阅者来自不同模块或第三方 / Subscribers from different modules or third-party
- 需要记录哪个订阅者失败 / Need to log which subscriber failed

**不需要使用的场景 / Not Needed When:**
- 事件只有一个订阅者 / Event has only one subscriber
- 订阅者在同一模块且可控 / Subscriber in same module and controlled
- 订阅者已有自己的异常处理 / Subscriber has own exception handling

#### 3. 事件订阅者层（内层防护）/ Event Subscriber Layer (Inner Protection)

事件订阅者（事件处理器）负责自己的异常处理，特别是涉及业务逻辑的处理器。

Event subscribers (event handlers) are responsible for their own exception handling, especially handlers with business logic.

**最佳实践 / Best Practice:**
```csharp
private void OnParcelDetected(object? sender, ParcelDetectedEventArgs e)
{
    try
    {
        // Business logic
        await ProcessParcelAsync(e.ParcelId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process parcel {ParcelId}", e.ParcelId);
        // Handle error appropriately
    }
}
```

## 当前实现状态 / Current Implementation Status

### ✅ 已实现 / Implemented

1. **BackgroundService 防护 / BackgroundService Protection**
   - 所有 6 个 BackgroundService 都已包裹 SafeExecution
   - All 6 BackgroundServices wrapped with SafeExecution

2. **EventHandlerExtensions**
   - 提供 `SafeInvoke` 扩展方法用于安全调用事件
   - Provides `SafeInvoke` extension method for safe event invocation
   - 捕获并记录每个订阅者的异常
   - Catches and logs each subscriber's exception

### 📋 可选优化 / Optional Enhancements

以下事件调用可以选择使用 `SafeInvoke`，但不是必需的（因为订阅者已在 BackgroundService 中受保护）：

The following event invocations can optionally use `SafeInvoke`, but it's not required (since subscribers are already protected in BackgroundServices):

1. **Communication Layer Events**
   - `ChuteAssignmentReceived` in RuleEngineClient implementations
   - `EmcLockEventReceived` in EmcResourceLockManager implementations
   - These are typically one-to-one communications

2. **Ingress Layer Events**
   - `ParcelDetected` in ParcelDetectionService
   - `DuplicateTriggerDetected` in ParcelDetectionService
   - `SensorFault`, `SensorRecovery` in SensorHealthMonitor
   - These are typically consumed by ParcelSortingOrchestrator

3. **Execution Layer Events**
   - `SegmentExecutionFailed`, `PathExecutionFailed` in PathFailureHandler
   - `NodeHealthChanged` in NodeHealthRegistry
   - These are internal events with controlled subscribers

## 决策矩阵 / Decision Matrix

| 场景 / Scenario | 使用 SafeInvoke? | 原因 / Reason |
|-----------------|------------------|---------------|
| 事件有多个未知订阅者 / Multiple unknown subscribers | ✅ 推荐 / Recommended | 防止订阅者相互影响 / Prevent mutual impact |
| 事件有单一已知订阅者 / Single known subscriber | ❌ 不需要 / Not needed | 订阅者自行处理 / Subscriber handles itself |
| 订阅者在 BackgroundService 中 / Subscriber in BackgroundService | ❌ 不需要 / Not needed | 已被 SafeExecution 保护 / Already protected by SafeExecution |
| 第三方插件订阅事件 / Third-party plugin subscribes | ✅ 强烈推荐 / Highly recommended | 不信任第三方代码 / Don't trust third-party code |
| 性能关键路径 / Performance-critical path | ❌ 可选 / Optional | SafeInvoke 有轻微开销 / SafeInvoke has slight overhead |

## 测试策略 / Testing Strategy

### 1. 单元测试 / Unit Tests

测试 `EventHandlerExtensions.SafeInvoke` 的异常隔离行为。

Test exception isolation behavior of `EventHandlerExtensions.SafeInvoke`.

```csharp
[Fact]
public void SafeInvoke_ShouldInvokeAllSubscribers_EvenIfOneThrows()
{
    // Arrange
    var callCount = 0;
    EventHandler<EventArgs> handler = null;
    handler += (s, e) => callCount++;
    handler += (s, e) => throw new Exception("Bad subscriber");
    handler += (s, e) => callCount++;
    
    // Act
    handler.SafeInvoke(this);
    
    // Assert
    Assert.Equal(2, callCount); // Both good subscribers were called
}
```

### 2. 集成测试 / Integration Tests

验证 BackgroundService 中的事件订阅不会因异常而崩溃。

Verify event subscriptions in BackgroundServices don't crash from exceptions.

### 3. E2E 测试 / E2E Tests

验证完整的事件流程（从传感器 → 检测服务 → 分拣编排器）在异常情况下仍然稳定。

Verify complete event flow (sensor → detection service → orchestrator) remains stable under exceptions.

## 监控和日志 / Monitoring and Logging

### 异常指标 / Exception Metrics

建议监控以下指标：

Recommend monitoring the following metrics:

1. 订阅者异常率 / Subscriber exception rate
2. 事件调用失败次数 / Event invocation failure count
3. 特定订阅者的错误频率 / Error frequency per subscriber

### 日志格式 / Log Format

SafeInvoke 使用以下日志格式记录订阅者异常：

SafeInvoke logs subscriber exceptions using this format:

```
[ERROR] 订阅者处理事件 'ParcelDetectedEventArgs' 时发生异常 / 
        Subscriber threw exception while handling event 'ParcelDetectedEventArgs': 
        Target=ParcelSortingOrchestrator, Method=OnParcelDetected
```

## 性能考虑 / Performance Considerations

### SafeInvoke 开销 / SafeInvoke Overhead

- `GetInvocationList()`: O(n) 其中 n 是订阅者数量 / O(n) where n is number of subscribers
- 每个订阅者的 try-catch: 无开销（无异常时） / Zero overhead when no exceptions
- 建议：仅在真正需要时使用 / Recommendation: Use only when truly needed

### 性能基准 / Performance Baseline

- 正常情况下（无异常）：SafeInvoke 与直接 Invoke 性能相同
- Normal case (no exceptions): SafeInvoke performs same as direct Invoke
- 异常情况下：SafeInvoke 捕获异常并继续，直接 Invoke 会中断
- Exception case: SafeInvoke catches and continues, direct Invoke breaks

## 总结 / Summary

本系统采用三层防护策略确保回调安全：

This system uses a three-layer protection strategy to ensure callback safety:

1. **BackgroundService 层**：所有后台服务用 SafeExecution 包裹（强制）
2. **Event Publisher 层**：提供 SafeInvoke 工具（可选，按需使用）
3. **Event Subscriber 层**：订阅者负责自己的异常处理（推荐）

关键原则：

Key principles:

- ✅ **外层保护优于内层保护** / Outer protection better than inner
- ✅ **默认安全优于按需安全** / Safe by default better than safe on demand
- ✅ **性能与安全平衡** / Balance performance and safety
- ✅ **记录失败便于调试** / Log failures for debugging

---

**文档版本 / Document Version**: 1.0  
**最后更新 / Last Updated**: 2025-11-22  
**维护团队 / Maintained By**: ZakYip Development Team
