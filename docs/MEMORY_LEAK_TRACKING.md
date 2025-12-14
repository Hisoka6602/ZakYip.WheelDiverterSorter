# 系统内存泄漏检测与修复跟踪
# System Memory Leak Detection and Fix Tracking

> **硬性规定**: 任何时候都不能有内存泄漏和内存溢出
> 
> **HARD REQUIREMENT**: There must NEVER be memory leaks or memory overflow at any time

## 检测日期 Detection Date: 2025-12-14

---

## ✅ 已修复的内存泄漏 Fixed Memory Leaks

### 1. PositionIntervalTracker - 包裹位置追踪泄漏 ✅

**问题**: `_parcelPositionTimes` 字典无限增长，丢失包裹的追踪数据未清理

**影响**: 
- 内存泄漏
- **关键Bug**: 导致丢失包裹的ID被后续包裹误用

**修复**:
- ✅ 添加 `ClearParcelTracking(long parcelId)` 方法
- ✅ 在包裹丢失时调用清理
- ✅ 在包裹完成时调用清理
- ✅ 自动清理机制：超过1000条记录时批量清理
- ✅ 使用配置值而非硬编码

**文件**: `src/Execution/ZakYip.WheelDiverterSorter.Execution/Tracking/PositionIntervalTracker.cs`

**PR**: copilot/add-parcel-detection-logs

---

### 2. ParcelLossMonitoringService - 已报告包裹追踪 ✅

**问题**: `_reportedLostParcels` 字典可能无限增长

**修复**:
- ✅ 定期清理机制（保留1小时内记录）
- ✅ 在每次监控循环结束时调用 `CleanupExpiredReportedParcels`

**文件**: `src/Execution/ZakYip.WheelDiverterSorter.Execution/Monitoring/ParcelLossMonitoringService.cs`

**PR**: copilot/add-parcel-detection-logs

---

## ⚠️ 待修复的内存泄漏 Pending Memory Leaks

### 高优先级 High Priority

#### 1. 事件订阅泄漏 Event Subscription Leaks

**文件清单**:
1. `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Servers/TouchSocketTcpRuleEngineServer.cs`
   - 订阅: 3, 取消订阅: 0
   - 需要在Dispose中取消订阅

2. `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/TouchSocketTcpRuleEngineClient.cs`
   - 订阅: 3, 取消订阅: 0

3. `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/SignalRRuleEngineClient.cs`
   - 订阅: 3, 取消订阅: 0

4. `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/MqttRuleEngineClient.cs`
   - 订阅: 2, 取消订阅: 0

5. `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/MqttEmcResourceLockManager.cs`
   - 订阅: 1, 取消订阅: 0

6. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Leadshine/CoordinatedEmcController.cs`
   - 订阅: 2, 取消订阅: 0

7. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Siemens/S7Connection.cs`
   - 订阅: 2, 取消订阅: 0

8. `src/Simulation/ZakYip.WheelDiverterSorter.Simulation/Services/SimulationRunner.cs`
   - 订阅: 1, 取消订阅: 0

9. `src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Adapters/SensorEventProviderAdapter.cs`
   - 订阅: 3, 取消订阅: 0

**修复方案**:
```csharp
public class ExampleService : IDisposable
{
    public void Start()
    {
        _someService.SomeEvent += OnSomeEvent;
    }
    
    public void Dispose()
    {
        // ✅ 必须取消订阅
        _someService.SomeEvent -= OnSomeEvent;
    }
}
```

**预计工作量**: 2-3小时

---

#### 2. Timer未释放 Timer Not Disposed

**文件清单**:
1. `src/Observability/ZakYip.WheelDiverterSorter.Observability/Tracing/LogCleanupHostedService.cs:56`
   ```csharp
   _timer = new Timer(...);  // ❌ 没有using或Dispose
   ```

2. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Siemens/S7Connection.cs:63`
   ```csharp
   _healthCheckTimer = new Timer(...);  // ❌ 没有Dispose
   ```

3. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Siemens/S7Connection.cs:92`

**修复方案**:
```csharp
private Timer? _timer;

public void Dispose()
{
    _timer?.Dispose();
    _timer = null;
}
```

**预计工作量**: 1小时

---

### 中优先级 Medium Priority

#### 3. 无清理机制的集合 Collections Without Cleanup

**文件清单**:
1. `src/Execution/ZakYip.WheelDiverterSorter.Execution/Health/NodeHealthRegistry.cs`
   - 可能需要定期清理过期节点

2. `src/Host/ZakYip.WheelDiverterSorter.Host/StateMachine/SystemStateManager.cs`
   - 状态历史记录可能需要限制

3. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/FactoryBasedDriverManager.cs`
   - Driver实例缓存需要清理机制

4. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Simulated/SimulatedSensorInputReader.cs`

5. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Simulated/SimulatedPanelInputReader.cs`

6. `src/Execution/ZakYip.WheelDiverterSorter.Execution/Pipeline/SortingPipeline.cs`

7. `src/Execution/ZakYip.WheelDiverterSorter.Execution/SelfTest/SystemSelfTestCoordinator.cs`

8. `src/Simulation/ZakYip.WheelDiverterSorter.Simulation/Services/CapacityTestingRunner.cs`

**评估**: 需要逐个文件检查，确定是否真的会无限增长

**预计工作量**: 4-6小时

---

### 低优先级 Low Priority

#### 4. 静态缓存 Static Caches

**文件清单**:
1. `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Topology/DefaultSwitchingPathGenerator.cs`
   - 可能使用静态缓存

2. `src/Core/ZakYip.WheelDiverterSorter.Core/Sorting/Strategy/SortingContext.cs`

3. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Leadshine/IoMapping/LeadshineIoMapper.cs`

**评估**: 如果是有限大小的查找表，可能是安全的

**预计工作量**: 1-2小时审查

---

## 🔍 检测工具 Detection Tools

### 自动化检测脚本

位置: `/tmp/memory_leak_check.sh`

运行:
```bash
cd /home/runner/work/ZakYip.WheelDiverterSorter/ZakYip.WheelDiverterSorter
bash /tmp/memory_leak_check.sh
```

### 手动检测清单

- [ ] 检查所有 `ConcurrentDictionary` / `Dictionary` 是否有清理机制
- [ ] 检查所有事件订阅是否有取消订阅
- [ ] 检查所有 `Timer` / `PeriodicTimer` 是否有Dispose
- [ ] 检查所有 `FileStream` / `MemoryStream` 是否有Dispose
- [ ] 检查所有 `BackgroundService` 的ExecuteAsync是否会无限循环累积数据

---

## 📋 修复计划 Fix Plan

### 阶段1: 关键修复 (已完成) ✅
- [x] PositionIntervalTracker 包裹追踪泄漏
- [x] ParcelLossMonitoringService 重复日志追踪

### 阶段2: 高优先级 (计划中)
- [ ] 修复所有事件订阅泄漏
- [ ] 修复Timer未释放问题
- [ ] 预计工作量: 3-4小时

### 阶段3: 中优先级
- [ ] 审查并修复无清理机制的集合
- [ ] 预计工作量: 4-6小时

### 阶段4: 验证
- [ ] 运行压力测试（长时间运行）
- [ ] 使用内存分析工具监控
- [ ] 添加内存监控告警

---

## 🛡️ 预防措施 Prevention Measures

### 编码规范

1. **集合使用规范**:
   ```csharp
   // ✅ 必须有清理机制
   private readonly ConcurrentDictionary<long, Data> _cache = new();
   
   public void Cleanup()
   {
       // 定期清理或基于阈值清理
       if (_cache.Count > 1000)
       {
           var toRemove = _cache.Keys.OrderBy(k => k).Take(200).ToList();
           foreach (var key in toRemove)
           {
               _cache.TryRemove(key, out _);
           }
       }
   }
   ```

2. **事件订阅规范**:
   ```csharp
   // ✅ 必须成对出现
   public void Subscribe()
   {
       _service.Event += OnEvent;
   }
   
   public void Dispose()
   {
       _service.Event -= OnEvent;  // ✅ 必须取消
   }
   ```

3. **Timer使用规范**:
   ```csharp
   // ✅ 必须Dispose
   private Timer? _timer;
   
   public void Start()
   {
       _timer = new Timer(...);
   }
   
   public void Dispose()
   {
       _timer?.Dispose();
       _timer = null;
   }
   ```

### Code Review检查点

- [ ] 新增的集合是否有边界限制？
- [ ] 新增的事件订阅是否有取消订阅？
- [ ] 新增的Timer是否有Dispose？
- [ ] BackgroundService是否会累积数据？

---

## 📊 监控建议 Monitoring Recommendations

### 1. 添加内存监控指标

```csharp
// 监控关键集合大小
_metrics.RecordGauge("parcel_tracking_count", _parcelPositionTimes.Count);
_metrics.RecordGauge("reported_lost_parcels_count", _reportedLostParcels.Count);
```

### 2. 添加告警规则

- 集合大小超过阈值90%
- 内存使用超过80%
- GC频率异常

### 3. 定期审查

- 每月运行内存泄漏检测脚本
- 每季度进行压力测试

---

## 📚 参考资料 References

- [Microsoft: .NET Memory Management](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/)
- [.NET Memory Profilers](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- [Event Subscription Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/events/)

---

**最后更新**: 2025-12-14
**负责人**: Development Team
**状态**: 🟡 进行中 (阶段1完成，阶段2-4待执行)
