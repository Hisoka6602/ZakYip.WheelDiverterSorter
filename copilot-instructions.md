# Copilot 编码规范

本文档定义了本项目的 C# 编码标准和最佳实践。所有开发人员和 AI 辅助工具应遵循这些规范。

## 0. 当前进行中的 PR

- **PR-PANEL-LIGHTS**: 增加电柜操作面板与三色状态灯能力（参考 ZakYip.Singulation 项目）
  - 目标：在 WheelDiverterSorter 中实现完整的面板按钮控制和三色灯状态显示
  - 包含：领域模型、硬件/仿真驱动、状态机联动、API 端点、E2E 测试
  - 状态：进行中

## 1. 使用 required + init 实现更安全的对象创建

**目的**: 确保某些属性在对象创建时必须被设置。通过避免部分初始化的对象来减少错误。

### 规则说明
- 对于必需的属性，使用 `required` 修饰符标记
- 使用 `init` 访问器使属性在初始化后不可变
- 避免在构造函数中设置大量参数，使用对象初始化器

### 示例

❌ **不推荐的做法**：
```csharp
public class RouteConfiguration
{
    public string ChuteId { get; set; }  // 可能为 null，不安全
    public List<DiverterConfig> Diverters { get; set; }  // 可能未初始化
}

// 使用时容易遗漏必需属性
var config = new RouteConfiguration();
// 忘记设置 ChuteId 和 Diverters，导致运行时错误
```

✅ **推荐的做法**：
```csharp
public class RouteConfiguration
{
    public required string ChuteId { get; init; }  // 必须在创建时设置
    public required List<DiverterConfig> Diverters { get; init; }  // 必须在创建时设置
    public double BeltSpeed { get; init; } = 1.5;  // 可选，有默认值
}

// 编译器强制设置必需属性
var config = new RouteConfiguration
{
    ChuteId = "CHUTE_A",
    Diverters = new List<DiverterConfig>()
};
```

### 适用场景
- 配置类 (Configuration classes)
- DTO (Data Transfer Objects)
- 领域模型中的关键属性
- API 请求/响应对象

## 2. 启用可空引用类型

**目的**: 让编译器对可能的空引用问题发出警告，在运行前发现问题。

### 规则说明
- 在项目文件 (.csproj) 中启用 `<Nullable>enable</Nullable>`
- 明确区分可空 (`?`) 和不可空引用类型
- 避免使用 null-forgiving 操作符 (`!`) 除非绝对必要
- 优先使用 null-coalescing (`??`) 和 null-conditional (`?.`) 操作符

### 示例

**项目文件配置**：
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>  <!-- 启用可空引用类型 -->
  </PropertyGroup>
</Project>
```

❌ **不推荐的做法**：
```csharp
public string GetChuteName(string chuteId)
{
    var config = _repository.GetById(chuteId);
    return config.Name;  // config 可能为 null，运行时异常风险
}

public void ProcessParcel(string parcelId)
{
    string targetChute = GetTargetChute(parcelId);
    Console.WriteLine(targetChute.ToUpper());  // targetChute 可能为 null
}
```

✅ **推荐的做法**：
```csharp
public string? GetChuteName(string chuteId)  // 明确返回可能为 null
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
```

### 适用场景
- 所有新代码必须启用可空引用类型
- 逐步迁移现有代码，优先处理关键模块
- 特别关注外部输入和数据库查询结果

## 3. 使用文件作用域类型实现真正封装

**目的**: 保持工具类在文件内私有，避免污染全局命名空间，帮助强制执行边界。

### 规则说明
- 使用 `file` 关键字限制类型的可见性在当前文件
- 工具类、辅助类、内部实现细节应使用 `file` 修饰符
- 减少不必要的 `public` 类型暴露

### 示例

❌ **不推荐的做法**：
```csharp
// PathGeneratorHelpers.cs
namespace ZakYip.WheelDiverterSorter.Core.Helpers;

public static class PathCalculator  // 暴露到全局，可能被误用
{
    public static double CalculateTravelTime(double distance, double speed)
    {
        return distance / speed;
    }
}

