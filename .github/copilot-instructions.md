# ZakYip.WheelDiverterSorter – Copilot 约束说明（必须遵守）

本文档定义了仓库级的编码规范、架构原则和流程约束。所有开发人员和 AI 辅助工具（包括 GitHub Copilot）在生成代码或 PR 时必须严格遵守这些规则。

**重要提示**: 任何违反下述规则的修改，均视为无效修改，不得合并。

---

## 一、总体原则（不可破坏）

### 1. 上游通讯必须遵守"Parcel-First"流程

**规则**: 必须先通过感应 IO 在本地创建包裹并生成 ParcelId，再向上游发送携带 ParcelId 的路由请求。

**禁止行为**: 
- 不允许存在"无本地包裹实体而向上游要路由"的逻辑
- 不允许先请求路由再创建包裹

**实施要求**:
```csharp
// ✅ 正确：先创建包裹，再请求路由
var parcel = await CreateParcelFromSensorEvent(sensorId);
var chuteId = await RequestRoutingFromUpstream(parcel.ParcelId);

// ❌ 错误：先请求路由，再创建包裹
var chuteId = await RequestRoutingFromUpstream();  // 没有 ParcelId！
var parcel = CreateParcel(chuteId);
```

**相关文档**: [PR42_PARCEL_FIRST_SPECIFICATION.md](../PR42_PARCEL_FIRST_SPECIFICATION.md)

### 2. 所有时间一律通过 ISystemClock 获取

**规则**: 统一使用 `ISystemClock` 接口获取时间，便于测试和时区管理。

**时间使用规范**:
- 使用 `ISystemClock.LocalNow` 表示业务时间（日志、记录、显示）
- 仅在与外部系统协议要求时使用 `ISystemClock.UtcNow`
- **禁止**在业务代码中直接调用 `DateTime.Now` 或 `DateTime.UtcNow`

**实施要求**:
```csharp
// ✅ 正确：通过 ISystemClock 获取时间
public class ParcelCreationService
{
    private readonly ISystemClock _clock;
    
    public ParcelCreationService(ISystemClock clock)
    {
        _clock = clock;
    }
    
    public Parcel CreateParcel(string sensorId)
    {
        return new Parcel
        {
            ParcelId = GenerateId(),
            CreatedAt = _clock.LocalNow  // ✅ 使用 ISystemClock
        };
    }
}

// ❌ 错误：直接使用 DateTime.Now
public Parcel CreateParcel(string sensorId)
{
    return new Parcel
    {
        ParcelId = GenerateId(),
        CreatedAt = DateTime.Now  // ❌ 禁止！
    };
}
```

**相关文档**: [SYSTEM_CONFIG_GUIDE.md - 系统时间说明](../SYSTEM_CONFIG_GUIDE.md)

### 3. 所有可能抛出异常的后台任务、循环、IO/通讯回调必须通过 SafeExecutionService 执行

**规则**: 所有后台服务（`BackgroundService`）的主循环必须使用 `ISafeExecutionService` 包裹，确保未捕获异常不会导致进程崩溃。

**实施要求**:
```csharp
// ✅ 正确：使用 SafeExecutionService 包裹后台任务
public class PackageSortingWorker : BackgroundService
{
    private readonly ISafeExecutionService _safeExecutor;
    
    public PackageSortingWorker(ISafeExecutionService safeExecutor)
    {
        _safeExecutor = safeExecutor;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // 你的业务逻辑
                    await ProcessNextParcel();
                }
            },
            operationName: "PackageSortingLoop",
            cancellationToken: stoppingToken
        );
    }
}

// ❌ 错误：未使用 SafeExecutionService，异常可能导致进程崩溃
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ProcessNextParcel();  // 可能抛出未捕获异常
    }
}
```

**相关文档**: [PR37_IMPLEMENTATION_SUMMARY.md](../PR37_IMPLEMENTATION_SUMMARY.md)

### 4. 任何跨线程共享的集合必须使用线程安全容器或明确的锁封装

**规则**: 防止并发访问导致的数据竞争和状态不一致。

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
}

// ❌ 错误：使用非线程安全容器但可能被多线程访问
public class ParcelTracker
{
    private readonly Dictionary<string, ParcelState> _parcels = new();
    
    public void UpdateParcelState(string parcelId, ParcelState state)
    {
        _parcels[parcelId] = state;  // ❌ 多线程访问时不安全
    }
}

