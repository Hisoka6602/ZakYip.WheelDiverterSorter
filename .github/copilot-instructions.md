# ZakYip.WheelDiverterSorter – Copilot 约束说明（必须遵守）

本文档定义了仓库级的编码规范、架构原则和流程约束。所有开发人员和 AI 辅助工具（包括 GitHub Copilot）在生成代码或 PR 时必须严格遵守这些规则。

**重要提示**: 任何违反下述规则的修改，均视为无效修改，不得合并。

---

## 零、强制性架构规则（最高优先级）🔴

> **所有规则通过 ArchTests 自动验证，违反任何规则将导致 PR 自动失败**
> 
> **详细文档**: `docs/MANDATORY_RULES_AND_DEAD_CODE.md`  
> **ArchTests**: `tests/ZakYip.WheelDiverterSorter.ArchTests/MandatoryArchitectureRulesTests.cs`

### 规则0: PR完整性约束 🔴

**规则**: 
- 评估工作量 **< 24小时** 的 PR 必须在单个 PR 中完成所有工作
- 评估工作量 **≥ 24小时** 的 PR 允许分阶段完成，但未完成部分必须记录到技术债务

**违规后果**: ❌ **PR自动失败**

**小型PR（< 24小时）强制完整性**:
- ❌ 禁止提交半完成状态（如：只删除接口但不修复引用）
- ❌ 禁止留下编译错误或测试失败
- ❌ 禁止使用"后续PR修复"作为理由
- ❌ 禁止代码中出现"TODO: 后续PR"等标记
- ✅ 必须保证代码可编译、测试通过、功能完整

**大型PR（≥ 24小时）分阶段处理**:
- ✅ 允许分多个 PR 逐步完成
- ✅ 每个阶段 PR 必须独立可编译、测试通过
- ✅ 未完成部分必须登记到 `TechnicalDebtLog.md`
- ✅ 技术债条目必须包含：
  - 已完成和未完成的工作清单
  - 详细的下一步指引（文件清单、修改建议、注意事项）
  - 预估工作量和风险等级

**ArchTests 验证**:
```csharp
[Fact] SmallPR_MustBeCompletelyFinished_NoCompilationErrors()
[Fact] SmallPR_MustBeCompletelyFinished_NoFailingTests()
[Fact] SmallPR_MustBeCompletelyFinished_NoTodoForNextPR()
[Fact] LargePR_IncompleteParts_MustBeDocumentedInTechnicalDebt()
```

---

### 规则1: 枚举位置强制约束 🔴

**规则**: 所有枚举必须定义在 `ZakYip.WheelDiverterSorter.Core/Enums/` 的子目录中（按类型分类）

**违规后果**: ❌ **PR自动失败**

**允许的枚举位置**:
```
src/Core/ZakYip.WheelDiverterSorter.Core/Enums/
├── Hardware/      # 硬件相关（DiverterDirection, IoLevel, SensorType等）
├── Parcel/        # 包裹相关（ParcelFinalStatus等）
├── System/        # 系统相关（SystemState, RuntimeMode等）
├── Communication/ # 通信相关（ConnectionMode, CommunicationMode等）
├── Sorting/       # 分拣相关（PathFailureReason, SortingMode等）
├── Simulation/    # 仿真相关（SimulationStepType等）
└── Monitoring/    # 监控相关（OverloadReason等）
```

**禁止行为**:
- ❌ 在 Drivers、Host、Execution、Communication 等项目中定义枚举
- ❌ 在任何非 Core/Enums 目录定义枚举

**ArchTests 验证**:
```csharp
[Fact] AllEnums_MustBeDefinedIn_CoreEnumsDirectory()
```

---

### 规则2: 事件载荷位置强制约束 🔴

**规则**: 所有事件载荷（EventArgs/Event）必须定义在 `ZakYip.WheelDiverterSorter.Core/Events/` 的子目录中（按类型分类）

**违规后果**: ❌ **PR自动失败**

**允许的事件位置**:
```
src/Core/ZakYip.WheelDiverterSorter.Core/Events/
├── Alarm/         # 报警事件（AlarmEvent等）
├── Hardware/      # 硬件事件（DeviceConnectionEventArgs等）
├── Sensor/        # 传感器事件（ParcelDetectedEventArgs, SensorFaultEventArgs等）
├── Sorting/       # 分拣事件（ParcelDivertedEventArgs, RoutePlannedEventArgs等）
├── Communication/ # 通信事件（EmcLockEventArgs等）
├── Simulation/    # 仿真事件（SimulatedSensorEvent等）
└── Monitoring/    # 监控事件（AlertRaisedEventArgs等）
```

**白名单例外**（特殊情况允许在其他位置）:
- Communication.Abstractions 中的接口定义事件（ClientConnectionEventArgs等）
- Execution 中的路径重规划事件（ReroutingSucceededEventArgs等）
- 仿真项目特有事件（SimulatedParcelResultEventArgs）

**禁止行为**:
- ❌ 在 Drivers、Host、Observability、Simulation/Models 等目录定义事件
- ❌ 在任何非 Core/Events 目录定义新事件（白名单除外）

**ArchTests 验证**:
```csharp
[Fact] AllEventArgs_MustBeDefinedIn_CoreEventsDirectory()
```