public static class DirectionMapper  // 不需要被外部访问
{
    public static int MapDirectionToAngle(Direction direction)
    {
        // 实现
    }
}
```

✅ **推荐的做法**：
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

// 仅在此文件内可见
file static class PathCalculator
{
    public static double CalculateTravelTime(double distance, double speed)
    {
        return distance / speed;
    }
}

file static class DirectionMapper
{
    public static int MapDirectionToAngle(Direction direction)
    {
        return direction switch
        {
            Direction.Straight => 0,
            Direction.Left => 30,
            Direction.Right => 45,
            _ => throw new ArgumentException("Unknown direction")
        };
    }
}
```

### 适用场景
- 工具类和辅助方法
- 内部实现细节
- 转换器和映射器
- 验证逻辑

## 4. 使用 record 处理不可变数据

**目的**: Record 是 DTO 和只读数据的理想选择，提供值语义和简洁语法。

### 规则说明
- 对于纯数据容器，优先使用 `record` 而不是 `class`
- 对于需要修改的情况，使用 `with` 表达式创建副本
- Record 自动实现值相等性比较

### 示例

❌ **不推荐的做法**：
```csharp
public class PathSegmentResult
{
    public string DiverterId { get; set; }
    public Direction Direction { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    
    // 需要手动实现 Equals、GetHashCode、ToString
    public override bool Equals(object? obj)
    {
        if (obj is PathSegmentResult other)
        {
            return DiverterId == other.DiverterId &&
                   Direction == other.Direction &&
                   IsSuccess == other.IsSuccess &&
                   ErrorMessage == other.ErrorMessage;
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(DiverterId, Direction, IsSuccess, ErrorMessage);
    }
}
```

✅ **推荐的做法**：
```csharp
// 简洁的 record 定义，自动实现值语义
public record PathSegmentResult(
    string DiverterId,
    Direction Direction,
    bool IsSuccess,
    string? ErrorMessage = null
);

// 使用示例
var result = new PathSegmentResult("D1", Direction.Left, true);

// 使用 with 表达式创建修改后的副本
var failedResult = result with { IsSuccess = false, ErrorMessage = "Timeout" };

// 自动的值相等性比较
var result1 = new PathSegmentResult("D1", Direction.Left, true);
var result2 = new PathSegmentResult("D1", Direction.Left, true);
Console.WriteLine(result1 == result2);  // True
```

### 适用场景
- DTO (Data Transfer Objects)
- API 请求/响应模型
- 配置对象
- 事件数据
- 领域事件
- 查询结果

## 5. 保持方法专注且小巧

**目的**: 一个方法 = 一个职责。较小的方法更易于阅读、测试和重用。

### 规则说明
- 每个方法应该只做一件事
- 方法长度建议不超过 20-30 行
- 复杂逻辑拆分为多个小方法
- 使用清晰的方法名表达意图

### 示例

❌ **不推荐的做法**：
```csharp
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    // 方法过长，做了太多事情
    var parcel = await _repository.GetParcelAsync(parcelId);
    if (parcel == null)
    {
        _logger.LogError($"Parcel {parcelId} not found");
        return new SortingResult { IsSuccess = false, Error = "Not found" };
    }
    
    var chuteId = await _ruleEngineClient.GetTargetChuteAsync(parcelId);
    if (string.IsNullOrEmpty(chuteId))
    {
        _logger.LogWarning($"No chute assigned for parcel {parcelId}");
        chuteId = "CHUTE_EXCEPTION";
    }
    
    var path = _pathGenerator.GeneratePath(chuteId);
    if (path == null)
    {
        _logger.LogError($"Failed to generate path for chute {chuteId}");
        return new SortingResult { IsSuccess = false, Error = "Path generation failed" };
    }
    
    var executionResult = await _executor.ExecutePathAsync(path);
    if (!executionResult.IsSuccess)
    {
        _logger.LogError($"Path execution failed: {executionResult.ErrorMessage}");
        return new SortingResult { IsSuccess = false, Error = executionResult.ErrorMessage };
    }
    
    await _repository.UpdateParcelStatusAsync(parcelId, "Sorted");
    _metrics.RecordSuccess(chuteId);
    
    return new SortingResult { IsSuccess = true, ActualChute = chuteId };
}
```

