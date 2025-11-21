# 场景 G：多厂商混合驱动仿真
# Scenario G: Multi-Vendor Mixed Driver Simulation

## 场景概述 / Scenario Overview

场景 G 是一个架构验证场景，用于测试系统的**驱动接口抽象能力**和**多厂商混合部署**的可行性。通过在同一系统中混合使用不同厂商的驱动实现，验证统一接口的"零侵入扩展"特性。

Scenario G is an architectural validation scenario designed to test the system's **driver interface abstraction capability** and the feasibility of **multi-vendor mixed deployment**. By mixing different vendor driver implementations in the same system, it validates the "zero-intrusion extension" feature of unified interfaces.

## 场景参数 / Scenario Parameters

| 参数 | 值 | 说明 |
|------|-----|------|
| **摆轮配置** | 混合厂商 | D1/D3/D5 模拟，D2/D4/D6 雷赛 |
| **包裹数量** | 100-500 件 | 足够验证所有摆轮 |
| **包裹间隔** | 500 ms | 标准间隔，便于观察 |
| **摩擦因子** | 0.95 - 1.05 | 低摩擦（±5%），避免干扰 |
| **掉包概率** | 0% | 无掉包，专注验证驱动 |
| **线速** | 1000 mm/s | 标准传送带速度 |
| **异常口** | 999 | 默认异常格口ID |

### 摆轮厂商分配 / Diverter Vendor Assignment

```
摆轮D1 → 模拟驱动 (SimulatedVendorDriverFactory)
  ├── 格口A (左转)
  └── 格口B (右转)

摆轮D2 → 雷赛驱动 (LeadshineVendorDriverFactory)
  ├── 格口C (左转)
  └── 格口D (右转)

摆轮D3 → 模拟驱动 (SimulatedVendorDriverFactory)
  ├── 格口E (左转)
  └── 格口F (右转)

摆轮D4 → 雷赛驱动 (LeadshineVendorDriverFactory)  # 如有6个摆轮
  ├── 格口G (左转)
  └── 格口H (右转)

... (以此类推)
```

## 测试目标 / Test Objectives

该场景验证系统架构的以下特性：

### 1. 统一驱动接口实现"零侵入扩展" ✅

**验证点**：
- 不同厂商驱动都实现相同的接口（`IWheelDiverterDriver`）
- 路径执行器（`HardwareSwitchingPathExecutor`）通过接口调用
- 添加新厂商驱动不需要修改 Execution 层代码

**预期行为**：
- 所有摆轮响应相同的方法调用（`TurnLeft()`, `TurnRight()`, `PassThrough()`）
- 路径执行器不感知底层厂商差异
- 摆轮动作正确执行

### 2. 工厂模式支持多厂商混合 🏭

**验证点**：
- `IVendorDriverFactory` 工厂接口支持多实现
- 可以为不同摆轮配置不同的工厂
- 依赖注入容器正确解析多个工厂

**预期行为**：
- 系统启动时正确创建所有厂商的驱动实例
- 每个摆轮使用正确的驱动实现
- 驱动实例之间无冲突

### 3. 不同厂商驱动可以共存 🤝

**验证点**：
- 模拟驱动和雷赛驱动同时运行
- 无资源冲突（端口、内存、锁）
- 性能无明显下降

**预期行为**：
- 所有摆轮正常工作
- 无异常日志
- 吞吐量正常

### 4. 零错分保证 ✅

**验证点**：
- 无论使用哪个厂商的驱动，`SortedToWrongChute` 计数必须为 0
- 所有成功分拣的包裹 `FinalChuteId == TargetChuteId`

## 架构验证要点 / Architecture Validation Points

### 1. 接口依赖而非实现依赖