---

### 规则3: 文档清理规则 🔴

**规则**: 文档文件必须及时清理或更新，不同类型文档有不同的生命周期限制

**违规后果**: ❌ **PR自动失败**

**文档生命周期规则**:
| 文档类型 | 最大保留时间 | 示例 |
|---------|-------------|------|
| PR总结文档 | 30天 | `PR_*_SUMMARY.md` |
| 任务清单 | 30天 | `*_TASKS.md`, `NEXT_*.md` |
| 修复记录 | 60天 | `FIX_*.md`, `fixes/*.md` |
| 实施计划 | 90天 | `*_IMPLEMENTATION.md`, `*_PLAN.md` |
| 一般文档 | 180天 | 其他未分类文档 |

**永久保留文档**（白名单）:
- 核心规范: `README.md`, `ARCHITECTURE_PRINCIPLES.md`, `CODING_GUIDELINES.md`, `RepositoryStructure.md`, `TechnicalDebtLog.md`, `CORE_ROUTING_LOGIC.md`, `MANDATORY_RULES_AND_DEAD_CODE.md`
- 使用指南: `guides/` 目录下所有文档
- 技术评估: `TOPOLOGY_LINEAR_N_DIVERTERS.md`, `S7_Driver_Enhancement.md`, `TouchSocket_Migration_Assessment.md`等

**处理建议**:
1. 删除已完成/过时的文档
2. 将历史记录整合到 `TechnicalDebtLog.md`
3. 将重要信息迁移到永久文档
4. 更新文档内容使其保持最新

**禁止行为**:
- ❌ 保留超过生命周期限制的过时文档
- ❌ 使用 `PR_*_SUMMARY.md` 等临时文档作为长期参考
- ❌ 创建 `TODO_*.md`, `REMAINING_*.md` 等待办文档（应使用 GitHub Issues）

**ArchTests 验证**:
```csharp
[Fact] Documentation_ShouldBeKeptUpToDate_NoOutdatedFiles()
[Fact] Documentation_ShouldFollowNamingConventions()
```

---

### 🔴 包裹路由与位置索引队列机制

**文档**: `docs/CORE_ROUTING_LOGIC.md`

**规则**: 任何PR如果可能影响以下核心机制，必须先获得明确批准才能合并：

1. **路径计算**: 基于拓扑和目标格口计算包裹路径
2. **位置索引队列**: 每个 `positionIndex` 对应一个 FIFO 任务队列
3. **触发机制**: 以IO触发为操作起点（禁止定时扫描或主动执行）
4. **超时处理**: 超时时执行异常动作并插入补偿任务
5. **队列清空**: 面板按钮（停止/急停/复位）时清空所有队列

**影响判定**: 以下类型的修改可能影响核心逻辑，必须在PR中明确标注并等待批准：
- 修改 `ISwitchingPathGenerator` 或其实现
- 修改拓扑结构模型或格口映射逻辑
- 新增或修改队列管理组件
- 修改传感器事件处理流程
- 修改超时判断或补偿逻辑
- 新增任何形式的自动触发机制

**强制约束**:
- ❌ 禁止在包裹创建时立即执行摆轮动作
- ❌ 禁止定时扫描包裹并主动执行动作
- ❌ 禁止破坏FIFO队列顺序（除超时补偿外）
- ✅ 必须仅在IO点触发时才执行动作
- ✅ 必须在面板按钮按下时清空队列

---

## 一、文档读取顺序

**在修改文档或规划 PR 时，必须按以下顺序阅读文档：**

1. **`docs/CORE_ROUTING_LOGIC.md`** - 核心路由逻辑（如果涉及路由/队列/触发机制）
2. **`docs/RepositoryStructure.md`** - 仓库结构、技术债索引
3. **`docs/DOCUMENTATION_INDEX.md`** - 完整文档索引
4. 具体目标 md 文件

**上游协议相关修改的额外读取要求**：

当 PR 涉及上游协议相关代码/DTO/文档修改时（如 `IUpstreamRoutingClient`、`ChuteAssignmentNotification`、`SortingCompletedNotification`、Communication 层客户端等），Copilot 必须优先读取：

1. **`docs/guides/UPSTREAM_CONNECTION_GUIDE.md`** - 上游协议的**唯一权威说明**
2. **`docs/RepositoryStructure.md`** 的「单一权威实现表」和「技术债索引」章节

> **目的**：防止再次制造"文档影分身"（TD-031），确保协议字段定义、示例 JSON、时序说明只在一处维护。

**禁止行为**：
- 禁止随意新增散落的 `*.md` 文件
- 新增文档必须同步更新 `docs/RepositoryStructure.md` 中的文档索引
- 禁止在 README 或其他文档中重复上游协议字段表/JSON 示例

**测试/工具项目相关修改的额外读取要求**（TD-032 新增）：

当 PR 涉及 `tests/` 或 `tools/` 目录的修改时，Copilot 必须优先读取：

1. **`docs/RepositoryStructure.md`** 的「3.10 工具项目结构」和「3.11 测试项目结构」章节
2. **`TechnicalDebtComplianceTests` 中的结构测试列表**，尤其是：
   - `TestProjectsStructureTests.ShouldNotDefineDomainModelsInTests`
   - `TestProjectsStructureTests.ShouldNotHaveLegacyDirectoriesInTests`
   - `TestProjectsStructureTests.ShouldNotUseGlobalUsingsInTests`
   - `TestProjectsStructureTests.ToolsShouldNotDefineDomainModels`

