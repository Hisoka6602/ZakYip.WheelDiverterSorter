# 路由计划仓储文件结构说明

> **文档目的**：说明包裹路由计划存储的文件结构，便于后续排查分拣异常

---

## 一、文件位置

### 1.1 接口定义

**文件**：`src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Routing/IRoutePlanRepository.cs`

**职责**：定义路由计划仓储的抽象接口

**方法**：
- `GetByParcelIdAsync(long parcelId)` - 根据包裹ID获取路由计划
- `SaveAsync(RoutePlan routePlan)` - 保存或更新路由计划
- `DeleteAsync(long parcelId)` - 删除路由计划

### 1.2 领域模型

**文件**：`src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Routing/RoutePlan.cs`

**职责**：路由计划聚合根，管理包裹分拣路由的完整生命周期

**核心属性**：
- `ParcelId` - 包裹唯一标识
- `InitialTargetChuteId` - 初始目标格口
- `CurrentTargetChuteId` - 当前有效目标格口（改口后更新）
- `Status` - 计划状态（Created, Executing, Completed, ExceptionRouted, Failed）
- `CreatedAt` - 创建时间
- `LastModifiedAt` - 最后修改时间
- `ChuteChangeCount` - 改口次数

**核心方法**：
- `TryApplyChuteChange()` - 尝试应用改口请求
- `MarkAsExecuting()` - 标记为执行中
- `MarkAsCompleted()` - 标记为已完成
- `MarkAsExceptionRouted()` - 标记为异常路由

### 1.3 实现类

**文件**：`src/Application/ZakYip.WheelDiverterSorter.Application/Services/Caching/InMemoryRoutePlanRepository.cs`

**职责**：基于内存缓存的路由计划仓储实现

**依赖**：
- `IMemoryCache` - .NET 内存缓存
- `ISafeExecutionService` - 安全执行服务（异常隔离）
- `ILogger` - 日志记录

**特点**：
- **3分钟滑动过期**：自动清理过期数据，防止内存泄漏
- **线程安全**：使用 `IMemoryCache` 的线程安全机制
- **异常隔离**：所有操作通过 `ISafeExecutionService` 包裹
- **无持久化**：进程重启后数据丢失（符合临时数据的语义）

---

## 二、存储方案演进历史

### 2.1 旧方案：LiteDB 持久化（已废弃）

**文件**：`src/Infrastructure/ZakYip.WheelDiverterSorter.Configuration.Persistence/Repositories/LiteDb/LiteDbRoutePlanRepository.cs`（已删除）

**废弃原因**：
1. ❌ **重复键异常**：时间戳 ParcelId 在高并发时可能重复，LiteDB 唯一索引直接抛异常
2. ❌ **空键异常**：ParcelId <= 0 时插入失败
3. ❌ **性能瓶颈**：磁盘 I/O 导致读写性能低（1-10ms vs 0.001ms）
4. ❌ **运维成本**：需要管理数据库文件、定期清理、监控磁盘空间
5. ❌ **过度设计**：路由计划是临时数据（10-30秒生命周期），持久化无意义

**删除时间**：2025-12-24

**相关文件**（已删除）：
- `LiteDbRoutePlanRepository.cs` - 实现类
- `LiteDbRoutePlanRepositoryTests.cs` - 单元测试

### 2.2 新方案：内存缓存（当前）

**切换时间**：2025-12-24

**优势**：
1. ✅ **彻底解决重复键问题**：自动覆盖，无异常
2. ✅ **性能提升 1000-10000 倍**：内存操作 vs 磁盘 I/O
3. ✅ **自动清理**：3分钟滑动过期，无需手动清理
4. ✅ **异常隔离**：所有操作通过 `ISafeExecutionService` 包裹
5. ✅ **语义正确**：临时数据用临时存储

**详细对比**：参见 `docs/ROUTE_PLAN_STORAGE_COMPARISON.md`

---

## 三、DI 注册

**文件**：`src/Application/ZakYip.WheelDiverterSorter.Application/Extensions/WheelDiverterSorterServiceCollectionExtensions.cs`

**注册代码**：
```csharp
// 注册改口功能服务（使用内存缓存，3分钟滑动过期）
services.AddSingleton<IRoutePlanRepository, InMemoryRoutePlanRepository>();
```

