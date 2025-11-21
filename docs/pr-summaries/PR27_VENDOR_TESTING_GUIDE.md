# PR-27: 新增厂商测试模板

## 概述

本文档说明如何为新厂商驱动运行标准回归测试，确保厂商实现符合系统规范。

## 标准测试场景

PR-27 定义了三组核心标准场景，所有厂商驱动必须通过这些场景的测试：

### 1. PR27-正常分拣场景 (Normal Sorting)

**目的**: 验证厂商驱动的基本分拣功能

**场景配置**:
- 50个包裹，均匀间隔（500ms）
- 无摩擦差异，无掉包，无故障注入
- 5个目标格口，轮询分配

**验收标准**:
- ✅ 100% 包裹成功分拣到目标格口
- ✅ `SortedToWrongChuteCount == 0`（无错分）
- ✅ 所有包裹 `Status == SortedToTargetChute`

### 2. PR27-上游延迟场景 (Upstream Delay)

**目的**: 验证厂商驱动在上游RuleEngine响应延迟时的处理

**场景配置**:
- 30个包裹
- 注入上游延迟（100-300ms随机延迟）
- 5个目标格口

**验收标准**:
- ✅ 部分包裹可能超时（允许）
- ✅ `SortedToWrongChuteCount == 0`（无错分）
- ✅ 成功分拣的包裹 `FinalChuteId == TargetChuteId`

### 3. PR27-节点故障场景 (Node Failure)

**目的**: 验证厂商驱动的故障恢复和降级处理

**场景配置**:
- 40个包裹
- 摆轮节点2和4故障
- 5个目标格口

**验收标准**:
- ✅ 受影响包裹路由到异常格口
- ✅ 其他包裹正常分拣
- ✅ `SortedToWrongChuteCount == 0`（无错分）

## 接入新厂商的测试流程

### 步骤 1: 实现厂商驱动工厂

创建新的厂商驱动工厂，实现 `IVendorDriverFactory` 接口：

```csharp
// 示例：欧姆龙厂商驱动工厂
public class OmronVendorDriverFactory : IVendorDriverFactory
{
    public VendorId VendorId => VendorId.Omron;

    public VendorCapabilities GetCapabilities()
    {
        return VendorCapabilities.Omron;
    }

    // 实现其他必需方法...
    public IReadOnlyList<IWheelDiverterDriver> CreateWheelDiverterDrivers() { /* ... */ }
    public IIoLinkageDriver CreateIoLinkageDriver() { /* ... */ }
    public IConveyorSegmentDriver? CreateConveyorSegmentDriver(string segmentId) { /* ... */ }
    public IReadOnlyList<IWheelDiverterActuator> CreateWheelDiverterActuators() { /* ... */ }
    public ISensorInputReader? CreateSensorInputReader() { /* ... */ }
}
```

### 步骤 2: 注册厂商驱动

在 `DriverServiceExtensions.cs` 中添加新厂商的 switch case：

```csharp
return vendorId switch
{
    VendorId.Leadshine => new LeadshineVendorDriverFactory(...),
    VendorId.Simulated => new SimulatedVendorDriverFactory(...),
    VendorId.Omron => new OmronVendorDriverFactory(...),  // 新增
    _ => throw new NotSupportedException($"厂商 {vendorId} 尚未实现驱动工厂")
};
```

### 步骤 3: 运行模拟驱动测试

首先在模拟环境下运行标准场景，验证场景定义正确：

```bash
# 运行E2E测试（使用模拟驱动）
dotnet test ZakYip.WheelDiverterSorter.E2ETests \
  --filter "Category=VendorAgnostic"

# 或运行仿真测试
dotnet run --project ZakYip.WheelDiverterSorter.Simulation \
  -- --scenario=PR27-Normal
```

### 步骤 4: 配置真实硬件驱动

在 `appsettings.json` 中配置新厂商的驱动：

```json
{
  "Driver": {
    "UseHardwareDriver": true,
    "VendorId": "Omron",
    "Omron": {
      // 厂商特定配置
      "Host": "192.168.1.10",
      "Port": 9600,
      "Diverters": [
        {
          "DiverterId": "D1",
          "Address": "D100"
        }
      ]
    }
  }
}
```

### 步骤 5: 运行真实硬件测试

使用环境变量切换到真实硬件驱动：

```bash
# 设置厂商ID
export VENDOR_ID=Omron

# 运行E2E测试
dotnet test ZakYip.WheelDiverterSorter.E2ETests \
  --filter "Category=VendorAgnostic"

# 或运行仿真测试
dotnet run --project ZakYip.WheelDiverterSorter.Simulation \
  -- --scenario=PR27-Normal --vendor=Omron
```

