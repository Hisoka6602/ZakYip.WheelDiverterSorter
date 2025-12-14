# 技术债务：API 重组剩余工作

**TD ID**: TD-API-REORG-001  
**创建日期**: 2025-12-14  
**优先级**: 高  
**预估工作量**: 3-4小时  
**状态**: 待开始

## 概述

本 PR 要求完成 API 重组工作，包括：
1. 将报警相关端点迁移到排序控制器
2. 修复通信状态API返回真实数据
3. 新增带缓存的排序统计端点

当前 PR 已完成：
- ✅ 包裹丢失检测基础设施
- ✅ 主动监控服务
- ✅ 优先级规则
- ✅ Orchestrator集成技术债文档

## 剩余工作详细说明

### 1. 迁移报警端点到排序控制器 (30分钟)

#### 1.1 移动端点

**源文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/AlarmsController.cs`  
**目标文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/SortingController.cs`

**需要移动的端点**:
1. `GET /api/Alarms/sorting-failure-rate` (第64-83行)
2. `POST /api/Alarms/reset-statistics` (第144-160行)

#### 1.2 实施步骤

**Step 1**: 在 `SortingController` 中注入 `AlarmService`

```csharp
// 在 SortingController.cs 构造函数中添加
private readonly AlarmService _alarmService;

public SortingController(
    // ... 现有参数
    AlarmService alarmService)
{
    // ... 现有初始化
    _alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));
}
```

**Step 2**: 添加失败率端点

```csharp
/// <summary>
/// 获取当前分拣失败率
/// </summary>
/// <returns>分拣失败率统计信息</returns>
/// <response code="200">成功返回失败率</response>
/// <response code="500">服务器内部错误</response>
/// <remarks>
/// 返回当前时间窗口内的分拣失败率，包括小数值和百分比形式
/// </remarks>
[HttpGet("failure-rate")]
[SwaggerOperation(
    Summary = "获取当前分拣失败率",
    Description = "返回系统当前的分拣失败率统计，包括失败率小数值和百分比表示",
    OperationId = "GetSortingFailureRate",
    Tags = new[] { "分拣管理" }
)]
[SwaggerResponse(200, "成功返回失败率", typeof(object))]
[SwaggerResponse(500, "服务器内部错误")]
[ProducesResponseType(typeof(object), 200)]
[ProducesResponseType(typeof(object), 500)]
public ActionResult<object> GetFailureRate()
{
    try
    {
        var failureRate = _alarmService.GetSortingFailureRate();
        return Ok(new
        {
            failureRate,
            percentage = $"{failureRate * 100:F2}%"
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "获取分拣失败率失败");
        return StatusCode(500, new { message = "获取分拣失败率失败" });
    }
}
```

**Step 3**: 添加重置统计端点

```csharp
/// <summary>
/// 重置分拣统计计数器
/// </summary>
/// <returns>操作结果</returns>
/// <response code="200">成功重置统计计数器</response>
/// <response code="500">服务器内部错误</response>
/// <remarks>
/// 重置分拣成功/失败计数器，用于开始新的统计周期或测试场景
/// </remarks>
[HttpPost("reset-statistics")]
[SwaggerOperation(
    Summary = "重置分拣统计计数器",
    Description = "清除当前的分拣统计数据，包括成功和失败计数器，通常用于开始新的统计周期",
    OperationId = "ResetSortingStatistics",
    Tags = new[] { "分拣管理" }
)]
[SwaggerResponse(200, "成功重置统计计数器", typeof(object))]
[SwaggerResponse(500, "服务器内部错误")]
[ProducesResponseType(typeof(object), 200)]
[ProducesResponseType(typeof(object), 500)]
public ActionResult ResetStatistics()
{
    try
    {
        _alarmService.ResetSortingStatistics();
        _logger.LogInformation("分拣统计计数器已重置 / Sorting statistics reset");
        return Ok(new { message = "统计计数器已重置 / Statistics reset" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "重置统计失败");
        return StatusCode(500, new { message = "重置统计失败" });
    }
}
```

**Step 4**: 从 `AlarmsController` 中删除这两个端点

删除 `AlarmsController.cs` 第64-83行和第144-160行的代码。

**Step 5**: 更新测试

更新 `tests/ZakYip.WheelDiverterSorter.Host.IntegrationTests/AllApiEndpointsTests.cs`:
- 将 `/api/Alarms/sorting-failure-rate` 改为 `/api/sorting/failure-rate`
- 将 `/api/Alarms/reset-statistics` 改为 `/api/sorting/reset-statistics`

---

