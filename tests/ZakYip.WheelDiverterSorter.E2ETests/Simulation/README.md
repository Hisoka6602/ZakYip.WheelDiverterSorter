# Simulation Regression Suite

本目录包含 PR-42 实施的仿真回归套件核心组件。

## 组件概览

### 1. SimulationScenarioAttribute.cs
自定义属性类，用于将测试方法与仿真场景 ID 关联。

**用法**:
```csharp
[Fact]
[SimulationScenario("MyScenario_Description")]
public async Task MyTest() { /* ... */ }
```

### 2. SimulationScenariosManifest.cs
仿真场景清单，记录所有 65 个已登记的仿真场景。

**场景分类**:
- `AllScenarioIds`: 主清单（65 个场景）
- `NormalSortingScenarios`: 正常分拣场景（13 个）
- `FaultScenarios`: 故障场景（11 个）
- `LongRunningScenarios`: 长时运行场景（4 个）

### 3. ParcelSimulationTrace.cs
包裹仿真追踪记录，用于验证 Parcel-First 时间顺序不变式。

**关键时间点**:
- `CreatedAt`: 包裹创建时间
- `UpstreamRequestedAt`: 上游请求时间
- `UpstreamRepliedAt`: 上游响应时间
- `RouteBoundAt`: 路由绑定时间
- `DropConfirmedAt`: 落格确认时间

**时间顺序不变式**:
```
t(Created) < t(UpstreamRequested) < t(UpstreamReplied) < t(RouteBound) < t(Diverted)
```

### 4. SimulationManifestValidationTests.cs
总控验证测试（7 个测试），确保清单与测试方法的完整性。

**验证项**:
- ✅ 清单中的每个场景都有对应的测试方法
- ✅ 测试中标记的每个场景都在清单中登记
- ✅ 场景 ID 唯一性
- ✅ 分类完整性
- ✅ 场景覆盖率报告生成

## 使用指南

### 添加新仿真场景

1. 在 `SimulationScenariosManifest.AllScenarioIds` 中注册新场景 ID
2. 在测试方法上添加 `[SimulationScenario("场景ID")]` 属性
3. 运行验证测试确保完整性：
   ```bash
   dotnet test --filter "FullyQualifiedName~SimulationManifestValidationTests"
   ```

### 运行仿真回归套件

```bash
# 运行所有验证测试
dotnet test tests/ZakYip.WheelDiverterSorter.E2ETests \
  --filter "FullyQualifiedName~SimulationManifestValidationTests"

# 运行所有仿真场景
./test-all-simulations.sh
```

## 验收标准

| 项目 | 状态 |
|------|------|
| 场景清单完整性 | ✅ 65/65 |
| 测试方法标记 | ✅ 65/65 |
| 验证测试通过 | ✅ 7/7 |
| 场景 ID 唯一性 | ✅ 0 duplicates |

## 参考文档

- `PR42_SIMULATION_REGRESSION_SUITE.md` - 详细实施总结
- `PR42_PARCEL_FIRST_SPECIFICATION.md` - Parcel-First 语义规范
- `PR41_E2E_SIMULATION_SUMMARY.md` - E2E 仿真基础
