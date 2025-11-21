# Code Review Checklist - 代码审查检查清单

## 概述

本文档定义了代码审查过程中必须检查的编码规范和技术债务合规性要求。所有 PR 在合并前必须满足这些标准。

---

## 1. 强制性检查项（必须通过）

### 1.1 构建状态 ✅
- [ ] 构建成功，0 错误，0 警告
- [ ] 所有项目都能正常编译

### 1.2 DateTime 使用规范 ✅ 
**禁止使用 UTC 时间 - 这是硬性规定**

- [ ] ❌ **不允许**直接使用 `DateTime.Now`
- [ ] ❌ **不允许**直接使用 `DateTime.UtcNow`  
- [ ] ❌ **不允许**直接使用 `DateTimeOffset.UtcNow`
- [ ] ❌ **不允许**使用 `_clock.UtcNow`（除非在 SystemClock 实现类中）
- [ ] ✅ **必须**使用 `ISystemClock.LocalNow` 获取当前时间
- [ ] ✅ **必须**使用 `ISystemClock.LocalNowOffset` 获取当前时间（DateTimeOffset）
- [ ] ✅ **必须**在构造函数中注入 `ISystemClock` 依赖

**验证命令**:
```bash
dotnet test --filter "FullyQualifiedName~DateTimeUsageComplianceTests"
```

**示例**:
```csharp
// ❌ 错误
public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

// ✅ 正确
public class MyService
{
    private readonly ISystemClock _clock;
    
    public MyService(ISystemClock clock)
    {
        _clock = clock;
    }
    
    public MyRecord CreateRecord()
    {
        return new MyRecord
        {
            CreatedAt = _clock.LocalNowOffset
        };
    }
}
```

### 1.3 可空引用类型 ✅
**所有项目必须启用可空引用类型**

- [ ] ✅ 所有 `.csproj` 文件包含 `<Nullable>enable</Nullable>`
- [ ] ❌ **不允许**在新代码中添加 `#nullable disable`
- [ ] ✅ 所有可能为 null 的引用类型标记为 `?`
- [ ] ✅ 所有不可为 null 的引用类型不使用 `?`

**验证命令**:
```bash
dotnet test --filter "FullyQualifiedName~CodingStandardsComplianceTests.AllProjectsShouldEnableNullableReferenceTypes"
```

**示例**:
```xml
<!-- .csproj 文件 -->
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

```csharp
// ✅ 正确
public string Name { get; init; }  // 不可为 null
public string? Email { get; init; }  // 可以为 null
```

### 1.4 SafeExecution 包装 ✅
**所有 BackgroundService 必须使用 SafeExecution**

- [ ] ✅ 所有 `BackgroundService` 的 `ExecuteAsync` 方法使用 `ISafeExecutionService.ExecuteAsync` 包裹
- [ ] ✅ 所有高风险回调（IO、通讯、驱动事件）使用 SafeExecution

**验证命令**:
```bash
dotnet test --filter "FullyQualifiedName~SafeExecutionCoverageTests"
```

**示例**:
```csharp
// ✅ 正确
public class MyWorker : BackgroundService
{
    private readonly ISafeExecutionService _safeExecutor;
    
    public MyWorker(ISafeExecutionService safeExecutor)
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
                    await DoWorkAsync();
                }
            },
            operationName: "MyWorkerLoop",
            cancellationToken: stoppingToken
        );
    }
}
```

### 1.5 枚举位置规范 ✅
**所有 Core 项目的枚举必须放在 Core/Enums 目录**

- [ ] ✅ **必须**将所有枚举放在 `Core/Enums` 目录下
- [ ] ✅ **必须**按类别创建子目录（Hardware, Sensors, Communication, IoBinding, Sorting, System, Routing, Conveyor）
- [ ] ❌ **不允许**在其他目录中定义新枚举
- [ ] ❌ **不允许**将枚举嵌入在其他类文件中

**目录结构**:
```
Core/Enums/
├── Hardware/        # 硬件相关枚举
├── Sensors/         # 传感器相关枚举
├── Communication/   # 通讯相关枚举
├── IoBinding/       # IO绑定相关枚举
├── Sorting/         # 分拣相关枚举
├── System/          # 系统相关枚举
├── Routing/         # 路由相关枚举
└── Conveyor/        # 输送相关枚举
```

**示例**:
```csharp
// ✅ 正确：在 Core/Enums/Hardware/DiverterDirection.cs
namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

