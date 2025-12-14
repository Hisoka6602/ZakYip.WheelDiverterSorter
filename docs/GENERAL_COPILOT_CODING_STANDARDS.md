# 通用 Copilot 编码规范 (General Copilot Coding Standards)

本文档定义了适用于各类 C# 项目的通用编码标准和 AI 辅助开发约束。这些规范专门为 GitHub Copilot 等 AI 辅助工具设计，确保代码质量和架构一致性。

**重要提示**: 违反这些规则的修改均视为无效修改。

---

## 目录

1. [PR 完整性约束](#pr-完整性约束)
2. [影分身零容忍策略（最重要）](#影分身零容忍策略最重要)
3. [类型使用规范](#类型使用规范)
4. [可空引用类型](#可空引用类型)
5. [时间处理规范](#时间处理规范)
6. [并发安全规范](#并发安全规范)
7. [异常处理规范](#异常处理规范)
8. [API 设计规范](#api-设计规范)
9. [方法设计原则](#方法设计原则)
10. [命名约定](#命名约定)
11. [分层架构原则](#分层架构原则)
12. [通讯与重试原则](#通讯与重试原则)
13. [测试与质量保证](#测试与质量保证)
14. [代码清理规范](#代码清理规范)
15. [代码审查清单](#代码审查清单)

---

## PR 完整性约束

### 规则

**小型 PR（< 24小时工作量）强制完整性**:
- ❌ **禁止**提交半完成状态（如：只删除接口但不修复引用）
- ❌ **禁止**留下编译错误或测试失败
- ❌ **禁止**使用"后续PR修复"作为理由
- ❌ **禁止**代码中出现"TODO: 后续PR"等标记
- ✅ **必须**保证代码可编译、测试通过、功能完整

**大型 PR（≥ 24小时工作量）分阶段处理**:
- ✅ 允许分多个 PR 逐步完成
- ✅ 每个阶段 PR 必须独立可编译、测试通过
- ✅ 未完成部分必须登记到技术债务文档
- ✅ 技术债条目必须包含：
  - 已完成和未完成的工作清单
  - 详细的下一步指引（文件清单、修改建议、注意事项）
  - 预估工作量和风险等级

**实施要求**:

```markdown
## 技术债务模板

### [TD-XXX] <任务名称>（⏳ 进行中）

**问题描述**: <简要描述要解决的问题>

**已完成工作（本PR）**:
- [x] 子任务1
- [x] 子任务2

**未完成工作（需后续PR）**:
- [ ] 子任务3 - 原因：依赖外部服务未就绪
- [ ] 子任务4 - 原因：需要重构影响范围过大

**下一步指引**:
- 受影响文件: `path/to/file1.cs`, `path/to/file2.cs`
- 修改建议: 1) 备份现有测试 2) 修改XXX接口调用为YYY
- 注意事项: ⚠️ 不要修改ZZZ配置，会影响生产环境

**预估工作量**: 4-6小时  
**风险等级**: 中等  
**优先级**: P1（高优先级）
```

---

## 影分身零容忍策略（最重要）

> **⚠️ 危险警告**: 影分身代码是最危险的技术债务类型，会导致：
> - 维护噩梦：需要同时修改多处相同逻辑
> - 状态不一致：不同实现可能产生不同结果
> - 难以定位问题：不知道应该使用哪个版本
> - 架构混乱：破坏单一职责原则

### 什么是"影分身"？

**影分身**是指功能相同或高度相似的重复代码，表现形式包括：

1. **重复接口**：同一职责出现第二个接口
2. **纯转发类型**：只做方法转发、不增加任何实质逻辑的 Facade/Adapter/Wrapper/Proxy
3. **重复 DTO/Model**：多处存在字段结构完全一致的数据传输对象
4. **重复 Options/Settings**：多处定义相同的配置类
5. **重复工具方法**：在不同类中重复实现相同逻辑的辅助方法
6. **重复常量**：在多个类中定义语义相同的常量（魔法数字）

### 零容忍策略

**规则**:

1. **新增影分身 = PR 不合规**
   - 一旦发现新增的影分身类型，即视为当前 PR 不合规
   - PR 必须在当前分支中删除该影分身类型或合并到既有实现中
   - **不能**"先留下以后再清理"

2. **历史影分身必须优先清理**
   - 若在当前 PR 涉及对应模块或调用链，必须优先尝试清理
   - 如短期内无法彻底清理，必须登记技术债并规划清理 PR

3. **禁止行为**
   - ❌ 新增任何形式的"影分身"类型，并期望后续再清理
   - ❌ 保留一套 Legacy 实现与一套新实现并存
   - ❌ 在 PR 描述中以"与本次改动无关"为理由保留新增影分身

### 影分身类型详解

#### 1. 纯转发 Facade/Adapter/Wrapper/Proxy（严格禁止）

**判定标准**（满足以下条件判定为影分身）：
- 类型以 `*Facade` / `*Adapter` / `*Wrapper` / `*Proxy` 结尾
- 只持有 1~2 个服务接口字段
- 方法体只做直接调用另一个服务的方法，没有任何附加逻辑

**附加逻辑包括**（有以下任一逻辑则合法）：
- 类型转换/协议映射逻辑（如 LINQ Select、new 对象初始化器）
- 事件订阅/转发机制（如 `+=` 事件绑定）
- 状态跟踪（如 `_lastKnownState` 字段）
- 批量操作聚合（如 `foreach` + `await`）
- 验证或重试逻辑

**示例**:

```csharp
// ❌ 错误：纯转发适配器（影分身）
public class LoggerAdapter : ICustomLogger
{
    private readonly ILogger _logger;

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);  // ❌ 一行转发，无附加值
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);  // ❌ 一行转发
    }
}

// ✅ 正确：直接使用 ILogger，删除无意义包装
public class OrderService
{
    private readonly ILogger _logger;  // ✅ 直接依赖 ILogger

    public OrderService(ILogger logger)
    {
        _logger = logger;
    }
}

// ✅ 正确：有附加值的适配器（类型转换 + 事件订阅）
public class SensorEventProviderAdapter : ISensorEventProvider
{
    private readonly IHardwareSensorService _hardwareService;

    public SensorEventProviderAdapter(IHardwareSensorService service)
    {
        _hardwareService = service;
        _hardwareService.SensorTriggered += OnHardwareSensorTriggered;  // ✅ 事件订阅
    }

    private void OnHardwareSensorTriggered(object? sender, HardwareSensorEventArgs e)
    {
        // ✅ 类型转换和协议映射
        var domainEvent = new SensorDetectedArgs
        {
            SensorId = e.DeviceId,
            DetectedAt = e.Timestamp,
            SignalStrength = e.RawValue
        };
        SensorDetected?.Invoke(this, domainEvent);
    }

    public event EventHandler<SensorDetectedArgs>? SensorDetected;
}
```

#### 2. 重复工具方法（严格禁止）

**规则**:
1. 具有相同语义的工具方法必须统一实现、统一调用
2. 在新增工具方法之前，必须搜索现有 `Utils` / `Extensions` / `Helpers` 类
3. 若发现多处功能重复的方法，应立即收敛为一个公共实现

**示例**:

```csharp
// ❌ 错误：在多个类中重复实现相同的字符串处理
// 文件：OrderService.cs
public class OrderService
{
    private string CleanInput(string input) => input.Trim().ToUpperInvariant();
}

// 文件：ProductService.cs
public class ProductService
{
    private string CleanInput(string input) => input.Trim().ToUpperInvariant();  // ❌ 重复！
}

// ✅ 正确：使用 file-scoped 工具类
// 文件：OrderService.cs
public class OrderService
{
    public Order CreateOrder(string orderId)
    {
        var cleanId = StringHelper.Clean(orderId);
        // ...
    }
}

file static class StringHelper
{
    public static string Clean(string input) => input.Trim().ToUpperInvariant();
}

// 文件：ProductService.cs
public class ProductService
{
    public Product CreateProduct(string productId)
    {
        var cleanId = StringHelper.Clean(productId);  // ✅ 复用同一实现
        // ...
    }
}

file static class StringHelper
{
    public static string Clean(string input) => input.Trim().ToUpperInvariant();
}

// ✅ 更好：如果多处使用，提取到公共 Utilities
// 文件：Core/Utilities/StringHelper.cs
namespace MyProject.Core.Utilities;

internal static class StringHelper
{
    public static string Clean(string input) => input.Trim().ToUpperInvariant();
}
```

#### 3. 重复 DTO/Model（严格禁止）

**规则**:
- 字段结构完全一致的 DTO/Model 只允许存在一个
- 不同层之间的数据传输使用同一个 DTO，或通过映射转换

**示例**:

```csharp
// ❌ 错误：重复定义相同结构的 DTO
// 文件：Api/Models/UserDto.cs
public record UserDto(string UserId, string UserName, string Email);

// 文件：Services/Models/UserDto.cs
public record UserDto(string UserId, string UserName, string Email);  // ❌ 影分身！

// ✅ 正确：只定义一次，共享使用
// 文件：Core/Models/UserDto.cs
namespace MyProject.Core.Models;

public record UserDto(string UserId, string UserName, string Email);

// 在 API 层使用
namespace MyProject.Api.Controllers;

using MyProject.Core.Models;

[ApiController]
public class UsersController : ControllerBase
{
    [HttpGet("{id}")]
    public ActionResult<UserDto> GetUser(string id)
    {
        // 使用共享的 UserDto
    }
}

// 在 Service 层使用
namespace MyProject.Services;

using MyProject.Core.Models;

public class UserService
{
    public UserDto GetUser(string id)
    {
        // 使用共享的 UserDto
    }
}
```

#### 4. 重复 Options/Settings（严格禁止）

**规则**:
- 配置类只定义一次，不同组件通过依赖注入共享

**示例**:

```csharp
// ❌ 错误：重复定义相同的配置类
// 文件：Services/ConnectionOptions.cs
public class ConnectionOptions
{
    public string Host { get; set; }
    public int Port { get; set; }
    public int TimeoutSeconds { get; set; }
}

// 文件：Infrastructure/ConnectionOptions.cs
public class ConnectionOptions  // ❌ 影分身！
{
    public string Host { get; set; }
    public int Port { get; set; }
    public int TimeoutSeconds { get; set; }
}

// ✅ 正确：只定义一次，通过 DI 共享
// 文件：Core/Configuration/ConnectionOptions.cs
namespace MyProject.Core.Configuration;

public class ConnectionOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5000;
    public int TimeoutSeconds { get; set; } = 30;
}

// Startup.cs
services.Configure<ConnectionOptions>(configuration.GetSection("Connection"));

// 在任何需要的地方注入使用
public class MyService
{
    private readonly ConnectionOptions _options;
    
    public MyService(IOptions<ConnectionOptions> options)
    {
        _options = options.Value;
    }
}
```

#### 5. 魔法数字（禁止重复定义）

**规则**:
1. 同一语义的常量在整个解决方案中仅允许定义一个权威来源
2. 禁止在多个不同类中重复定义语义相同的常量

**示例**:

```csharp
// ❌ 错误：在多个类中重复定义相同的超时常量
// 文件：OrderService.cs
public class OrderService
{
    private const int TimeoutMilliseconds = 30000;  // ❌ 重复定义
}

// 文件：PaymentService.cs
public class PaymentService
{
    private const int TimeoutMilliseconds = 30000;  // ❌ 重复定义
}

// ✅ 正确：统一定义常量
// 文件：Core/Constants/TimeoutConstants.cs
namespace MyProject.Core.Constants;

public static class TimeoutConstants
{
    public const int DefaultTimeoutMilliseconds = 30000;
    public const int LongOperationTimeoutMilliseconds = 60000;
}

// 在需要的地方引用
public class OrderService
{
    public async Task ProcessOrderAsync(string orderId)
    {
        using var cts = new CancellationTokenSource(TimeoutConstants.DefaultTimeoutMilliseconds);
        // ...
    }
}

public class PaymentService
{
    public async Task ProcessPaymentAsync(string paymentId)
    {
        using var cts = new CancellationTokenSource(TimeoutConstants.DefaultTimeoutMilliseconds);
        // ...
    }
}
```

### 影分身检测与修复流程

**检测步骤**:
1. 搜索相同类名、相同方法名
2. 比较类型的字段结构和方法签名
3. 检查是否存在功能重复的实现

**修复步骤**:
1. 识别权威实现（通常是最完整、最新的版本）
2. 删除所有重复实现
3. 更新所有调用方，改为使用权威实现
4. 运行测试确保功能正常
5. 更新文档说明唯一的权威来源

---

## 类型使用规范

### 1. 优先使用 record 处理不可变数据

**规则**: DTO 和只读数据优先使用 `record` 而不是 `class`。

**适用场景**:
- DTO (Data Transfer Objects)
- API 请求/响应模型
- 配置对象
- 事件数据
- 查询结果

**示例**:

```csharp
// ✅ 正确：使用 record
public record UserDto(
    string UserId,
    string UserName,
    string Email,
    DateTime CreatedAt
);

// 使用 with 表达式创建修改后的副本
var user = new UserDto("U001", "John", "john@example.com", DateTime.Now);
var updatedUser = user with { UserName = "John Doe" };

// ❌ 错误：使用 class 且未实现值语义
public class UserDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 2. 使用 readonly struct 确保不可变性

**规则**: 对于小型值类型（≤16 字节），使用 `readonly struct` 确保不可变性并提高性能。

**示例**:

```csharp
// ✅ 正确：使用 readonly record struct
public readonly record struct Point(int X, int Y);

// ❌ 错误：可变 struct
public struct Point
{
    public int X { get; set; }  // 可变
    public int Y { get; set; }  // 可变
}
```

### 3. 使用 file 作用域类型封装内部实现

**规则**: 工具类、辅助类、内部实现细节应使用 `file` 修饰符限制可见性。

**示例**:

```csharp
// MainService.cs
namespace MyApp.Services;

public class MainService
{
    public double Calculate(double value)
    {
        return MathHelper.Square(value);
    }
}

// ✅ 正确：file 作用域类型，仅在此文件可见
file static class MathHelper
{
    public static double Square(double value)
    {
        return value * value;
    }
}
```

### 4. 使用 required + init 确保对象完整初始化

**规则**: 必需的属性使用 `required` 修饰符标记，使用 `init` 访问器使属性在初始化后不可变。

**示例**:

```csharp
// ✅ 正确
public class Configuration
{
    public required string ConnectionString { get; init; }
    public required int MaxRetries { get; init; }
    public int TimeoutSeconds { get; init; } = 30;  // 可选，有默认值
}

// 编译器强制设置必需属性
var config = new Configuration
{
    ConnectionString = "Server=localhost;Database=mydb",
    MaxRetries = 3
};
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
public string? GetUserName(string userId)
{
    var user = _repository.GetById(userId);
    return user?.Name;  // 使用 null-conditional 操作符
}

public void ProcessUser(string userId)
{
    string? userName = GetUserName(userId);
    
    // 方式 1: null-coalescing 提供默认值
    Console.WriteLine((userName ?? "Unknown").ToUpper());
    
    // 方式 2: 明确的 null 检查
    if (userName is not null)
    {
        Console.WriteLine(userName.ToUpper());
    }
}

// ❌ 错误：未明确可空性
public string GetUserName(string userId)
{
    var user = _repository.GetById(userId);
    return user.Name;  // user 可能为 null，运行时异常风险
}
```

### 2. 禁止新增 #nullable disable

**规则**: 
- **严格禁止**在新增文件中添加 `#nullable disable`
- 对于遗留代码，逐步移除 `#nullable disable`

---

## 时间处理规范

### 1. 统一使用时间抽象接口

**规则**: 
- 所有时间获取必须通过抽象接口（如 `ISystemClock`）
- **严格禁止**直接使用 `DateTime.Now` 或 `DateTime.UtcNow`

**接口定义示例**:

```csharp
public interface ISystemClock
{
    DateTime LocalNow { get; }
    DateTime UtcNow { get; }
}

public class SystemClock : ISystemClock
{
    public DateTime LocalNow => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
}
```

**使用示例**:

```csharp
// ✅ 正确
public class OrderService
{
    private readonly ISystemClock _clock;
    
    public OrderService(ISystemClock clock)
    {
        _clock = clock;
    }
    
    public Order CreateOrder(string customerId)
    {
        return new Order
        {
            OrderId = GenerateId(),
            CreatedAt = _clock.LocalNow,  // ✅ 使用抽象接口
            CustomerId = customerId
        };
    }
}

// ❌ 错误
public Order CreateOrder(string customerId)
{
    return new Order
    {
        OrderId = GenerateId(),
        CreatedAt = DateTime.Now  // ❌ 禁止直接使用
    };
}
```

**原因**:
- 便于单元测试（可以 Mock 时间）
- 统一时区管理
- 避免时区转换错误
- 支持时间旅行测试场景

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
public class SessionTracker
{
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new();
    
    public void UpdateSession(string sessionId, SessionState state)
    {
        _sessions.AddOrUpdate(sessionId, state, (_, __) => state);
    }
}

// ✅ 正确：使用明确的锁
public class SessionTracker
{
    private readonly Dictionary<string, SessionState> _sessions = new();
    private readonly object _lock = new();
    
    public void UpdateSession(string sessionId, SessionState state)
    {
        lock (_lock)
        {
            _sessions[sessionId] = state;
        }
    }
}

// ❌ 错误：非线程安全
public class SessionTracker
{
    private readonly Dictionary<string, SessionState> _sessions = new();
    
    public void UpdateSession(string sessionId, SessionState state)
    {
        _sessions[sessionId] = state;  // ❌ 多线程不安全
    }
}
```

---

## 异常处理规范

### 1. 后台服务必须使用安全执行包装器

**规则**: 所有 `BackgroundService` 的主循环必须使用安全执行包装器（如 `ISafeExecutionService`），确保未捕获异常不会导致进程崩溃。

**接口定义示例**:

```csharp
public interface ISafeExecutionService
{
    Task ExecuteAsync(
        Func<Task> operation,
        string operationName,
        CancellationToken cancellationToken = default);
}
```

**使用示例**:

```csharp
// ✅ 正确
public class DataProcessingWorker : BackgroundService
{
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<DataProcessingWorker> _logger;
    
    public DataProcessingWorker(
        ISafeExecutionService safeExecutor,
        ILogger<DataProcessingWorker> logger)
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
                    await ProcessNextBatch();
                    await Task.Delay(1000, stoppingToken);
                }
            },
            operationName: "DataProcessingLoop",
            cancellationToken: stoppingToken
        );
    }
}

// ❌ 错误：未使用安全执行包装器
public class DataProcessingWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessNextBatch();  // 异常可能导致进程崩溃
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

### 2. 异常信息必须清晰具体

**规则**: 抛出异常时，必须提供清晰、具体的错误信息，便于快速定位问题。

**示例**:

```csharp
// ✅ 正确
if (string.IsNullOrWhiteSpace(userId))
{
    throw new ArgumentException(
        $"用户 ID 不能为空或仅包含空白字符。RequestId: {requestId}",
        nameof(userId));
}

if (items.Count == 0)
{
    throw new InvalidOperationException(
        $"无法处理空列表。操作: {operationName}, RequestId: {requestId}");
}

// ❌ 错误：信息不清晰
if (string.IsNullOrWhiteSpace(userId))
{
    throw new ArgumentException("Invalid input");  // ❌ 信息不明确
}

if (items.Count == 0)
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
public record CreateUserRequest
{
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(50, ErrorMessage = "用户名长度不能超过 50 个字符")]
    public required string UserName { get; init; }
    
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public required string Email { get; init; }
    
    [Range(18, 120, ErrorMessage = "年龄必须在 18-120 之间")]
    public int? Age { get; init; }
}

// ❌ 错误
public class CreateUserRequest
{
    public string UserName { get; set; }  // ❌ 缺少 required, 缺少验证
    public string Email { get; set; }     // ❌ 缺少验证
}
```

### 2. 响应统一使用包装类型

**规则**: 所有 API 端点的响应应使用统一的响应包装类型。

**响应模型示例**:

```csharp
public record ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public List<string>? Errors { get; init; }
    public DateTime Timestamp { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "操作成功")
        => new() { Success = true, Data = data, Message = message, Timestamp = DateTime.UtcNow };

    public static ApiResponse<T> Error(string message, List<string>? errors = null)
        => new() { Success = false, Message = message, Errors = errors, Timestamp = DateTime.UtcNow };
}
```

**使用示例**:

```csharp
// ✅ 正确
[HttpPost]
public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser(
    [FromBody] CreateUserRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ApiResponse<UserDto>.Error(
            "请求参数验证失败",
            ModelState.SelectMany(x => x.Value?.Errors ?? [])
                      .Select(e => e.ErrorMessage)
                      .ToList()));
    }
    
    var user = await _service.CreateUserAsync(request);
    return Ok(ApiResponse<UserDto>.Ok(user, "用户创建成功"));
}
```

### 3. Swagger 文档注释规范

**规则**: 所有 API 端点必须具有完整的 Swagger 注释。

**要求**:
- 每个 Controller 类必须有 `/// <summary>` 注释
- 每个 Action 方法必须使用 `[SwaggerOperation]` 特性
- 每个 Action 方法必须使用 `[SwaggerResponse]` 特性标注所有可能的响应码
- DTO 属性必须有 `/// <summary>` 注释

**示例**:

```csharp
/// <summary>
/// 获取用户信息
/// </summary>
[HttpGet("{id}")]
[SwaggerOperation(
    Summary = "获取用户信息",
    Description = "根据用户ID获取用户详细信息",
    OperationId = "GetUser",
    Tags = new[] { "用户管理" }
)]
[SwaggerResponse(200, "成功返回用户信息", typeof(ApiResponse<UserDto>))]
[SwaggerResponse(404, "用户不存在", typeof(ApiResponse<object>))]
public ActionResult<ApiResponse<UserDto>> GetUser(string id)
{
    // ...
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
public async Task<OrderResult> ProcessOrderAsync(string orderId)
{
    var order = await ValidateOrderAsync(orderId);
    var paymentResult = await ProcessPaymentAsync(order);
    var shipmentResult = await ArrangeShipmentAsync(order);
    
    await UpdateOrderStatusAsync(orderId, OrderStatus.Completed);
    
    return new OrderResult { IsSuccess = true, OrderId = orderId };
}

// ❌ 错误：方法过长（50+ 行），做了太多事情
public async Task<OrderResult> ProcessOrderAsync(string orderId)
{
    // 验证订单
    var order = await _repository.GetOrderAsync(orderId);
    if (order == null) { /* ... */ }
    
    // 处理支付
    var paymentResult = await _paymentService.ProcessPaymentAsync(order.PaymentInfo);
    if (!paymentResult.IsSuccess) { /* ... */ }
    
    // 安排发货
    var shipmentResult = await _shipmentService.ArrangeShipmentAsync(order.ShippingInfo);
    if (!shipmentResult.IsSuccess) { /* ... */ }
    
    // ... 更多代码
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

### 3. async 方法必须包含 await 操作符

**规则**: 
- 所有 `async` 方法必须在方法体内使用至少一个 `await` 操作符
- 如果方法不需要异步操作，应移除 `async` 修饰符

**示例**:

```csharp
// ✅ 正确：async 方法包含 await
public async Task<bool> ValidateUserAsync(string userId)
{
    var user = await _repository.GetUserAsync(userId);
    return user != null && user.IsActive;
}

// ✅ 正确：不需要异步时移除 async 修饰符
public Task<bool> ValidateUserAsync(string userId)
{
    var user = _repository.GetUser(userId);  // 同步方法
    return Task.FromResult(user != null && user.IsActive);
}

// ❌ 错误：async 方法缺少 await（CS1998 警告）
public async Task<bool> ValidateUserAsync(string userId)
{
    var user = _repository.GetUser(userId);  // ❌ 没有 await
    return user != null && user.IsActive;
}
```

---

## 命名约定

### 1. 命名空间

- 使用 PascalCase
- 遵循项目结构：`CompanyName.ProjectName.<Layer>.<Feature>`
- 命名空间必须与文件夹结构匹配

**示例**:
```csharp
// ✅ 正确：命名空间与文件夹结构匹配
// 文件位置：src/Services/Extensions/ServiceExtensions.cs
namespace MyCompany.MyProject.Services.Extensions;

// ❌ 错误：命名空间与文件夹结构不匹配
// 文件位置：src/Services/Extensions/ServiceExtensions.cs
namespace MyCompany.MyProject.Services;  // ❌ 缺少 .Extensions
```

### 2. 类、接口、结构、枚举

- 使用 PascalCase
- 接口以 `I` 开头
- 抽象类可以使用 `Base` 或 `Abstract` 后缀

### 3. 方法和属性

- 使用 PascalCase
- 异步方法以 `Async` 结尾

### 4. 字段和变量

- 私有字段使用 `_camelCase` 前缀
- 局部变量使用 camelCase
- 常量使用 PascalCase

### 5. 参数

- 使用 camelCase

---

## 分层架构原则

### 1. 分层职责清晰（DDD 分层）

**架构层次**:

```
┌─────────────────────────────────────┐
│        Presentation（表示层）         │
│  - API Controllers                   │
│  - 输入验证                          │
└─────────────────────────────────────┘
              ↓ 依赖
┌─────────────────────────────────────┐
│       Application（应用层）           │
│  - 业务编排                          │
│  - 用例实现                          │
└─────────────────────────────────────┘
              ↓ 依赖
┌─────────────────────────────────────┐
│         Domain（领域层）              │
│  - 领域模型                          │
│  - 业务规则                          │
└─────────────────────────────────────┘
              ↓ 依赖
┌─────────────────────────────────────┐
│    Infrastructure（基础设施层）       │
│  - 数据持久化                        │
│  - 外部系统通信                      │
└─────────────────────────────────────┘
```

**层次依赖规则**:
- 上层可以依赖下层
- 下层**不得**依赖上层
- 同层之间通过接口通信
- 基础设施层实现领域层定义的接口（依赖反转）

### 2. Presentation 层职责（严格限制）

**允许的职责**:
- ✅ API Controller 端点定义（仅调用应用层服务）
- ✅ 输入模型验证
- ✅ 响应格式化

**禁止的行为**:
- ❌ 直接包含业务逻辑
- ❌ 直接访问数据库或仓储
- ❌ 直接访问外部系统
- ❌ 复杂的数据处理和转换

---

## 通讯与重试原则

### 1. 客户端连接失败应无限重试 + 指数退避

**原则**: 与外部系统的连接采用**无限重试**策略，使用指数退避算法，设置合理的最大退避时间。

**推荐退避策略**:
```
初始退避: 200ms
指数增长: 200ms → 400ms → 800ms → 1600ms → 2000ms（最大）
持续退避: 2000ms, 2000ms, 2000ms, ...（无限重试）
```

**示例**:

```csharp
// ✅ 正确：无限重试，指数退避
public class ExternalServiceClient
{
    private const int InitialBackoffMs = 200;
    private const int MaxBackoffMs = 2000;
    
    public async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        int backoffMs = InitialBackoffMs;
        int attemptCount = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                attemptCount++;
                _logger.LogInformation($"尝试连接外部服务（第 {attemptCount} 次）...");
                
                await ConnectAsync();
                
                _logger.LogInformation("成功连接到外部服务");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"连接失败，{backoffMs}ms 后重试: {ex.Message}");
                
                await Task.Delay(backoffMs, cancellationToken);
                
                // 指数退避，但不超过最大值
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
        }
    }
}
```

### 2. 请求失败应记录日志但不自动重试

**原则**: 单次请求失败**只记录日志**，不进行自动重试，由调用方决定如何处理。

**示例**:

```csharp
// ✅ 正确：请求失败只记录日志
public async Task<bool> SendRequestAsync(string requestId, object payload)
{
    try
    {
        await _httpClient.PostAsJsonAsync("/api/endpoint", payload);
        
        _logger.LogInformation($"请求已发送: {requestId}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"发送请求失败: {requestId}");
        return false;  // ✅ 不重试，由调用方处理
    }
}
```

---

## 测试与质量保证

### 1. 单元测试覆盖核心逻辑

**原则**: 核心业务逻辑必须有充分的单元测试覆盖。

**测试覆盖率目标**:
- 领域层：≥ 85%
- 应用层：≥ 80%
- 基础设施层：≥ 70%
- 表示层：≥ 60%

### 2. 禁止删除或注释测试来绕过规则

**原则**: **严格禁止**注释或删除现有的测试用例来绕过规则检查。

**示例**:

```csharp
// ❌ 错误：注释测试
// [Fact]
// public async Task Should_Return_Error_When_Invalid_Input()
// {
//     // 这个测试失败了，先注释掉
// }

// ✅ 正确：修复代码或更新测试
[Fact]
public async Task Should_Return_Error_When_Invalid_Input()
{
    // 修复代码使测试通过
    // 或更新测试以反映新的业务规则
    var result = await _service.ProcessAsync("");
    Assert.False(result.IsSuccess);
    Assert.Contains("Invalid input", result.ErrorMessage);
}
```

### 3. 所有测试失败必须在当前 PR 中修复

**规则**:

1. 任意测试失败，一旦在本 PR 的 CI 中出现，就视为本 PR 的工作内容，**必须在当前 PR 中修复**
2. **禁止**在 PR 描述中说明"这是已有问题 / 与本 PR 无关"
3. **禁止**把测试失败留给"后续 PR 处理"

---

## 代码清理规范

### 1. 过时/废弃/重复代码必须立即删除

**规则**:

1. 一旦新增实现已经覆盖旧实现，旧实现必须在**同一个 PR 中立即删除**
2. 相同语义的两套实现不允许并存
3. 不允许通过 `[Obsolete]`、`Legacy`、`Deprecated` 等方式长期保留废弃代码

**禁止行为**:
- ❌ 新实现已投入使用，却仍长时间保留旧实现
- ❌ 为了"兼容历史"同时维护两套等价实现
- ❌ 新增代码继续依赖已明确不推荐使用的旧实现

### 2. 禁止使用 global using

**规则**:
1. 代码中禁止使用 `global using` 指令
2. 现有的 `global using` 应在后续重构中逐步移除

---

## 代码审查清单

在提交代码前，请检查：

### PR 完整性
- [ ] PR 可独立编译、测试通过
- [ ] 未留下"TODO: 后续PR"标记
- [ ] 大型 PR 的未完成部分已登记技术债

### 影分身检查（最重要）
- [ ] 未创建纯转发 Facade/Adapter/Wrapper/Proxy
- [ ] 未重复定义相同的工具方法
- [ ] 未重复定义相同结构的 DTO/Model
- [ ] 未重复定义相同的 Options/Settings
- [ ] 未在多处定义相同的常量
- [ ] 已清理历史影分身（如果涉及相关模块）

### 类型使用
- [ ] DTO 和只读数据使用 `record` / `record struct`
- [ ] 小型值类型使用 `readonly struct`
- [ ] 工具类使用 `file` 作用域类型
- [ ] 必填属性使用 `required + init`

### 可空引用类型
- [ ] 启用可空引用类型（`Nullable=enable`）
- [ ] 未新增 `#nullable disable`
- [ ] 明确区分可空和不可空引用

### 时间处理
- [ ] 所有时间通过抽象接口（如 `ISystemClock`）获取
- [ ] 未直接使用 `DateTime.Now` / `DateTime.UtcNow`

### 并发安全
- [ ] 跨线程集合使用线程安全容器或锁
- [ ] 无数据竞争风险

### 异常处理
- [ ] 后台服务使用安全执行包装器
- [ ] 异常信息清晰具体

### API 设计
- [ ] 请求模型使用 `record + required + 验证`
- [ ] 响应使用统一的包装类型
- [ ] 具有完整的 Swagger 注释

### 方法设计
- [ ] 方法短小（< 30 行）
- [ ] 单一职责
- [ ] 清晰的命名
- [ ] `async` 方法包含 `await` 操作符

### 命名约定
- [ ] 遵循 PascalCase / camelCase 约定
- [ ] 接口以 `I` 开头
- [ ] 异步方法以 `Async` 结尾
- [ ] 私有字段使用 `_camelCase` 前缀
- [ ] 命名空间与文件夹结构匹配

### 分层架构
- [ ] 遵循分层职责
- [ ] 表示层不包含业务逻辑
- [ ] 通过接口访问基础设施

### 测试
- [ ] 核心逻辑有单元测试覆盖
- [ ] 所有测试通过
- [ ] 未删除或注释测试

### 代码清理
- [ ] 未使用 `global using`
- [ ] 已删除过时/废弃/重复代码
- [ ] 未保留 `[Obsolete]` / `Legacy` / `Deprecated` 代码

---

## 总结

本文档提供了适用于各类 C# 项目的通用编码标准，特别强调了**影分身零容忍策略**。

**最关键的原则**:

1. **零容忍影分身**：重复代码是最危险的技术债务
2. **PR 完整性**：小型 PR 必须完整，大型 PR 必须登记技术债
3. **使用现代 C# 特性**：record, readonly struct, file class, required, init
4. **启用可空引用类型**：明确表达可空性
5. **通过抽象接口访问系统资源**：时间、基础设施
6. **确保线程安全和并发正确性**
7. **保持方法短小精悍**：单一职责
8. **遵循清晰的命名约定**
9. **遵守分层架构原则**
10. **充分的测试覆盖**

**违规后果**: 任何违反本文档规则的修改，均视为**无效修改**，不得合并到主分支。

---

**文档版本**: 1.0  
**最后更新**: 2025-12-14  
**适用范围**: 通用 C# 项目（Copilot 驱动开发）
