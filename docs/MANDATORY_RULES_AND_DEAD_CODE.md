# 强制性架构规则与死代码检测报告

> **生成时间**: 2025-12-12  
> **PR**: PR-NOSHADOW-ALL  
> **状态**: 新增强制性规则

---

## 一、强制性架构规则

### 规则1: 枚举位置强制约束

**规则**: 所有枚举必须定义在 `ZakYip.WheelDiverterSorter.Core.Enums` 的子目录中（按类型分类）

**违规后果**: ❌ **PR自动失败**

#### 1.1 当前验证结果

✅ **通过** - 所有枚举已正确位于 `Core/Enums`

**验证命令**:
```bash
find src -name "*.cs" -type f -exec grep -l "^public enum\|^internal enum" {} \; | grep -v "/Core/.*Core/Enums/"
# 结果: 无输出，所有枚举位于正确位置
```

**枚举组织结构**:
```
Core/Enums/
├── Hardware/           # 硬件相关枚举
│   ├── DiverterDirection.cs
│   ├── IoLevel.cs
│   ├── SensorType.cs
│   ├── SignalTowerChannel.cs  # 待删除
│   └── ...
├── Parcel/            # 包裹相关枚举
│   ├── ParcelFinalStatus.cs
│   └── ...
├── System/            # 系统相关枚举
│   ├── SystemState.cs
│   └── ...
├── Communication/     # 通信相关枚举
│   ├── CommunicationMode.cs
│   └── ...
└── Sorting/           # 分拣相关枚举
    └── ...
```

#### 1.2 实施检查

**CI/CD集成**:
```yaml
# .github/workflows/ci.yml
- name: Verify Enum Location
  run: |
    # 查找Core/Enums之外的枚举定义
    INVALID_ENUMS=$(find src -name "*.cs" -type f -exec grep -l "^public enum\|^internal enum" {} \; | grep -v "/Core/.*/Enums/" || true)
    if [ ! -z "$INVALID_ENUMS" ]; then
      echo "❌ 发现违反枚举位置规则的文件:"
      echo "$INVALID_ENUMS"
      exit 1
    fi
    echo "✅ 所有枚举位于正确位置"
```

**ArchTests测试**:
```csharp
[Fact]
public void AllEnums_ShouldBeDefinedIn_CoreEnumsDirectory()
{
    var enums = Types.InAssembly(typeof(CoreAssemblyMarker).Assembly)
        .That()
        .AreEnums()
        .Should()
        .ResideInNamespaceStartingWith("ZakYip.WheelDiverterSorter.Core.Enums");
        
    enums.Should().BeSuccessful();
}
```

---

### 规则2: 事件载荷位置强制约束

**规则**: 所有事件载荷（EventArgs）必须定义在 `ZakYip.WheelDiverterSorter.Core.Events` 的子目录中（按类型分类）

**违规后果**: ❌ **PR自动失败**

#### 2.1 当前验证结果

⚠️ **部分违规** - 发现5个事件文件位于错误位置

**违规文件清单**:

| 文件 | 当前位置 | 应该位置 | 状态 |
|------|---------|---------|------|
| `AlarmEvent.cs` | `Observability/` | `Core/Events/Alarm/` | ❌ 违规 |
| `DeviceConnectionEventArgs.cs` | `Drivers/Vendors/ShuDiNiao/Events/` | `Core/Events/Hardware/` | ❌ 违规 |
| `DeviceStatusEventArgs.cs` | `Drivers/Vendors/ShuDiNiao/Events/` | `Core/Events/Hardware/` | ❌ 违规 |
| `SimulatedSensorEvent.cs` | `Simulation/Models/` | `Core/Events/Simulation/` | ❌ 违规 |
| `SensorEvent.cs` | `Ingress/Models/` | 可能是DTO，需审查 | ⚠️ 待确认 |