> **目的**：防止在测试/工具项目中重新定义 DTO/Options/Enums 等"影分身"类型，确保业务模型只在 src/ 目录中定义。

**测试项目约束**：
- ❌ 禁止定义 `ZakYip.WheelDiverterSorter.Core.*` 命名空间的类型
- ❌ 禁止定义以 `.Domain` 结尾的命名空间的类型
- ❌ 禁止 Legacy 目录
- ❌ 禁止 global using
- ✅ 允许测试辅助类型（Mock/Stub/Fake/Test/Helper/Builder/Factory 等命名模式）
- ✅ 允许引用 src 中的所有项目（用于测试）

**工具项目约束**：
- ❌ 禁止定义 Core/Domain 命名空间的业务模型
- ✅ 允许引用 Core 项目获取模型定义
- ✅ 工具专用类型应使用工具项目自己的命名空间（`*.Tools.*`）

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

### 6. async 方法必须包含 await 操作符（硬性要求）

**规则**: 
- 所有 `async` 方法必须在方法体内使用至少一个 `await` 操作符
- 缺少 `await` 的 `async` 方法会导致编译警告 CS1998，并可能引发运行时问题
- 如果方法不需要异步操作，应移除 `async` 修饰符并返回 `Task.CompletedTask` 或 `Task.FromResult<T>()`

**禁止行为**:
- 定义 `async` 方法但方法体内没有任何 `await` 调用
- 通过 `#pragma warning disable CS1998` 抑制警告而不修复根本问题

**实施要求**:
```csharp
// ✅ 正确：async 方法包含 await
public async Task<bool> ValidateDeviceAsync(string deviceId)
{
    var device = await _repository.GetDeviceAsync(deviceId);
    return device != null && device.IsEnabled;
}

// ✅ 正确：不需要异步时移除 async 修饰符
public Task<bool> ValidateDeviceAsync(string deviceId)
{
    var device = _repository.GetDevice(deviceId);  // 同步方法
    return Task.FromResult(device != null && device.IsEnabled);
}

// ❌ 错误：async 方法缺少 await（CS1998 警告）
public async Task<bool> ValidateDeviceAsync(string deviceId)
{
    var device = _repository.GetDevice(deviceId);  // ❌ 没有 await
    return device != null && device.IsEnabled;
}

// ❌ 错误：通过抑制警告绕过问题
#pragma warning disable CS1998  // ❌ 禁止
public async Task ProcessAsync()
{
    // 没有 await 的逻辑
}
#pragma warning restore CS1998
```

**例外情况**:
- 接口定义本身不包含实现，因此接口中的 `async Task` 方法签名是允许的
- 抽象方法或虚方法的基类定义中可能没有 `await`，但派生类实现必须遵守此规则

**检测方法**:
```bash
# 检测所有 CS1998 警告
dotnet build | grep "CS1998"
```

### 7. 禁止创建"纯转发"Facade/Adapter/Wrapper/Proxy 类型（PR-S2 新增）

**规则**: 禁止创建"只为转发调用而存在"的 Facade/Adapter/Wrapper/Proxy 类型。

**纯转发类型定义**（满足以下条件判定为影分身，禁止存在）：
- 类型以 `*Facade` / `*Adapter` / `*Wrapper` / `*Proxy` 结尾
- 只持有 1~2 个服务接口字段
- 方法体只做直接调用另一个服务的方法，没有任何附加逻辑

**附加逻辑包括**（有以下任一逻辑则合法）：
- 类型转换/协议映射逻辑（如 LINQ Select、new 对象初始化器）
- 事件订阅/转发机制（如 `+=` 事件绑定）
- 状态跟踪（如 `_lastKnownState` 字段）
- 批量操作聚合（如 `foreach` + `await`）
- 验证或重试逻辑

**实施要求**:
```csharp
// ❌ 错误：纯转发适配器
public class CommunicationLoggerAdapter : ICommunicationLogger
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
public class ExponentialBackoffRetryPolicy
{
    private readonly ILogger _logger;  // ✅ 直接依赖 ILogger

    public ExponentialBackoffRetryPolicy(ILogger logger)
    {
        _logger = logger;
    }
}

// ✅ 正确：有附加值的适配器（类型转换）
public class SensorEventProviderAdapter : ISensorEventProvider
{
    private readonly IParcelDetectionService _parcelDetectionService;

    public SensorEventProviderAdapter(IParcelDetectionService service)
    {
        _parcelDetectionService = service;
        _parcelDetectionService.ParcelDetected += OnUnderlyingParcelDetected;  // ✅ 事件订阅
    }

    private void OnUnderlyingParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        // ✅ 类型转换
        var executionArgs = new ParcelDetectedArgs
        {
            ParcelId = e.ParcelId,
            DetectedAt = e.DetectedAt,
            SensorId = e.SensorId
        };
        ParcelDetected?.Invoke(this, executionArgs);
    }
}

// ✅ 正确：有附加值的适配器（状态跟踪）
public class ShuDiNiaoWheelDiverterDeviceAdapter : IWheelDiverterDevice
{
    private readonly IWheelDiverterDriver _driver;
    private WheelDiverterState _lastKnownState = WheelDiverterState.Unknown;  // ✅ 状态跟踪

    public async Task<OperationResult> ExecuteAsync(WheelCommand command, CancellationToken ct)
    {
        bool success = command.Direction switch  // ✅ 协议转换
        {
            DiverterDirection.Left => await _driver.TurnLeftAsync(ct),
            DiverterDirection.Right => await _driver.TurnRightAsync(ct),
            DiverterDirection.Straight => await _driver.PassThroughAsync(ct),
            _ => false
        };
        
        if (success) _lastKnownState = ...;  // ✅ 状态更新
        return success ? OperationResult.Success() : OperationResult.Failure(...);
    }
}
```

