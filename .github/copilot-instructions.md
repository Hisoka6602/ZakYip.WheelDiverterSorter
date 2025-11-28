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

### 6. Swagger API 文档注释规范

**规则**: 所有 API 端点必须具有完整的 Swagger 注释，确保 API 文档清晰、准确。

**Swagger 注释要求**:
- 每个 Controller 类必须有完整的 `/// <summary>` 和 `/// <remarks>` 注释
- 每个 Action 方法必须使用 `[SwaggerOperation]` 特性，包含 `Summary`、`Description`、`OperationId` 和 `Tags`
- 每个 Action 方法必须使用 `[SwaggerResponse]` 特性标注所有可能的响应码
- 请求/响应 DTO 的所有属性必须有 `/// <summary>` 注释
- 复杂字段应使用 `/// <remarks>` 提供详细说明
- 使用 `/// <example>` 提供示例值

**实施要求**:
```csharp
// ✅ 正确：完整的 Swagger 注释
/// <summary>
/// 获取系统配置
/// </summary>
/// <returns>当前系统配置信息</returns>
/// <response code="200">成功返回配置</response>
/// <response code="500">服务器内部错误</response>
[HttpGet]
[SwaggerOperation(
    Summary = "获取系统配置",
    Description = "返回当前系统的配置信息，包括基本参数和运行状态",
    OperationId = "GetSystemConfig",
    Tags = new[] { "系统配置" }
)]
[SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<SystemConfigDto>))]
[SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
public ActionResult<ApiResponse<SystemConfigDto>> GetSystemConfig()

// ✅ 正确：DTO 属性的完整注释
/// <summary>
/// 系统配置响应模型
/// </summary>
public record SystemConfigDto
{
    /// <summary>
    /// 配置唯一标识
    /// </summary>
    /// <example>1</example>
    public int Id { get; init; }
    
    /// <summary>
    /// 异常格口编号
    /// </summary>
    /// <remarks>
    /// 当分拣失败或路由超时时，包裹会被分配到此格口。
    /// 默认值为 999。
    /// </remarks>
    /// <example>999</example>
    public int ExceptionChuteId { get; init; }
}

// ❌ 错误：缺少 Swagger 注释
[HttpGet]
public ActionResult<SystemConfigDto> GetSystemConfig()  // ❌ 缺少 SwaggerOperation
{
    // ...
}
```

**Code Review 检查点**:
- 新增或修改的 API 端点是否有完整的 Swagger 注释
- DTO 字段是否有 `<summary>` 注释
- 复杂字段是否有 `<remarks>` 说明
- 是否提供了 `<example>` 示例值

### 7. 配置模型的 CreatedAt/UpdatedAt 默认值

**规则**: 所有配置模型的 `CreatedAt` 和 `UpdatedAt` 字段必须有有效的默认值，不能是 `"0001-01-01T00:00:00"`。

**时间默认值规范**:
- 在仓储层创建/更新记录时，由仓储通过 `ISystemClock.LocalNow` 设置时间
- 在 `GetDefault()` 静态方法中，使用 `ConfigurationDefaults.DefaultTimestamp` 作为默认值
- 如果没有更新，`UpdatedAt` 应该等于 `CreatedAt`

**实施要求**:
```csharp
// ✅ 正确：在 GetDefault() 中设置默认时间
public static SystemConfiguration GetDefault()
{
    var now = ConfigurationDefaults.DefaultTimestamp;
    return new SystemConfiguration
    {
        ConfigName = "system",
        ExceptionChuteId = 999,
        Version = 1,
        CreatedAt = now,
        UpdatedAt = now  // 未更新时等于 CreatedAt
    };
}

// ✅ 正确：仓储在插入时使用 ISystemClock
public void Insert(SystemConfiguration config)
{
    config.CreatedAt = _clock.LocalNow;
    config.UpdatedAt = _clock.LocalNow;
    _collection.Insert(config);
}

// ✅ 正确：仓储在更新时使用 ISystemClock
public void Update(SystemConfiguration config)
{
    config.UpdatedAt = _clock.LocalNow;
    _collection.Update(config);
}

// ❌ 错误：GetDefault() 未设置时间，导致 "0001-01-01T00:00:00"
public static SystemConfiguration GetDefault()
{
    return new SystemConfiguration
    {
        ConfigName = "system",
        ExceptionChuteId = 999
        // CreatedAt 和 UpdatedAt 未设置，将是默认值 DateTime.MinValue
    };
}
```

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

