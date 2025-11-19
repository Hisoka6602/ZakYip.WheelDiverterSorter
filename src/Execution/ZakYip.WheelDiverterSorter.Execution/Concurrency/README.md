# 并发控制组件说明

本目录包含直线摆轮分拣系统的并发控制实现。

## 组件概览

### 核心接口

#### IDiverterResourceLock
摆轮资源锁接口，提供读写锁功能。

```csharp
public interface IDiverterResourceLock
{
    string DiverterId { get; }
    Task<IDisposable> AcquireWriteLockAsync(CancellationToken cancellationToken = default);
    Task<IDisposable> AcquireReadLockAsync(CancellationToken cancellationToken = default);
}
```

**用途**：
- 读锁：查询摆轮状态（可多个并发）
- 写锁：控制摆轮动作（互斥访问）

#### IDiverterResourceLockManager
摆轮资源锁管理器接口，管理所有摆轮的锁实例。

```csharp
public interface IDiverterResourceLockManager
{
    IDiverterResourceLock GetLock(string diverterId);
}
```

#### IParcelQueue
包裹队列接口，支持优先级和批量处理。

```csharp
public interface IParcelQueue
{
    int Count { get; }
    Task EnqueueAsync(ParcelQueueItem item, CancellationToken cancellationToken = default);
    Task<ParcelQueueItem> DequeueAsync(CancellationToken cancellationToken = default);
    bool TryDequeue(out ParcelQueueItem? item);
    Task<IReadOnlyList<ParcelQueueItem>> DequeueBatchAsync(int maxBatchSize, CancellationToken cancellationToken = default);
}
```

### 实现类

#### DiverterResourceLock
基于 `ReaderWriterLockSlim` 的锁实现。

**特性**：
- 支持读写分离
- 自动释放（IDisposable）
- 线程安全

#### DiverterResourceLockManager
使用 `ConcurrentDictionary` 管理锁实例。

**特性**：
- 线程安全的锁创建
- 按需创建（lazy initialization）
- 资源自动清理

#### PriorityParcelQueue
基于 `System.Threading.Channels` 的优先级队列实现。

**特性**：
- 线程安全
- 支持容量限制
- 支持批量出队
- 高性能（Channel API）

#### ConcurrentSwitchingPathExecutor
带并发控制的路径执行器，装饰现有执行器。

**特性**：
- 并发限流（SemaphoreSlim）
- 摆轮资源锁
- 超时保护
- 非侵入式设计

### 配置类

#### ConcurrencyOptions
并发控制配置选项。

```csharp
public class ConcurrencyOptions
{
    public int MaxConcurrentParcels { get; set; } = 10;
    public int ParcelQueueCapacity { get; set; } = 100;
    public int MaxBatchSize { get; set; } = 5;
    public bool EnableBatchProcessing { get; set; } = true;
    public int DiverterLockTimeoutMs { get; set; } = 5000;
}
```

### 模型类

#### ParcelQueueItem
包裹队列项，包含包裹信息和优先级。

```csharp
public class ParcelQueueItem
{
    public required string ParcelId { get; init; }
    public required string TargetChuteId { get; init; }
    public int Priority { get; init; } = 0;
    public DateTimeOffset EnqueuedAt { get; init; }
    public TaskCompletionSource<string>? CompletionSource { get; init; }
}
```

### 扩展类

#### ConcurrencyServiceExtensions
依赖注入扩展方法。

```csharp
public static class ConcurrencyServiceExtensions
{
    public static IServiceCollection AddConcurrencyControl(
        this IServiceCollection services,
        IConfiguration configuration);
    
    public static IServiceCollection DecorateWithConcurrencyControl(
        this IServiceCollection services);
}
```

## 使用示例

### 基本使用

```csharp
// 在 Program.cs 中注册
builder.Services.AddConcurrencyControl(builder.Configuration);
builder.Services.DecorateWithConcurrencyControl();
```

### 手动使用锁

```csharp
// 注入锁管理器
private readonly IDiverterResourceLockManager _lockManager;

// 获取锁并使用
var lock = _lockManager.GetLock("D1");
using (await lock.AcquireWriteLockAsync(cancellationToken))
{
    // 控制摆轮 D1
    await diverter.SetAngleAsync(angle, cancellationToken);
}
```

### 使用队列

```csharp
// 注入队列
private readonly IParcelQueue _queue;

// 入队
await _queue.EnqueueAsync(new ParcelQueueItem
{
    ParcelId = "P001",
    TargetChuteId = "C01",
    Priority = 1
}, cancellationToken);

// 出队
var item = await _queue.DequeueAsync(cancellationToken);

// 批量出队（相同目标）
var batch = await _queue.DequeueBatchAsync(maxBatchSize: 5, cancellationToken);
```

## 设计模式

### 装饰器模式
`ConcurrentSwitchingPathExecutor` 使用装饰器模式包装现有执行器，添加并发控制功能。

```
原执行器 → 装饰器（添加并发控制） → 对外接口不变
```

### 工厂模式
`DiverterResourceLockManager` 使用工厂模式按需创建锁实例。

### 资源管理模式
使用 `IDisposable` 确保资源自动释放。

## 性能考虑

### 锁粒度
- **粗粒度**：整个路径一个锁（简单但效率低）
- **细粒度**：每个摆轮一个锁（复杂但效率高）✓ 采用

### 队列选择
- **ConcurrentQueue**: 简单但功能有限
- **PriorityQueue**: 不是线程安全的
- **Channel**: 高性能且线程安全 ✓ 采用

### 并发限制
使用 `SemaphoreSlim` 而非 `Semaphore`：
- 更轻量
- 支持异步
- 性能更好

## 线程安全

所有组件都是线程安全的：

- ✅ `DiverterResourceLock`: ReaderWriterLockSlim
- ✅ `DiverterResourceLockManager`: ConcurrentDictionary
- ✅ `PriorityParcelQueue`: Channel
- ✅ `ConcurrentSwitchingPathExecutor`: SemaphoreSlim

## 测试建议

### 单元测试
1. 测试锁的获取和释放
2. 测试队列的入队出队
3. 测试并发限制

### 集成测试
1. 测试多个包裹同时请求同一摆轮
2. 测试超过并发限制时的排队
3. 测试批量处理功能

### 性能测试
1. 测试高并发场景下的吞吐量
2. 测试锁竞争对性能的影响
3. 测试队列的性能表现

## 故障排查

### 问题：获取锁超时
**可能原因**：
- 摆轮执行时间过长
- DiverterLockTimeoutMs 设置过小
- 存在死锁

**解决方案**：
- 检查摆轮执行逻辑
- 增加超时时间
- 检查日志排查死锁

### 问题：队列满
**可能原因**：
- 处理速度慢于入队速度
- ParcelQueueCapacity 设置过小

**解决方案**：
- 增加并发处理数（MaxConcurrentParcels）
- 增加队列容量
- 优化执行速度

### 问题：系统过载
**可能原因**：
- MaxConcurrentParcels 设置过大
- 硬件资源不足

**解决方案**：
- 降低并发数
- 升级硬件
- 启用批量处理优化

## 相关文档

- [并发控制机制文档](../../CONCURRENCY_CONTROL.md)
- [项目主 README](../../README.md)