**事件组织结构**:
```
Core/Events/
├── Hardware/          # 硬件事件
│   ├── DiverterDirectionChangedEventArgs.cs
│   ├── HardwareEventArgs.cs (基类，未使用)
│   └── [待迁移] DeviceConnectionEventArgs.cs
│   └── [待迁移] DeviceStatusEventArgs.cs
├── Sorting/           # 分拣事件
│   ├── ParcelDivertedToExceptionEventArgs.cs
│   ├── SortOrderCreatedEventArgs.cs
│   └── ...
├── Queue/             # 队列事件
│   ├── ParcelTimedOutEventArgs.cs
│   └── ...
├── Path/              # 路径事件
│   ├── PathSegmentFailedEventArgs.cs
│   └── ...
├── Chute/             # 格口事件
│   └── ...
├── Communication/     # 通信事件
│   └── ...
└── [待新增] Alarm/     # 告警事件
    └── [待迁移] AlarmEvent.cs
└── [待新增] Simulation/ # 仿真事件
    └── [待迁移] SimulatedSensorEvent.cs
```

#### 2.2 迁移计划

**阶段1: 迁移厂商事件到Core**
- 从 `Drivers/Vendors/ShuDiNiao/Events/` 移动到 `Core/Events/Hardware/`
- `DeviceConnectionEventArgs.cs`
- `DeviceStatusEventArgs.cs`

**阶段2: 迁移可观测性事件到Core**
- 从 `Observability/` 移动到 `Core/Events/Alarm/`
- `AlarmEvent.cs`

**阶段3: 迁移仿真事件到Core**
- 从 `Simulation/Models/` 移动到 `Core/Events/Simulation/`
- `SimulatedSensorEvent.cs`

**阶段4: 审查Ingress层事件**
- `SensorEvent.cs` - 确认是事件载荷还是DTO
- 如果是事件，迁移到 `Core/Events/Ingress/`

#### 2.3 实施检查

**CI/CD集成**:
```yaml
# .github/workflows/ci.yml
- name: Verify Event Location
  run: |
    # 查找Core/Events之外的EventArgs类
    INVALID_EVENTS=$(find src -name "*EventArgs.cs" | grep -v "/Core/.*/Events/" || true)
    if [ ! -z "$INVALID_EVENTS" ]; then
      echo "❌ 发现违反事件位置规则的文件:"
      echo "$INVALID_EVENTS"
      exit 1
    fi
    
    # 查找Core/Events之外的Event类（排除Provider/Adapter等非载荷类）
    INVALID_EVENT_CLASSES=$(grep -r "public class.*Event\|public sealed class.*Event" src --include="*.cs" | \
      grep -v "/Core/.*/Events/" | \
      grep -v "Provider\|Adapter\|Factory\|Service" || true)
    if [ ! -z "$INVALID_EVENT_CLASSES" ]; then
      echo "❌ 发现违反事件位置规则的Event类:"
      echo "$INVALID_EVENT_CLASSES"
      exit 1
    fi
    echo "✅ 所有事件载荷位于正确位置"
```

**ArchTests测试**:
```csharp
[Fact]
public void AllEventArgs_ShouldBeDefinedIn_CoreEventsDirectory()
{
    var eventArgs = Types.InCurrentDomain()
        .That()
        .Inherit(typeof(EventArgs))
        .Or()
        .HaveNameEndingWith("EventArgs")
        .Should()
        .ResideInNamespaceStartingWith("ZakYip.WheelDiverterSorter.Core.Events");
        
    eventArgs.Should().BeSuccessful();
}
```

---

## 二、死代码检测报告

### 2.1 未使用的接口（Dead Interfaces）

#### 高优先级删除（完全未使用）

| 接口 | 位置 | 使用次数 | 状态 | 原因 |
|------|------|---------|------|------|
| `IAlarmOutputController` | Core/Hardware/ | 0 | ❌ 删除 | 功能已被IO联动替代 |
| `IHeartbeatCapable` | Core/Hardware/Devices/ | 0 | ❌ 删除 | 无任何实现或调用 |
| `ISortingContextProvider` | Core/Sorting/Interfaces/ | 0 | ❌ 删除 | 无任何实现或调用 |
| `ISortingDecisionService` | Core/Sorting/Interfaces/ | 0 | ❌ 删除 | 无任何实现或调用 |
| `ISignalTowerOutput` | Core/LineModel/Bindings/ | 1 | ❌ 删除 | 信号塔概念已废弃 |

#### 中优先级审查（仅有定义，无实质使用）