## 场景配置文件

标准场景可以导出为 JSON 文件，便于版本控制和复用：

### 导出场景

```csharp
var scenario = ScenarioDefinitions.CreatePR27NormalSorting();
await SimulationScenarioSerializer.SaveToFileAsync(
    scenario, 
    "scenarios/pr27-normal-sorting.json"
);
```

### 加载场景

```csharp
var scenario = await SimulationScenarioSerializer.LoadFromFileAsync(
    "scenarios/pr27-normal-sorting.json"
);
```

### 场景文件示例

```json
{
  "scenarioName": "PR27-正常分拣场景",
  "description": "厂商无关的正常分拣验证场景，用于回归测试",
  "options": {
    "parcelCount": 50,
    "lineSpeedMmps": 1000,
    "parcelInterval": "00:00:00.5000000",
    "sortingMode": "RoundRobin",
    "fixedChuteIds": [1, 2, 3, 4, 5],
    "exceptionChuteId": 999,
    "isEnableRandomFriction": false,
    "isEnableRandomDropout": false
  },
  "vendorId": "simulated",
  "topology": {
    "diverterCount": 5,
    "chuteCount": 5,
    "totalLineLengthMm": 10000
  },
  "parcelGeneration": {
    "mode": "uniformInterval",
    "randomSeed": 42
  }
}
```

## 自定义场景

除了标准场景外，还可以创建自定义场景：

```csharp
var customScenario = new SimulationScenario
{
    ScenarioName = "自定义高负载场景",
    Description = "测试高密度包裹流",
    VendorId = VendorId.Omron,
    Options = new SimulationOptions
    {
        ParcelCount = 100,
        LineSpeedMmps = 1500m,
        ParcelInterval = TimeSpan.FromMilliseconds(200),
        SortingMode = "RoundRobin",
        // ... 其他配置
    },
    Topology = new SimulationTopology
    {
        DiverterCount = 10,
        ChuteCount = 20,
        TotalLineLengthMm = 15000
    },
    FaultInjection = new FaultInjectionConfig
    {
        InjectUpstreamDelay = true,
        UpstreamDelayRangeMs = (50, 150)
    }
};
```

## 验收清单

新厂商接入时，必须完成以下验收步骤：

- [ ] 实现 `IVendorDriverFactory` 接口
- [ ] 在 DI 容器中注册新厂商
- [ ] 通过 PR27-正常分拣场景（模拟驱动）
- [ ] 通过 PR27-上游延迟场景（模拟驱动）
- [ ] 通过 PR27-节点故障场景（模拟驱动）
- [ ] 配置真实硬件驱动
- [ ] 通过 PR27-正常分拣场景（真实硬件）
- [ ] 通过 PR27-上游延迟场景（真实硬件）
- [ ] 通过 PR27-节点故障场景（真实硬件）
- [ ] 编写厂商特定的配置文档
- [ ] 更新 `VENDOR_EXTENSION_GUIDE.md`

## 常见问题

### Q: 如何在无硬件环境下运行测试？

A: 默认情况下，所有测试使用模拟驱动（`VendorId.Simulated`），无需真实硬件。只有当显式设置 `VENDOR_ID` 环境变量时才会使用真实硬件驱动。

### Q: 如何调试特定场景？

A: 可以将场景导出为 JSON 文件，修改配置后重新加载：

```bash
# 导出场景
dotnet run --project ZakYip.WheelDiverterSorter.Simulation \
  -- --export-scenario=PR27-Normal --output=custom-scenario.json

# 修改 custom-scenario.json ...

# 加载并运行自定义场景
dotnet run --project ZakYip.WheelDiverterSorter.Simulation \
  -- --load-scenario=custom-scenario.json
```

### Q: 如何添加新的标准场景？

A: 在 `ScenarioDefinitions.cs` 中添加新的静态方法：

```csharp
public static SimulationScenario CreatePR27MyNewScenario(int parcelCount = 50)
{
    return new SimulationScenario
    {
        ScenarioName = "PR27-我的新场景",
        Description = "场景描述",
        // ... 配置
    };
}
```

## 相关文档

- [VENDOR_EXTENSION_GUIDE.md](../VENDOR_EXTENSION_GUIDE.md) - 厂商扩展指南
- [PR24_IMPLEMENTATION_SUMMARY.md](../PR24_IMPLEMENTATION_SUMMARY.md) - 驱动抽象层设计
- [SIMULATION_GUIDE.md](../ZakYip.WheelDiverterSorter.Simulation/SIMULATION_GUIDE.md) - 仿真使用指南
- [E2E_TESTING_SUMMARY.md](../E2E_TESTING_SUMMARY.md) - E2E测试指南