// ✅ 正确：使用明确的锁封装
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
}
```

**相关文档**: [CONCURRENCY_CONTROL.md](../CONCURRENCY_CONTROL.md)

### 5. API 端点规范

**规则**: 所有 API 端点必须遵循统一的设计规范，确保类型安全和参数验证。

**请求模型要求**:
- 必须使用 DTO（`record`），启用可空引用类型
- 所有必填字段使用 `required + init`
- 所有字段需通过特性或 FluentValidation 做参数验证

**响应模型要求**:
- 统一使用 `ApiResponse<T>` 包装响应数据

**实施要求**:
```csharp
// ✅ 正确：符合规范的 API 端点
public record CreateChuteRequest
{
    [Required]
    [StringLength(50)]
    public required string ChuteId { get; init; }
    
    [Required]
    [Range(1, 100)]
    public required int Capacity { get; init; }
    
    [StringLength(200)]
    public string? Description { get; init; }
}

[HttpPost]
public async Task<ActionResult<ApiResponse<ChuteDto>>> CreateChute(
    [FromBody] CreateChuteRequest request)
{
    var chute = await _service.CreateChuteAsync(request);
    return Ok(ApiResponse.Success(chute));
}

// ❌ 错误：不符合规范
public class CreateChuteRequest  // ❌ 应使用 record
{
    public string ChuteId { get; set; }  // ❌ 缺少 required, 缺少验证特性
    public int Capacity { get; set; }    // ❌ 缺少验证特性
}

[HttpPost]
public async Task<ActionResult<ChuteDto>> CreateChute(  // ❌ 未使用 ApiResponse<T>
    [FromBody] CreateChuteRequest request)
{
    var chute = await _service.CreateChuteAsync(request);
    return Ok(chute);  // ❌ 应返回 ApiResponse.Success(chute)
}
```

**相关文档**: [CONFIGURATION_API.md](../CONFIGURATION_API.md)

---

## 二、代码风格与类型使用

### 1. 启用可空引用类型，禁止新增 #nullable disable

**规则**: 
- 所有项目必须在 `.csproj` 中启用 `<Nullable>enable</Nullable>`
- **禁止**在新增文件中添加 `#nullable disable`
- 对当前尚未处理的旧代码，可以临时通过 `#nullable disable` 包住，后续凭 PR 慢慢清理

**实施要求**:
```csharp
// ✅ 正确：明确可空性
public string? GetChuteName(string chuteId)  // 明确返回可能为 null
{
    var config = _repository.GetById(chuteId);
    return config?.Name;
}

// ❌ 错误：未明确可空性
public string GetChuteName(string chuteId)
{
    var config = _repository.GetById(chuteId);
    return config.Name;  // config 可能为 null，运行时异常风险
}
```

### 2. DTO / 只读数据优先使用 record / record struct

**规则**: 对于纯数据容器，优先使用 `record` 而不是 `class`。

**实施要求**:
```csharp
// ✅ 正确：使用 record
public record PathSegmentResult(
    string DiverterId,
    Direction Direction,
    bool IsSuccess,
    string? ErrorMessage = null
);

// ❌ 错误：使用 class 且未实现值语义
public class PathSegmentResult
{
    public string DiverterId { get; set; }
    public Direction Direction { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### 3. 非可变结构优先使用 readonly struct

**规则**: 对于小型值类型（≤16 字节），使用 `readonly struct` 确保不可变性并提高性能。

**实施要求**:
```csharp
// ✅ 正确：使用 readonly struct
public readonly struct DiverterPosition
{
    public string DiverterId { get; init; }
    public int SequenceNumber { get; init; }
    
    public DiverterPosition(string diverterId, int sequenceNumber)
    {
        DiverterId = diverterId;
        SequenceNumber = sequenceNumber;
    }
}

// 或使用 record struct（C# 10+）
public readonly record struct DiverterPosition(
    string DiverterId,
    int SequenceNumber
);
```

### 4. 工具类与内部类型优先使用文件作用域类型（file class / file struct）

**规则**: 使用 `file` 关键字限制类型的可见性在当前文件，避免污染全局命名空间。

**实施要求**:
```csharp
// PathGenerator.cs
namespace ZakYip.WheelDiverterSorter.Core;

public class PathGenerator
{
    public SwitchingPath GeneratePath(string chuteId)
    {
        double time = PathCalculator.CalculateTravelTime(10.0, 1.5);
        // 使用辅助类
    }
}

// ✅ 正确：仅在此文件内可见
file static class PathCalculator
{
    public static double CalculateTravelTime(double distance, double speed)
    {
        return distance / speed;
    }
}

