# 架构原则 (Architecture Principles)

本文档定义了 ZakYip.WheelDiverterSorter 项目的核心架构原则和设计约束。

**重要**: 违反这些原则的 PR 不得合并到主分支。

---

## 目录

1. [核心业务原则](#核心业务原则)
2. [分层架构原则](#分层架构原则)
3. [通讯与重试原则](#通讯与重试原则)
4. [并发与性能原则](#并发与性能原则)
5. [测试与质量保证原则](#测试与质量保证原则)
6. [可观测性原则](#可观测性原则)
7. [安全性原则](#安全性原则)
8. [持久化与序列化原则](#8-持久化与序列化原则)

---

## 核心业务原则

### 1. Parcel-First 分拣流程（不可破坏）

**原则**: 必须先通过感应 IO 在本地创建包裹并生成 ParcelId，再向上游发送携带 ParcelId 的路由请求。

**业务逻辑**:
```
传感器触发 → 创建包裹（生成 ParcelId）→ 向上游请求路由（携带 ParcelId）→ 生成路径 → 执行分拣
```

**禁止行为**:
- ❌ 先向上游请求路由，再创建包裹
- ❌ 无本地包裹实体而向上游要路由
- ❌ 创建包裹后不携带 ParcelId 请求路由

**实施要求**:

```csharp
// ✅ 正确：Parcel-First 流程
public async Task OnSensorTriggeredAsync(string sensorId)
{
    // 1. 先创建本地包裹
    var parcel = await CreateParcelFromSensorAsync(sensorId);
    _logger.LogInformation($"包裹已创建: {parcel.ParcelId}");
    
    // 2. 再向上游请求路由（携带 ParcelId）
    var chuteId = await RequestRoutingFromUpstreamAsync(parcel.ParcelId);
    
    // 3. 生成路径并执行
    var path = _pathGenerator.GeneratePath(chuteId);
    await _executor.ExecutePathAsync(path);
}

// ❌ 错误：违反 Parcel-First 原则
public async Task OnSensorTriggeredAsync(string sensorId)
{
    // ❌ 错误：先请求路由，没有 ParcelId
    var chuteId = await RequestRoutingFromUpstreamAsync();
    
    // ❌ 错误：再创建包裹
    var parcel = CreateParcel(sensorId, chuteId);
}
```

**原因**:
- 确保包裹追踪的完整性（从创建到完成的完整生命周期）
- 便于异常处理和问题定位（每个包裹有唯一 ID）
- 符合真实物理流程（包裹先进入系统，再分配目标）

**相关文档**: [PR42_PARCEL_FIRST_SPECIFICATION.md](../PR42_PARCEL_FIRST_SPECIFICATION.md)

### 2. 零错分不变量（最高优先级）

**原则**: 系统在任何异常情况下，**必须保证不会将包裹分拣到错误的格口**。

**不变量定义**:
```
sorting_mis_sort_total = 0  // 必须始终为 0
```

**实施规则**:
- 所有路径执行失败必须立即路由到异常格口（`ExceptionChuteId`）
- 无法确定目标格口时，必须路由到异常格口
- 传感器证据链不完整时，必须标记为失败（`Timeout` / `Dropped` / `Error`）
- **严格禁止**将包裹分拣到非目标且非异常的格口

**异常场景处理**:

| 异常场景 | 处理策略 |
|---------|----------|
| RuleEngine 连接失败 | 立即路由到异常格口 |
| 格口分配超时（默认 10 秒） | 自动路由到异常格口 |
| 格口未配置 | 自动路由到异常格口 |
| 路径生成失败 | 自动路由到异常格口 |
| 摆轮控制失败 | 触发纠错，路由到异常格口 |
| 路径执行超时（TTL） | 触发纠错，路由到异常格口 |
| 传感器故障 | 记录告警，路由到异常格口 |

**实施要求**:

```csharp
// ✅ 正确：零错分保证
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    try
    {
        var chuteId = await DetermineChuteAsync(parcelId);
        var path = GeneratePathOrThrow(chuteId);
        await ExecutePathAsync(path);
        
        return new SortingResult 
        { 
            IsSuccess = true, 
            ActualChute = chuteId,
            Status = ParcelStatus.SortedToTargetChute
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"包裹 {parcelId} 处理失败，路由到异常格口");
        
        // ✅ 路由到异常格口，确保零错分
        await RouteToExceptionChuteAsync(parcelId);
        
        return new SortingResult
        {
            IsSuccess = false,
            ActualChute = _config.ExceptionChuteId,
            Status = ParcelStatus.Error,
            ErrorMessage = ex.Message
        };
    }
}

// ❌ 错误：可能导致错分
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    try
    {
        var chuteId = await DetermineChuteAsync(parcelId);
        var path = GeneratePath(chuteId);  // 可能返回 null
        
        if (path == null)
        {
            // ❌ 错误：随机选择一个格口
            chuteId = GetRandomChute();
            path = GeneratePath(chuteId);
        }
        
        await ExecutePathAsync(path);
        return new SortingResult { IsSuccess = true, ActualChute = chuteId };
    }
    catch
    {
        // ❌ 错误：吞掉异常，未路由到异常格口
        return new SortingResult { IsSuccess = false };
    }
}
```

**相关文档**: 
- [PATH_FAILURE_DETECTION_GUIDE.md](../PATH_FAILURE_DETECTION_GUIDE.md)
- [ERROR_CORRECTION_MECHANISM.md](../ERROR_CORRECTION_MECHANISM.md)

### 3. 本地时间统一原则

**原则**: 系统内部所有业务时间统一使用本地时间（通过 `ISystemClock.LocalNow` 获取），不使用 UTC 时间。

**时间使用规范**:
- 业务时间（日志、记录、显示）：使用 `ISystemClock.LocalNow`
- 外部系统协议要求：使用 `ISystemClock.UtcNow`
- **严格禁止**：直接使用 `DateTime.Now` / `DateTime.UtcNow`

**原因**:
- 运维人员需要直观的本地时间进行故障排查
- 日志、指标、数据库记录统一使用本地时间，便于关联分析
- 避免时区转换导致的混乱和错误
- 便于单元测试（可以 Mock `ISystemClock`）

**实施要求**:

```csharp
// ✅ 正确：使用 ISystemClock
public class ParcelCreationService
{
    private readonly ISystemClock _clock;
    
    public Parcel CreateParcel(string sensorId)
    {
        return new Parcel
        {
            ParcelId = GenerateId(),
            CreatedAt = _clock.LocalNow,  // ✅ 本地时间
            SensorId = sensorId
        };
    }
    
    public void LogEvent(string message)
    {
        _logger.LogInformation(
            "[{Timestamp}] {Message}",
            _clock.LocalNow.ToString("yyyy-MM-dd HH:mm:ss"),
            message);
    }
}

// ❌ 错误：直接使用 DateTime
public Parcel CreateParcel(string sensorId)
{
    return new Parcel
    {
        ParcelId = GenerateId(),
        CreatedAt = DateTime.Now  // ❌ 禁止
    };
}
```

**相关文档**: [SYSTEM_CONFIG_GUIDE.md - 系统时间说明](../SYSTEM_CONFIG_GUIDE.md)

---

## 分层架构原则

### 1. 分层职责清晰（DDD 分层）

**架构层次**:

```
┌─────────────────────────────────────┐
│        Host（应用宿主层）             │
│  - 依赖注入配置                       │
│  - API Controller 壳                 │
│  - 配置绑定                           │
└─────────────────────────────────────┘
              ↓ 依赖
┌─────────────────────────────────────┐
│   Application / Execution（应用层）   │
│  - 业务编排                           │
│  - 路径执行                           │
│  - 并发控制                           │
└─────────────────────────────────────┘
              ↓ 依赖
┌─────────────────────────────────────┐
│         Core（核心领域层）            │
│  - 领域模型                           │
│  - 路径生成                           │
│  - 业务规则                           │
└─────────────────────────────────────┘
              ↓ 依赖
┌─────────────────────────────────────┐
│    Drivers / Ingress（基础设施层）    │
│  - 硬件驱动抽象                       │
│  - 传感器管理                         │
│  - 外部系统通信                       │
└─────────────────────────────────────┘
```

**层次依赖规则**:
- 上层可以依赖下层
- 下层**不得**依赖上层
- 同层之间通过接口通信

### 2. Host 层职责（严格限制）

**允许的职责**:
- ✅ 依赖注入配置（`Program.cs` / `Startup.cs`）
- ✅ API Controller 端点定义（仅调用服务）
- ✅ 配置绑定（`appsettings.json` → Configuration 对象）
- ✅ 中间件配置（日志、认证、CORS 等）

**禁止的行为**:
- ❌ 直接包含业务逻辑
- ❌ 直接访问仓储 / 数据库
- ❌ 直接访问硬件驱动
- ❌ 复杂的数据处理和转换

**实施要求**:

```csharp
// ✅ 正确：Host 层 Controller 只调用服务
[ApiController]
[Route("api/sorting")]
public class SortingController : ControllerBase
{
    private readonly IParcelSortingOrchestrator _orchestrator;
    
    public SortingController(IParcelSortingOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }
    
    [HttpPost("process")]
    public async Task<ActionResult<ApiResponse<SortingResultDto>>> ProcessParcel(
        [FromBody] ProcessParcelRequest request)
    {
        // ✅ 只调用服务，不包含业务逻辑
        var result = await _orchestrator.ProcessParcelAsync(request.ParcelId);
        return Ok(ApiResponse.Success(result));
    }
}

// ❌ 错误：Host 层包含业务逻辑
[ApiController]
[Route("api/sorting")]
public class SortingController : ControllerBase
{
    private readonly IParcelRepository _parcelRepo;
    private readonly IDiverterDriver _driver;
    
    [HttpPost("process")]
    public async Task<ActionResult<SortingResultDto>> ProcessParcel(
        [FromBody] ProcessParcelRequest request)
    {
        // ❌ 错误：业务逻辑在 Controller 中
        var parcel = await _parcelRepo.GetByIdAsync(request.ParcelId);
        if (parcel == null)
        {
            parcel = new Parcel { ParcelId = request.ParcelId };
            await _parcelRepo.SaveAsync(parcel);
        }
        
        // ❌ 错误：直接访问硬件驱动
        await _driver.SetDirectionAsync("D1", Direction.Left);
        
        return Ok(new SortingResultDto { IsSuccess = true });
    }
}
```

### 3. Core / Application 层负责业务逻辑

**规则**: 所有业务规则、领域逻辑必须放在 `Core` 或 `Application` / `Execution` 层。

**Core 层职责**:
- 领域模型定义（`Parcel`, `SwitchingPath`, `ChuteConfiguration` 等）
- 领域服务（`ISwitchingPathGenerator`, `IPathFailureHandler` 等）
- 业务规则验证
- 仓储接口定义

**Application / Execution 层职责**:
- 业务流程编排（`IParcelSortingOrchestrator`）
- 路径执行管理（`ISwitchingPathExecutor`）
- 并发控制（摆轮资源锁、包裹队列）
- 事务管理

### 4. Drivers 层必须通过接口访问

**规则**: 所有硬件相关操作必须通过 `Drivers` 层的接口访问，便于对接多厂商设备和测试。

**支持的驱动类型**:
- 雷赛控制器（`LeadshineDiverterDriver`）
- 西门子 S7（`S7DiverterDriver`）
- 模拟驱动（`MockDiverterDriver` / `SimulatorDiverterDriver`）
- 其他厂商（通过接口扩展）

**实施要求**:

```csharp
// ✅ 正确：通过接口依赖
public class PathExecutor : ISwitchingPathExecutor
{
    private readonly IDiverterDriver _driver;  // 接口依赖
    private readonly ISensorDriver _sensorDriver;
    
    public PathExecutor(IDiverterDriver driver, ISensorDriver sensorDriver)
    {
        _driver = driver;  // 可以注入任何实现
        _sensorDriver = sensorDriver;
    }
    
    public async Task<PathExecutionResult> ExecuteAsync(SwitchingPath path)
    {
        foreach (var segment in path.Segments)
        {
            await _driver.SetDirectionAsync(segment.DiverterId, segment.Direction);
            await WaitForSensorTriggerAsync(segment.DiverterId);
        }
        return PathExecutionResult.Success();
    }
}

// ❌ 错误：直接依赖具体实现
public class PathExecutor
{
    private readonly LeadshineDiverterDriver _driver;  // ❌ 依赖具体类
    
    public async Task ExecuteAsync(SwitchingPath path)
    {
        // 无法切换到其他厂商驱动
        // 无法使用 Mock 进行测试
    }
}
```

**相关文档**: [VENDOR_EXTENSION_GUIDE.md](../VENDOR_EXTENSION_GUIDE.md)

---

## 通讯与重试原则

### 1. 客户端无限重连 + 2 秒最大退避（硬编码）

**原则**: 与上游系统（RuleEngine）的连接采用**客户端模式无限重试**，最大退避时间**硬编码为 2 秒**。

**重试策略**:
```
初始退避: 200ms
指数增长: 200ms → 400ms → 800ms → 1600ms → 2000ms（最大）
持续退避: 2000ms, 2000ms, 2000ms, ...（无限重试）
```

**实施要求**:

```csharp
// ✅ 正确：无限重试，最大退避 2 秒
public class UpstreamConnectionManager
{
    private const int InitialBackoffMs = 200;
    private const int MaxBackoffMs = 2000;  // 硬编码上限
    
    public async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        int backoffMs = InitialBackoffMs;
        int attemptCount = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                attemptCount++;
                _logger.LogInformation($"尝试连接上游系统（第 {attemptCount} 次）...");
                
                await ConnectAsync();
                
                _logger.LogInformation("成功连接到上游系统");
                return;
            }
            catch (Exception ex)
            {
                if (_logDeduplicator.ShouldLog(LogLevel.Warning, "UpstreamConnectionFailed"))
                {
                    _logger.LogWarning($"连接失败，{backoffMs}ms 后重试: {ex.Message}");
                    _logDeduplicator.RecordLog(LogLevel.Warning, "UpstreamConnectionFailed");
                }
                
                await Task.Delay(backoffMs, cancellationToken);
                
                // 指数退避，但不超过 2 秒
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
        }
    }
}

// ❌ 错误：有限重试或退避时间过长
public async Task ConnectWithRetryAsync()
{
    for (int i = 0; i < 5; i++)  // ❌ 有限重试
    {
        try
        {
            await ConnectAsync();
            return;
        }
        catch
        {
            await Task.Delay(10000);  // ❌ 退避时间过长（10 秒）
        }
    }
    throw new Exception("连接失败");  // ❌ 放弃重试
}
```

**原因**:
- 保证系统在上游暂时不可用时能够自动恢复
- 2 秒退避时间平衡了网络压力和恢复速度
- 无限重试确保系统最终能够连接成功

**相关文档**: [PR38_IMPLEMENTATION_SUMMARY.md](../PR38_IMPLEMENTATION_SUMMARY.md)

### 2. 发送失败不重试，只记录日志

**原则**: 发送失败**只记录日志**，不进行自动重试，当前包裹自动路由到异常格口。

**实施要求**:

```csharp
// ✅ 正确：发送失败只记录日志
public async Task<bool> SendRoutingRequestAsync(string parcelId)
{
    try
    {
        var request = new RoutingRequest { ParcelId = parcelId };
        await _client.SendAsync(request);
        
        _logger.LogInformation($"路由请求已发送: {parcelId}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"发送路由请求失败，包裹 {parcelId} 将路由到异常格口");
        return false;  // ✅ 不重试，由调用方处理
    }
}

// ❌ 错误：自动重试
public async Task<bool> SendRoutingRequestAsync(string parcelId)
{
    for (int i = 0; i < 3; i++)  // ❌ 不应自动重试
    {
        try
        {
            await _client.SendAsync(new RoutingRequest { ParcelId = parcelId });
            return true;
        }
        catch (Exception ex)
        {
            if (i < 2)
            {
                await Task.Delay(1000);  // ❌ 重试会阻塞包裹处理
                continue;
            }
        }
    }
    return false;
}
```

**原因**:
- 避免阻塞包裹处理流程
- 快速失败，确保包裹及时路由到异常格口
- 由业务层决定如何处理失败（路由到异常格口）

### 3. 支持 Client / Server 双模式

**原则**: 系统必须支持作为客户端或服务器与上游 RuleEngine 通信。

**支持的协议**:
- TCP (Client / Server)
- SignalR (Client)
- MQTT (Client / Server)
- HTTP (Client)

**相关文档**: 
- [COMMUNICATION_INTEGRATION.md](../COMMUNICATION_INTEGRATION.md)
- [UPSTREAM_CONNECTION_GUIDE.md](../UPSTREAM_CONNECTION_GUIDE.md)

---

## 并发与性能原则

### 1. 线程安全容器强制要求

**原则**: 任何跨线程共享的集合必须使用线程安全容器或明确的锁封装。

**线程安全容器**:
- `ConcurrentDictionary<TKey, TValue>` - 线程安全字典
- `ConcurrentQueue<T>` - 线程安全队列
- `ConcurrentBag<T>` - 线程安全集合
- `ImmutableList<T>` / `ImmutableDictionary<TKey, TValue>` - 不可变集合

**实施要求**:

```csharp
// ✅ 正确：使用线程安全容器
public class ParcelTracker
{
    private readonly ConcurrentDictionary<string, ParcelState> _parcels = new();
    
    public void UpdateParcelState(string parcelId, ParcelState state)
    {
        _parcels.AddOrUpdate(parcelId, state, (_, __) => state);
    }
    
    public bool TryGetParcelState(string parcelId, out ParcelState? state)
    {
        return _parcels.TryGetValue(parcelId, out state);
    }
}

// ✅ 正确：使用明确的锁
public class ParcelTracker
{
    private readonly Dictionary<string, ParcelState> _parcels = new();
    private readonly object _lock = new();
    
    public void UpdateParcelState(string parcelId, ParcelState state)
    {
        lock (_lock)
        {
            _parcels[parcelId] = state;
        }
    }
    
    public bool TryGetParcelState(string parcelId, out ParcelState? state)
    {
        lock (_lock)
        {
            return _parcels.TryGetValue(parcelId, out state);
        }
    }
}

// ❌ 错误：非线程安全
public class ParcelTracker
{
    private readonly Dictionary<string, ParcelState> _parcels = new();
    
    public void UpdateParcelState(string parcelId, ParcelState state)
    {
        _parcels[parcelId] = state;  // ❌ 多线程不安全
    }
}
```

**相关文档**: [CONCURRENCY_CONTROL.md](../CONCURRENCY_CONTROL.md)

### 2. SafeExecutionService 强制使用

**原则**: 所有后台服务（`BackgroundService`）的主循环必须使用 `ISafeExecutionService` 包裹。

**实施要求**:

```csharp
// ✅ 正确：使用 SafeExecutionService
public class PackageSortingWorker : BackgroundService
{
    private readonly ISafeExecutionService _safeExecutor;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ProcessNextParcel();
                }
            },
            operationName: "PackageSortingLoop",
            cancellationToken: stoppingToken
        );
    }
}

// ❌ 错误：未使用 SafeExecutionService
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ProcessNextParcel();  // 异常可能导致进程崩溃
    }
}
```

**原因**:
- 防止未捕获异常导致整个 Host 进程崩溃
- 统一的异常处理和日志记录
- 生产环境高可用性保证

**相关文档**: [PR37_IMPLEMENTATION_SUMMARY.md](../PR37_IMPLEMENTATION_SUMMARY.md)

### 3. 摆轮资源锁保护

**原则**: 摆轮资源必须通过分布式锁或本地锁保护，防止并发冲突。

**实施规则**:
- 同一时刻只有一个包裹可以占用特定摆轮
- 路径执行前必须获取所有摆轮的锁
- 路径执行完成后必须释放锁
- 锁超时自动释放（基于 TTL）

**相关文档**: 
- [CONCURRENCY_CONTROL.md](../CONCURRENCY_CONTROL.md)
- [EMC_DISTRIBUTED_LOCK.md](../EMC_DISTRIBUTED_LOCK.md)

---

## 测试与质量保证原则

### 1. 仿真和 E2E 测试必须通过

**原则**: 任何对核心业务逻辑、通讯层、硬件驱动、面板控制的修改，都必须确保现有测试通过。

**测试覆盖要求**:
- ✅ 单元测试：验证单个组件的正确性
- ✅ 集成测试：验证组件间的交互
- ✅ E2E 测试：验证完整的分拣流程
- ✅ 仿真测试：验证各种异常场景

**E2E 测试场景**:
```
API 配置启动 IO → 面板启动 → 创建包裹 → 上游路由 → 摆轮分拣 → 落格
```

**实施要求**:

```bash
# 提交前必须运行所有测试
dotnet test

# 确保所有测试通过
# Test run for /path/to/test.dll (.NETCoreApp,Version=v8.0)
# Total tests: 120     Passed: 120     Failed: 0     Skipped: 0
```

**相关文档**: 
- [TESTING_STRATEGY.md](../TESTING_STRATEGY.md)
- [E2E_TESTING_SUMMARY.md](../E2E_TESTING_SUMMARY.md)
- [PR42_SIMULATION_REGRESSION_SUITE.md](../PR42_SIMULATION_REGRESSION_SUITE.md)

### 2. 禁止删除或注释测试来绕过规则

**原则**: **严格禁止**注释或删除现有的测试用例来绕过规则检查。

**实施要求**:

```csharp
// ❌ 错误：注释测试
// [Fact]
// public async Task Should_Route_To_Exception_Chute_When_Timeout()
// {
//     // 这个测试失败了，先注释掉
// }

// ✅ 正确：修复代码或更新测试
[Fact]
public async Task Should_Route_To_Exception_Chute_When_Timeout()
{
    // 修复代码使测试通过
    // 或更新测试以反映新的业务规则
    var result = await _orchestrator.ProcessParcelAsync("PKG001");
    Assert.Equal(ExceptionChuteId, result.ActualChuteId);
    Assert.Equal(ParcelStatus.Timeout, result.Status);
}
```

### 3. 新增功能必须补充测试

**原则**: 所有新增功能都必须补充相应的单元测试和集成测试。

**测试覆盖率目标**:
- 核心业务逻辑：≥ 85%
- Execution 层：≥ 85%
- Drivers 层：≥ 80%
- Host 层：≥ 70%

---

## 可观测性原则

### 1. 关键操作必须记录日志

**原则**: 所有关键业务操作必须记录日志，便于问题排查和审计。

**必须记录的操作**:
- 包裹创建
- 路由请求发送和接收
- 路径生成
- 路径执行开始和结束
- 异常处理和纠错
- 系统状态变更

**日志级别规范**:
- `Information`: 正常业务流程
- `Warning`: 可恢复的异常（路由到异常格口）
- `Error`: 系统错误（需要人工介入）
- `Critical`: 严重故障（系统不可用）

**实施要求**:

```csharp
// ✅ 正确：记录关键操作
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    _logger.LogInformation($"开始处理包裹: {parcelId}");
    
    try
    {
        var chuteId = await DetermineChuteAsync(parcelId);
        _logger.LogInformation($"包裹 {parcelId} 分配到格口: {chuteId}");
        
        var path = GeneratePathOrThrow(chuteId);
        _logger.LogInformation($"包裹 {parcelId} 路径生成完成，包含 {path.Segments.Count} 个段");
        
        await ExecutePathAsync(path);
        _logger.LogInformation($"包裹 {parcelId} 成功到达格口 {chuteId}");
        
        return new SortingResult { IsSuccess = true, ActualChute = chuteId };
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, $"包裹 {parcelId} 处理失败，路由到异常格口");
        await RouteToExceptionChuteAsync(parcelId);
        return new SortingResult { IsSuccess = false, ActualChute = ExceptionChuteId };
    }
}
```

### 2. 关键指标必须记录

**原则**: 所有关键业务指标必须记录到日志和追踪系统中，便于监控和分析。

**必须记录的指标**:
- 包裹处理速率（通过日志统计）
- 成功率（通过日志统计）
- 异常率（通过日志统计）
- 错分次数（应该为 0）
- 摆轮使用率（`diverter_utilization`）
- 路径执行延迟（`path_execution_duration`）

**相关文档**: 
- [PROMETHEUS_GUIDE.md](../PROMETHEUS_GUIDE.md)
- [GRAFANA_DASHBOARD_GUIDE.md](../GRAFANA_DASHBOARD_GUIDE.md)

### 3. 日志去重机制

**原则**: 高频日志必须使用去重机制，防止日志刷屏和磁盘耗尽。

**去重规则**:
- 默认时间窗口：1 秒
- 相同日志在窗口内只记录一次
- 自动清理过期条目

**相关文档**: [PR37_IMPLEMENTATION_SUMMARY.md](../PR37_IMPLEMENTATION_SUMMARY.md)

---

## 安全性原则

### 1. 不得暴露敏感信息

**原则**: 日志和 API 响应不得包含敏感信息（密钥、密码、令牌等）。

**实施要求**:
- ❌ 禁止在日志中记录密码、密钥、令牌
- ❌ 禁止在 API 响应中返回完整的配置（包含敏感信息）
- ✅ 使用脱敏处理（`SecretKey: ****`)

### 2. API 端点必须进行参数验证

**原则**: 所有 API 端点必须通过 DataAnnotations 或 FluentValidation 进行参数验证。

**实施要求**:

```csharp
// ✅ 正确：参数验证
public record CreateChuteRequest
{
    [Required(ErrorMessage = "格口 ID 不能为空")]
    [StringLength(50, ErrorMessage = "格口 ID 长度不能超过 50 个字符")]
    public required string ChuteId { get; init; }
    
    [Range(1, 100, ErrorMessage = "容量必须在 1-100 之间")]
    public required int Capacity { get; init; }
}

[HttpPost]
public async Task<ActionResult<ApiResponse<ChuteDto>>> CreateChute(
    [FromBody] CreateChuteRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ApiResponse.Error<ChuteDto>("参数验证失败"));
    }
    
    // ...
}
```

### 3. 配置统一走 API

**原则**: 所有系统配置的增删改查必须通过**配置管理 API** 进行，不允许直接修改数据库或配置文件（生产环境）。

**原因**:
- 配置变更需要验证（格式、范围、业务规则）
- 统一的 API 可以记录操作审计日志
- 支持配置热更新，无需重启服务
- 防止手动修改导致的配置错误

**相关文档**: [CONFIGURATION_API.md](../CONFIGURATION_API.md)

---

## 8. 持久化与序列化原则

### LiteDB 序列化约束

**原则**: 所有需要持久化到 LiteDB 的实体必须符合 BsonMapper 序列化要求。

**背景**: 由于 .NET 9 + LiteDB 5.0.21 兼容性限制，`BsonMapper.IncludeNonPublic` 必须设置为 `false`。

**强制要求**:

1. **公共属性访问器**
   ```csharp
   // ✅ 正确：public set 允许 LiteDB 反序列化
   public long ParcelId { get; set; }
   public long TargetChuteId { get; set; }
   
   // ❌ 错误：internal set 导致反序列化失败
   public long ParcelId { get; internal set; }
   ```

2. **公共无参构造函数**
   ```csharp
   // ✅ 正确：提供无参构造函数供 LiteDB 使用
   public class RoutePlan
   {
       public RoutePlan() { }  // LiteDB 反序列化用
       
       public RoutePlan(long parcelId, long chuteId)  // 业务代码用
       {
           ParcelId = parcelId;
           TargetChuteId = chuteId;
       }
   }
   ```

3. **通过方法封装业务规则**
   
   由于属性必须是 `public set`，业务规则通过方法封装：
   
   ```csharp
   public class RoutePlan
   {
       // 属性必须 public set（序列化需要）
       public long ParcelId { get; set; }
       public RoutePlanStatus Status { get; set; }
       
       // 业务规则通过方法封装
       public OperationResult TryApplyChuteChange(long newChuteId, DateTimeOffset changedAt, out Decision decision)
       {
           // 验证业务规则
           if (Status != RoutePlanStatus.Created)
           {
               return OperationResult.Failure("Cannot change chute after execution started");
           }
           
           // 更新状态
           CurrentTargetChuteId = newChuteId;
           ChuteChangeCount++;
           return OperationResult.Success();
       }
   }
   ```

**禁止行为**:
- ❌ 使用 `internal set` 或 `private set`（LiteDB 无法访问）
- ❌ 依赖 `IncludeNonPublic = true`（.NET 9 兼容性问题）
- ❌ 缺少无参构造函数

**相关技术债**: [TD-082] LiteDB RoutePlan 序列化兼容性修复

---

## 违规处理

任何违反上述原则的修改，均视为**无效修改**，不得合并到主分支。

Code Review 时会重点检查：

### 核心业务原则
- [ ] Parcel-First 流程
- [ ] 零错分不变量
- [ ] 本地时间统一

### 分层架构原则
- [ ] 分层职责清晰
- [ ] Host 层不包含业务逻辑
- [ ] 通过接口访问硬件驱动

### 通讯与重试原则
- [ ] 客户端无限重连 + 2 秒最大退避
- [ ] 发送失败只记录日志

### 并发与性能原则
- [ ] 线程安全容器
- [ ] SafeExecutionService 包裹后台服务

### 测试与质量保证原则
- [ ] 所有测试通过
- [ ] 未删除或注释测试
- [ ] 新增功能补充测试

### 可观测性原则
- [ ] 关键操作记录日志
- [ ] 关键指标暴露

### 安全性原则
- [ ] 不暴露敏感信息
- [ ] API 参数验证
- [ ] 配置统一走 API

---

**相关文档**:
- [编码规范](CODING_GUIDELINES.md)
- [Copilot 约束说明](../.github/copilot-instructions.md)
- [测试策略](../TESTING_STRATEGY.md)

**文档版本**: 1.0  
**最后更新**: 2025-11-21  
**维护团队**: ZakYip Development Team