public enum DiverterDirection
{
    Straight = 0,
    Left = 1,
    Right = 2
}

// ❌ 错误：在其他目录定义枚举
namespace ZakYip.WheelDiverterSorter.Core.LineModel;

public enum SomeEnum  // 不允许！
{
    // ...
}

// ❌ 错误：嵌入在类文件中
public class MyClass
{
    public enum MyEnum  // 不允许！应提取到独立文件
    {
        // ...
    }
}
```

---

## 2. 推荐性检查项（强烈建议）

### 2.1 使用 required + init 模式 ⭐
**确保对象创建时必填字段被设置**

- [ ] DTO/Model 中的必填属性使用 `required` 关键字
- [ ] 不可变属性使用 `init` 而不是 `set`
- [ ] 结合验证特性（`[Required]`, `[StringLength]` 等）

**示例**:
```csharp
// ✅ 推荐
public record CreateUserRequest
{
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }
    
    [EmailAddress]
    public string? Email { get; init; }  // Optional
}

// ❌ 不推荐
public class CreateUserRequest
{
    public string Name { get; set; }  // 可能未初始化
    public string Email { get; set; }  // 可变
}
```

### 2.2 使用 record 处理不可变数据 ⭐
**Record 是 DTO 和只读数据的理想选择**

- [ ] DTO/Response/Request 类优先使用 `record` 而不是 `class`
- [ ] 事件参数（EventArgs）使用 `record`
- [ ] 只读配置对象使用 `record`

**验证命令**:
```bash
dotnet test --filter "FullyQualifiedName~CodingStandardsComplianceTests.DTOsShouldUseRecordTypes"
```

**示例**:
```csharp
// ✅ 推荐
public record UserDto(string Name, int Age);

// ✅ 或带验证的 record
public record CreateUserRequest
{
    [Required]
    public required string Name { get; init; }
    
    [Range(1, 120)]
    public required int Age { get; init; }
}

// ❌ 不推荐
public class UserDto
{
    public string Name { get; set; }
    public int Age { get; set; }
}
```

### 2.3 使用 readonly struct ⭐
**小型值类型使用 readonly struct 提高性能**

- [ ] 小型值类型（≤16 字节）使用 `readonly struct`
- [ ] 不需要可变性时使用 `readonly struct`
- [ ] 考虑使用 `readonly record struct`（C# 10+）

**示例**:
```csharp
// ✅ 推荐
public readonly struct Point
{
    public double X { get; init; }
    public double Y { get; init; }
    
    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }
}

// ✅ 或使用 record struct
public readonly record struct Point(double X, double Y);

// ❌ 不推荐（可变）
public struct Point
{
    public double X { get; set; }
    public double Y { get; set; }
}
```

### 2.4 使用文件作用域类型 ⭐
**工具类和内部类型使用 file 关键字**

- [ ] 内部辅助类使用 `file class`
- [ ] 只在当前文件使用的类型使用 `file` 修饰符
- [ ] 避免污染全局命名空间

**示例**:
```csharp
// PathGenerator.cs
namespace ZakYip.WheelDiverterSorter.Core;

public class PathGenerator
{
    public SwitchingPath GeneratePath(string chuteId)
    {
        double time = PathCalculator.CalculateTravelTime(10.0, 1.5);
        // ...
    }
}

// ✅ 推荐：文件作用域，不污染全局
file static class PathCalculator
{
    public static double CalculateTravelTime(double distance, double speed)
    {
        return distance / speed;
    }
}