```csharp
// ✅ 正确：Execution 层依赖接口
public class HardwareSwitchingPathExecutor : ISwitchingPathExecutor
{
    private readonly Dictionary<string, IWheelDiverterDriver> _diverters;
    
    public HardwareSwitchingPathExecutor(
        IEnumerable<IWheelDiverterDriver> diverters)  // 接口注入
    {
        _diverters = diverters.ToDictionary(d => d.DiverterId);
    }
}

// ❌ 错误：直接依赖具体实现
public class WrongExecutor
{
    private readonly LeadshineDiverterController _controller;  // 耦合到具体实现
}
```

### 2. 工厂模式创建驱动实例

```csharp
// ✅ 正确：通过工厂创建
public interface IVendorDriverFactory
{
    VendorId VendorId { get; }
    IReadOnlyList<IWheelDiverterDriver> CreateWheelDiverterDrivers();
}

// 注册多个工厂
services.AddSingleton<IVendorDriverFactory, LeadshineVendorDriverFactory>();
services.AddSingleton<IVendorDriverFactory, SimulatedVendorDriverFactory>();

// 创建混合驱动列表
var allDrivers = new List<IWheelDiverterDriver>();
foreach (var factory in vendorFactories)
{
    // 根据配置决定使用哪个工厂创建哪些摆轮
    allDrivers.AddRange(factory.CreateWheelDiverterDrivers());
}
```

### 3. 语义化操作而非硬件细节

```csharp
// ✅ 正确：使用业务语义
await diverter.TurnLeftAsync();    // 语义清晰
await diverter.TurnRightAsync();   // 与硬件解耦

// ❌ 错误：暴露硬件细节
await diverter.SetAngleAsync(45);  // 角度是硬件细节
await diverter.SetRelayAsync(2);   // 继电器通道是硬件细节
```

## 预期结果 / Expected Results

### 成功率 / Success Rate

| 指标 | 预期值 | 说明 |
|------|--------|------|
| 整体成功率 | > 95% | 低摩擦、无掉包，应接近理想状态 |
| 模拟驱动成功率 | > 95% | D1/D3/D5 成功率 |
| 雷赛驱动成功率 | > 95% | D2/D4/D6 成功率 |

### 统计指标 / Statistics

**必须满足**：
- ✅ `SortedToWrongChute` = 0（零错分）
- ✅ `simulation_mis_sort_total` = 0（Prometheus 指标）
- ✅ 模拟驱动和雷赛驱动的成功率相当

**预期范围**：
- `SortedToTargetChute`：95-100%
- `Timeout`：0-3%（允许少量超时）
- `Dropped`：0%（无掉包配置）

## 如何运行 / How to Run

### 方法一：使用启动脚本（推荐）

```bash
# Linux/macOS
./performance-tests/run-scenario-g-multi-vendor-mixed.sh \
  --parcels=200 \
  --verify-vendors

# Windows PowerShell
.\performance-tests\run-scenario-g-multi-vendor-mixed.ps1 `
  -Parcels 200 `
  -VerifyVendors
```

**参数说明**：
- `--parcels`: 包裹总数（默认 200）
- `--verify-vendors`: 启用厂商验证模式（详细输出每个摆轮的厂商信息）

### 方法二：手动运行仿真程序

```bash
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run -c Release -- \
  --Simulation:ParcelCount=200 \
  --Simulation:ParcelInterval=00:00:00.500 \
  --Simulation:MultiVendorMode=true \
  --Simulation:VendorAssignment:D1=Simulated \
  --Simulation:VendorAssignment:D2=Leadshine \
  --Simulation:VendorAssignment:D3=Simulated \
  --Simulation:VendorAssignment:D4=Leadshine \
  --Simulation:IsEnableRandomFriction=true \
  --Simulation:FrictionModel:MinFactor=0.95 \
  --Simulation:FrictionModel:MaxFactor=1.05 \
  --Simulation:IsEnableRandomDropout=false \
  --Simulation:ExceptionChuteId=999 \
  --Simulation:IsPauseAtEnd=false
```

### 方法三：集成测试

