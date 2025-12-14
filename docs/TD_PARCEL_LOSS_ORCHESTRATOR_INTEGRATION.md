# 技术债务：包裹丢失编排器集成

**TD ID**: TD-LOSS-ORCHESTRATOR-001  
**创建日期**: 2025-12-14  
**优先级**: 高  
**预估工作量**: 4-6小时  
**依赖**: 包裹丢失检测基础设施（已完成于当前PR）

## 概述

当前PR已完成包裹丢失检测的完整基础设施，包括：
- 事件模型 (`ParcelLostEventArgs`)
- 队列清理机制 (`RemoveAllTasksForParcel`)
- 主动监控服务 (`ParcelLossMonitoringService`)
- 优先级规则（丢失 > 超时）

**待完成工作**：将丢失检测集成到 `SortingOrchestrator`，实现完整的丢失处理流程。

## 业务需求

当检测到包裹丢失时（例如P3丢失）：

1. **质量重路由**：所有队列中现存的包裹（P1, P2, P4, P5, P6）必须全部重路由到异常口
2. **上游通知**：
   - 丢失包裹（P3）：上报丢失状态
   - 受影响包裹（P1, P2, P4, P5, P6）：上报受丢失影响导致的异常口分拣
3. **恢复正常**：新创建的包裹（P7+）恢复正常分拣

## 详细实施方案

### 1. 订阅监控服务事件

**文件**: `src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`

**位置**: 构造函数中

```csharp
public class SortingOrchestrator
{
    private readonly ParcelLossMonitoringService? _lossMonitoringService;
    
    public SortingOrchestrator(
        // ... 现有参数
        ParcelLossMonitoringService? lossMonitoringService = null)
    {
        // ... 现有初始化
        _lossMonitoringService = lossMonitoringService;
        
        // 订阅丢失事件
        if (_lossMonitoringService != null)
        {
            _lossMonitoringService.ParcelLostDetected += OnParcelLostDetectedAsync;
        }
    }
}
```

### 2. 实现丢失事件处理器

**新增方法**：

```csharp
private async void OnParcelLostDetectedAsync(object? sender, ParcelLostEventArgs e)
{
    await _safeExecutor.ExecuteAsync(
        async () =>
        {
            _logger.LogError(
                "[包裹丢失] 检测到包裹 {ParcelId} 在 Position {Position} 丢失",
                e.LostParcelId, e.DetectedAtPositionIndex);
            
            // 1. 从所有队列删除丢失包裹的任务
            var removedTasks = _queueManager!.RemoveAllTasksForParcel(e.LostParcelId);
            
            // 2. 批量重路由所有受影响的包裹
            var affectedParcels = await RerouteAllExistingParcelsToExceptionAsync(e.LostParcelId);
            
            // 3. 上报丢失包裹
            await NotifyUpstreamParcelLostAsync(e);
            
            // 4. 上报所有受影响的包裹
            await NotifyUpstreamAffectedParcelsAsync(affectedParcels, e.LostParcelId);
            
            // 5. 记录指标
            // TODO: 更新丢失计数和受影响包裹计数
        },
        operationName: "HandleParcelLost",
        cancellationToken: CancellationToken.None
    );
}
```

### 3. 实现批量重路由方法

**新增方法**：

```csharp
/// <summary>
/// 将所有队列中的现存包裹重路由到异常口
/// </summary>
/// <param name="lostParcelId">丢失的包裹ID（用于日志）</param>
/// <returns>受影响的包裹ID列表</returns>
private async Task<List<long>> RerouteAllExistingParcelsToExceptionAsync(long lostParcelId)
{
    var affectedParcels = new HashSet<long>();
    var allQueueStatuses = _queueManager!.GetAllQueueStatuses();
    
    foreach (var (positionIndex, status) in allQueueStatuses)
    {
        if (status.QueueLength == 0) continue;
        
        // 获取队列中所有任务并替换为Straight
        var queue = GetQueueForPosition(positionIndex); // 需要添加辅助方法
        var tasksToReroute = new List<PositionQueueItem>();
        
        // 清空队列并收集所有任务
        while (queue.TryDequeue(out var task))
        {
            tasksToReroute.Add(task);
            affectedParcels.Add(task.ParcelId);
        }
        
        // 重新入队，所有动作改为Straight
        foreach (var task in tasksToReroute)
        {
            var reroutedTask = task with
            {
                DiverterAction = DiverterDirection.Straight,
                FallbackAction = DiverterDirection.Straight
            };
            queue.Enqueue(reroutedTask);
        }
        
        _logger.LogWarning(
            "[批量重路由] Position {Position}: 重路由 {Count} 个任务到异常口 (受包裹 {LostParcelId} 丢失影响)",
            positionIndex, tasksToReroute.Count, lostParcelId);
    }
    
    return affectedParcels.ToList();
}
```