✅ **推荐的做法**：
```csharp
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    // 主方法清晰简洁，展示高层逻辑
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
        _logger.LogWarning($"No chute assigned for parcel {parcelId}, using exception chute");
        return "CHUTE_EXCEPTION";
    }
    return chuteId;
}

private SwitchingPath GeneratePathOrThrow(string chuteId)
{
    var path = _pathGenerator.GeneratePath(chuteId);
    if (path == null)
    {
        throw new PathGenerationException($"Failed to generate path for chute {chuteId}");
    }
    return path;
}

private async Task ExecutePathAsync(SwitchingPath path)
{
    var result = await _executor.ExecutePathAsync(path);
    if (!result.IsSuccess)
    {
        throw new PathExecutionException($"Path execution failed: {result.ErrorMessage}");
    }
}

private async Task RecordSuccessAsync(string parcelId, string chuteId)
{
    await _repository.UpdateParcelStatusAsync(parcelId, "Sorted");
    _metrics.RecordSuccess(chuteId);
}
```

### 适用场景
- 所有业务逻辑方法
- 复杂的算法实现
- 数据处理流程
- API 控制器方法

## 6. 不需要可变性时优先使用 readonly struct

**目的**: 防止意外更改并提高性能。readonly struct 确保不可变性，减少内存分配。

### 规则说明
- 对于小型值类型（≤16 字节），使用 `readonly struct`
- `readonly struct` 的所有字段必须是 readonly
- 适用于坐标、配置值、标识符等不可变数据

### 示例

❌ **不推荐的做法**：
```csharp
public struct DiverterPosition
{
    public string DiverterId { get; set; }  // 可变
    public int SequenceNumber { get; set; }  // 可变
    
    // 可能被意外修改，导致逻辑错误
}

void ProcessPosition(DiverterPosition position)
{
    position.SequenceNumber = 999;  // 意外修改！
    // ...
}
```

✅ **推荐的做法**：
```csharp
public readonly struct DiverterPosition
{
    public string DiverterId { get; init; }  // 只能在初始化时设置
    public int SequenceNumber { get; init; }  // 只能在初始化时设置
    
    public DiverterPosition(string diverterId, int sequenceNumber)
    {
        DiverterId = diverterId;
        SequenceNumber = sequenceNumber;
    }
}

// 或者使用 record struct（C# 10+）
public readonly record struct DiverterPosition(
    string DiverterId,
    int SequenceNumber
);

void ProcessPosition(DiverterPosition position)
{
    // position.SequenceNumber = 999;  // 编译错误！无法修改
    // 安全地使用位置数据
    Console.WriteLine($"{position.DiverterId}: {position.SequenceNumber}");
}
```

### 性能优势示例
```csharp
// 测量性能差异
public class PerformanceComparison
{
    // 可变 struct - 可能触发防御性复制
    public struct MutablePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
    
    // 不可变 readonly struct - 无防御性复制
    public readonly struct ImmutablePoint
    {
        public double X { get; init; }
        public double Y { get; init; }
    }
    
    public void ProcessPoints()
    {
        var mutablePoints = new MutablePoint[1000];
        var immutablePoints = new ImmutablePoint[1000];
        
        // ImmutablePoint 性能更好：
        // 1. 无防御性复制
        // 2. 编译器优化更好
        // 3. 栈分配更高效
    }
}
```

### 适用场景
- 坐标和位置数据 (Coordinates, Positions)
- 时间范围 (Time ranges, Durations)
- 配置值 (Configuration values)
- 标识符组合 (Identifier combinations)
- 度量单位 (Measurement units)

### 何时不使用 readonly struct
- 包含可变引用类型字段
- 大型数据结构（>16 字节）
- 需要在创建后修改的场景

## 7. 附加最佳实践

### 7.1 使用命名空间文件作用域声明（C# 10+）
```csharp
// ✅ 推荐：减少缩进层级
namespace ZakYip.WheelDiverterSorter.Core;

public class PathGenerator
{
    // 类实现
}
```

### 7.2 使用模式匹配优化代码
```csharp
// ✅ 使用 switch 表达式
public string GetDirectionName(Direction direction) => direction switch
{
    Direction.Straight => "直行",
    Direction.Left => "左转",
    Direction.Right => "右转",
    _ => throw new ArgumentException("未知方向")
};

// ✅ 使用 is 模式
if (result is { IsSuccess: true, ActualChute: not null })
{
    // 处理成功情况
}
```