**防线测试**：`TechnicalDebtComplianceTests.PureForwardingTypeDetectionTests.ShouldNotHavePureForwardingFacadeAdapterTypes`

**修复建议**（当检测到纯转发类型时）：
1. 删除纯转发类型
2. 调整 DI 注册与调用方，改为直接使用真正的服务接口
3. 如果有简单日志逻辑，移动到被调用服务内部
4. 如果确实需要装饰器模式，确保有明确的横切职责并在注释中说明

### 8. 禁止魔法数字（Magic Numbers）

**规则：**

1. 除极少数约定俗成的常量外（例如：`0`/`1`/`-1`、明显的布尔标记、`int.MaxValue` 等），业务代码与协议实现中**禁止直接书写魔法数字**：
   - 包括但不限于：阈值、重试次数、时间间隔、端口号、地址偏移、位掩码、协议命令码等；
   - 任何非显而易见的数值都必须通过**有含义的常量/枚举/配置**表达。

2. 协议与厂商相关数值：
   - 即便来自厂商协议文档（例如寄存器地址、命令码、标志位），也必须通过：
     - 枚举（带 `Description` 特性和中文注释），或
     - `static` 常量字段（命名必须体现语义）
     进行封装；
   - 禁止在协议解析、打包报文或状态判断中直接使用裸的数值字面量。

3. 阈值与业务常量：
   - 与业务行为相关的阈值（如“落格超时时间”“最大排队长度”“最大重试次数”等）必须通过：
     - Options / 配置模型（`*Options` / `*Settings`）；
     - 或 `static` 只读常量字段；
     暴露含义明确的名称，不允许在代码中直接写 `2000`、`3` 等数值。

4. 重用原则：
   - 同一语义的常量在整个解决方案中仅允许定义一个权威来源；
   - 禁止在多个不同类中重复定义语义相同的常量（这类情况同时也属于“影分身”技术债）。

**实施示例：**

```csharp
// ❌ 错误：魔法数字散落在逻辑中
if (elapsedMs > 2000)
{
    // 超时处理
}

var isError = (status & 0x10) != 0;

// ✅ 正确：使用具名常量或配置
public static class UpstreamRoutingConstants
{
    public const int MaxRoutingTimeoutMilliseconds = 2000;
}

[Flags]
public enum ShuDiNiaoStatusFlags
{
    [Description("无错误")]
    None = 0,

    [Description("通信错误")]
    CommunicationError = 0x10,
}

if (elapsedMs > UpstreamRoutingConstants.MaxRoutingTimeoutMilliseconds)
{
    // 超时处理
}

var hasCommunicationError = (status & (int)ShuDiNiaoStatusFlags.CommunicationError) != 0;
```

### 9. 命名空间必须与文件夹结构匹配（PR-SD8 新增）

**规则：**

1. 所有 C# 文件的命名空间必须与其所在的文件夹结构完全匹配：
   - 命名空间应基于项目根命名空间加上文件相对于项目根目录的路径
   - 例如：文件 `src/Execution/ZakYip.WheelDiverterSorter.Execution/Extensions/NodeHealthServiceExtensions.cs`
     必须使用命名空间 `ZakYip.WheelDiverterSorter.Execution.Extensions`

2. 命名空间计算规则：
   - 项目根命名空间为 `ZakYip.WheelDiverterSorter.<ProjectName>`（如 `Core`、`Execution`、`Drivers` 等）
   - 子目录名称直接追加到根命名空间后，用 `.` 分隔
   - 例如：`Services/Config/` 目录对应 `.Services.Config` 命名空间后缀

3. 此规则适用于所有 `src/` 目录下的 C# 文件，包括：
   - 类、接口、枚举、记录、结构体定义文件
   - 扩展方法文件
   - 配置文件

**实施要求：**

```csharp
// ✅ 正确：命名空间与文件夹结构匹配
// 文件位置：src/Execution/ZakYip.WheelDiverterSorter.Execution/Extensions/NodeHealthServiceExtensions.cs
namespace ZakYip.WheelDiverterSorter.Execution.Extensions;

public static class NodeHealthServiceExtensions
{
    // ...
}

// ❌ 错误：命名空间与文件夹结构不匹配
// 文件位置：src/Execution/ZakYip.WheelDiverterSorter.Execution/Extensions/NodeHealthServiceExtensions.cs
namespace ZakYip.WheelDiverterSorter.Execution;  // ❌ 缺少 .Extensions 后缀

public static class NodeHealthServiceExtensions
{
    // ...
}
```