// ❌ 错误：暴露到全局
public static class PathCalculator  // 可能被误用
{
    public static double CalculateTravelTime(double distance, double speed)
    {
        return distance / speed;
    }
}
```

### 5. 方法应保持单一职责，小而清晰

**规则**: 
- 每个方法应该只做一件事
- 方法长度建议不超过 20-30 行
- 复杂逻辑拆分为多个小方法

**实施要求**:
```csharp
// ✅ 正确：方法专注且小巧
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    var parcel = await ValidateParcelAsync(parcelId);
    var chuteId = await DetermineChuteAsync(parcelId);
    var path = GeneratePathOrThrow(chuteId);
    
    await ExecutePathAsync(path);
    await RecordSuccessAsync(parcelId, chuteId);
    
    return new SortingResult { IsSuccess = true, ActualChute = chuteId };
}

// ❌ 错误：方法过长，做了太多事情（50+ 行）
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    // 验证包裹
    var parcel = await _repository.GetParcelAsync(parcelId);
    if (parcel == null) { /* ... */ }
    
    // 请求格口
    var chuteId = await _ruleEngineClient.GetTargetChuteAsync(parcelId);
    if (string.IsNullOrEmpty(chuteId)) { /* ... */ }
    
    // 生成路径
    var path = _pathGenerator.GeneratePath(chuteId);
    if (path == null) { /* ... */ }
    
    // 执行路径
    var executionResult = await _executor.ExecutePathAsync(path);
    if (!executionResult.IsSuccess) { /* ... */ }
    
    // 记录结果
    await _repository.UpdateParcelStatusAsync(parcelId, "Sorted");
    _metrics.RecordSuccess(chuteId);
    
    return new SortingResult { IsSuccess = true, ActualChute = chuteId };
}
```

---

## 三、通讯与重试规则

### 1. 与上游通讯支持 Client / Server 模式

**规则**: 系统必须支持作为客户端或服务器与上游 RuleEngine 通信。

**支持的协议**:
- TCP (作为客户端或服务器)
- SignalR (作为客户端)
- MQTT (作为客户端或服务器)
- HTTP (作为客户端)

### 2. 作为客户端时的连接规则

**规则**: 
- 连接失败必须进行**无限重试**
- 退避时间上限为 **2 秒**（硬编码）
- 更新连接参数后，使用新参数继续无限重试

**实施要求**:
```csharp
// ✅ 正确：无限重试，最大退避 2 秒
public class UpstreamConnectionManager
{
    private const int MaxBackoffMs = 2000;  // 硬编码上限
    
    public async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        int backoffMs = 200;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync();
                return;  // 连接成功
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"连接失败，{backoffMs}ms 后重试: {ex.Message}");
                await Task.Delay(backoffMs, cancellationToken);
                
                // 指数退避，但不超过 2 秒
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
        }
    }
}
```

**相关文档**: [PR38_IMPLEMENTATION_SUMMARY.md](../PR38_IMPLEMENTATION_SUMMARY.md)

### 3. 发送失败处理规则

**规则**: 
- 发送失败**只记录日志**，不进行发送重试
- 当前包裹自动路由到异常格口

**实施要求**:
```csharp
// ✅ 正确：发送失败只记录日志
public async Task<bool> SendRoutingRequestAsync(string parcelId)
{
    try
    {
        await _client.SendAsync(new RoutingRequest { ParcelId = parcelId });
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError($"发送路由请求失败，包裹 {parcelId} 将路由到异常格口: {ex.Message}");
        return false;  // ✅ 不重试，由调用方处理
    }
}

// ❌ 错误：发送失败后自动重试
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
            if (i < 2) continue;  // ❌ 重试
        }
    }
    return false;
}
```

---

## 四、Host 层、Execution、Drivers 结构

### 1. Host 层只负责 DI / 配置绑定 / API Controller 壳

**规则**: 
- Host 层不允许直接写业务逻辑或访问仓储/驱动
- Host 层只负责依赖注入配置、API Controller 端点定义

**实施要求**:
```csharp
// ✅ 正确：Host 层 Controller 只调用服务
[ApiController]
[Route("api/config")]
public class ConfigurationController : ControllerBase
{
    private readonly ISystemConfigService _configService;
    
    public ConfigurationController(ISystemConfigService configService)
    {
        _configService = configService;
    }
    
    [HttpGet("system")]
    public async Task<ActionResult<ApiResponse<SystemConfigDto>>> GetSystemConfig()
    {
        var config = await _configService.GetSystemConfigAsync();
        return Ok(ApiResponse.Success(config));
    }
}

// ❌ 错误：Host 层直接包含业务逻辑
[ApiController]
[Route("api/config")]
public class ConfigurationController : ControllerBase
{
    private readonly ILiteDbRepository _repository;  // ❌ 不应直接访问仓储
    