**辅助方法**（需要添加）：

```csharp
private ConcurrentQueue<PositionQueueItem> GetQueueForPosition(int positionIndex)
{
    // 通过反射或新增IPositionIndexQueueManager接口方法获取队列
    // 建议：在IPositionIndexQueueManager中添加GetQueue方法
    throw new NotImplementedException("需要在IPositionIndexQueueManager中添加GetQueue方法");
}
```

**推荐接口改进**（`IPositionIndexQueueManager.cs`）：

```csharp
/// <summary>
/// 获取指定位置的队列（用于批量操作）
/// </summary>
ConcurrentQueue<PositionQueueItem>? GetQueue(int positionIndex);
```

### 4. 实现上游通知方法

**丢失包裹通知**：

```csharp
private async Task NotifyUpstreamParcelLostAsync(ParcelLostEventArgs e)
{
    if (_upstreamClient == null) return;
    
    try
    {
        var notification = new SortingCompletedNotification
        {
            ParcelId = e.LostParcelId.ToString(),
            Status = "Lost", // 或使用枚举
            ChuteId = null, // 丢失包裹无格口
            Timestamp = e.DetectedAt,
            Metadata = new Dictionary<string, object>
            {
                ["LostAtPosition"] = e.DetectedAtPositionIndex,
                ["DelayMs"] = e.DelayMs,
                ["TotalLifetimeMs"] = e.TotalLifetimeMs,
                ["LostThresholdMs"] = e.LostThresholdMs
            }
        };
        
        await _upstreamClient.SendAsync(notification);
        
        _logger.LogInformation(
            "[上游通知] 已上报丢失包裹 {ParcelId}",
            e.LostParcelId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "[上游通知失败] 无法上报丢失包裹 {ParcelId}",
            e.LostParcelId);
    }
}
```

**受影响包裹通知**：

```csharp
private async Task NotifyUpstreamAffectedParcelsAsync(
    List<long> affectedParcelIds,
    long lostParcelId)
{
    if (_upstreamClient == null) return;
    
    foreach (var parcelId in affectedParcelIds)
    {
        try
        {
            var notification = new SortingCompletedNotification
            {
                ParcelId = parcelId.ToString(),
                Status = "AffectedByLoss", // 或 "RoutedToExceptionDueToLoss"
                ChuteId = _systemConfig?.ExceptionChuteId.ToString(),
                Timestamp = _clock.LocalNow,
                Metadata = new Dictionary<string, object>
                {
                    ["AffectedByLostParcel"] = lostParcelId,
                    ["Reason"] = "ReroutedToExceptionDueToParcelLoss"
                }
            };
            
            await _upstreamClient.SendAsync(notification);
            
            _logger.LogInformation(
                "[上游通知] 已上报受影响包裹 {ParcelId} (受 {LostParcelId} 丢失影响)",
                parcelId, lostParcelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[上游通知失败] 无法上报受影响包裹 {ParcelId}",
                parcelId);
        }
    }
}
```

### 5. 注册监控服务到DI

**文件**: `src/Execution/ZakYip.WheelDiverterSorter.Execution/ExecutionServiceExtensions.cs`

```csharp
public static IServiceCollection AddExecutionServices(
    this IServiceCollection services)
{
    // ... 现有注册
    
    // 注册包裹丢失监控服务
    services.AddHostedService<ParcelLossMonitoringService>();
    services.AddSingleton<ParcelLossMonitoringService>(); // 同时注册为单例以便注入
    
    return services;
}
```

## 测试建议

### 单元测试

