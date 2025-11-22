# PR-9: 路由与拓扑分层约束规范

## Architecture Layering Constraints for Routing and Topology

本文档定义了路由（Routing）层和拓扑（Topology）层之间的严格分层约束，确保系统架构清晰、可维护。

This document defines strict layering constraints between the Routing and Topology layers to ensure clear and maintainable system architecture.

---

## 一、分层原则 / Layering Principles

### 1. 路由层 (Routing Layer)

**命名空间 / Namespace:**
- `ZakYip.WheelDiverterSorter.Core.LineModel.Routing`
- `ZakYip.WheelDiverterSorter.Application.Routing`

**职责 / Responsibilities:**
- 路由配置模型（Routes）
- 路由规则（根据条码/客户/目的地计算 ChuteId）
- 路由导入导出
- 路由计划生命周期管理（`RoutePlan`）

**禁止包含 / Must NOT contain:**
- ❌ 线体长度、速度、摆轮物理顺序等拓扑信息
- ❌ 路径生成逻辑（属于 Topology 层）
- ❌ 直接引用 `Topology` 命名空间的类型

**示例 / Examples:**
```csharp
// ✅ 正确：纯路由逻辑
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Routing;

public class RoutePlan
{
    public long ParcelId { get; private set; }
    public long InitialTargetChuteId { get; private set; }
    public long CurrentTargetChuteId { get; private set; }
    public RoutePlanStatus Status { get; private set; }
    
    // 纯路由决策逻辑，不涉及拓扑
    public OperationResult TryApplyChuteChange(long requestedChuteId) { ... }
}

// ❌ 错误：路由层不应包含拓扑逻辑
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Routing;

public interface IPathReroutingService  // ❌ 违规：协调 Routing + Topology
{
    Task<ReroutingResult> TryRerouteAsync(
        long parcelId,
        SwitchingPath currentPath,  // ❌ SwitchingPath 来自 Topology 层
        long failedNodeId,
        PathFailureReason failureReason);
}
```

---

### 2. 拓扑层 (Topology Layer)

**命名空间 / Namespace:**
- `ZakYip.WheelDiverterSorter.Core.LineModel.Topology`
- `ZakYip.WheelDiverterSorter.Application.Topology`

**职责 / Responsibilities:**
- 线体拓扑结构（`LineTopology`, `LineSegment`, `DiverterNode`）
- 格口映射（`ChuteMapping`）
- 路径生成（`ISwitchingPathGenerator`, `SwitchingPath`）
- 路径距离计算、理论到达时间计算

**禁止包含 / Must NOT contain:**
- ❌ 业务路由规则（如"客户 A 走 Chute 6"）
- ❌ 条码解析、业务策略
- ❌ 直接引用 `Routing` 命名空间的类型

**示例 / Examples:**
```csharp
// ✅ 正确：纯拓扑逻辑
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

public record class SwitchingPath
{
    public required long TargetChuteId { get; init; }
    public required IReadOnlyList<SwitchingPathSegment> Segments { get; init; }
    public required long FallbackChuteId { get; init; }
}

public interface ISwitchingPathGenerator
{
    SwitchingPath? GeneratePath(long targetChuteId);
}

// ✅ 正确：Topology 可以使用 Hardware 层的枚举
public record class SwitchingPathSegment
{
    public required long DiverterId { get; init; }
    public required DiverterDirection TargetDirection { get; init; }  // ✅ 来自 Hardware 层
    public required int TtlMilliseconds { get; init; }
}
```

---

### 3. 编排层 (Orchestration Layer)

**命名空间 / Namespace:**
- `ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration`
- `ZakYip.WheelDiverterSorter.Application.Orchestration`
- `ZakYip.WheelDiverterSorter.Host.Application.Services`

**职责 / Responsibilities:**
- 协调 Routing 和 Topology 层
- 完整的分拣流程编排
- 路径重规划服务（`IPathReroutingService`）

**允许 / Allowed:**
- ✅ 同时引用 `Routing` 和 `Topology` 命名空间
- ✅ 协调两层之间的交互

**示例 / Examples:**
```csharp
// ✅ 正确：Orchestration 层协调多层
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;  // ✅ 允许
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology; // ✅ 允许

public interface IPathReroutingService
{
    Task<ReroutingResult> TryRerouteAsync(
        long parcelId,
        SwitchingPath currentPath,      // ✅ Topology 类型
        long failedNodeId,
        PathFailureReason failureReason); // ✅ Routing 类型
}

// ✅ 正确：Application 层的 Orchestrator
namespace ZakYip.WheelDiverterSorter.Host.Application.Services;

using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;  // ✅ 允许
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology; // ✅ 允许

public class SortingOrchestrator : ISortingOrchestrator
{
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ISwitchingPathGenerator _pathGenerator;
    
    public async Task<SortingResult> ProcessParcelAsync(long parcelId)
    {
        // 步骤 1: 从 Routing 获取目标格口
        var targetChuteId = await DetermineTargetChuteAsync(parcelId);
        
        // 步骤 2: 从 Topology 生成路径
        var path = _pathGenerator.GeneratePath(targetChuteId);
        
        // 步骤 3: 执行路径
        return await ExecutePathAsync(path);
    }
}
```