| 接口 | 位置 | 使用次数 | 状态 | 说明 |
|------|------|---------|------|------|
| `ICapacityEstimator` | Core/Sorting/Interfaces/ | 1 | ⚠️ 审查 | 可能是未完成功能 |
| `IChaosInjector` | Core/LineModel/Testing/ | 1 | ✅ 保留 | 测试工具，保留 |
| `IChuteAssignmentTimeoutCalculator` | Core/Sorting/Interfaces/ | 1 | ⚠️ 审查 | 可能是未完成功能 |
| `IIoLinkageCoordinator` | Core/LineModel/Bindings/ | 1 | ✅ 保留 | 有实现（DefaultIoLinkageCoordinator） |
| `IIoLinkageExecutor` | Core/LineModel/Bindings/ | 1 | ✅ 保留 | 有实现（IoLinkageExecutor） |
| `INetworkConnectivityChecker` | Core/Hardware/Connectivity/ | 1 | ✅ 保留 | 网络检查功能 |
| `IPanelIoCoordinator` | Core/LineModel/Bindings/ | 1 | ✅ 保留 | 面板IO协调器 |
| `IRouteTopologyConsistencyChecker` | Core/Routing/Validation/ | 1 | ⚠️ 审查 | 可能是未完成功能 |
| `IUpstreamContractMapper` | Core/Abstractions/Upstream/ | 1 | ✅ 保留 | 上游协议映射 |
| `IWheelProtocolMapper` | Core/Hardware/Devices/ | 1 | ✅ 保留 | 摆轮协议映射 |

### 2.2 未使用的事件（Dead Events）

#### 完全未使用（0次引用）

| EventArgs | 位置 | 使用次数 | 状态 | 原因 |
|-----------|------|---------|------|------|
| `ParcelTimedOutEventArgs` | Core/Events/Queue/ | 0 | ❌ 删除 | 无任何订阅或触发 |
| `HardwareEventArgs` | Core/Events/Hardware/ | 0 | ❌ 删除 | 基类，无派生类使用 |
| `PathSegmentFailedEventArgs` | Core/Events/Path/ | 0 | ❌ 删除 | 无任何订阅或触发 |

#### 定义但未实质使用（仅1次引用，可能只有定义）

| EventArgs | 位置 | 使用次数 | 状态 | 说明 |
|-----------|------|---------|------|------|
| `ParcelDivertedToExceptionEventArgs` | Core/Events/Sorting/ | 1 | ⚠️ 审查 | 确认是否有触发代码 |
| `SortOrderCreatedEventArgs` | Core/Events/Sorting/ | 1 | ⚠️ 审查 | 确认是否有触发代码 |
| `OverloadEvaluatedEventArgs` | Core/Events/Sorting/ | 1 | ⚠️ 审查 | 确认是否有触发代码 |
| `ChuteAssignmentNotificationEventArgs` | Core/Events/Chute/ | 1 | ⚠️ 审查 | 确认是否有触发代码 |

### 2.3 未使用的实现类（Dead Implementations）

**已识别**:
- `LeadshineDiscreteIoAdapter.cs` - IDiscreteIoGroup/Port 实现（无调用方）
- `SimulatedDiscreteIo.cs` - IDiscreteIoGroup/Port 实现（无调用方）
- `SimulatedSignalTowerOutput.cs` - ISignalTowerOutput 实现（信号塔已废弃）

### 2.4 删除优先级

**P0 - 立即删除（已确认完全未使用）**:
1. `IAlarmOutputController` + 0个实现
2. `IHeartbeatCapable` + 0个实现
3. `ISortingContextProvider` + 0个实现
4. `ISortingDecisionService` + 0个实现
5. `ISignalTowerOutput` + 1个实现（SimulatedSignalTowerOutput）
6. `IDiscreteIoGroup` + 2个实现
7. `IDiscreteIoPort` + 2个实现
8. `ParcelTimedOutEventArgs`
9. `HardwareEventArgs`
10. `PathSegmentFailedEventArgs`

**P1 - 审查后删除（需确认是否真的未使用）**:
1. `ICapacityEstimator`
2. `IChuteAssignmentTimeoutCalculator`
3. `IRouteTopologyConsistencyChecker`
4. `ParcelDivertedToExceptionEventArgs`
5. `SortOrderCreatedEventArgs`
6. `OverloadEvaluatedEventArgs`
7. `ChuteAssignmentNotificationEventArgs`