## 六、分层与结构约束（Copilot 必须遵守）

> 本节用于约束代码结构和分层，防止随意新增项目、目录或破坏既有架构。任何违反本节的修改均视为无效。

### 1. 项目与目录结构禁止随意新增

**规则**:

1. 业务项目限定为下列集合，不得新增同级业务项目：

   - ZakYip.WheelDiverterSorter.Core
   - ZakYip.WheelDiverterSorter.Execution
   - ZakYip.WheelDiverterSorter.Drivers
   - ZakYip.WheelDiverterSorter.Ingress
   - ZakYip.WheelDiverterSorter.Communication
   - ZakYip.WheelDiverterSorter.Observability
   - ZakYip.WheelDiverterSorter.Host
   - ZakYip.WheelDiverterSorter.Simulation
   - ZakYip.WheelDiverterSorter.Analyzers

2. `src/` 目录下的一级子目录限定为现有结构：
   
   - `src/Core`
   - `src/Execution`
   - `src/Drivers`
   - `src/Ingress`
   - `src/Communication`
   - `src/Observability`
   - `src/Host`
   - `src/Simulation`
   - `src/Analyzers`

**禁止行为**:

- 在 `src/` 下新增诸如 `Plugins/`, `Modules/`, `Infra/`, `Common/` 等新的根目录。
- 新建新的业务项目（如 `ZakYip.WheelDiverterSorter.Plugins`、`ZakYip.WheelDiverterSorter.Shared` 等）。

**例外流程**:

- 如确有新增项目 / 根目录需求，必须先：
  1. 更新 `docs/RepositoryStructure.md` 和架构文档；
  2. 更新 ArchTests / TechnicalDebtComplianceTests 对应白名单；
  3. 经过人工 Code Review 通过后方可合并。

---

### 2. Abstractions 目录位置固定

**规则**:

1. 仅允许在下列位置存在 `Abstractions` 目录：

   - `Core/Abstractions/**`
   - （如存在）`Execution/Abstractions/ExecutionSpecific/**`

2. 其他任何项目、路径一律 **禁止** 新建 `Abstractions` 目录。

**禁止行为**:

- 在 `Drivers/`, `Ingress/`, `Communication/`, `Host/`, `Simulation/` 等项目中新建 `Abstractions` 目录。
- 新增仅包含 `global using` 或简单别名转发的“空壳 Abstractions 文件”。

---

### 3. 命名空间与层级对应约束

**规则**:

1. 所有业务代码命名空间必须以 `ZakYip.WheelDiverterSorter.` 开头。
2. `Controller` 结尾的类型 **必须** 位于 `ZakYip.WheelDiverterSorter.Host.Controllers` 或其子命名空间。
3. 硬件厂商实现类 **必须** 位于：

   - `ZakYip.WheelDiverterSorter.Drivers.Vendors.*`
   - 或 `ZakYip.WheelDiverterSorter.Drivers.Simulated.*`（仿真）

4. Core 和 Execution 不得直接引用具体厂商命名空间：

   - 禁止在 `Core` 和 `Execution` 中使用：
     - `using ZakYip.WheelDiverterSorter.Drivers.Vendors.*;`

**实施要求**:

- 访问硬件能力一律通过 Core 定义的抽象接口（如 `IWheelDiverterDriver`、`IDiverterController` 等），由 DI 注入不同厂商实现。

---

### 4. 新增代码放置规则

**规则**: 根据功能类型，将新代码放在指定位置，不得自行发明新层。

- 新增硬件厂商实现：

  - 路径：`src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/<VendorName>/`
  - 必须实现 Core 中的硬件抽象接口（例如 `IWheelDiverterDriver`、`IInputPort` 等）。
  - 不得直接在 Execution/Host 中 new 出具体 Vendor 实现。

- 新增分拣策略 / 路径生成策略：

  - 路径：`src/Execution/ZakYip.WheelDiverterSorter.Execution/Sorting/Strategies/`
  - 必须通过接口（如 `IChuteSelectionStrategy`, `ISwitchingPathGenerator`）对外暴露。

- 新增上游协议接入（TCP/SignalR/MQTT/HTTP）：

  - 路径：`src/Communication/ZakYip.WheelDiverterSorter.Communication/Gateways/`
  - 必须实现统一的上游客户端抽象接口（如 `IUpstreamRoutingClient`）。

- 新增配置模型：

  - 路径：`Core/LineModel/Configuration/Models/`
  - 对应仓储接口放在 `Core/LineModel/Configuration/Repositories/Interfaces/`
  - 具体实现（如 LiteDB）放在 `Core/LineModel/Configuration/Repositories/LiteDb/`