**防线测试**：`TechnicalDebtComplianceTests.NamespaceLocationTests.AllFileNamespacesShouldMatchFolderStructure`

**修复建议**（当检测到命名空间不匹配时）：
1. 修改文件中的命名空间声明，使其与文件夹结构匹配
2. 更新所有引用该命名空间的 `using` 语句
3. 如果需要将文件移动到不同目录，同时更新命名空间

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

   - `Core/Abstractions/**`（仅包含 Execution/、Ingress/、Upstream/ 子目录，**不包含 Drivers/ 子目录**）
   - `Infrastructure/ZakYip.WheelDiverterSorter.Communication/Abstractions/`

2. **HAL 接口唯一权威位置**：所有硬件抽象层接口（原 `Core/Abstractions/Drivers/`）已统一迁移到 `Core/Hardware/` 及其子目录：
   - `Core/Hardware/Devices/` - 设备驱动接口（IWheelDiverterDriver、IEmcController 等）
   - `Core/Hardware/Ports/` - IO 端口接口（IInputPort、IOutputPort）
   - `Core/Hardware/IoLinkage/` - IO 联动接口（IIoLinkageDriver）
   - `Core/Hardware/Mappings/` - IO 映射接口（IVendorIoMapper）
   - `Core/Hardware/Providers/` - 配置提供者接口（ISensorVendorConfigProvider）

3. 其他任何项目、路径一律 **禁止** 新建 `Abstractions` 目录。

**禁止行为**:

- 在 `Drivers/`, `Ingress/`, `Communication/`, `Host/`, `Simulation/` 等项目中新建 `Abstractions` 目录。
- 新增仅包含 `global using` 或简单别名转发的"空壳 Abstractions 文件"。
- **在 `Core/Abstractions/` 下重建 `Drivers/` 目录**（HAL 接口必须放在 `Core/Hardware/`）。

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

- 访问硬件能力一律通过 Core/Hardware/ 定义的 HAL 接口（如 `IWheelDiverterDriver`、`IInputPort`、`IOutputPort` 等），由 DI 注入不同厂商实现。
- **HAL 唯一权威位置**：`Core/Hardware/` 及其子目录（Devices/、Ports/、IoLinkage/、Mappings/、Providers/）。Copilot 只能在此目录下新增或修改硬件抽象接口。
- 原 `Core/Abstractions/Drivers/` 目录已删除，**禁止**在 Abstractions 下重建任何硬件相关目录。

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

### 3. 提交前 API 端点全量健康检查（强制）

**规则：**

1. 每个 PR 在提交前，必须对**所有对外公开的 API 端点**做一次“可访问性”健康检查：
   - 无论本次 PR 是否修改了该端点，只要仍然对外暴露，就必须检查；
   - 检查内容至少包括：HTTP 状态码是否为预期（通常为 2xx / 预期错误码）、路由是否存在、模型绑定是否成功。

2. 发现任意 API 端点无法正常访问（例如 404、500、模型绑定异常、启动即崩溃等）时：
   - 当前 PR **必须**一并修复该问题；
   - 禁止以“该端点与本次 PR 无关”为理由跳过修复；
   - 禁止将“已知有 API 端点访问失败”的状态合并到主分支。

3. 健康检查可以通过以下方式之一实现（推荐优先使用自动化方式）：
   - 使用集成测试 / E2E 测试自动逐个访问所有公开端点；
   - 使用已维护的 Postman/HTTP 文件集合或脚本执行一轮 Smoke Test；
   - 通过 `Swagger` 导出的 OpenAPI 文档驱动的自动化探测脚本。

**禁止行为：**

- 只测试与当前 PR 直接相关的少量端点，而忽略其他现有端点；
- 已知某些端点访问失败，但在 PR 中既不修复也不记录技术债，仍然尝试合并；
- 依赖人工随机点几下 Swagger 页面，而不做系统性检查。

**Code Review 检查点：**

- PR 描述中是否说明已经完成一轮 API 端点健康检查；
- CI 是否包含针对 API 端点的集成测试 / E2E 测试步骤；
- 是否存在“端点访问失败但仅被忽略”的情况。

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

**允许的工具类/扩展方法位置（PR-SD6 新增）：**

| 位置 | 用途 | 类型要求 |
|------|------|----------|
| `Core/Utilities/` | 通用公共工具（如 ISystemClock） | 公开接口和实现类 |
| `Core/LineModel/Utilities/` | LineModel 专用工具（如 ChuteIdHelper, LoggingHelper） | 必须使用 `file static class` |
| `Observability/Utilities/` | 可观测性相关工具（如 ISafeExecutionService） | 公开接口和实现类 |

**实施要求：**

