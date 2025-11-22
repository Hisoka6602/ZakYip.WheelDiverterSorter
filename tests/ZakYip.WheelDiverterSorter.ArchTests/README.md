# ZakYip.WheelDiverterSorter.ArchTests

Architecture tests for enforcing layering constraints between Routing and Topology layers.

路由与拓扑分层约束的架构测试项目。

## Purpose / 目的

This project contains automated architecture tests that enforce strict separation between the Routing and Topology layers, ensuring:

本项目包含自动化架构测试，强制执行 Routing 和 Topology 层之间的严格分离，确保：

1. **Routing layer does not depend on Topology layer**  
   路由层不依赖拓扑层
   
2. **Topology layer does not depend on Routing layer**  
   拓扑层不依赖路由层
   
3. **Only Orchestration layer can reference both**  
   只有编排层可以同时引用两者

## Running Tests / 运行测试

### Run all architecture tests / 运行所有架构测试

```bash
dotnet test tests/ZakYip.WheelDiverterSorter.ArchTests
```

### Run with detailed output / 运行并显示详细输出

```bash
dotnet test tests/ZakYip.WheelDiverterSorter.ArchTests --logger "console;verbosity=detailed"
```

### Run specific test / 运行特定测试

```bash
dotnet test --filter FullyQualifiedName~Routing_ShouldNotDependOn_Topology
```

## Test Coverage / 测试覆盖

### RoutingTopologyLayerTests

1. **`Routing_ShouldNotDependOn_Topology`**
   - Ensures no types in `*.LineModel.Routing` namespace reference types from `*.LineModel.Topology`
   - 确保 `*.LineModel.Routing` 命名空间中的类型不引用 `*.LineModel.Topology` 的类型

2. **`Topology_ShouldNotDependOn_Routing`**
   - Ensures no types in `*.LineModel.Topology` namespace reference types from `*.LineModel.Routing`
   - 确保 `*.LineModel.Topology` 命名空间中的类型不引用 `*.LineModel.Routing` 的类型

3. **`Routing_Namespace_ShouldExist`**
   - Verifies the Routing namespace structure exists
   - 验证 Routing 命名空间结构存在

4. **`Topology_Namespace_ShouldExist`**
   - Verifies the Topology namespace structure exists
   - 验证 Topology 命名空间结构存在

## How It Works / 工作原理

The tests use reflection to:
1. Load the Core assembly
2. Identify all types in Routing and Topology namespaces
3. Inspect each type's dependencies (fields, properties, methods, constructors)
4. Verify no cross-layer dependencies exist (except for Orchestration layer)

测试使用反射来：
1. 加载 Core 程序集
2. 识别 Routing 和 Topology 命名空间中的所有类型
3. 检查每个类型的依赖（字段、属性、方法、构造函数）
4. 验证不存在跨层依赖（编排层除外）

## Allowed Exceptions / 允许的例外

### Orchestration Layer / 编排层

Types in the following namespaces are allowed to reference both Routing and Topology:
- `*.LineModel.Orchestration`
- `*.Application.Orchestration`
- `*.Application.Services`

以下命名空间中的类型允许同时引用 Routing 和 Topology：
- `*.LineModel.Orchestration`
- `*.Application.Orchestration`
- `*.Application.Services`

**Example / 示例:**
```csharp
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;  // ✅ Allowed
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology; // ✅ Allowed

public interface IPathReroutingService
{
    Task<ReroutingResult> TryRerouteAsync(
        long parcelId,
        SwitchingPath currentPath,      // From Topology
        long failedNodeId,
        PathFailureReason failureReason); // From Routing
}
```

## Test Failures / 测试失败

If a test fails, it means:
1. A type in Routing layer is referencing Topology types
2. A type in Topology layer is referencing Routing types
3. The violating type is not in an allowed Orchestration namespace

如果测试失败，表示：
1. Routing 层的某个类型引用了 Topology 类型
2. Topology 层的某个类型引用了 Routing 类型
3. 违规类型不在允许的编排层命名空间中

### How to Fix / 如何修复

1. **Review the test output** to identify which type is violating the constraint
   查看测试输出，识别哪个类型违反了约束

2. **Refactor the code:**
   重构代码：
   - Move the violating type to the Orchestration layer, OR
     将违规类型移至编排层，或者
   - Restructure the logic to avoid cross-layer dependencies
     重构逻辑以避免跨层依赖

3. **Re-run the tests** to verify the fix
   重新运行测试以验证修复

## CI Integration / CI 集成

These tests are automatically run in the CI pipeline:

这些测试会在 CI 流程中自动运行：

```yaml
- name: Run Architecture Tests
  run: dotnet test tests/ZakYip.WheelDiverterSorter.ArchTests --no-build --configuration Release --verbosity normal
```

Any PR that violates layering constraints will fail the CI build.

任何违反分层约束的 PR 都会导致 CI 构建失败。

## Documentation / 文档

For detailed documentation on layering principles, see:
详细的分层原则文档，请参阅：

📄 [PR-9 Routing/Topology Layering Specification](../../docs/PR-9_ROUTING_TOPOLOGY_LAYERING.md)

## Contributing / 贡献

When adding new architecture tests:
添加新架构测试时：

1. Follow the existing test naming conventions
   遵循现有的测试命名约定

2. Document the test purpose clearly
   清楚地记录测试目的

3. Ensure tests are fast and reliable
   确保测试快速且可靠

4. Update this README with new test descriptions
   更新此 README，添加新测试的描述

---

**Project:** ZakYip.WheelDiverterSorter  
**Test Framework:** xUnit  
**Target Framework:** .NET 9.0  
**Maintained by:** ZakYip Development Team