// ❌ 不推荐：全局可见
public static class PathCalculator
{
    public static double CalculateTravelTime(double distance, double speed)
    {
        return distance / speed;
    }
}
```

### 2.5 保持方法小巧和专注 ⭐
**一个方法 = 一个职责**

- [ ] 方法长度不超过 50 行（理想情况下 < 30 行）
- [ ] 每个方法只做一件事
- [ ] 复杂逻辑拆分为多个小方法
- [ ] 方法名清晰描述其功能

**验证命令**:
```bash
dotnet test --filter "FullyQualifiedName~CodingStandardsComplianceTests.LargeMethodsShouldBeReported"
```

**示例**:
```csharp
// ✅ 推荐：小而专注的方法
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
    // 验证逻辑
}

private async Task<string> DetermineChuteAsync(string parcelId)
{
    // 格口确定逻辑
}

// ❌ 不推荐：一个方法做太多事情（50+ 行）
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    // 验证
    var parcel = await _repository.GetParcelAsync(parcelId);
    if (parcel == null) { /* 10 行错误处理 */ }
    
    // 路由
    var chuteId = await _ruleEngineClient.GetTargetChuteAsync(parcelId);
    if (string.IsNullOrEmpty(chuteId)) { /* 10 行错误处理 */ }
    
    // 生成路径
    var path = _pathGenerator.GeneratePath(chuteId);
    if (path == null) { /* 10 行错误处理 */ }
    
    // 执行
    var result = await _executor.ExecutePathAsync(path);
    if (!result.IsSuccess) { /* 10 行错误处理 */ }
    
    // 记录
    await _repository.UpdateParcelStatusAsync(parcelId, "Sorted");
    // ... 更多逻辑
}
```

---

## 3. 线程安全检查项

### 3.1 跨线程集合 ⚠️
**多线程访问的集合必须线程安全**

- [ ] 跨线程共享的集合使用 `ConcurrentDictionary`, `ConcurrentQueue` 等
- [ ] 或使用明确的锁保护
- [ ] 或标记为 `[SingleThreadedOnly]`（如果确认单线程）

**验证命令**:
```bash
dotnet test --filter "FullyQualifiedName~ThreadSafeCollectionTests"
```

**示例**:
```csharp
// ✅ 推荐：线程安全集合
private readonly ConcurrentDictionary<string, ParcelState> _parcels = new();

// ✅ 或使用锁
private readonly object _lock = new();
private readonly Dictionary<string, ParcelState> _parcels = new();

public void UpdateParcelState(string parcelId, ParcelState state)
{
    lock (_lock)
    {
        _parcels[parcelId] = state;
    }
}

// ✅ 或标记为单线程
[SingleThreadedOnly]
private readonly List<string> _items = new();

// ❌ 不推荐：非线程安全且多线程访问
private readonly Dictionary<string, ParcelState> _parcels = new();

public void UpdateParcelState(string parcelId, ParcelState state)
{
    _parcels[parcelId] = state;  // 多线程访问时不安全
}
```

---

## 4. API 端点检查项

### 4.1 请求模型规范 ⭐

- [ ] 使用 `record` 定义请求 DTO
- [ ] 必填字段使用 `required + init`
- [ ] 添加验证特性（`[Required]`, `[StringLength]`, `[Range]` 等）

### 4.2 响应模型规范 ⭐

- [ ] 统一使用 `ApiResponse<T>` 包装响应
- [ ] 响应模型使用 `record`
- [ ] 包含时间戳和状态信息

**示例**:
```csharp
// ✅ 推荐
public record CreateChuteRequest
{
    [Required]
    [StringLength(50)]
    public required string ChuteId { get; init; }
    
    [Range(1, 100)]
    public required int Capacity { get; init; }
}

[HttpPost]
public async Task<ActionResult<ApiResponse<ChuteDto>>> CreateChute(
    [FromBody] CreateChuteRequest request)
{
    var chute = await _service.CreateChuteAsync(request);
    return Ok(ApiResponse.Success(chute));
}
```

---

## 5. 代码审查流程

### 5.1 提交前（开发者）

**必须执行的命令**:
```bash
# 1. 运行合规性测试
dotnet test tests/ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/

# 2. 运行所有单元测试
dotnet test