```csharp
// ✅ 正确：在 Core/Utilities/ 中定义通用工具接口
// 位置：src/Core/ZakYip.WheelDiverterSorter.Core/Utilities/ISystemClock.cs
namespace ZakYip.WheelDiverterSorter.Core.Utilities;

public interface ISystemClock
{
    DateTime LocalNow { get; }
}

// ✅ 正确：在 LineModel/Utilities/ 中定义领域专用工具（使用 file static class）
// 位置：src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Utilities/ChuteIdHelper.cs
file static class ChuteIdHelper
{
    public static bool TryParseChuteId(string? chuteId, out long result) { /* ... */ }
}

// ❌ 错误：在非标准位置新建工具类
// 位置：src/Execution/ZakYip.WheelDiverterSorter.Execution/Utils/StringHelper.cs
public static class StringHelper  // ❌ 应放在 Core/Utilities/ 或使用 file static class
{
    // ...
}

// ❌ 错误：同名工具类在多个命名空间中定义
// 位置：src/Core/.../LoggingHelper.cs
public static class LoggingHelper { }  // ❌ 与 LineModel/Utilities/LoggingHelper 冲突
```

**防线测试**：
- `TechnicalDebtComplianceTests.DuplicateTypeDetectionTests.UtilityTypesShouldNotBeDuplicatedAcrossNamespaces`（本 PR 新增）
- `TechnicalDebtComplianceTests.DuplicateTypeDetectionTests.UtilitiesDirectoriesShouldFollowConventions`（已有）

**禁止行为：**

- 在不同项目或不同类中重复实现相同逻辑的小工具方法（例如多处自行实现相同的字符串处理、时间转换、日志封装等）。
- 为绕开既有工具方法而复制粘贴一份略有差异的实现。
- **新增**：在非标准位置创建 `*Extensions`、`*Helper`、`*Utils`、`*Utilities` 类（除非是 `file static class`）。
- **新增**：在多个命名空间中定义同名工具类。

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
## 十三、技术债务登记与 PR 优先级

> 本节用于约束技术债务（Technical Debt）的记录方式，以及所有 PR 对技术债务的处理优先级，确保技术债务不会被忽略或遗忘。

### 1. 技术债务文档结构（PR-RS13 更新）

**文档分工**：
- **`docs/RepositoryStructure.md` 第 5 章节**：技术债索引表（ID + 状态 + 简短摘要）
- **`docs/TechnicalDebtLog.md`**：技术债详细日志（PR 号、文件迁移列表、测试更新说明等）

**规则**：

1. 所有已知的技术债务必须在 `docs/RepositoryStructure.md` 第 5 章节的索引表中登记：
   - 登记点是索引表，包含 ID、状态、1-2 行摘要
   - 不再在 RepositoryStructure.md 中保留完整的过程描述
   - 禁止出现"代码中有 TODO / FIXME，但在索引表中没有对应记录"的情况

2. 技术债的详细描述（PR 过程、文件列表、测试变更等）统一存放在 `docs/TechnicalDebtLog.md` 中：
   - 按 TD-xxx 编号组织
   - 包含完整的问题描述、解决方案、影响范围
   - 索引表中的详情链接指向此文件的对应章节

3. 当在代码中使用 `TODO` / `FIXME` 等注释时：
   - 必须在 RepositoryStructure.md 索引表中新增条目
   - 在代码注释中标明关联标识（例如 `TD-030`）
   - 在 TechnicalDebtLog.md 中添加详细描述

**禁止行为**：

- 仅在代码中写 TODO / FIXME，而不在索引表中记录对应技术债
- 仅在 PR 描述中提到"这里有技术债务"，但不更新文档
- 在 RepositoryStructure.md 中保留大段过程性描述（应移至 TechnicalDebtLog.md）

---

### 2. 开启 PR 时必须优先处理技术债务

**规则：**

1. 在开启任何 PR 之前，必须先阅读 `RepositoryStructure.md` 中的技术债务部分，尤其是与本次改动涉及模块相关的条目：
   - 边改代码边对照该模块的技术债务清单；
   - 优先考虑是否可以在本 PR 内一并解决。

2. 若本次 PR 涉及的代码范围与某条技术债务相关，则当前 PR 必须优先处理这部分技术债务，具体要求：

   - 能在本 PR 中修复的，必须修复，并在 `RepositoryStructure.md` 中标记该条目为已解决或删除该条目；
   - 如果因客观原因无法在本 PR 内处理（例如影响范围极大、需独立大改），则必须：
     - 在 PR 描述中说明原因；
     - 追加创建一个专门的“技术债务清理 PR”计划（包括范围和大致方案），并更新 `RepositoryStructure.md` 中对应条目的说明。

3. 禁止以“技术债与本 PR 无关”“时间不够”“先留着以后再说”等理由忽略与当前改动区域相关的技术债务。

4. 新增技术债务时（例如为了解决当前问题临时引入过渡方案）：

   - 必须在 `RepositoryStructure.md` 中立即新增条目，标明是“过渡性方案”；
   - 必须在 PR 描述中明确说明这是新增的技术债务，并给出后续清理思路。

**禁止行为：**

- 开启或合并 PR 时，对 `RepositoryStructure.md` 中的技术债务完全不检查；
- 在与某模块强相关的改动中，刻意绕开该模块已知技术债务，不做任何处理或更新。

---

### 3. 技术债务闭环与 PR 描述要求

**规则：**

1. 每个 PR 完成后，必须保证与本次改动相关的技术债务条目已经同步更新：

   - 已完全解决的技术债务：在 `RepositoryStructure.md` 中删除该条目，或标记为“已解决”，并简要记录解决方式（例如“通过 PR #123 合并两个实现并删除 LegacyXxx”）；
   - 部分解决或重构中的技术债务：更新条目内容，说明当前进展和剩余工作。