**禁止行为**:

- 在未知位置创建新目录（如 `Core/ConfigModels`、`Execution/Io` 等），而不遵守上述路径约定。
- 将硬件逻辑直接写在 Execution 或 Host 中。

---

### 5. 结构调整与旧实现删除规则

**规则**:

1. 当新增实现完全覆盖旧实现时，旧实现必须在同一个 PR 中删除，不允许保留“影子代码”。
2. 禁止为了兼容旧代码而同时维护两套等价实现（例如两个功能完全相同的 Orchestrator 或路径生成器）。

**实施要求**:

- PR 描述中必须列出：
  - 新增的核心实现；
  - 被替换、删除的旧实现列表；
  - 声明“旧实现已被新实现完全覆盖，可安全删除”。

---

### 6. Copilot 生成代码前的决策顺序

**规则**: Copilot 在生成新代码时，必须按以下顺序决策：

1. 优先使用已有接口和抽象（Core/Abstractions、Execution/Abstractions）。
2. 在已存在的项目和目录中扩展，而不是新建项目或新建根目录。
3. 必须遵循本文件和 `RepositoryStructure.md` 中定义的结构说明。
4. 如无法在现有结构中合理放置新代码，应生成注释说明原因，交由人工决策，不得自行创建新层。

---
## 七、API 文档与测试约束

> 本节用于约束所有对外 API 端点的文档与测试要求，防止出现“无注释、无测试”的接口。

### 1. Swagger 文档注释

**规则：**

1. 所有对外公开的 API 端点（Controller Action）必须在 Swagger 中有清晰的中文说明。
2. 每个 Action 至少需要具备以下其一：
   - 通过 XML 注释提供 `<summary>` / `<remarks>`，并在启动代码中启用 `IncludeXmlComments` 生成 Swagger 文档；
   - 或显式标注如 `[SwaggerOperation(Summary = "...", Description = "...")]` 等带描述信息的特性。

**禁止行为：**

- 新增没有任何中文说明的 API 端点。
- 使用与业务无关的占位描述（例如 “TODO”、“Test” 等）。

---

### 2. API 端点测试要求

**规则：**

1. 每个对外公开的 API 端点必须至少有一条自动化测试用例覆盖，测试类型可以是：
   - 集成测试（推荐）；
   - 功能测试 / 端到端测试。
2. 提交 PR 前必须确保：
   - 所有与 API 端点相关的测试全部通过；
   - CI 流水线中的测试步骤全部通过。

**禁止行为：**

- 新增 API 端点而不编写对应测试用例。
- 已知测试失败仍提交 PR，期待后续再修复。

---

## 八、类型与 Id 约束

> 本节用于统一 Id 类型与构建质量要求，避免类型不一致和“半成品”提交。

### 1. Id 类型统一使用 long

**规则：**

1. 除数据库自增主键（如 EF Core 中的自增列）或外部系统强制使用特定类型的 Key 以外，所有内部定义的 Id 均必须使用 `long` 类型：
   - 领域模型中的 Id（如包裹 Id、格口 Id、小车 Id 等）；
   - DTO、命令对象、事件载荷中的 Id 字段；
   - 配置模型中的 Id 字段。
2. 禁止在同一语义下混用 `int` 与 `long`，例如某处使用 `int ChuteId`，另一处使用 `long ChuteId`。

**允许的例外：**

- 数据库表中已有历史字段为 `int` 且暂时无法迁移时，可以在数据访问层做类型转换，但领域层 / 应用层应尽量统一为 `long`。

**禁止行为：**

- 新增任何非数据库自增 / 非外部依赖的 Id 字段使用 `int` / `Guid` 等类型，且未经过架构确认。
- 相同语义的 Id 在不同层使用不同类型。

---

### 2. 构建质量要求

**规则：**

1. 提交 PR 前必须确保解决方案中所有项目均能成功构建：
   - 本地执行 `dotnet build`（或等价命令）必须无构建错误；
   - CI 构建步骤必须全部通过。
2. 禁止提交“已知无法构建”的中间状态代码。

**禁止行为：**

- 依赖 CI 修复编译错误，将无法通过构建的代码提交到远程仓库。
- 存在编译错误或严重警告被显式忽略的情况（除非为已知技术债且由架构确认）。

---

## 九、小工具方法与厂商代码归属约束