# 3. 运行构建
dotnet build

# 4. 检查 git 状态
git status
```

**检查清单**:
- [ ] ✅ 所有测试通过（包括合规性测试）
- [ ] ✅ 构建成功（0 错误，0 警告）
- [ ] ✅ 没有 UTC 时间使用
- [ ] ✅ 没有未提交的临时文件

### 5.2 代码审查（Reviewer）

**必须检查的项目**:

1. **技术债务合规性**
   - [ ] DateTime 使用规范（无 UTC 时间）
   - [ ] SafeExecution 覆盖（BackgroundService 已包裹）
   - [ ] 可空引用类型已启用
   - [ ] 线程安全集合使用正确

2. **编码规范**
   - [ ] 使用 `required + init` 模式
   - [ ] DTO 使用 `record`
   - [ ] 小型值类型使用 `readonly struct`
   - [ ] 工具类使用 `file class`
   - [ ] 方法小巧且专注

3. **架构规范**
   - [ ] Host 层不包含业务逻辑
   - [ ] 业务逻辑在 Application/Core 层
   - [ ] 硬件访问通过 Drivers 接口

4. **测试覆盖**
   - [ ] 新功能有对应的测试
   - [ ] 修改的功能测试已更新
   - [ ] 所有测试通过

### 5.3 自动化检查（CI/CD）

**GitHub Actions 应该运行**:
```yaml
- name: Compliance Tests
  run: dotnet test tests/ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/
  
- name: Unit Tests
  run: dotnet test
  
- name: Build
  run: dotnet build
```

---

## 6. 快速参考

### 禁止模式 ❌

```csharp
// ❌ DateTime
DateTime.Now
DateTime.UtcNow
DateTimeOffset.UtcNow
_clock.UtcNow

// ❌ 可空性
#nullable disable  // 新代码中

// ❌ 可变 DTO
public class UserDto
{
    public string Name { get; set; }
}

// ❌ BackgroundService 无保护
protected override async Task ExecuteAsync(...)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        // 无 SafeExecution 包裹
    }
}
```

### 推荐模式 ✅

```csharp
// ✅ DateTime
_clock.LocalNow
_clock.LocalNowOffset

// ✅ 可空性
<Nullable>enable</Nullable>
public string Name { get; init; }
public string? Email { get; init; }

// ✅ DTO
public record UserDto
{
    [Required]
    public required string Name { get; init; }
    
    [EmailAddress]
    public string? Email { get; init; }
}

// ✅ BackgroundService
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await _safeExecutor.ExecuteAsync(
        async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 业务逻辑
            }
        },
        operationName: "WorkerLoop",
        cancellationToken: stoppingToken
    );
}

// ✅ 小方法
public async Task ProcessAsync()
{
    await ValidateAsync();
    await ExecuteAsync();
    await RecordAsync();
}
```

---

## 7. 违规处理

### 轻微违规
- 方法稍长（50-80 行）
- 可以改进但不影响功能的命名

**处理**: 留评论建议改进，可以合并但建议后续优化

### 严重违规
- 使用 UTC 时间
- BackgroundService 未使用 SafeExecution
- 未启用可空引用类型
- 跨线程集合不安全

**处理**: 要求修复后才能合并，不接受例外

---

## 8. 工具和资源

### 自动化工具
- **合规性测试**: `dotnet test tests/ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/`
- **代码分析器**: 已集成 Roslyn 分析器
- **格式化**: `.editorconfig` 已配置

### 文档
- `copilot-instructions.md` - 完整编码规范
- `tests/TechnicalDebtComplianceTests/README.md` - 合规性测试使用指南
- `docs/implementation/TECHNICAL_DEBT_COMPLIANCE_STATUS.md` - 技术债务状态

### 报告
运行测试后生成的报告：
- `/tmp/datetime_violations_report.md`
- `/tmp/background_service_coverage_report.md`
- `/tmp/thread_safe_collection_report.md`
- `/tmp/coding_standards_compliance_report.md`

---

**版本**: 1.0  
**最后更新**: 2025-11-21  
**维护者**: Development Team