**生命周期**：Singleton（与 `IMemoryCache` 生命周期一致）

---

## 四、使用场景

### 4.1 调用方

**主要调用方**：
1. `SortingOrchestrator`（`src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`）
   - 在上游分配格口后保存路由计划
   - 读取路由计划以支持改口决策

2. `ChangeParcelChuteService`（`src/Application/ZakYip.WheelDiverterSorter.Application/Services/Sorting/ChangeParcelChuteService.cs`）
   - 处理改口请求，更新路由计划

### 4.2 典型流程

```
1. 包裹检测 → 上游分配格口
   ↓
2. SortingOrchestrator 创建 RoutePlan
   ↓
3. SaveAsync(routePlan) → 存入内存缓存（3分钟滑动过期）
   ↓
4. [可选] 上游发送改口通知
   ↓
5. ChangeParcelChuteService 调用 GetByParcelIdAsync()
   ↓
6. 调用 routePlan.TryApplyChuteChange() 判断是否可改口
   ↓
7. SaveAsync(routePlan) → 更新内存缓存
   ↓
8. 包裹完成分拣 → 3分钟后自动从缓存中移除
```

---

## 五、异常排查指南

### 5.1 常见问题

#### 问题1：包裹改口失败

**症状**：上游发送改口通知，但包裹仍按原格口分拣

**排查步骤**：
1. 检查日志：`InMemoryRoutePlanRepository.GetByParcelIdAsync` 是否命中缓存
2. 检查日志：`RoutePlan.TryApplyChuteChange` 返回的决策结果
3. 可能原因：
   - 包裹已完成分拣（`Status = Completed`）
   - 包裹已进入异常路径（`Status = ExceptionRouted`）
   - 超过最后可改口时间（`LastReplanDeadline`）

#### 问题2：内存占用过高

**症状**：系统内存占用持续增长

**排查步骤**：
1. 检查日志：确认路由计划是否正常过期（3分钟后应该被清理）
2. 检查指标：通过 `IMemoryCache` 的监控指标查看缓存条目数量
3. 可能原因：
   - 包裹分拣速度过快，3分钟内积累大量路由计划（正常情况）
   - `IMemoryCache` 配置的 `SizeLimit` 过大

#### 问题3：进程重启后改口失败

**症状**：进程重启后，上游改口请求无法生效

**原因**：内存缓存中的路由计划已丢失（预期行为）

**解决方案**：
- 进程重启后，上游需要重新发送包裹检测通知
- 或者，系统重启后应清空所有待处理包裹，从头开始

### 5.2 日志追踪

**关键日志**：
- `InMemoryRoutePlanRepository.SaveAsync: 成功保存路由计划 ParcelId={ParcelId}, TargetChuteId={ChuteId}, Status={Status}`
- `InMemoryRoutePlanRepository.GetByParcelIdAsync: 缓存命中 ParcelId={ParcelId}`
- `InMemoryRoutePlanRepository.GetByParcelIdAsync: 缓存未命中 ParcelId={ParcelId}`

**日志级别**：
- Info：保存/更新路由计划
- Debug：缓存命中/未命中
- Warning：无效 ParcelId
- Error：通过 `ISafeExecutionService` 捕获的异常

---

## 六、性能指标

### 6.1 预期性能

| 操作 | 响应时间 | 说明 |
|------|---------|------|
| SaveAsync | < 1ms | 内存字典插入操作 |
| GetByParcelIdAsync | < 0.1ms | 内存字典查找操作 |
| DeleteAsync | < 0.5ms | 内存字典删除操作 |

### 6.2 容量估算

**单条路由计划内存占用**：约 200-500 bytes

**示例**：
- 1000 个并发包裹 → 约 500 KB
- 10000 个并发包裹 → 约 5 MB

**结论**：内存占用非常低，即使高并发场景下也不会成为瓶颈

---

## 七、相关文档

- [路由计划存储方案对比](./ROUTE_PLAN_STORAGE_COMPARISON.md) - 内存缓存 vs LiteDB 详细对比
- [Copilot 约束说明](../.github/copilot-instructions.md) - 编码规范和架构约束
- [核心路由逻辑](./CORE_ROUTING_LOGIC.md) - 包裹路由与位置索引队列机制

---

**文档版本**：1.0  
**创建时间**：2025-12-24  
**维护团队**：ZakYip Development Team