### 2. 修复通信状态API (1小时)

#### 2.1 问题分析

当前 `GET /api/communication/status` 返回的 `messagesSent` 和 `messagesReceived` 可能为 0，因为：
1. `CommunicationStatsService` 有正确的实现
2. 但可能没有在发送/接收消息时调用 `IncrementSent()` 和 `IncrementReceived()`

#### 2.2 验证调用点

需要检查以下文件中是否调用了统计方法：

**发送消息处**:
- `src/Communication/ZakYip.WheelDiverterSorter.Communication/Gateways/TcpRuleEngineClient.cs`
- `src/Communication/ZakYip.WheelDiverterSorter.Communication/Gateways/SignalRRuleEngineClient.cs`
- `src/Communication/ZakYip.WheelDiverterSorter.Communication/Gateways/MqttRuleEngineClient.cs`

**接收消息处**:
- 同上，接收处理方法

#### 2.3 实施步骤

**Step 1**: 在通信客户端中注入 `ICommunicationStatsService`

检查每个客户端实现是否已注入 `ICommunicationStatsService`。

**Step 2**: 在发送消息方法中调用

```csharp
public async Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken)
{
    try
    {
        // ... 发送逻辑
        
        // ✅ 增加发送计数
        _statsService.IncrementSent();
        
        return true;
    }
    catch
    {
        return false;
    }
}
```

**Step 3**: 在接收消息处理中调用

```csharp
private void OnMessageReceived(ChuteAssignmentNotification notification)
{
    // ✅ 增加接收计数
    _statsService.IncrementReceived();
    
    // ... 处理逻辑
}
```

**Step 4**: 验证连接/断开记录

确保在连接建立和断开时调用：
```csharp
// 连接时
_statsService.RecordConnected();

// 断开时
_statsService.RecordDisconnected();
```

---

### 3. 新增排序统计端点 (1.5-2小时)

#### 3.1 创建统计服务

**文件**: `src/Application/ZakYip.WheelDiverterSorter.Application/Services/Metrics/SortingStatisticsService.cs`

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ZakYip.WheelDiverterSorter.Application.Services.Metrics;

/// <summary>
/// 分拣统计服务接口
/// </summary>
public interface ISortingStatisticsService
{
    /// <summary>
    /// 分拣成功数量
    /// </summary>
    long SuccessCount { get; }
    
    /// <summary>
    /// 分拣超时数量
    /// </summary>
    long TimeoutCount { get; }
    
    /// <summary>
    /// 包裹丢失数量
    /// </summary>
    long LostCount { get; }
    
    /// <summary>
    /// 受影响包裹数量（因其他包裹丢失而被重路由到异常口）
    /// </summary>
    long AffectedCount { get; }
    
    /// <summary>
    /// 增加成功计数
    /// </summary>
    void IncrementSuccess();
    
    /// <summary>
    /// 增加超时计数
    /// </summary>
    void IncrementTimeout();
    
    /// <summary>
    /// 增加丢失计数
    /// </summary>
    void IncrementLost();
    
    /// <summary>
    /// 增加受影响计数
    /// </summary>
    /// <param name="count">受影响的包裹数量</param>
    void IncrementAffected(int count = 1);
    
    /// <summary>
    /// 重置所有计数器
    /// </summary>
    void Reset();
}

/// <summary>
/// 分拣统计服务 - 使用原子操作保证线程安全，支持超高并发
/// </summary>
public class SortingStatisticsService : ISortingStatisticsService
{
    private long _successCount;
    private long _timeoutCount;
    private long _lostCount;
    private long _affectedCount;
    
    public long SuccessCount => Interlocked.Read(ref _successCount);
    public long TimeoutCount => Interlocked.Read(ref _timeoutCount);
    public long LostCount => Interlocked.Read(ref _lostCount);
    public long AffectedCount => Interlocked.Read(ref _affectedCount);
    
    public void IncrementSuccess() => Interlocked.Increment(ref _successCount);
    public void IncrementTimeout() => Interlocked.Increment(ref _timeoutCount);
    public void IncrementLost() => Interlocked.Increment(ref _lostCount);
    public void IncrementAffected(int count = 1) => Interlocked.Add(ref _affectedCount, count);
    