2. PR 描述中建议增加固定小节，例如：

   - `Technical Debt / 技术债处理：`
     - 列出本 PR 修复或更新的技术债务条目 ID 或描述；
     - 如本 PR 未处理任何技术债务，必须显式说明原因（例如“本 PR 仅为紧急线上修复，不引入新技术债务，且与现有技术债条目无交集”）。

3. CI / 审查流程建议要求：

   - 审查者在 Review 时需检查：
     - 与改动相关的技术债务条目是否已更新；
     - 是否出现“新增 TODO / Legacy 代码但文档未同步”的情况。
   - 在未来可以考虑将对 `RepositoryStructure.md` 技术债务小节的变更纳入 TechnicalDebtComplianceTests，保证每次结构性改动都伴随文档更新。

**禁止行为：**

- 代码中已经完成技术债务修复，但 `RepositoryStructure.md` 仍显示该问题为未解决；
- 在 PR 中新增明显技术债务（例如新的重复实现、Legacy 目录），却不在技术债务清单中登记；
- 以“文档以后再补”为理由合并涉及结构/架构变更的 PR。

---

### 4. 处理技术债的步骤 (PR-TD-ZERO02 新增)

**工作流程**：

Copilot 在修改或新增技术债时，必须按以下步骤执行：

1. **读取文档**：
   - 先读 `RepositoryStructure.md` 第 5 章节（技术债索引表）
   - 再读 `TechnicalDebtLog.md` 对应 TD 段落（如存在）

2. **同步更新三处位置**：
   - **技术债索引表**（`RepositoryStructure.md` 第 5 章节）：更新状态和摘要
   - **技术债统计表**（`RepositoryStructure.md` 技术债统计）：更新数量
   - **详细日志**（`TechnicalDebtLog.md`）：更新详细描述

3. **更新合规测试**：
   - 如果新增技术债，`TechnicalDebtIndexComplianceTests.TechnicalDebtIndexShouldNotContainPendingItems` 会失败
   - 可通过设置环境变量 `ALLOW_PENDING_TECHNICAL_DEBT=true` 临时禁用零技术债检查
   - 技术债解决后，移除环境变量设置，恢复零技术债测试通过

**新增技术债时的处理**：

```markdown
1. 在 TechnicalDebtLog.md 中登记：
   - 添加新的 [TD-xxx] 章节
   - 状态设为「❌ 未开始」或「⏳ 进行中」

2. 在 RepositoryStructure.md 第 5 章节同步更新：
   - 在索引表中添加新条目
   - 更新统计表数量

3. 设置环境变量临时禁用零技术债检查（如需要）：
   - export ALLOW_PENDING_TECHNICAL_DEBT=true

4. 完成技术债后：
   - 更新状态为「✅ 已解决」
   - 更新统计表
   - 移除环境变量设置
```

**防线测试**：

- `TechnicalDebtComplianceTests.TechnicalDebtIndexComplianceTests.TechnicalDebtIndexShouldNotContainPendingItems`
- `TechnicalDebtComplianceTests.TechnicalDebtIndexComplianceTests.TechnicalDebtStatisticsShouldBeConsistent`
- `TechnicalDebtComplianceTests.TechnicalDebtIndexComplianceTests.TechnicalDebtEntriesShouldBeConsistentBetweenDocuments`

---
## 十四、Copilot PR 工作流：优先读取 RepositoryStructure.md 与 copilot-instructions.md

> 本节用于约束 Copilot 在创建 PR 时的工作顺序，要求始终以 `docs/RepositoryStructure.md` 和 `copilot-instructions.md` 作为项目结构、技术债务与编码规范的优先信息来源，从而减少无效尝试和错误假设。

### 1. 创建 PR 前必须优先读取文档（PR-RS13 更新）

**文档阅读顺序**：

1. **`docs/RepositoryStructure.md`**（必读）：
   - **第 1-4 章节**：项目结构、分层边界、命名空间约定与依赖关系
   - **第 5 章节**：技术债索引表（ID + 状态 + 摘要）
   - **第 6 章节**：单一权威实现表（防止影分身）

2. **`copilot-instructions.md`**（必读）：
   - 统一的编码规范、命名约束、枚举和 Id 规则
   - PR 行为要求（必须修复测试、禁止 global using 等）

3. **`docs/TechnicalDebtLog.md`**（按需阅读）：
   - 当需要了解某个技术债的详细过程时查阅
   - 在 RepositoryStructure.md 第 5 章节看到 `[TD-xxx]` 标记时跳转查阅

**规则**：

1. 每次创建 PR（包括功能开发、重构、技术债清理、Bug 修复），Copilot 在进行任何结构推断、修改建议或代码重构之前，必须按上述顺序读取并理解文档。

2. 在未读取上述文件之前，Copilot 不得：
   - 自行推测项目分层结构、模块职责；
   - 自行决定新增项目/目录的层级位置；
   - 自行放宽已有的编码规范或测试/技术债约束。