### 7.3 使用现代集合初始化语法
```csharp
// ✅ 集合表达式（C# 12+）
List<string> chutes = ["CHUTE_A", "CHUTE_B", "CHUTE_C"];

// ✅ 目标类型 new
Dictionary<string, int> mapping = new()
{
    ["CHUTE_A"] = 1,
    ["CHUTE_B"] = 2
};
```

## 8. 代码审查检查清单

在提交代码前，请检查：

- [ ] 所有必需属性使用 `required` 修饰符
- [ ] 启用并正确处理可空引用类型
- [ ] 内部工具类使用 `file` 关键字
- [ ] DTO 和不可变数据使用 `record`
- [ ] 方法保持短小专注（< 30 行）
- [ ] 小型值类型使用 `readonly struct`
- [ ] 无不必要的可变性
- [ ] 使用现代 C# 特性简化代码

## 9. 参考资源

- [C# 语言规范](https://learn.microsoft.com/en-us/dotnet/csharp/)
- [.NET API 设计指南](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [C# 编码约定](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

## 10. Simulation / 仿真运行目标

### 10.1 仿真运行的目的

仿真运行是为了在**不连接任何真实 PLC / IO 板卡**的情况下，验证和测试系统的完整分拣流程。仿真模式的核心特点包括：

- **纯软件运行**：仅依赖 `Drivers` 层的模拟实现（Mock/Simulator）和 `Ingress` 层的"虚拟传感器事件"
- **完整流程验证**：复用当前的三种分拣模式（`Formal` / `FixedChute` / `RoundRobin`），验证完整的分拣流程：
  - 入口检测 → 格口分配请求 → 路径生成 → 路径执行 → 异常纠错
- **多场景覆盖**：支持正常分拣、异常处理、超时纠错、设备故障等各类场景的仿真测试

### 10.2 仿真的实现约束

为保持系统架构的纯净性和可维护性，仿真实现必须遵循以下约束：

#### 约束1：禁止硬编码仿真分支
**严格禁止**在 `Core` / `Execution` / `Ingress` 等核心业务层中添加任何"仿真专用 if 分支"。

❌ **不允许的做法**：
```csharp
// 在 PathExecutor.cs 中硬编码仿真判断
public async Task<PathExecutionResult> ExecuteAsync(SwitchingPath path)
{
    if (_isSimulationMode)  // ❌ 禁止！
    {
        // 仿真逻辑
        return SimulatedExecution(path);
    }
    else
    {
        // 真实硬件逻辑
        return RealHardwareExecution(path);
    }
}
```

✅ **正确的做法**：
```csharp
// 通过依赖注入不同的实现
public class PathExecutor : ISwitchingPathExecutor
{
    private readonly IDiverterDriver _driver;  // 可以是真实驱动或模拟驱动
    
    public PathExecutor(IDiverterDriver driver)
    {
        _driver = driver;  // 由 DI 容器根据配置注入
    }
    
    public async Task<PathExecutionResult> ExecuteAsync(SwitchingPath path)
    {
        // 统一的执行逻辑，无需区分仿真或真实
        foreach (var segment in path.Segments)
        {
            await _driver.SetDirectionAsync(segment.DiverterId, segment.Direction);
        }
    }
}
```

**实现原则**：
- 仿真模式和生产模式使用**完全相同的业务代码**
- 通过 DI 容器注入不同的实现（`MockDiverterDriver` vs `LeadshineDiverterDriver`）
- 仿真和真实环境的切换仅通过配置和依赖注入完成

#### 约束2：事件载荷类型规范
所有新建的事件载荷类型必须遵循以下规范：

- 使用 `record struct` 或 `record class` 定义
- 类型名称必须以 `EventArgs` 结尾
- 包含 `required` 修饰的必需属性

✅ **正确的事件定义示例**：
```csharp
// 传感器触发事件
public readonly record struct SensorTriggeredEventArgs(
    string SensorId,
    string ParcelId,
    DateTimeOffset TriggerTime
) : IEventArgs;

// 路径执行失败事件
public record class PathExecutionFailedEventArgs
{
    public required string ParcelId { get; init; }
    public required string FailureReason { get; init; }
    public required DateTimeOffset FailureTime { get; init; }
    public SwitchingPathSegment? FailedSegment { get; init; }
}
```

### 10.3 与现有文档的关系

仿真运行必须遵守项目中已有的架构设计和技术约束：

| 相关文档 | 必须遵守的约束 |
|---------|--------------|
| [README.md](README.md) | 仿真必须使用相同的系统拓扑、分拣模式、异常纠错逻辑 |
| [DYNAMIC_TTL_GUIDE.md](DYNAMIC_TTL_GUIDE.md) | 仿真中的路径段超时检测使用相同的动态TTL计算公式 |
| [PATH_FAILURE_DETECTION_GUIDE.md](PATH_FAILURE_DETECTION_GUIDE.md) | 仿真中的异常检测和纠错机制与真实环境完全一致 |
| [OBSERVABILITY_TESTING.md](OBSERVABILITY_TESTING.md) | 仿真运行必须产生完整的监控指标和日志，便于测试和验证 |
| [RELATIONSHIP_WITH_RULEENGINE.md](RELATIONSHIP_WITH_RULEENGINE.md) | 仿真模式可以选择使用 Mock RuleEngine 或真实 RuleEngine |

### 10.4 仿真运行物理约束

本节定义仿真运行的物理假设和规则，确保所有仿真实现都遵循统一的物理模型，不随意创造规则。

#### 10.4.1 皮带线速度约束

**核心原则**：皮带线速度视为恒定的全局配置参数。

- **LineSpeedMmps** 为全局配置，代表皮带线的恒定速度（单位：毫米/秒）
- 仿真中**不得在单个包裹上随意修改物理线速**
- 所有包裹在同一条皮带线上以相同的物理速度移动
- 速度配置统一在系统配置中管理，不允许运行时动态调整单个包裹的速度

**正确做法**：
```csharp
// 系统配置中定义全局皮带速度
public class SystemConfiguration
{
    public double LineSpeedMmps { get; init; } = 1500.0; // 1.5 m/s = 1500 mm/s
}

// 仿真中所有包裹使用相同的全局速度
var travelTime = distance / systemConfig.LineSpeedMmps;
```

**错误做法**：
```csharp
// ❌ 禁止：在单个包裹上修改速度
var adjustedSpeed = baseSpeed * randomFactor; // 错误！
var travelTime = distance / adjustedSpeed;
```

#### 10.4.2 包裹摩擦差异模拟

**核心原则**：通过时间抖动模拟摩擦差异，而不修改物理线速。

包裹与皮带之间的摩擦差异通过每段距离的"时间抖动"来模拟：

```csharp
// 有效行程时间计算公式
effectiveTravelTime = (idealDistance / lineSpeed) × frictionFactor
```

**参数说明**：
- `idealDistance`: 理想传输距离（毫米）
- `lineSpeed`: 全局配置的 LineSpeedMmps（恒定值）
- `frictionFactor`: 摩擦系数，范围为 [FrictionMin, FrictionMax]

**摩擦系数**：
- `frictionFactor` 为 `[FrictionMin, FrictionMax]` 之间的随机或预设系数
- 默认建议范围：[0.95, 1.05]（即 ±5% 时间抖动）
- 可通过配置调整模拟不同的包裹特性（重量、形状、表面材质等）

**实现示例**：
```csharp
public record struct FrictionSimulationConfig
{
    public double FrictionMin { get; init; } = 0.95;
    public double FrictionMax { get; init; } = 1.05;
}

// 仿真中计算有效行程时间
var idealTime = segmentDistance / lineSpeedMmps;
var frictionFactor = Random.Shared.NextDouble() * 
                     (config.FrictionMax - config.FrictionMin) + 
                     config.FrictionMin;
var effectiveTravelTime = idealTime * frictionFactor;
```

**关键点**：
- 物理线速始终不变
- 仅通过时间系数模拟包裹行为差异
- 摩擦系数可配置，支持不同仿真场景

#### 10.4.3 包裹异常场景

仿真中的包裹可能出现以下异常情况：

##### 超时（Timeout）
包裹在某段路径上未在 TTL（Time To Live）时间内到达下一个传感器：

```csharp
// 超时检测
if (actualArrivalTime > expectedArrivalTime + ttl)
{
    // 标记为超时
    status = ParcelStatus.Timeout;
}
```

##### 掉落（Dropped）
包裹在某个节点"掉出"系统，视为从皮带/设备上掉落，丧失控制：

```csharp
// 掉落场景示例
public enum DropReason
{
    DiverterMalfunction,    // 摆轮故障
    BeltSlippage,           // 皮带打滑
    PhysicalObstacle,       // 物理障碍
    ExcessiveSpeed          // 速度过快导致飞出
}
```

**检测规则**：
- 掉落检测必须基于传感器证据链缺失
- 如果包裹在预期位置未被检测到，且超过最大容忍时间，则判定为掉落

#### 10.4.4 硬约束：不得错分

**最高优先级规则**：任何情况下 **不得错分**。

##### 错分定义
- 包裹被分拣到**非目标格口**且**非异常格口**
- 包裹状态标记为 `SortedSuccess`，但实际格口与目标格口不一致

##### 失败判断原则
若无法确定包裹是否按计划进入目标格口，则**必须**视为失败：

1. **路由到异常格口**：
   ```csharp
   // 无法确定目标时，路由到异常格口
   if (cannotConfirmTarget)
   {
       targetChuteId = systemConfig.ExceptionChuteId;
   }
   ```

2. **标记为失败状态**：
   ```csharp
   // 不能标记为 SortedSuccess
   status = ParcelStatus.Dropped;    // 或
   status = ParcelStatus.Timeout;    // 或
   status = ParcelStatus.Error;      // 绝不是 SortedSuccess
   ```

##### 遵守的检测规则
上述判断必须遵守：
- [PATH_FAILURE_DETECTION_GUIDE.md](PATH_FAILURE_DETECTION_GUIDE.md) 中定义的路径失败检测规则
- [ERROR_CORRECTION_MECHANISM.md](ERROR_CORRECTION_MECHANISM.md) 中定义的纠错与异常路由机制

#### 10.4.5 仿真结果模型

仿真必须提供明确的包裹最终状态枚举：

```csharp
/// <summary>
/// 包裹最终状态枚举
/// </summary>
public enum ParcelFinalStatus
{
    /// <summary>
    /// 成功分拣到目标格口
    /// </summary>
    SortedToTargetChute,
    
    /// <summary>
    /// 错分到非目标格口（严重错误，不应发生）
    /// </summary>
    SortedToWrongChute,
    
    /// <summary>
    /// 超时：未在TTL内到达传感器
    /// </summary>
    Timeout,
    
    /// <summary>
    /// 掉落：从皮带/设备上掉落
    /// </summary>
    Dropped,
    
    /// <summary>
    /// 错误：其他执行错误
    /// </summary>
    Error
}
```

**使用示例**：
```csharp
public record struct SimulationResultEventArgs
{
    public required string ParcelId { get; init; }
    public required ParcelFinalStatus FinalStatus { get; init; }
    public required int TargetChuteId { get; init; }
    public required int ActualChuteId { get; init; }
    public string? FailureReason { get; init; }
    public DateTimeOffset CompletionTime { get; init; }
}
```

#### 10.4.6 不变量：传感器证据链要求

仿真和测试中必须维护以下不变量：

**不变量定义**：
```
对任何包裹，若状态为 SortedToTargetChute，则必须有完整的传感器证据链；
若证据链缺失或 TTL 异常，则状态不得为 SortedToTargetChute，
而应为 Timeout/Dropped/Error。
```

**传感器证据链**：
- 包裹在每个关键节点（入口、摆轮、格口）的传感器触发记录
- 每个触发记录包含：传感器ID、触发时间、包裹ID
- 证据链必须连续且符合时序逻辑

**验证规则**：
```csharp
// 伪代码：验证传感器证据链
public bool ValidateSensorEvidenceChain(ParcelTrackingData tracking)
{
    // 1. 检查是否有入口传感器记录
    if (!tracking.HasEntrySensorEvent)
        return false;
    
    // 2. 检查路径上每个摆轮的传感器记录
    foreach (var segment in tracking.Path.Segments)
    {
        if (!tracking.HasSensorEventAt(segment.DiverterId))
            return false;
            
        // 3. 检查时序是否合理（未超过TTL）
        if (tracking.GetArrivalTime(segment.DiverterId) > 
            tracking.GetExpectedTime(segment.DiverterId) + segment.Ttl)
            return false;
    }
    
    // 4. 检查是否有目标格口传感器记录
    if (!tracking.HasExitSensorEventAt(tracking.TargetChuteId))
        return false;
    
    return true;
}

// 状态判定
public ParcelFinalStatus DetermineFinalStatus(ParcelTrackingData tracking)
{
    if (!ValidateSensorEvidenceChain(tracking))
    {
        // 证据链不完整，不能标记为成功
        if (tracking.HasTimeout)
            return ParcelFinalStatus.Timeout;
        else if (tracking.HasDroppedSignal)
            return ParcelFinalStatus.Dropped;
        else
            return ParcelFinalStatus.Error;
    }
    
    // 证据链完整，验证目标格口
    if (tracking.ActualChuteId == tracking.TargetChuteId)
        return ParcelFinalStatus.SortedToTargetChute;
    else
        return ParcelFinalStatus.SortedToWrongChute; // 严重错误
}
```

#### 10.4.7 实现约束

##### 约束1：禁止仿真专用分支
仿真代码**不得**在 `Core` / `Execution` / `Ingress` 中引入"Simulation 专用 if 分支"。

**错误示例**：
```csharp
// ❌ 禁止在核心业务层添加仿真分支
public async Task<PathExecutionResult> ExecuteAsync(SwitchingPath path)
{
    if (_isSimulationMode)
    {
        return await SimulateExecution(path);
    }
    else
    {
        return await RealExecution(path);
    }
}
```

**正确做法**：
- 通过依赖注入不同的实现（`IDiverterDriver` 的 Mock 实现 vs 真实实现）
- 通过配置参数控制行为（如摩擦系数、时间抖动范围）
- 核心业务逻辑对仿真/真实环境完全透明

##### 约束2：通过注入和配置实现仿真
仿真只能通过以下方式实现：

1. **注入不同实现**：
   ```csharp
   // 配置仿真驱动
   services.AddSingleton<IDiverterDriver, MockDiverterDriver>();
   
   // 配置真实驱动
   services.AddSingleton<IDiverterDriver, LeadshineDiverterDriver>();
   ```

2. **配置参数**：
   ```csharp
   public class SimulationPhysicsConfig
   {
       public double LineSpeedMmps { get; init; } = 1500.0;
       public double FrictionMin { get; init; } = 0.95;
       public double FrictionMax { get; init; } = 1.05;
       public double DropProbability { get; init; } = 0.001; // 0.1%
   }
   ```

##### 约束3：事件载荷类型规范
所有新建的事件载荷类型必须：

- 使用 `record struct xxxEventArgs` 或 `record class xxxEventArgs`
- 名称以 `EventArgs` 结尾
- 包含必需属性使用 `required` 修饰符

**正确示例**：
```csharp
// 使用 record struct（小型数据）
public readonly record struct SensorTriggeredEventArgs(
    string SensorId,
    string ParcelId,
    DateTimeOffset TriggerTime
);

// 使用 record class（包含可选字段或较大数据）
public record class PathExecutionFailedEventArgs
{
    public required string ParcelId { get; init; }
    public required string FailureReason { get; init; }
    public required DateTimeOffset FailureTime { get; init; }
    public SwitchingPathSegment? FailedSegment { get; init; }
}

// ❌ 错误示例
public class SensorTriggered // 错误：未使用 record，未以 EventArgs 结尾
{
    public string SensorId { get; set; } // 错误：未使用 required 和 init
}
```

#### 10.4.8 物理约束总结

仿真运行的核心物理约束：

| 约束项 | 规则 | 实现方式 |
|-------|------|---------|
| 皮带速度 | 全局恒定，不可单独调整 | 系统配置中的 LineSpeedMmps |
| 摩擦模拟 | 通过时间抖动，不改变物理速度 | frictionFactor ∈ [FrictionMin, FrictionMax] |
| 超时检测 | 基于 TTL 和实际到达时间 | actualTime > expectedTime + TTL |
| 掉落检测 | 基于传感器证据链缺失 | 预期位置无传感器事件 |
| 不得错分 | 无法确定目标时必须标记失败 | 路由到异常格口或标记 Timeout/Dropped/Error |
| 证据链完整性 | SortedToTargetChute 必须有完整证据链 | 验证每个节点的传感器记录和时序 |
| 代码纯净性 | 核心层禁止仿真专用分支 | 通过 DI 和配置实现模拟 |
| 事件规范 | 事件载荷使用 record xxxEventArgs | 统一命名和类型约定 |

这些约束确保仿真的准确性、可维护性和与真实环境的一致性。

### 10.5 后续仿真相关 PR 的要求

所有与仿真相关的 Pull Request 必须遵循以下原则：

#### 原则1：使用现有分层结构
- ✅ 必须使用 `Core` / `Execution` / `Drivers` / `Ingress` / `Host` / `Observability` 的分层结构
- ✅ 不得破坏 DDD 边界，各层职责清晰：
  - `Core`：领域模型和业务规则
  - `Execution`：路径执行和并发控制
  - `Drivers`：硬件抽象和模拟实现
  - `Ingress`：传感器和上游通信
  - `Host`：应用宿主和API
  - `Observability`：监控指标和日志
- ❌ 禁止创建"仿真专用层"或"仿真并行实现"

#### 原则2：优先重用已有接口
仿真实现必须优先重用系统中已有的接口和抽象：

| 功能模块 | 必须重用的接口 | 说明 |
|---------|--------------|------|
| 路径生成 | `ISwitchingPathGenerator` | 使用相同的路径生成逻辑 |
| 路径执行 | `ISwitchingPathExecutor` | 通过注入 Mock Driver 实现仿真 |
| 硬件驱动 | `IDiverterDriver` | 实现 `MockDiverterDriver` 或 `SimulatorDiverterDriver` |
| 传感器 | `ISensorDriver` | 实现 `MockSensorDriver` 提供虚拟传感器事件 |
| 异常纠错 | `IPathFailureHandler` | 使用相同的异常处理逻辑 |
| 监控指标 | `ISorterMetrics` | 仿真运行产生真实的监控数据 |

#### 原则3：通过配置切换模式
仿真模式和生产模式的切换必须通过配置完成，不能硬编码：

```json
// appsettings.Simulation.json 示例
{
  "Simulation": {
    "Enabled": true,
    "VirtualSensorDelayMs": 500,
    "MockDiverterResponseTimeMs": 100
  },
  "Drivers": {
    "DriverType": "Mock"  // 或 "Leadshine" / "S7"
  },
  "Ingress": {
    "SensorType": "Mock"  // 或 "Leadshine" / "S7"
  }
}
```

#### 原则4：完整的测试覆盖
仿真相关的 PR 必须包含：
- 单元测试：验证 Mock 实现的正确性
- 集成测试：验证仿真模式下的端到端流程
- 文档更新：更新配置说明和使用指南

### 10.6 仿真实现的推荐架构

```
仿真运行架构
├── Host (应用宿主)
│   ├── Program.cs: 根据配置注册 Mock 或真实实现
│   └── appsettings.Simulation.json: 仿真模式配置
├── Drivers (硬件驱动层)
│   ├── IDiverterDriver (接口，不变)
│   ├── MockDiverterDriver (仿真实现，新增)
│   └── LeadshineDiverterDriver (真实实现，已有)
├── Ingress (入口管理层)
│   ├── ISensorDriver (接口，不变)
│   ├── MockSensorDriver (仿真实现，新增)
│   └── VirtualSensorEventGenerator (虚拟事件生成器，新增)
├── Core (核心业务层，完全不变)
│   ├── ISwitchingPathGenerator
│   └── 路径生成、配置管理等业务逻辑
└── Execution (执行层，完全不变)
    ├── ISwitchingPathExecutor
    ├── IPathFailureHandler
    └── 并发控制、异常纠错等逻辑
```

**关键点**：
- `Core` 和 `Execution` 层**零修改**
- 仅在 `Drivers` 和 `Ingress` 层新增 Mock 实现
- 通过 DI 容器根据配置切换实现

---

**文档版本**: 1.1  
**最后更新**: 2025-11-16  
**适用项目**: ZakYip.WheelDiverterSorter