    public void Reset()
    {
        Interlocked.Exchange(ref _successCount, 0);
        Interlocked.Exchange(ref _timeoutCount, 0);
        Interlocked.Exchange(ref _lostCount, 0);
        Interlocked.Exchange(ref _affectedCount, 0);
    }
}
```

#### 3.2 注册服务

**文件**: `src/Application/ZakYip.WheelDiverterSorter.Application/ApplicationServiceExtensions.cs`

```csharp
// 注册为单例以确保计数器全局唯一
services.AddSingleton<ISortingStatisticsService, SortingStatisticsService>();
```

#### 3.3 在SortingController中添加端点

```csharp
/// <summary>
/// 获取排序统计数据
/// </summary>
/// <returns>排序统计信息</returns>
/// <response code="200">成功返回统计数据</response>
/// <response code="500">服务器内部错误</response>
/// <remarks>
/// 返回分拣系统的实时统计数据，包括：
/// - successCount: 分拣成功数量
/// - timeoutCount: 分拣超时数量（包裹延迟但仍被导向异常口）
/// - lostCount: 包裹丢失数量（包裹物理丢失，从队列删除）
/// - affectedCount: 受影响包裹数量（因其他包裹丢失而被重路由到异常口）
/// 
/// 统计数据：
/// - 永久存储在内存中，不过期
/// - 使用原子操作保证线程安全
/// - 支持超高并发查询（无锁设计）
/// - 可通过 POST /api/sorting/reset-statistics 重置
/// 
/// 示例响应：
/// ```json
/// {
///   "successCount": 12345,
///   "timeoutCount": 23,
///   "lostCount": 5,
///   "affectedCount": 8,
///   "timestamp": "2025-12-14T12:00:00Z"
/// }
/// ```
/// </remarks>
[HttpGet("statistics")]
[SwaggerOperation(
    Summary = "获取排序统计数据",
    Description = "返回分拣系统的实时统计数据（成功/超时/丢失/受影响），支持超高并发查询",
    OperationId = "GetSortingStatistics",
    Tags = new[] { "分拣管理" }
)]
[SwaggerResponse(200, "成功返回统计数据", typeof(SortingStatisticsDto))]
[SwaggerResponse(500, "服务器内部错误")]
[ProducesResponseType(typeof(SortingStatisticsDto), 200)]
[ProducesResponseType(typeof(object), 500)]
public ActionResult<SortingStatisticsDto> GetStatistics()
{
    try
    {
        var stats = new SortingStatisticsDto
        {
            SuccessCount = _statisticsService.SuccessCount,
            TimeoutCount = _statisticsService.TimeoutCount,
            LostCount = _statisticsService.LostCount,
            AffectedCount = _statisticsService.AffectedCount,
            Timestamp = _clock.LocalNow
        };
        
        return Ok(stats);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "获取分拣统计数据失败");
        return StatusCode(500, new { message = "获取分拣统计数据失败" });
    }
}
```

#### 3.4 创建DTO

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Models/SortingStatisticsDto.cs`

```csharp
namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 分拣统计数据DTO
/// </summary>
public record SortingStatisticsDto
{
    /// <summary>
    /// 分拣成功数量
    /// </summary>
    public required long SuccessCount { get; init; }
    
    /// <summary>
    /// 分拣超时数量（包裹延迟但仍导向异常口）
    /// </summary>
    public required long TimeoutCount { get; init; }
    
    /// <summary>
    /// 包裹丢失数量（包裹物理丢失，从队列删除）
    /// </summary>
    public required long LostCount { get; init; }
    
    /// <summary>
    /// 受影响包裹数量（因其他包裹丢失而被重路由到异常口）
    /// </summary>
    public required long AffectedCount { get; init; }
    
    /// <summary>
    /// 统计时间戳
    /// </summary>
    public required DateTime Timestamp { get; init; }
}
```

#### 3.5 集成到分拣流程

在 `SortingOrchestrator` 或相关服务中调用统计方法：

```csharp
// 分拣成功时
_statisticsService.IncrementSuccess();

// 分拣超时时
_statisticsService.IncrementTimeout();

// 包裹丢失时
_statisticsService.IncrementLost();

// 批量重路由时
_statisticsService.IncrementAffected(affectedParcelCount);
```

---

### 4. 更新reset-statistics端点 (15分钟)

当前 `reset-statistics` 端点只重置 AlarmService 的计数器，需要同时重置 SortingStatisticsService。

```csharp
[HttpPost("reset-statistics")]
public ActionResult ResetStatistics()
{
    try
    {
        _alarmService.ResetSortingStatistics();
        _statisticsService.Reset();  // ✅ 新增：同时重置详细统计
        
        _logger.LogInformation("分拣统计计数器已重置（包含失败率和详细统计）");
        return Ok(new { message = "统计计数器已重置 / Statistics reset" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "重置统计失败");
        return StatusCode(500, new { message = "重置统计失败" });
    }
}
```