> 本节用于约束通用工具方法的统一收敛，以及所有厂商相关代码的归属位置。

### 1. 小工具方法统一收敛

**规则：**

1. 具有相同语义的工具方法必须统一实现、统一调用，禁止在各处重复实现。
2. 在新增工具方法之前，应按以下顺序处理：
   - 搜索现有 `Utils` / `Extensions` / `Helpers` 等工具类；
   - 若已存在功能等价或高度相似的方法，应直接复用现有方法；
   - 若发现多处功能重复的方法，应在本次重构或后续 PR 中收敛为一个公共实现。
3. 新增公共工具方法时，应放置在约定的工具类或工具命名空间中，而不是随意新建零散静态类。

**禁止行为：**

- 在不同项目或不同类中重复实现相同逻辑的小工具方法（例如多处自行实现相同的字符串处理、时间转换、日志封装等）。
- 为绕开既有工具方法而复制粘贴一份略有差异的实现。

---

### 2. 厂商相关代码归属

**规则：**

1. 所有厂商相关实现（包括 IO 板卡、摆轮、线体驱动等），必须放在 Drivers 项目的 `Vendors` 子目录中，路径形式为：

   `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/<VendorName>/...`

2. 厂商相关内容只能出现在 Drivers 项目下的 `Vendors` 目录中：
   - 不允许在 Core / Execution / Ingress / Communication / Observability / Host / Simulation 项目中直接放置厂商实现类；
   - 不允许在上述项目中新建 `Vendors` 目录。
3. Core 与 Execution 层只能依赖厂商无关的抽象接口（例如 `IWheelDiverterDriver`、`IInputPort` 等），具体厂商类必须在 Drivers 项目中实现，并通过依赖注入注册。

**禁止行为：**

- 在 Drivers 项目以外实现任何厂商逻辑（例如在 Execution 或 Host 中直接编写某个厂商的协议解析或驱动类）。
- 在 Core / Execution / Ingress 等项目中引入 `ZakYip.WheelDiverterSorter.Drivers.Vendors.*` 命名空间。
- 在 Drivers 项目中将多个不同厂商的实现混在同一目录，而不按厂商名称拆分子目录。

---

### 3. 与结构规则的联动

**规则：**

1. 当新增厂商实现时，必须同时满足：
   - 物理路径位于 `Drivers/Vendors/<VendorName>/` 目录下；
   - 实现 Core 中已有的硬件抽象接口，而不是在 Drivers 中再创建一套与 Core 平行的抽象；
   - 通过架构测试（ArchTests）、技术债合规测试（TechnicalDebtComplianceTests）以及分析器（Analyzers）对命名空间和依赖关系的校验。
2. 当收敛重复工具方法时，应在对应 PR 描述中列出：
   - 被保留的统一实现方法名称与所在位置；
   - 被删除或被替换的重复方法列表；
   - 声明后续统一调用路径（新代码应仅调用统一实现）。

---
## 十、结构文档同步与修改优先级

> 本节用于约束每次提交代码时对结构文档的维护方式，以及修改代码时的优先级取向。

### 1. RepositoryStructure.md 必须同步更新

**规则：**

1. 每次提交代码（包括功能开发、重构、Bug 修复），如满足以下任一情况，必须同步更新 `docs/RepositoryStructure.md`：
   - 新增或删除项目；
   - 新增或删除目录（尤其是 `src` 下的一级 / 二级目录）；
   - 移动文件导致类型所在位置或命名空间发生变化；
   - 引入新的重要服务、Orchestrator、驱动、上下游协议实现等核心角色；
   - 调整分层边界或依赖关系（例如某项目新增对另一个项目的引用）。
2. 如本次改动不涉及以上内容，也必须显式检查并确认文档无需更新：
   - PR 描述中应注明“已检查 RepositoryStructure.md，本次改动无需更新”或“已同步更新 RepositoryStructure.md”。

**禁止行为：**

- 变更项目结构（新增/删除项目、移动大量文件）而不更新 `RepositoryStructure.md`。
- 让文档长期与实际代码结构不一致，导致 Copilot 和后续开发者基于过期结构做决策。

---

### 2. 修改策略：结构清晰与性能优先，而非最小修改量优先

**规则：**

1. 在设计修改方案时，应遵循以下优先级顺序：
   1. 保证结构清晰、分层边界明确、职责单一；
   2. 在不破坏可读性的前提下，优先选择性能更优的实现；
   3. 在满足前两点的前提下，再考虑控制修改范围。