```bash
cd ZakYip.WheelDiverterSorter.E2ETests
dotnet test --filter "DisplayName~ScenarioG"
```

## 监控与可观测性 / Monitoring & Observability

### Prometheus 指标 / Prometheus Metrics

| 指标名称 | 类型 | 说明 |
|---------|------|------|
| `driver_operations_total{vendor,operation}` | Counter | 驱动操作次数（按厂商和操作分类） |
| `driver_operation_duration_seconds{vendor}` | Histogram | 驱动操作延迟（按厂商） |
| `driver_operation_errors_total{vendor}` | Counter | 驱动操作错误次数（按厂商） |

### 厂商性能对比 / Vendor Performance Comparison

**Grafana 查询示例**：

```promql
# 各厂商操作延迟对比
histogram_quantile(0.95, 
  sum(rate(driver_operation_duration_seconds_bucket{vendor=~"Simulated|Leadshine"}[5m])) 
  by (vendor, le)
)

# 各厂商成功率对比
sum(rate(driver_operations_total{vendor=~"Simulated|Leadshine",result="success"}[5m])) by (vendor)
/
sum(rate(driver_operations_total{vendor=~"Simulated|Leadshine"}[5m])) by (vendor)
```

## 故障排查 / Troubleshooting

### 问题 1：某个厂商的驱动未初始化

**症状**：日志显示"找不到摆轮控制器: D2"

**可能原因**：
- 工厂未正确注册到 DI 容器
- 工厂创建驱动时配置错误
- DiverterId 不匹配

**排查步骤**：
1. 检查 `Program.cs` 中工厂注册
2. 检查工厂的 `CreateWheelDiverterDrivers()` 返回值
3. 检查 DiverterId 命名规范

### 问题 2：驱动操作失败率高

**症状**：某个厂商的驱动错误率 > 10%

**可能原因**：
- 驱动实现有 bug
- 硬件配置错误（雷赛驱动）
- 资源冲突（端口占用）

**排查步骤**：
1. 隔离测试单个厂商（只使用一种驱动）
2. 检查驱动实现的异常日志
3. 验证硬件连接（如有真实硬件）

### 问题 3：性能差异过大

**症状**：模拟驱动和雷赛驱动延迟相差 > 10倍

**可能原因**：
- 雷赛驱动涉及真实硬件通信（预期会慢一些）
- 模拟驱动实现过于简单（立即返回）
- 网络延迟（如使用网络设备）

**排查步骤**：
1. 查看 `driver_operation_duration_seconds` 指标
2. 确认是否使用真实硬件
3. 检查网络延迟

## 验收标准 / Acceptance Criteria

✅ **架构要求**：
- Execution 层只依赖 `IWheelDiverterDriver` 接口
- 不同厂商驱动实现相同接口
- 通过工厂模式创建驱动实例
- 添加新厂商无需修改 Execution 层

✅ **功能要求**：
- `SortedToWrongChuteCount == 0`：无错分
- `simulation_mis_sort_total == 0`：Prometheus 指标验证
- 所有摆轮正确执行转向指令
- 模拟驱动和雷赛驱动成功率相当

✅ **性能要求**：
- 整体成功率 > 95%
- 无资源冲突
- 吞吐量正常（与单一厂商相当）

## 与其他场景的对比 / Comparison with Other Scenarios

| 场景 | 驱动配置 | 验证重点 | 特点 |
|------|---------|---------|------|
| A-E | 单一驱动（模拟或雷赛） | 业务逻辑、异常处理 | 功能验证 |
| **G (多厂商)** | **混合驱动** | **架构抽象、接口设计** | **架构验证** |

## 应用场景 / Use Cases

场景 G 模拟的是实际项目中的典型需求：

### 1. 渐进式硬件升级 🔧
- 旧设备使用旧驱动（如雷赛）
- 新设备使用新驱动（如西门子）
- 系统平滑过渡，无需停机