3. 若 `RepositoryStructure.md` 与 `copilot-instructions.md` 内容存在冲突时，应遵循以下处理顺序：

   1. 以 `copilot-instructions.md` 中的"全局规范与约束"为硬规则基线（如禁止 global using、Id 必须为 long、测试必须通过等）；
   2. 在不违反上述硬规则的前提下，以 `RepositoryStructure.md` 作为架构与结构设计的权威来源；
   3. 如确认现有文档本身存在设计缺陷或已过期，本次 PR 必须包含：
      - 对相应文档的更新（两者保持一致）；
      - 对代码结构或实现的同步调整。

**禁止行为**：

- 在未阅读 `RepositoryStructure.md` 和 `copilot-instructions.md` 的前提下，直接生成重构方案或 PR 计划。
- 无视两个文档中已存在的约束，自行设计与文档冲突的结构或编码风格。

---

### 2. 以两个文档为 PR 规划与验收的基础

**规则：**

1. Copilot 在生成 PR 描述与任务列表时，应基于两个文档的信息进行规划：

   - 从 `RepositoryStructure.md` 获取：
     - 当前各项目/目录的职责边界；
     - 相关技术债务条目及其状态；
     - 对结构和依赖方向的约束。
   - 从 `copilot-instructions.md` 获取：
     - 统一的编码与命名规范；
     - 对测试、技术债务、枚举、Id、global using 等的约束；
     - PR 中必须遵守的行为要求（如所有测试失败必须在本 PR 修复）。

2. 在 PR 描述中，应明确体现对这两个文件的遵循情况，例如：

   - 说明本次修改对应 `RepositoryStructure.md` 中哪些模块与技术债条目；
   - 说明本次修改如何满足或加强 `copilot-instructions.md` 中的约束（例如删除 global using、统一 Id 为 long、补充 Swagger 注释、修复测试等）。

3. PR 完成后，Copilot 应以两个文档作为对照基准，检查：

   - 代码结构是否符合 `RepositoryStructure.md` 描述；
   - 新增/修改的代码是否符合 `copilot-instructions.md` 的编码与测试要求；
   - 涉及的技术债务条目是否已更新或关闭。

**禁止行为：**

- 仅参考代码现状生成 PR 计划，不对照两个文档。
- 修改结构或规范相关内容后不更新 `RepositoryStructure.md` / `copilot-instructions.md`，导致文档与实现长期不一致。

---

### 3. 利用文档减少无效尝试与重复结构推断

**规则：**

1. Copilot 在需要决定以下事项时，必须首先查阅两个文档，而不是从零开始猜测：

   - 新类型应该放在哪个项目和目录；
   - 某个职责属于 Core / Application / Execution / Drivers / Ingress / Host / Simulation 的哪一层；
   - 厂商相关实现与配置类应该存放在哪个 Vendors 子目录；
   - 是否允许引入新的过渡实现或技术债务。

2. 若两个文档已经明确给出答案（例如“所有 Id 使用 long、禁止 global using、厂商实现和配置类必须在同一 Vendors 目录下、过时代码必须立即删除并调整调用方”等），Copilot 必须严格遵守，不得在 PR 中试图绕开这些规则。

3. 如发现代码与文档存在偏离，Copilot 应优先将代码和文档拉回统一，而不是基于错误现状继续堆叠新结构。

**禁止行为：**

- 自行创建新的顶层目录（如 `Common/`, `Shared/`, `Infra/` 等），而文档中未定义这些结构。
- 自行引入新的分层（例如额外的 `*.Application` 或 `*.Infrastructure` 项目），而未在 `RepositoryStructure.md` 与 `copilot-instructions.md` 中体现。
- 利用“当前代码就是这么写的”为理由，忽略文档中已有的结构与规范设计。

---
## 十五. 影分身零容忍策略（总则）

**规则：**

1. 本仓库对“影分身”代码（重复抽象 / 纯转发 Facade / 重复 DTO / 重复 Options / 重复 Utilities 等）采取**零容忍**策略：
   - 一旦发现新增的影分身类型，即视为当前 PR 不合规；
   - PR 必须在当前分支中删除该影分身类型或合并到既有实现中，不能“先留下以后再清理”。

2. 对于历史遗留的影分身类型：
   - 若在当前 PR 涉及对应模块或调用链，必须优先尝试清理；
   - 如短期内无法彻底清理，必须在 `RepositoryStructure.md` 中登记技术债，并规划专门的清理 PR。

3. 影分身判定标准以本文件中关于接口影分身、Facade/Adapter 影分身、DTO/Options/Utilities 影分身三个小节的规则为准：
   - 同一职责出现第二个接口 / DTO / Options / 工具方法；
   - 只做一层方法转发、不增加任何实质逻辑的 Facade/Adapter/Wrapper/Proxy；
   - 多处存在字段结构完全一致的 DTO/Model/Response 类型。

**禁止行为：**

- 新增任何形式的“影分身”类型，并期望后续再清理；
- 保留一套 Legacy 实现与一套新实现并存，而调用方只使用其中一套；
- 在 PR 描述中以“与本次改动无关”为理由保留新增影分身。

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
16. **上游协议相关修改是否只引用权威文档**（禁止在 README 等处重复字段表/JSON 示例）
17. **测试/工具项目修改是否遵守结构约束**（TD-032）
---

**文档版本**: 1.4 (PR-RS-TESTS01)  
**最后更新**: 2025-12-02  
**维护团队**: ZakYip Development Team