2. 当“最小修改量”与“结构更清晰、性能更优”的方案发生冲突时，应优先选择结构清晰、性能更优的方案，即使这意味着需要调整更多文件：
   - 可以拆分为多个 PR 逐步实施，但不应为了减少改动量而牺牲结构合理性；
   - 对于临时性兼容/过渡方案，应在代码和 PR 描述中明确标注为“过渡实现”，并规划后续清理。

**禁止行为：**

- 为了“最小修改量”，在错误的层级继续堆叠逻辑，进一步加剧结构混乱。
- 明知现有结构已经不合理，仍选择局部打补丁，而不是在合理范围内进行必要的抽象和收敛。
- 在性能明显存在瓶颈的核心路径上，仅做最小语义改动而不考虑更合理的性能优化方案。

**实施要求：**

- 当 PR 中涉及较大重构（例如移动类型、拆分服务、引入新抽象）时，应在 PR 描述中说明：
  - 选择该方案的结构原因（如何简化分层关系、提升可读性）；
  - 性能影响评估（例如减少重复计算、降低 IO 次数等）；
  - 若存在替代的“最小修改量方案”，说明未采纳的理由。

---


## 十一、代码清理与 using 规范

> 本节用于约束过时代码的清理策略，以及 using 的使用方式。

### 1. 过时 / 废弃 / 重复代码必须立即删除

**规则：**

1. 一旦新增实现已经覆盖旧实现，旧实现必须在**同一个 PR 中立即删除**，并同步调整所有调用方的引用：
   - 禁止仅新增新实现而保留旧实现不动；
   - 禁止“后续再清理”的长期过渡做法。
2. 相同语义的两套实现不允许并存：
   - 工具方法、服务、Orchestrator、驱动实现等，一旦确定统一实现，应保留唯一版本，并删除所有重复版本；
   - 调用方必须在本次修改中一并切换到统一实现。
3. 不允许通过 `[Obsolete]`、`Legacy`、`Deprecated` 等方式长期保留废弃代码：
   - 如确需临时标记（极短期迁移），也必须在同一轮重构内完成调用方替换和旧实现删除；
   - 不允许给新代码增加对任何“Legacy/Deprecated” 类型的引用。
4. 测试项目 / 工具项目中的辅助类型如已不再被任何测试或工具使用，也必须在本次重构中一并清理。

**禁止行为：**

- 新实现已投入使用，却仍长时间保留旧实现，仅在注释或特性中标记“废弃 / Legacy / Deprecated”。
- 为了“兼容历史”同时维护两套等价实现，只更新其中一套。
- 新增代码继续依赖已明确不推荐使用的旧实现。

---

### 2. 禁止使用 global using

**规则：**

1. 代码中禁止使用 `global using` 指令：
   - 不得在任何 `.cs` 文件中新增 `global using`；
   - 不得新增专门用于集中放置 `global using` 的文件。
2. 现有的 `global using` 应在后续重构中逐步移除，并替换为显式 `using`：
   - 将 `global using` 替换为各文件内的普通 `using`；
   - 如依赖过多，可通过合理的命名空间划分和工具类收敛减少公共依赖。

**允许的例外：**

- SDK 默认启用的隐式 usings（`ImplicitUsings`）如确实不影响分层结构，可暂时保留；如需关闭，应在 `.csproj` 级别统一配置。

**禁止行为：**

- 在任何新代码中引入 `global using`。
- 以 `global using` 的方式在低层项目中直接暴露高层命名空间，从而绕过分层约束。

---
## 十二、测试失败处理与技术债闭环

> 本节用于约束测试失败的处理方式，禁止以“历史问题 / 与本 PR 无关”为理由忽略红灯，确保技术债务在每个 PR 中被真正消灭。

### 1. 所有测试失败必须在当前 PR 中修复

**规则：**

1. 任意测试失败（包括本地单测、集成测试、E2E 测试、ArchTests、TechnicalDebtComplianceTests），一旦在本 PR 的 CI 中出现，就视为本 PR 的工作内容，**必须在当前 PR 中修复**：
   - 禁止在 PR 描述或评论里说明“这是已有问题 / 与本 PR 无关”，然后继续合并；
   - 禁止把测试失败留给“后续 PR 处理”。

2. 若测试失败为历史遗留问题：
   - 当前 PR 必须扩展 Scope，一并修复；
   - 或者先提交一个专门的“修复测试失败 / 技术债清理 PR”，在该 PR 合并之前，任何依赖这些测试的其他 PR **不得合并**。