    [HttpGet("system")]
    public async Task<ActionResult<SystemConfigDto>> GetSystemConfig()
    {
        // ❌ 业务逻辑应该在 Application / Core 层
        var config = _repository.GetSystemConfig();
        if (config == null)
        {
            config = CreateDefaultConfig();
            _repository.SaveSystemConfig(config);
        }
        return Ok(config);
    }
}
```

### 2. 业务逻辑必须放在 Application / Core

**规则**: 所有业务规则、领域逻辑必须放在 `Core` 或 `Application` 层，不得放在 `Host` 层。

### 3. 硬件相关逻辑必须通过 Drivers 抽象访问

**规则**: 
- 所有硬件相关操作必须通过 `Drivers` 层的接口访问
- 便于对接多厂商设备（雷赛、西门子、三菱、欧姆龙等）
- 支持 Mock 实现用于测试和仿真

**实施要求**:
```csharp
// ✅ 正确：通过接口访问硬件
public class PathExecutor : ISwitchingPathExecutor
{
    private readonly IDiverterDriver _driver;  // 接口依赖
    
    public PathExecutor(IDiverterDriver driver)
    {
        _driver = driver;  // 可以是 LeadshineDiverterDriver 或 MockDiverterDriver
    }
    
    public async Task<PathExecutionResult> ExecuteAsync(SwitchingPath path)
    {
        foreach (var segment in path.Segments)
        {
            await _driver.SetDirectionAsync(segment.DiverterId, segment.Direction);
        }
    }
}

// ❌ 错误：直接依赖具体实现
public class PathExecutor
{
    private readonly LeadshineDiverterDriver _driver;  // ❌ 依赖具体实现
    
    public async Task ExecuteAsync(SwitchingPath path)
    {
        // 无法支持其他厂商设备，无法 Mock 测试
    }
}
```

**相关文档**: [VENDOR_EXTENSION_GUIDE.md](../VENDOR_EXTENSION_GUIDE.md)

---

## 五、仿真与测试

### 1. Copilot 修改分拣逻辑/通讯/IO/面板时，必须保持所有仿真和 E2E 测试通过

**规则**: 
- 任何对核心业务逻辑、通讯层、硬件驱动、面板控制的修改，都必须确保现有测试通过
- 新增功能必须补充相应的测试用例

**测试要求**:
- 单元测试：验证单个组件的正确性
- 集成测试：验证组件间的交互
- E2E 测试：验证完整的分拣流程（从 API 配置启动 IO → 面板启动 → 创建包裹 → 上游路由 → 摆轮分拣 → 落格）

**实施要求**:
```bash
# 在提交前必须运行测试
dotnet test

# 确保所有测试通过
# 如果测试失败，必须修复或更新测试
```

### 2. 不允许为绕过规则而注释或删除现有仿真测试

**规则**: 
- **严格禁止**注释或删除现有的测试用例来绕过规则检查
- 如果测试失败，必须修复代码或合理更新测试

**实施要求**:
```csharp
// ❌ 错误：注释测试来绕过检查
// [Fact]
// public async Task Should_Route_To_Exception_Chute_When_Timeout()
// {
//     // 这个测试失败了，先注释掉
// }

// ✅ 正确：修复代码或更新测试
[Fact]
public async Task Should_Route_To_Exception_Chute_When_Timeout()
{
    // 修复代码使测试通过，或更新测试以反映新的行为
    var result = await _orchestrator.ProcessParcelAsync("PKG001");
    Assert.Equal(ExceptionChuteId, result.ActualChuteId);
}
```

**相关文档**: 
- [TESTING_STRATEGY.md](../TESTING_STRATEGY.md)
- [E2E_TESTING_SUMMARY.md](../E2E_TESTING_SUMMARY.md)
- [PR42_SIMULATION_REGRESSION_SUITE.md](../PR42_SIMULATION_REGRESSION_SUITE.md)

---

## 违规处理

任何违反上述规则的修改，均视为**无效修改**，不得合并到主分支。

Code Review 时会重点检查：
1. 是否遵守 Parcel-First 流程
2. 是否使用 `ISystemClock` 而非直接使用 `DateTime.Now/UtcNow`
3. 是否使用 `ISafeExecutionService` 包裹后台任务
4. 是否使用线程安全容器或明确的锁
5. API 端点是否遵循 DTO + 验证 + `ApiResponse<T>` 规范
6. 是否启用可空引用类型且未新增 `#nullable disable`
7. 是否使用 `record` / `readonly struct` / `file class` 等现代 C# 特性
8. 是否遵守分层架构，Host 层不包含业务逻辑
9. 是否通过接口访问硬件驱动
10. 是否保持所有仿真和 E2E 测试通过

---

**文档版本**: 1.0  
**最后更新**: 2025-11-21  
**维护团队**: ZakYip Development Team