---

## 测试要求

### 单元测试

**文件**: `tests/ZakYip.WheelDiverterSorter.Application.Tests/Services/Metrics/SortingStatisticsServiceTests.cs`

```csharp
[Fact]
public void IncrementSuccess_ShouldIncreaseCount()
{
    var service = new SortingStatisticsService();
    service.IncrementSuccess();
    Assert.Equal(1, service.SuccessCount);
}

[Fact]
public void IncrementAffected_WithCount_ShouldIncreaseByCount()
{
    var service = new SortingStatisticsService();
    service.IncrementAffected(5);
    Assert.Equal(5, service.AffectedCount);
}

[Fact]
public void ConcurrentIncrements_ShouldMaintainCorrectCount()
{
    var service = new SortingStatisticsService();
    var tasks = Enumerable.Range(0, 1000)
        .Select(_ => Task.Run(() => service.IncrementSuccess()))
        .ToArray();
    
    Task.WaitAll(tasks);
    Assert.Equal(1000, service.SuccessCount);
}
```

### 集成测试

**文件**: `tests/ZakYip.WheelDiverterSorter.Host.IntegrationTests/SortingControllerStatisticsTests.cs`

```csharp
[Fact]
public async Task GetStatistics_ShouldReturnCorrectData()
{
    // Arrange: 模拟一些分拣操作
    var statsService = _factory.Services.GetRequiredService<ISortingStatisticsService>();
    statsService.IncrementSuccess();
    statsService.IncrementSuccess();
    statsService.IncrementTimeout();
    
    // Act
    var response = await _client.GetAsync("/api/sorting/statistics");
    
    // Assert
    response.EnsureSuccessStatusCode();
    var stats = await response.Content.ReadFromJsonAsync<SortingStatisticsDto>();
    Assert.Equal(2, stats.SuccessCount);
    Assert.Equal(1, stats.TimeoutCount);
}

[Fact]
public async Task ResetStatistics_ShouldClearAllCounters()
{
    // Arrange
    var statsService = _factory.Services.GetRequiredService<ISortingStatisticsService>();
    statsService.IncrementSuccess();
    
    // Act
    var response = await _client.PostAsync("/api/sorting/reset-statistics", null);
    
    // Assert
    response.EnsureSuccessStatusCode();
    Assert.Equal(0, statsService.SuccessCount);
}
```

### API端点测试

更新 `AllApiEndpointsTests.cs`:
- 验证 `/api/sorting/failure-rate` 可访问
- 验证 `/api/sorting/reset-statistics` 可访问
- 验证 `/api/sorting/statistics` 可访问
- 移除 `/api/Alarms/sorting-failure-rate` 和 `/api/Alarms/reset-statistics`

---

## 验收标准

- [ ] `/api/sorting/failure-rate` 端点工作正常
- [ ] `/api/sorting/reset-statistics` 端点工作正常
- [ ] `/api/sorting/statistics` 端点返回正确数据
- [ ] `/api/communication/status` 返回真实的 messagesSent 和 messagesReceived
- [ ] `/api/Alarms/sorting-failure-rate` 已删除
- [ ] `/api/Alarms/reset-statistics` 已删除
- [ ] 单元测试覆盖率 > 80%
- [ ] 集成测试验证完整流程
- [ ] 并发测试：1000个并发请求 < 100ms响应时间
- [ ] 所有 API 端点测试通过
- [ ] Swagger 文档更新正确

---

## 性能考虑

### 统计服务性能优化

**设计亮点**:
1. **无锁设计**: 使用 `Interlocked` 原子操作，无需锁
2. **内存缓存**: 数据存储在内存中，读取O(1)
3. **单例模式**: 全局唯一实例，避免重复创建
4. **简单数据结构**: 仅存储4个 `long` 计数器

**性能指标**（预期）:
- 读取延迟: < 1µs
- 写入延迟: < 10µs
- 并发支持: > 10,000 QPS
- 内存占用: < 100 bytes

---

## 下一步行动

1. 实施端点迁移（30分钟）
2. 修复通信状态（1小时）
3. 实现统计服务和端点（2小时）
4. 编写单元测试（30分钟）
5. 编写集成测试（30分钟）
6. 更新API文档（15分钟）
7. 性能测试和优化（15分钟）

**总计**: 约4-5小时

---

## 参考资料

- 当前PR已完成工作
- `AlarmService.cs` - 现有失败率实现
- `CommunicationStatsService.cs` - 现有统计服务模式
- `SortingController.cs` - 目标控制器
- `AlarmsController.cs` - 源控制器