### 2. 多品牌设备集成 🏭
- 客户现场有多种品牌的设备
- 统一接入同一系统
- 降低集成复杂度

### 3. 厂商锁定风险降低 🔓
- 不依赖单一厂商
- 可灵活切换驱动实现
- 提升系统灵活性

### 4. 开发测试便利性 💻
- 开发环境使用模拟驱动（无需硬件）
- 生产环境使用真实驱动
- 测试环境可混合部署

## 扩展：添加新厂商的步骤 / Adding a New Vendor

### 1. 实现驱动接口

```csharp
// 1. 创建厂商驱动目录
//    ZakYip.WheelDiverterSorter.Drivers/Vendors/NewVendor/

// 2. 实现 IWheelDiverterDriver
public sealed class NewVendorDiverterDriver : IWheelDiverterDriver
{
    public string DiverterId { get; }
    
    public Task<bool> TurnLeftAsync(CancellationToken ct = default)
    {
        // 厂商特定实现
    }
    
    public Task<bool> TurnRightAsync(CancellationToken ct = default)
    {
        // 厂商特定实现
    }
    
    public Task<bool> PassThroughAsync(CancellationToken ct = default)
    {
        // 厂商特定实现
    }
    
    public Task<bool> StopAsync(CancellationToken ct = default)
    {
        // 厂商特定实现
    }
    
    public Task<string> GetStatusAsync()
    {
        // 厂商特定实现
    }
}
```

### 2. 实现工厂接口

```csharp
// 3. 实现 IVendorDriverFactory
public sealed class NewVendorDriverFactory : IVendorDriverFactory
{
    public VendorId VendorId => VendorId.NewVendor;
    
    public VendorCapabilities GetCapabilities()
    {
        return new VendorCapabilities
        {
            SupportsWheelDiverter = true,
            SupportsIoLinkage = true,
            // ...
        };
    }
    
    public IReadOnlyList<IWheelDiverterDriver> CreateWheelDiverterDrivers()
    {
        // 根据配置创建驱动实例
        var drivers = new List<IWheelDiverterDriver>();
        foreach (var config in _diverterConfigs)
        {
            drivers.Add(new NewVendorDiverterDriver(config));
        }
        return drivers;
    }
    
    // 实现其他工厂方法...
}
```

### 3. 注册到 DI 容器

```csharp
// 4. 在 Program.cs 或扩展方法中注册
services.AddSingleton<IVendorDriverFactory, NewVendorDriverFactory>();
```

### 4. 零修改上层代码 ✅

```csharp
// Execution 层代码无需任何修改！
// 依赖注入会自动解析新的工厂和驱动
```

## 相关文件 / Related Files

- 场景定义：`ZakYip.WheelDiverterSorter.Simulation/Scenarios/ScenarioDefinitions.cs::CreateScenarioG()`
- 单元测试：`ZakYip.WheelDiverterSorter.E2ETests/SimulationScenariosTests.cs::ScenarioG_*`
- 启动脚本：`performance-tests/run-scenario-g-multi-vendor-mixed.sh`
- 驱动接口：`ZakYip.WheelDiverterSorter.Drivers/Abstractions/IWheelDiverterDriver.cs`
- 工厂接口：`ZakYip.WheelDiverterSorter.Drivers/IVendorDriverFactory.cs`
- 路径执行器：`ZakYip.WheelDiverterSorter.Drivers/HardwareSwitchingPathExecutor.cs`

## 维护建议 / Maintenance Recommendations

- **定期验证**：每次添加新厂商后运行场景 G 验证兼容性
- **性能基准**：记录各厂商驱动的性能基准，便于对比
- **文档更新**：新增厂商时更新本文档的厂商列表
- **测试覆盖**：为每个厂商编写单元测试和集成测试

---

**场景版本：** v1.0  
**创建日期：** 2025-11-19  
**适用版本：** >= PR-39  
**架构依赖：** 驱动接口抽象、工厂模式、依赖注入