**文件**: `tests/ZakYip.WheelDiverterSorter.Execution.Tests/Orchestration/SortingOrchestratorLossHandlingTests.cs`

```csharp
public class SortingOrchestratorLossHandlingTests
{
    [Fact]
    public async Task WhenParcelLost_ShouldRerouteAllExistingParcelsToException()
    {
        // Arrange: 创建6个包裹，P3丢失
        // Act: 触发P3丢失事件
        // Assert: P1, P2, P4, P5, P6都重路由到异常口
    }
    
    [Fact]
    public async Task WhenParcelLost_ShouldNotifyUpstreamForLostParcel()
    {
        // Arrange
        // Act
        // Assert: 验证上游收到P3丢失通知
    }
    
    [Fact]
    public async Task WhenParcelLost_ShouldNotifyUpstreamForAllAffectedParcels()
    {
        // Arrange
        // Act
        // Assert: 验证上游收到P1-P6的受影响通知
    }
    
    [Fact]
    public async Task WhenParcelLostAndNewParcelCreated_ShouldResumeNormalSorting()
    {
        // Arrange: P3丢失后创建P7
        // Act
        // Assert: P7正常分拣，不受影响
    }
}
```

### 集成测试

**文件**: `tests/ZakYip.WheelDiverterSorter.Integration.Tests/ParcelLossE2ETests.cs`

```csharp
[Fact]
public async Task ParcelLoss_E2E_Flow()
{
    // 1. 创建P1-P6包裹
    // 2. 模拟P3丢失（超过LostDetectionDeadline）
    // 3. 验证：
    //    - P3从队列删除
    //    - P1, P2, P4, P5, P6重路由到异常口
    //    - 上游收到7条通知（1条丢失 + 6条受影响）
    // 4. 创建P7
    // 5. 验证P7正常分拣
}
```

## 潜在问题与解决方案

### 问题1：队列访问

**问题**：`IPositionIndexQueueManager` 没有提供直接访问队列的方法

**解决方案**：
- 方案A：添加 `GetQueue(int positionIndex)` 方法
- 方案B：添加 `RerouteAllTasksToException()` 方法直接在管理器中处理

**推荐**：方案B，保持封装性

### 问题2：并发安全

**问题**：丢失事件和正常IO触发可能并发访问队列

**解决方案**：
- 使用 `ConcurrentQueue` 的线程安全特性
- 在重路由期间不影响正常的Dequeue操作
- 考虑添加短暂的"重路由中"标记

### 问题3：上游通知失败

**问题**：上游通知失败不应阻塞分拣

**解决方案**：
- 所有上游通知使用 try-catch 包裹
- 失败仅记录日志，不抛出异常
- 考虑添加重试队列（可选）

### 问题4：性能影响

**问题**：大量包裹重路由可能影响性能

**解决方案**：
- 批量操作在后台线程执行（已通过SafeExecutor实现）
- 限制单次处理的最大包裹数（如100个）
- 添加性能监控指标

## 依赖的基础设施（已完成）

✅ `ParcelLostEventArgs` - 事件模型  
✅ `IPositionIndexQueueManager.RemoveAllTasksForParcel()` - 队列清理  
✅ `ParcelLossMonitoringService` - 主动监控  
✅ `PositionQueueItem.LostDetectionDeadline` - 计时器字段  
✅ 优先级规则 - 丢失 > 超时  

## 验收标准

- [ ] 丢失事件触发时，所有队列中的包裹重路由到异常口
- [ ] 丢失包裹和所有受影响包裹都上报上游
- [ ] 新创建的包裹不受影响，正常分拣
- [ ] 单元测试覆盖率 > 80%
- [ ] 集成测试验证完整E2E流程
- [ ] 性能测试：100个包裹重路由 < 50ms
- [ ] 并发测试：丢失处理不阻塞正常分拣

## 参考资料

- 当前PR基础设施实现
- `CORE_ROUTING_LOGIC.md` - 核心路由逻辑约束
- `copilot-instructions.md` - 编码规范

## 下一步行动

1. 在 `IPositionIndexQueueManager` 中添加批量重路由方法
2. 实现 `SortingOrchestrator` 的丢失事件处理
3. 注册监控服务到DI容器
4. 编写单元测试
5. 编写集成测试
6. 性能测试和优化
