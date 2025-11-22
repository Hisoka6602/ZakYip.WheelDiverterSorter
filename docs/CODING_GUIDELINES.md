# 编码规范 (Coding Guidelines)

本文档定义了 ZakYip.WheelDiverterSorter 项目的 C# 编码标准和最佳实践。

**重要**: 违反这些规范的 PR 不得合并到主分支。

---

## 目录

1. [类型使用规范](#类型使用规范)
2. [可空引用类型](#可空引用类型)
3. [时间处理规范](#时间处理规范)
4. [并发安全规范](#并发安全规范)
5. [异常处理规范](#异常处理规范)
6. [API 设计规范](#api-设计规范)
7. [方法设计原则](#方法设计原则)
8. [命名约定](#命名约定)
9. [代码审查清单](#代码审查清单)

---

## 类型使用规范

### 1. 优先使用 record 处理不可变数据

**规则**: DTO 和只读数据优先使用 `record` 而不是 `class`。

**适用场景**:
- DTO (Data Transfer Objects)
- API 请求/响应模型
- 配置对象
- 事件数据
- 领域事件
- 查询结果

**示例**:

```csharp
// ✅ 正确：使用 record
public record PathSegmentResult(
    string DiverterId,
    Direction Direction,
    bool IsSuccess,
    string? ErrorMessage = null
);

// 使用 with 表达式创建修改后的副本
var result = new PathSegmentResult("D1", Direction.Left, true);
var failedResult = result with { IsSuccess = false, ErrorMessage = "Timeout" };

// ❌ 错误：使用 class
public class PathSegmentResult
{
    public string DiverterId { get; set; }
    public Direction Direction { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### 2. 使用 readonly struct 确保不可变性

**规则**: 对于小型值类型（≤16 字节），使用 `readonly struct` 确保不可变性并提高性能。

**示例**:

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

// 或使用 record struct（推荐）
public readonly record struct DiverterPosition(
    string DiverterId,
    int SequenceNumber
);

// ❌ 错误：可变 struct
public struct DiverterPosition
{
    public string DiverterId { get; set; }  // 可变
    public int SequenceNumber { get; set; }  // 可变
}
```

### 3. 使用 file 作用域类型封装内部实现

**规则**: 工具类、辅助类、内部实现细节应使用 `file` 修饰符限制可见性。

**示例**:

```csharp
// PathGenerator.cs
namespace ZakYip.WheelDiverterSorter.Core;

public class PathGenerator
{
    public SwitchingPath GeneratePath(string chuteId)
    {
        double time = PathCalculator.CalculateTravelTime(10.0, 1.5);
        int angle = DirectionMapper.MapDirectionToAngle(Direction.Left);
        // ...
    }
}

// ✅ 正确：file 作用域类型
file static class PathCalculator
{
    public static double CalculateTravelTime(double distance, double speed)
    {
        return distance / speed;
    }
}

file static class DirectionMapper
{
    public static int MapDirectionToAngle(Direction direction) => direction switch
    {
        Direction.Straight => 0,
        Direction.Left => 30,
        Direction.Right => 45,
        _ => throw new ArgumentException("Unknown direction")
    };
}
```

### 4. 使用 required + init 确保对象完整初始化

**规则**: 必需的属性使用 `required` 修饰符标记，使用 `init` 访问器使属性在初始化后不可变。

**示例**:

```csharp
// ✅ 正确
public class RouteConfiguration
{
    public required string ChuteId { get; init; }
    public required List<DiverterConfig> Diverters { get; init; }
    public double BeltSpeed { get; init; } = 1.5;  // 可选，有默认值
}

// 编译器强制设置必需属性
var config = new RouteConfiguration
{
    ChuteId = "CHUTE_A",
    Diverters = new List<DiverterConfig>()
};

// ❌ 错误：可能未初始化
public class RouteConfiguration
{
    public string ChuteId { get; set; }  // 可能为 null
    public List<DiverterConfig> Diverters { get; set; }  // 可能未初始化
}
```

---

## 可空引用类型

### 1. 启用可空引用类型

**规则**: 
- 所有项目必须在 `.csproj` 中启用 `<Nullable>enable</Nullable>`
- 明确区分可空 (`?`) 和不可空引用类型
- 避免使用 null-forgiving 操作符 (`!`) 除非绝对必要

**项目配置**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

**代码示例**:

```csharp
// ✅ 正确：明确可空性
public string? GetChuteName(string chuteId)
{
    var config = _repository.GetById(chuteId);
    return config?.Name;  // 使用 null-conditional 操作符
}

public void ProcessParcel(string parcelId)
{
    string? targetChute = GetTargetChute(parcelId);
    
    // 方式 1: null-coalescing 提供默认值
    Console.WriteLine((targetChute ?? "UNKNOWN").ToUpper());
    
    // 方式 2: 明确的 null 检查
    if (targetChute is not null)
    {
        Console.WriteLine(targetChute.ToUpper());
    }
}

// ❌ 错误：未明确可空性
public string GetChuteName(string chuteId)
{
    var config = _repository.GetById(chuteId);
    return config.Name;  // config 可能为 null
}
```

### 2. 禁止新增 #nullable disable

**规则**: 
- **严格禁止**在新增文件中添加 `#nullable disable`
- 对于遗留代码，逐步移除 `#nullable disable`

---

## 时间处理规范

### 1. 统一使用 ISystemClock

**规则**: 
- 所有时间获取必须通过 `ISystemClock` 接口
- 使用 `ISystemClock.LocalNow` 表示业务时间
- 仅在与外部系统协议要求时使用 `ISystemClock.UtcNow`
- **严格禁止**直接使用 `DateTime.Now` 或 `DateTime.UtcNow`

**示例**:

```csharp
// ✅ 正确
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
            CreatedAt = _clock.LocalNow,  // ✅ 使用 ISystemClock
            CreatedAtUtc = _clock.UtcNow  // ✅ 仅在需要 UTC 时使用
        };
    }
}

// ❌ 错误
public Parcel CreateParcel(string sensorId)
{
    return new Parcel
    {
        ParcelId = GenerateId(),
        CreatedAt = DateTime.Now  // ❌ 禁止直接使用
    };
}
```

**原因**:
- 便于单元测试（可以 Mock 时间）
- 统一时区管理
- 避免时区转换错误

---

## 并发安全规范

### 1. 跨线程共享集合必须使用线程安全容器

**规则**: 任何跨线程共享的集合必须使用线程安全容器或明确的锁封装。

**线程安全容器**:
- `ConcurrentDictionary<TKey, TValue>`
- `ConcurrentQueue<T>`
- `ConcurrentBag<T>`
- `ConcurrentStack<T>`
- `ImmutableList<T>` / `ImmutableDictionary<TKey, TValue>`

**示例**:

```csharp
// ✅ 正确：使用线程安全容器
public class ParcelTracker
{
    private readonly ConcurrentDictionary<string, ParcelState> _parcels = new();
    
    public void UpdateParcelState(string parcelId, ParcelState state)
    {
        _parcels.AddOrUpdate(parcelId, state, (_, __) => state);
    }
    
    public ParcelState? GetParcelState(string parcelId)
    {
        _parcels.TryGetValue(parcelId, out var state);
        return state;
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
    
    public ParcelState? GetParcelState(string parcelId)
    {
        lock (_lock)
        {
            _parcels.TryGetValue(parcelId, out var state);
            return state;
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

---

## 异常处理规范

### 1. 后台服务必须使用 SafeExecutionService

**规则**: 所有 `BackgroundService` 的主循环必须使用 `ISafeExecutionService` 包裹，确保未捕获异常不会导致进程崩溃。

**示例**:

```csharp
// ✅ 正确
public class PackageSortingWorker : BackgroundService
{
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<PackageSortingWorker> _logger;
    
    public PackageSortingWorker(
        ISafeExecutionService safeExecutor,
        ILogger<PackageSortingWorker> logger)
    {
        _safeExecutor = safeExecutor;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ProcessNextParcel();
                    await Task.Delay(100, stoppingToken);
                }
            },
            operationName: "PackageSortingLoop",
            cancellationToken: stoppingToken
        );
    }
}

// ❌ 错误：未使用 SafeExecutionService
public class PackageSortingWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessNextParcel();  // 异常可能导致进程崩溃
            await Task.Delay(100, stoppingToken);
        }
    }
}
```

### 2. 异常信息必须清晰具体

**规则**: 抛出异常时，必须提供清晰、具体的错误信息，便于快速定位问题。

**示例**:

```csharp
// ✅ 正确
if (string.IsNullOrWhiteSpace(chuteId))
{
    throw new ArgumentException(
        $"格口 ID 不能为空或仅包含空白字符。ParcelId: {parcelId}",
        nameof(chuteId));
}

if (path.Segments.Count == 0)
{
    throw new InvalidOperationException(
        $"无法为包裹 {parcelId} 生成路径：目标格口 {chuteId} 没有配置摆轮路径。");
}

// ❌ 错误：信息不清晰
if (string.IsNullOrWhiteSpace(chuteId))
{
    throw new ArgumentException("Invalid input");  // ❌ 信息不明确
}

if (path.Segments.Count == 0)
{
    throw new Exception("Error");  // ❌ 异常类型和信息都不明确
}
```

---

## API 设计规范

### 1. 请求模型使用 record + required + 验证特性

**规则**: 
- 请求模型必须使用 `record`
- 必填字段使用 `required + init`
- 所有字段通过特性或 FluentValidation 进行验证

**示例**:

```csharp
// ✅ 正确
public record CreateChuteRequest
{
    [Required(ErrorMessage = "格口 ID 不能为空")]
    [StringLength(50, ErrorMessage = "格口 ID 长度不能超过 50 个字符")]
    public required string ChuteId { get; init; }
    
    [Required(ErrorMessage = "容量不能为空")]
    [Range(1, 100, ErrorMessage = "容量必须在 1-100 之间")]
    public required int Capacity { get; init; }
    
    [StringLength(200, ErrorMessage = "描述长度不能超过 200 个字符")]
    public string? Description { get; init; }
}

// ❌ 错误
public class CreateChuteRequest
{
    public string ChuteId { get; set; }  // ❌ 缺少 required, 缺少验证
    public int Capacity { get; set; }    // ❌ 缺少验证
}
```

### 2. 响应统一使用 ApiResponse<T>

**规则**: 所有 API 端点的响应必须统一使用 `ApiResponse<T>` 包装。

**示例**:

```csharp
// ✅ 正确
[HttpPost]
public async Task<ActionResult<ApiResponse<ChuteDto>>> CreateChute(
    [FromBody] CreateChuteRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ApiResponse.Error<ChuteDto>(
            "请求参数验证失败",
            ModelState.SelectMany(x => x.Value?.Errors ?? [])
                      .Select(e => e.ErrorMessage)
                      .ToList()));
    }
    
    var chute = await _service.CreateChuteAsync(request);
    return Ok(ApiResponse.Success(chute, "格口创建成功"));
}

// ❌ 错误：未使用 ApiResponse<T>
[HttpPost]
public async Task<ActionResult<ChuteDto>> CreateChute(
    [FromBody] CreateChuteRequest request)
{
    var chute = await _service.CreateChuteAsync(request);
    return Ok(chute);  // ❌ 应使用 ApiResponse.Success
}
```

---

## 方法设计原则

### 1. 保持方法短小精悍

**规则**: 
- 每个方法应该只做一件事
- 方法长度建议不超过 20-30 行
- 复杂逻辑拆分为多个小方法

**示例**:

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

private async Task<Parcel> ValidateParcelAsync(string parcelId)
{
    var parcel = await _repository.GetParcelAsync(parcelId);
    if (parcel == null)
    {
        throw new ParcelNotFoundException(parcelId);
    }
    return parcel;
}

private async Task<string> DetermineChuteAsync(string parcelId)
{
    var chuteId = await _ruleEngineClient.GetTargetChuteAsync(parcelId);
    if (string.IsNullOrEmpty(chuteId))
    {
        _logger.LogWarning($"包裹 {parcelId} 未分配格口，使用异常格口");
        return _config.ExceptionChuteId;
    }
    return chuteId;
}

// ❌ 错误：方法过长（50+ 行）
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    // 验证包裹
    var parcel = await _repository.GetParcelAsync(parcelId);
    if (parcel == null)
    {
        _logger.LogError($"包裹 {parcelId} 不存在");
        return new SortingResult { IsSuccess = false, Error = "Not found" };
    }
    
    // 请求格口
    var chuteId = await _ruleEngineClient.GetTargetChuteAsync(parcelId);
    if (string.IsNullOrEmpty(chuteId))
    {
        _logger.LogWarning($"包裹 {parcelId} 未分配格口");
        chuteId = _config.ExceptionChuteId;
    }
    
    // 生成路径
    var path = _pathGenerator.GeneratePath(chuteId);
    if (path == null)
    {
        _logger.LogError($"无法生成路径：格口 {chuteId}");
        return new SortingResult { IsSuccess = false, Error = "Path generation failed" };
    }
    
    // 执行路径
    var executionResult = await _executor.ExecutePathAsync(path);
    if (!executionResult.IsSuccess)
    {
        _logger.LogError($"路径执行失败: {executionResult.ErrorMessage}");
        return new SortingResult { IsSuccess = false, Error = executionResult.ErrorMessage };
    }
    
    // 记录结果
    await _repository.UpdateParcelStatusAsync(parcelId, "Sorted");
    _metrics.RecordSuccess(chuteId);
    
    return new SortingResult { IsSuccess = true, ActualChute = chuteId };
}
```

### 2. 使用清晰的方法名

**规则**: 方法名应该清晰表达意图，使用动词开头。

**推荐前缀**:
- `Get` / `Fetch` - 获取数据
- `Create` / `Add` - 创建新实体
- `Update` / `Modify` - 更新现有实体
- `Delete` / `Remove` - 删除实体
- `Validate` - 验证逻辑
- `Calculate` - 计算逻辑
- `Process` - 处理逻辑

**示例**:

```csharp
// ✅ 正确：清晰的方法名
public async Task<Parcel> GetParcelByIdAsync(string parcelId);
public async Task<SwitchingPath> GeneratePathToChuteAsync(string chuteId);
public async Task<bool> ValidateParcelStateAsync(string parcelId);
public double CalculateTravelTime(double distance, double speed);

// ❌ 错误：不清晰的方法名
public async Task<Parcel> DoAsync(string id);  // ❌ 不知道做什么
public async Task<SwitchingPath> PathAsync(string id);  // ❌ 是生成还是获取？
public async Task<bool> CheckAsync(string id);  // ❌ 检查什么？
public double Calc(double d, double s);  // ❌ 缩写不清晰
```

---

## 命名约定

### 1. 命名空间

- 使用 PascalCase
- 遵循项目结构：`ZakYip.WheelDiverterSorter.<Layer>.<Feature>`

**示例**:
```csharp
namespace ZakYip.WheelDiverterSorter.Core.PathGeneration;
namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;
namespace ZakYip.WheelDiverterSorter.Drivers.Leadshine;
```

### 2. 类、接口、结构、枚举

- 使用 PascalCase
- 接口以 `I` 开头
- 抽象类可以使用 `Base` 或 `Abstract` 后缀

**示例**:
```csharp
public interface ISwitchingPathGenerator { }
public class PathGenerator : ISwitchingPathGenerator { }
public abstract class DiverterDriverBase { }
public enum ParcelStatus { }
public record struct DiverterPosition { }
```

### 3. 方法和属性

- 使用 PascalCase
- 异步方法以 `Async` 结尾

**示例**:
```csharp
public string ParcelId { get; init; }
public async Task<Parcel> CreateParcelAsync(string sensorId);
public double CalculateTravelTime(double distance, double speed);
```

### 4. 字段和变量

- 私有字段使用 `_camelCase` 前缀
- 局部变量使用 camelCase
- 常量使用 PascalCase

**示例**:
```csharp
private readonly ISystemClock _clock;
private readonly ConcurrentDictionary<string, Parcel> _parcels;

public void ProcessParcel(string parcelId)
{
    var parcel = GetParcel(parcelId);
    int retryCount = 0;
}

private const int MaxRetryCount = 3;
private const string DefaultChuteId = "CHUTE_EXCEPTION";
```

### 5. 参数

- 使用 camelCase

**示例**:
```csharp
public Parcel CreateParcel(string sensorId, DateTime detectedAt)
{
    // ...
}
```


---

## 基础设施与工具类规范

### 1. Result 类型策略

**规则**: 避免重复创建相似的 Result 类型。使用三种标准 Result 类型之一。

**三种标准 Result 类型**:

#### ① ApiResponse\<T\> (Host.Models)
**用途**: API 层 HTTP 响应  
**使用场景**: 仅在 API Controllers 中使用  
**特性**: 状态码、消息、时间戳、HTTP 语义

```csharp
// ✅ 正确：在 API Controller 中使用
[HttpGet]
public ActionResult<ApiResponse<SystemConfigDto>> GetConfig()
{
    var config = _service.GetConfig();
    return Ok(ApiResponse<SystemConfigDto>.Ok(config));
}
```

#### ② OperationResult\<T\> (Ingress.Upstream)
**用途**: 上游通信结果，带性能追踪  
**使用场景**: 通信层、上游集成  
**特性**: 延迟追踪、降级标识、来源信息

```csharp
// ✅ 正确：在上游通信中使用
var result = await _upstreamClient.RequestRoutingAsync(parcelId);
if (result.IsSuccess) 
{
    _metrics.RecordLatency(result.LatencyMs);
}
```

#### ③ OperationResult (Core.LineModel.Routing) - 非泛型
**用途**: 简单的成功/失败操作结果  
**使用场景**: 内部领域逻辑、状态转换  
**特性**: 布尔成功标志、错误消息

```csharp
// ✅ 正确：在领域逻辑中使用
var result = routePlan.TryApplyChuteChange(newChuteId, timestamp, out decision);
if (!result.IsSuccess)
{
    _logger.LogWarning(result.ErrorMessage);
}
```

**领域特定 Result 类型** - 仅在以下情况允许:
- 携带领域特定信息（如 `PathExecutionResult` 包含失败段详情）
- 在单个限界上下文内使用
- 具有独特语义，不被标准类型覆盖

**❌ 禁止**:
```csharp
// ❌ 错误：不要创建功能重复的 Result 类型
public class MyOperationResult  // 与 OperationResult 重复
{
    public bool Success { get; set; }
    public string Message { get; set; }
}
```

### 2. Helper 类规范

**规则**: 创建 Helper 类前必须检查是否已存在等价实现。

**优先级顺序**:
1. **File-scoped** (`file static class`) - 仅在单个文件使用
2. **Internal** (`internal static class`) - 仅在单个项目使用
3. **Public** (`public static class`) - 跨项目使用（需强有力理由）

**示例**:

```csharp
// ✅ 正确：File-scoped helper（推荐）
namespace MyNamespace;

public class MyService
{
    public void DoSomething()
    {
        var cleaned = StringHelper.Clean(input);
    }
}

// 仅在此文件可见
file static class StringHelper
{
    public static string Clean(string input) => input.Trim();
}
```

```csharp
// ✅ 正确：Internal helper（项目内共享）
namespace MyNamespace.Utilities;

internal static class ValidationHelper
{
    public static bool IsValidChuteId(int id) => id > 0;
}
```

**❌ 禁止**:
```csharp
// ❌ 错误：不必要的 public helper
public static class StringUtils  // 应该是 file 或 internal
{
    public static string Trim(string input) => input.Trim();
}
```

**创建 Helper 前检查清单**:
- [ ] 此功能是否已存在于现有 Helper 中？
- [ ] 能否使用 `file static class` 替代？
- [ ] 能否使用 `internal static class` 替代？
- [ ] 是否真的需要 Helper 类，还是可以内联逻辑？

### 3. Extension 方法规范

**规则**: Extension 方法必须有明确的必要性。

**允许的场景**:
- **ServiceExtensions** - 仅用于 DI 注册
- 扩展你不拥有的类型（如 .NET 框架类型）

```csharp
// ✅ 正确：ServiceExtensions for DI
public static class MyFeatureServiceExtensions
{
    public static IServiceCollection AddMyFeature(this IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
        return services;
    }
}
```

**优先考虑实例方法**:
```csharp
// ✅ 推荐：实例方法
public class Parcel
{
    public bool IsExpired(ISystemClock clock) 
        => clock.LocalNow > ExpirationTime;
}

// ❌ 不推荐：Extension 方法（对于你拥有的类型）
public static class ParcelExtensions
{
    public static bool IsExpired(this Parcel parcel, ISystemClock clock)
        => clock.LocalNow > parcel.ExpirationTime;
}
```

---

## 代码审查清单

在提交代码前，请检查：

### 类型使用
- [ ] DTO 和只读数据使用 `record` / `record struct`
- [ ] 小型值类型使用 `readonly struct`
- [ ] 工具类使用 `file` 作用域类型
- [ ] 必填属性使用 `required + init`

### 基础设施与工具类
- [ ] 未创建重复的 Result 类型（使用三种标准类型之一）
- [ ] Helper 类使用适当的可见性（`file` > `internal` > `public`）
- [ ] 新 Helper 类前已检查现有实现
- [ ] Extension 方法有明确必要性
- [ ] 对自有类型优先使用实例方法而非扩展方法

### 可空引用类型
- [ ] 启用可空引用类型（`Nullable=enable`）
- [ ] 未新增 `#nullable disable`
- [ ] 明确区分可空和不可空引用

### 时间处理
- [ ] 所有时间通过 `ISystemClock` 获取
- [ ] 未直接使用 `DateTime.Now` / `DateTime.UtcNow`

### 并发安全
- [ ] 跨线程集合使用线程安全容器或锁
- [ ] 无数据竞争风险

### 异常处理
- [ ] 后台服务使用 `ISafeExecutionService`
- [ ] 异常信息清晰具体

### API 设计
- [ ] 请求模型使用 `record + required + 验证`
- [ ] 响应使用 `ApiResponse<T>`

### 方法设计
- [ ] 方法短小（< 30 行）
- [ ] 单一职责
- [ ] 清晰的命名

### 命名约定
- [ ] 遵循 PascalCase / camelCase 约定
- [ ] 接口以 `I` 开头
- [ ] 异步方法以 `Async` 结尾

---

**相关文档**:
- [架构原则](ARCHITECTURE_PRINCIPLES.md)
- [Copilot 约束说明](../.github/copilot-instructions.md)
- [测试策略](../TESTING_STRATEGY.md)

**文档版本**: 1.0  
**最后更新**: 2025-11-21  
**维护团队**: ZakYip Development Team