---

## 三、更新copilot-instructions.md

### 3.1 新增规则

在 `.github/copilot-instructions.md` 中新增以下章节：

```markdown
## 十六、枚举与事件位置强制约束（PR自动失败规则）

### 1. 枚举位置强制约束

**规则**: 所有枚举必须定义在 `ZakYip.WheelDiverterSorter.Core.Enums` 的子目录中（按类型分类）

**子目录分类**:
- `Hardware/` - 硬件相关枚举（DiverterDirection, IoLevel, SensorType等）
- `Parcel/` - 包裹相关枚举（ParcelFinalStatus等）
- `System/` - 系统相关枚举（SystemState等）
- `Communication/` - 通信相关枚举（CommunicationMode等）
- `Sorting/` - 分拣相关枚举

**违规后果**: ❌ **PR自动失败，不得合并**

**实施要求**:
```csharp
// ✅ 正确：枚举位于 Core/Enums 子目录
namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

public enum DiverterDirection
{
    Left,
    Right,
    Straight
}

// ❌ 错误：枚举位于其他位置
namespace ZakYip.WheelDiverterSorter.Drivers;  // ❌ 禁止

public enum DiverterDirection  // ❌ 必须在 Core/Enums 中定义
{
    Left,
    Right,
    Straight
}
```

**检测方法**:
- CI/CD自动检测：任何在 `Core/Enums` 之外定义的枚举将导致构建失败
- ArchTests测试：`AllEnums_ShouldBeDefinedIn_CoreEnumsDirectory()`

---

### 2. 事件载荷位置强制约束

**规则**: 所有事件载荷（EventArgs及相关Event类）必须定义在 `ZakYip.WheelDiverterSorter.Core.Events` 的子目录中（按类型分类）

**子目录分类**:
- `Hardware/` - 硬件事件（DiverterDirectionChangedEventArgs, DeviceConnectionEventArgs等）
- `Sorting/` - 分拣事件（ParcelDivertedToExceptionEventArgs等）
- `Queue/` - 队列事件（ParcelTimedOutEventArgs等）
- `Path/` - 路径事件（PathSegmentFailedEventArgs等）
- `Chute/` - 格口事件
- `Communication/` - 通信事件
- `Alarm/` - 告警事件
- `Simulation/` - 仿真事件

**违规后果**: ❌ **PR自动失败，不得合并**

**实施要求**:
```csharp
// ✅ 正确：事件载荷位于 Core/Events 子目录
namespace ZakYip.WheelDiverterSorter.Core.Events.Hardware;

public class DiverterDirectionChangedEventArgs : EventArgs
{
    public string DiverterId { get; init; }
    public DiverterDirection Direction { get; init; }
}

// ❌ 错误：事件载荷位于其他位置
namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao.Events;  // ❌ 禁止

public class DeviceConnectionEventArgs : EventArgs  // ❌ 必须在 Core/Events 中定义
{
    // ...
}
```

**例外情况**:
- Provider、Adapter、Factory、Service 类不受此约束（它们不是事件载荷）
- 示例：`SensorEventProviderAdapter` 是适配器，可以在 `Ingress/Adapters/` 目录

**检测方法**:
- CI/CD自动检测：任何在 `Core/Events` 之外定义的 EventArgs 类将导致构建失败
- ArchTests测试：`AllEventArgs_ShouldBeDefinedIn_CoreEventsDirectory()`

---

### 3. 死代码零容忍策略

**规则**: 不允许提交完全未使用的接口、类、方法

**死代码定义**:
- 接口：无任何实现类，且无任何地方引用
- 类：无任何地方实例化或继承
- 方法：私有方法且无任何调用

**检测工具**:
- 定期运行死代码检测脚本
- Code Review 时重点检查新增代码是否被使用

**删除流程**:
1. 识别死代码（通过工具或人工审查）
2. 确认确实未使用（搜索引用、检查测试）
3. 在单独的 PR 中删除死代码
4. 更新相关文档

**禁止行为**:
- 提交"计划将来使用"但当前完全未使用的接口或类
- 保留"可能有用"但实际无调用的工具方法
- 注释掉代码而不删除（应使用 Git 历史）
```