---

## 二、架构测试 / Architecture Tests

为确保分层约束得到遵守，我们在 `ZakYip.WheelDiverterSorter.ArchTests` 项目中实现了自动化架构测试。

To ensure layering constraints are respected, we have implemented automated architecture tests in the `ZakYip.WheelDiverterSorter.ArchTests` project.

### 测试内容 / Test Coverage

1. **`Routing_ShouldNotDependOn_Topology`**
   - 确保 `Routing` 命名空间中的类型不引用 `Topology` 命名空间的类型
   - Ensures types in `Routing` namespace do not reference types from `Topology` namespace

2. **`Topology_ShouldNotDependOn_Routing`**
   - 确保 `Topology` 命名空间中的类型不引用 `Routing` 命名空间的类型
   - Ensures types in `Topology` namespace do not reference types from `Routing` namespace

3. **`Routing_Namespace_ShouldExist`** / **`Topology_Namespace_ShouldExist`**
   - 验证命名空间结构存在
   - Verifies namespace structure exists

### 运行测试 / Running Tests

```bash
# 运行架构测试
dotnet test tests/ZakYip.WheelDiverterSorter.ArchTests

# 仅运行架构测试（通过过滤）
dotnet test --filter FullyQualifiedName~RoutingTopologyLayerTests
```

### 违规处理 / Violation Handling

如果架构测试失败，表示存在分层违规：

If architecture tests fail, it indicates a layering violation:

1. **检查失败信息** - 查看哪些类型违反了约束
2. **重构代码** - 将违规代码移动到合适的层
3. **重新运行测试** - 确保修复有效

---

## 三、PR 检查清单 / PR Checklist

在提交 PR 时，必须确认以下检查项：

When submitting a PR, you must confirm the following checklist items:

### 路由 / 拓扑 分层检查

- [ ] 本次改动未在 `Routing` 命名空间中使用任何 `Topology` 类型或概念
- [ ] 本次改动未在 `Topology` 命名空间中加入任何业务路由规则
- [ ] 如需同时使用 `Routing` 和 `Topology`，相关代码已确认位于 `Orchestration` 命名空间或 `Application.Services` 层
- [ ] 架构测试（`ZakYip.WheelDiverterSorter.ArchTests`）已通过

---

## 四、CI/CD 集成 / CI/CD Integration

架构测试已集成到 CI 流程中，任何破坏分层约束的改动都会导致构建失败。

Architecture tests are integrated into the CI pipeline. Any changes that violate layering constraints will cause the build to fail.

### GitHub Actions 配置示例

```yaml
- name: Run Architecture Tests
  run: dotnet test tests/ZakYip.WheelDiverterSorter.ArchTests --no-build --verbosity normal
```

---

## 五、常见问题 / FAQ

### Q1: 如果需要在 Routing 层使用路径信息怎么办？

**A:** 路径信息属于 Topology 层。如果需要协调路由决策和路径生成，应将相关逻辑放在 `Orchestration` 层。

**Example:**
```csharp
// ❌ 错误：Routing 层不应处理路径
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
public class RouteValidator
{
    public bool ValidatePath(SwitchingPath path) { ... }  // ❌ 违规
}

// ✅ 正确：Orchestration 层协调
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
public class RoutePathValidator
{
    public bool ValidatePath(long targetChuteId, SwitchingPath path) { ... }  // ✅ 合法
}
```

### Q2: 拓扑层可以使用哪些枚举？

**A:** 拓扑层可以使用 `Hardware` 层的枚举（如 `DiverterDirection`），但不应使用 `Routing` 层的枚举（如 `RoutePlanStatus`）。

### Q3: 如何判断一个接口应该放在哪一层？

**A:** 问自己三个问题：
1. 这个接口是否只涉及路由决策？→ `Routing` 层
2. 这个接口是否只涉及拓扑结构和路径？→ `Topology` 层
3. 这个接口是否需要协调路由和拓扑？→ `Orchestration` 层

---

## 六、参考文档 / References

- [PR Template](.github/PULL_REQUEST_TEMPLATE.md)
- [Repository Constraints](../.github/copilot-instructions.md)
- [Architecture Tests](../tests/ZakYip.WheelDiverterSorter.ArchTests)

---

**文档版本 / Document Version:** 1.0  
**创建日期 / Created:** 2025-11-22  
**最后更新 / Last Updated:** 2025-11-22  
**维护团队 / Maintained by:** ZakYip Development Team