3. 禁止通过以下方式“伪修复”测试失败：
   - 注释掉测试代码或断言；
   - 给测试加 `[Ignore]`、`[Skipped]` 或类似标记规避执行；
   - 简单捕获异常后吞掉，不再验证行为正确性。

**禁止行为：**

- 在 PR 描述中写出类似：“测试失败是本来就存在的，与本 PR 无关”，并尝试继续合并。
- 仅为了让 CI 通过而删除或禁用测试，而没有修复根因。

---

### 2. 枚举相关测试与厂商协议枚举的处理

**规则：**

1. 所有枚举（包括厂商协议枚举）都必须满足当前枚举测试规则（例如必须带有 `Description` 特性等）：
   - `Core`、`Execution`、`Drivers`（包含 `Vendors` 子目录）、`Application`、`Host` 中的枚举一视同仁；
   - 不允许以“这是厂商协议枚举”为理由绕过枚举相关测试。

2. 当枚举相关测试失败时（包括 `ShuDiNiaoProtocolEnums.cs`、`ModiProtocolEnums.cs` 等厂商协议枚举）：
   - 当前 PR 必须在同一个 PR 内修复：
     - 要么给这些枚举补齐约定的特性（如 `Description`），让它们完全符合测试规则；
     - 要么在测试中显式建模“厂商协议枚举特例”的合理规则，并保持代码与测试语义一致，而不是简单排除。
   - 禁止只在 PR 描述中说明“这是厂商特定协议枚举，属于厂商实现，与本 PR 无关”而不处理失败。

3. 若当前测试规则设计存在缺陷（确实不适合某些特例）：
   - 也必须在本 PR 中同时修正测试与被测代码，使二者在语义上重新达成一致；
   - 不允许保留“测试与代码对不齐”的状态。

**禁止行为：**

- 声明“驱动供应商目录中的枚举是厂商协议枚举，不在测试覆盖范围”，从而忽略测试失败。
- 在不修复测试的前提下继续调整业务代码，放大技术债。

---

### 3. PR 描述与 CI 要求

**规则：**

1. 所有 PR 必须满足以下前置条件才能被视为“可合并”：
   - 所有项目构建成功；
   - 所有自动化测试（含 ArchTests、TechnicalDebtComplianceTests、枚举规则测试等）全部通过；
   - 不存在被注释掉或标记忽略的关键测试。

2. PR 描述中需要明确说明：
   - 若本次工作过程中遇到历史失败测试，已在本 PR 中一并修复；
   - 若本 PR 专门用于清理历史测试失败，需要列出修复的测试名称/模块列表。

3. 对于 Copilot 生成的说明文字，应当禁止类似下述措辞出现在最终 PR 描述中：
   - “测试失败是本来就存在的，与我的更改无关”
   - “这些失败属于其他模块，不在本 PR 范围内”
   - “枚举测试失败是厂商实现导致，与当前改动无关”

**禁止行为：**

- 在 CI 红灯状态下申请合并 PR。
- 通过人工手动勾选“强制合并”，忽略测试失败。
- 允许 PR 在有已知测试失败的前提下长期挂起而不清理。

---

## 违规处理

任何违反上述规则的修改，均视为**无效修改**，不得合并到主分支。

Code Review 时会重点检查：
1. 是否遵守 Parcel-First 流程
2. 是否使用 `ISystemClock` 而非直接使用 `DateTime.Now/UtcNow`（禁止使用Utc时间）
3. 是否使用 `ISafeExecutionService` 包裹后台任务
4. 是否使用线程安全容器或明确的锁
5. API 端点是否遵循 DTO + 验证 + `ApiResponse<T>` 规范
6. **是否具有完整的 Swagger 注释**（`SwaggerOperation`、`SwaggerResponse`、属性 `<summary>` 等）
7. 是否启用可空引用类型且未新增 `#nullable disable`
8. 是否使用 `record` / `readonly struct` / `file class` 等现代 C# 特性
9. 是否遵守分层架构，Host 层不包含业务逻辑
10. 是否通过接口访问硬件驱动
11. 是否保持所有仿真和 E2E 测试通过
12. 是否已更新`RepositoryStructure.md`(每次修改都需要更新当前结构和技术债务)
13. 是否使用`global using`(代码中禁止使用 `global using`)
14. 不能包含过时方法、属性、字段、类
15. 不能抑制错误和警告,有错误和警告都必须处理
---

**文档版本**: 1.1  
**最后更新**: 2025-11-26  
**维护团队**: ZakYip Development Team