---

## 四、实施检查清单

### 4.1 立即执行

- [ ] 在 `.github/copilot-instructions.md` 中添加新规则章节
- [ ] 在 `tests/ArchTests` 中添加枚举位置验证测试
- [ ] 在 `tests/ArchTests` 中添加事件位置验证测试
- [ ] 在 CI/CD 中添加枚举位置检查步骤
- [ ] 在 CI/CD 中添加事件位置检查步骤

### 4.2 迁移事件到正确位置

- [ ] 迁移 `AlarmEvent.cs` 到 `Core/Events/Alarm/`
- [ ] 迁移 `DeviceConnectionEventArgs.cs` 到 `Core/Events/Hardware/`
- [ ] 迁移 `DeviceStatusEventArgs.cs` 到 `Core/Events/Hardware/`
- [ ] 迁移 `SimulatedSensorEvent.cs` 到 `Core/Events/Simulation/`
- [ ] 审查 `SensorEvent.cs` 是否需要迁移

### 4.3 删除死代码

- [ ] 删除 P0 优先级的10个死接口/死事件
- [ ] 审查并删除 P1 优先级的7个可疑接口/事件
- [ ] 更新 HARDWARE_SHADOW_CODE_ANALYSIS.md 文档
- [ ] 更新 INTERFACE_CLEANUP_ANALYSIS.md 文档

---

## 五、附录

### 附录 A：死代码检测脚本

```bash
#!/bin/bash
# tools/detect-dead-code.sh

echo "=== 死代码检测 ==="

echo ""
echo "1. 未使用的接口（使用次数 ≤ 1）:"
for interface in $(grep -r "^public interface I" src/Core --include="*.cs" | cut -d: -f2 | sed 's/public interface //' | sed 's/ .*//' | sed 's/{.*//' | sort -u); do
    count=$(grep -r ": $interface\|<$interface>\|($interface " src --include="*.cs" | wc -l)
    if [ "$count" -le 1 ]; then
        echo "  ⚠️  $interface - 使用次数: $count"
    fi
done

echo ""
echo "2. 未使用的EventArgs（使用次数 ≤ 1）:"
for event in $(find src/Core -path "*/Events/*" -name "*EventArgs.cs" -exec basename {} \; | sed 's/.cs//'); do
    count=$(grep -r "$event" src --include="*.cs" | grep -v "class $event" | wc -l)
    if [ "$count" -le 1 ]; then
        echo "  ⚠️  $event - 使用次数: $count"
    fi
done

echo ""
echo "3. 错误位置的枚举:"
find src -name "*.cs" -type f -exec grep -l "^public enum\|^internal enum" {} \; | grep -v "/Core/.*/Enums/"

echo ""
echo "4. 错误位置的事件:"
find src -name "*EventArgs.cs" | grep -v "/Core/.*/Events/"
```

### 附录 B：ArchTests 示例

```csharp
public class MandatoryArchitectureRulesTests
{
    [Fact]
    public void AllEnums_MustBeDefinedIn_CoreEnumsDirectory()
    {
        var result = Types.InCurrentDomain()
            .That()
            .AreEnums()
            .Should()
            .ResideInNamespaceStartingWith("ZakYip.WheelDiverterSorter.Core.Enums")
            .GetResult();
            
        result.IsSuccessful.Should().BeTrue(
            "所有枚举必须定义在 Core/Enums 子目录中，违反此规则的PR将自动失败");
    }
    
    [Fact]
    public void AllEventArgs_MustBeDefinedIn_CoreEventsDirectory()
    {
        var result = Types.InCurrentDomain()
            .That()
            .Inherit(typeof(EventArgs))
            .Or()
            .HaveNameEndingWith("EventArgs")
            .Should()
            .ResideInNamespaceStartingWith("ZakYip.WheelDiverterSorter.Core.Events")
            .GetResult();
            
        result.IsSuccessful.Should().BeTrue(
            "所有事件载荷必须定义在 Core/Events 子目录中，违反此规则的PR将自动失败");
    }
}
```

---

**文档版本**: 1.0  
**最后更新**: 2025-12-12  
**维护团队**: ZakYip Development Team
