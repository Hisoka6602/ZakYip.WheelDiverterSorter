# Copilot 编码规范

本文档定义了本项目的 C# 编码标准和最佳实践。所有开发人员和 AI 辅助工具应遵循这些规范。

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

### 10.4 后续仿真相关 PR 的要求

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

### 10.5 仿真实现的推荐架构

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
