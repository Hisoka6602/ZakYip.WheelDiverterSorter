# 下个PR任务清单 (Next PR Tasks)

> **创建时间**：2025-12-09  
> **基于PR**：TD-053 (移除UseHardware配置 + 默认真实硬件架构)  
> **当前分支**：copilot/resolve-technical-debt  
> **最后提交**：1bfaedb

---

## 📊 当前完成状态

### ✅ 本PR已完成的工作

1. **UseHardware配置彻底删除**（7个提交，18个文件修改）
   - [x] 删除 `SensorOptions.UseHardwareSensor`
   - [x] 删除 `ISensorVendorConfigProvider.UseHardwareSensor`
   - [x] 删除 `DriverConfiguration.UseHardwareDriver`
   - [x] 删除 `IRuntimeProfile.UseHardwareDriver`
   - [x] 删除 `DriverOptions.UseHardwareDriver`
   - [x] 删除 `PanelConfiguration.UseSimulation`
   - [x] 更新所有引用这些字段的服务、控制器、DTO、测试

2. **架构原则实施**
   - [x] 系统默认使用真实硬件
   - [x] 通过 `IRuntimeProfile.IsSimulationMode` 判断仿真模式
   - [x] 只有调用仿真API端点才进入仿真模式
   - [x] 编译成功：0 Warning(s), 0 Error(s)

3. **技术债务记录**
   - [x] TD-053 标记为已解决
   - [x] TD-054~057 详细记录为新技术债
   - [x] 更新 RepositoryStructure.md 技术债索引表
   - [x] 更新 TechnicalDebtLog.md 详细描述

### 📝 修改文件清单（供参考）

<details>
<summary>点击展开查看18个修改文件</summary>

**Core 层（3个文件）**
- `src/Core/.../Hardware/Providers/ISensorVendorConfigProvider.cs`
- `src/Core/.../LineModel/Configuration/Models/DriverConfiguration.cs`
- `src/Core/.../LineModel/Configuration/Models/PanelConfiguration.cs`
- `src/Core/.../LineModel/Runtime/IRuntimeProfile.cs`

**Drivers 层（2个文件）**
- `src/Drivers/.../DriverOptions.cs`
- `src/Drivers/.../Vendors/Leadshine/Configuration/LeadshineSensorVendorConfigProvider.cs`

**Ingress 层（2个文件）**
- `src/Ingress/.../Configuration/SensorOptions.cs`
- `src/Ingress/.../SensorServiceExtensions.cs`

**Application 层（3个文件）**
- `src/Application/.../Extensions/WheelDiverterSorterServiceCollectionExtensions.cs`
- `src/Application/.../Services/Config/VendorConfigService.cs`
- `src/Application/.../Services/Health/PreRunHealthCheckService.cs`

**Host 层（5个文件）**
- `src/Host/.../Controllers/HardwareConfigController.cs`
- `src/Host/.../Controllers/HealthController.cs`
- `src/Host/.../Controllers/PanelConfigController.cs`
- `src/Host/.../Models/Panel/PanelConfigModels.cs`
- `src/Host/.../appsettings.json` （注：仍保留 Worker 配置节）

**Tests（3个文件）**
- `tests/.../Core.Tests/LiteDbPanelConfigurationRepositoryTests.cs`
- `tests/.../Host.Application.Tests/RuntimeProfileServiceExtensionsTests.cs`

**文档（2个文件）**
- `docs/RepositoryStructure.md`
- `docs/TechnicalDebtLog.md`

</details>

---

## 🎯 下个PR建议任务：TD-054（Worker配置API化）

### 任务概述

将 Worker 轮询间隔配置从 `appsettings.json` 迁移到数据库 `SystemConfiguration`，通过 `GET/PUT /api/config/system` API 端点管理。

### 📋 详细步骤

#### 第1步：数据模型修改

**文件**：`src/Core/.../LineModel/Configuration/Models/SystemConfiguration.cs`

**修改内容**：
```csharp
/// <summary>
/// 系统配置模型
/// </summary>
public class SystemConfiguration
{
    public int Id { get; set; }
    public string ConfigName { get; set; } = "system";
    public int ExceptionChuteId { get; set; } = 999;
    
    // 新增：Worker 轮询间隔配置
    public WorkerIntervals? WorkerIntervals { get; set; }
    
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public static SystemConfiguration GetDefault()
    {
        var now = ConfigurationDefaults.DefaultTimestamp;
        return new SystemConfiguration
        {
            ConfigName = "system",
            ExceptionChuteId = 999,
            WorkerIntervals = WorkerIntervals.GetDefault(), // ← 新增
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

/// <summary>
/// Worker 轮询间隔配置
/// </summary>
public class WorkerIntervals
{
    /// <summary>
    /// 状态检查间隔（毫秒），默认 500ms
    /// </summary>
    public int StateCheckIntervalMs { get; set; } = 500;
    
    /// <summary>
    /// 错误恢复延迟（毫秒），默认 2000ms
    /// </summary>
    public int ErrorRecoveryDelayMs { get; set; } = 2000;
    
    public static WorkerIntervals GetDefault()
    {
        return new WorkerIntervals
        {
            StateCheckIntervalMs = 500,
            ErrorRecoveryDelayMs = 2000
        };
    }
}
```

#### 第2步：API 端点更新

**文件**：`src/Host/.../Controllers/SystemConfigController.cs`

**修改内容**：
- `GET /api/config/system` 响应中包含 `WorkerIntervals` 字段
- `PUT /api/config/system` 请求中接受 `WorkerIntervals` 字段
- 更新 `SystemConfigResponse` DTO 包含 `WorkerIntervals`

**示例代码**：
```csharp
public record SystemConfigResponse
{
    public int Id { get; init; }
    public string ConfigName { get; init; } = string.Empty;
    public int ExceptionChuteId { get; init; }
    public WorkerIntervalsDto? WorkerIntervals { get; init; } // ← 新增
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record WorkerIntervalsDto
{
    public int StateCheckIntervalMs { get; init; }
    public int ErrorRecoveryDelayMs { get; init; }
}
```

#### 第3步：DI 注册修改

**文件**：`src/Host/.../Services/Extensions/WheelDiverterSorterHostServiceCollectionExtensions.cs`

**当前代码**：
```csharp
// 从 appsettings.json 读取
services.Configure<WorkerOptions>(configuration.GetSection("Worker"));
```

**修改为**：
```csharp
// 从数据库读取
services.AddSingleton<IOptions<WorkerOptions>>(sp =>
{
    var systemConfigService = sp.GetRequiredService<ISystemConfigService>();
    var systemConfig = systemConfigService.GetSystemConfig();
    var workerOptions = new WorkerOptions
    {
        StateCheckIntervalMs = systemConfig.WorkerIntervals?.StateCheckIntervalMs ?? 500,
        ErrorRecoveryDelayMs = systemConfig.WorkerIntervals?.ErrorRecoveryDelayMs ?? 2000
    };
    return Options.Create(workerOptions);
});
```

#### 第4步：移除 appsettings.json 配置

**文件**：`src/Host/.../appsettings.json`

**移除以下配置节**：
```json
// 删除此节
"Worker": {
  "StateCheckIntervalMs": 500,
  "ErrorRecoveryDelayMs": 2000
}
```

#### 第5步：测试更新

**文件**：`tests/.../Host.Application.Tests/SensorActivationWorkerTests.cs`

**修改内容**：
- 更新测试以使用数据库配置而非 appsettings.json
- 模拟 `ISystemConfigService` 返回包含 WorkerIntervals 的配置

**示例代码**：
```csharp
// 更新测试设置
var mockSystemConfigService = new Mock<ISystemConfigService>();
mockSystemConfigService.Setup(s => s.GetSystemConfig())
    .Returns(new SystemConfiguration
    {
        WorkerIntervals = new WorkerIntervals
        {
            StateCheckIntervalMs = 500,
            ErrorRecoveryDelayMs = 2000
        }
    });
```

#### 第6步：验证步骤

1. **编译验证**：
   ```bash
   dotnet build
   # 预期：0 Warning(s), 0 Error(s)
   ```

2. **单元测试**：
   ```bash
   dotnet test tests/ZakYip.WheelDiverterSorter.Host.Application.Tests
   # 预期：所有测试通过
   ```

3. **功能验证**：
   - 启动系统
   - 调用 `GET /api/config/system` 查看 WorkerIntervals
   - 调用 `PUT /api/config/system` 修改 WorkerIntervals
   - 重启系统验证新配置生效

4. **文档更新**：
   - 标记 TD-054 为 ✅ 已解决
   - 更新 RepositoryStructure.md 技术债索引表

### 📊 预计影响

- **文件修改数**：5-6个文件
- **代码行数**：约100-150行
- **测试更新**：2-3个测试文件
- **预计工作量**：2-4小时
- **优先级**：🟡 中

---

## 🎯 后续PR建议顺序

### PR-2: TD-055（传感器独立轮询周期配置）

**目标**：每个传感器可配置独立的 PollingIntervalMs

**关键文件**：
- `src/Core/.../SensorIoEntry.cs` - 添加 `PollingIntervalMs` 字段（int? 可选）
- `src/Host/.../LeadshineSensorsController.cs` - API 端点更新
- `src/Drivers/.../LeadshineSensorFactory.cs` - 使用 per-sensor 配置
- `src/Ingress/.../SensorServiceExtensions.cs` - 传感器注册逻辑

**实施步骤**：
1. 在 `SensorIoEntry` 添加 `PollingIntervalMs` 属性
2. 更新 API 端点接受/返回此字段
3. 修改 `LeadshineSensorFactory.CreateSensor()` 使用 sensor 的 PollingIntervalMs
4. 如果 PollingIntervalMs 为 null，回退到全局默认值 10ms
5. 更新 Swagger 文档注释

**验证点**：
- 可以为每个传感器设置不同轮询周期
- null 值时使用全局默认 10ms
- API 响应包含 PollingIntervalMs 字段

---

### PR-3: TD-056（日志优化 - 仅状态变化时记录）

**目标**：优化日志记录，仅在状态转换时输出

**关键文件**：
- `src/Execution/.../NodeHealthMonitorService.cs`
- `src/Drivers/.../ShuDiNiaoWheelDiverterDriver.cs`
- `src/Execution/.../WheelDiverterHeartbeatMonitor.cs`

**实施步骤**：

1. **NodeHealthMonitorService**：
   ```csharp
   // 添加状态跟踪字段
   private readonly ConcurrentDictionary<string, NodeHealthState> _lastHealthStates = new();
   
   // 仅在状态变化时记录
   private void LogIfStateChanged(string nodeId, NodeHealthState newState)
   {
       var oldState = _lastHealthStates.GetOrAdd(nodeId, NodeHealthState.Unknown);
       if (oldState != newState)
       {
           _logger.LogInformation($"节点 {nodeId} 健康状态变化: {oldState} → {newState}");
           _lastHealthStates[nodeId] = newState;
       }
   }
   ```

2. **ShuDiNiaoWheelDiverterDriver**：
   ```csharp
   // 添加心跳状态跟踪
   private HeartbeatState _lastHeartbeatState = HeartbeatState.Unknown;
   
   // 仅在 Timeout → Normal 转换时记录
   private void LogHeartbeatIfChanged(HeartbeatState newState)
   {
       if (_lastHeartbeatState != newState)
       {
           if (newState == HeartbeatState.Normal && _lastHeartbeatState == HeartbeatState.Timeout)
           {
               _logger.LogInformation($"摆轮 {DiverterId} 心跳恢复正常");
           }
           _lastHeartbeatState = newState;
       }
   }
   ```

3. **WheelDiverterHeartbeatMonitor**：
   - 类似方式添加状态跟踪
   - 仅在状态转换时记录

**验证点**：
- 正常运行时日志不再洪水
- 状态转换时准确记录
- 异常日志仍然输出但频率可控

---

### PR-4: TD-057（包裹创建代码去重 + 影分身防线）

**目标**：审计并合并重复的包裹创建逻辑

**第1步：审计现有实现**

需要审计的模块：
- `src/Ingress/.../ParcelDetectionService.cs`
- `src/Execution/.../` 目录（查找包裹创建相关代码）
- `src/Application/.../` 目录（查找包裹创建相关代码）

审计命令：
```bash
# 搜索包裹创建相关代码
grep -r "new Parcel" src/ --include="*.cs"
grep -r "CreateParcel" src/ --include="*.cs"
grep -r "ParcelCreation" src/ --include="*.cs"
```

**第2步：识别重复逻辑**

检查点：
- 是否有多处创建 Parcel 对象的代码
- 是否有重复的 ParcelId 生成逻辑
- 是否有重复的时间戳设置逻辑

**第3步：合并实现**

可能的方案：
- 建立单一 `IParcelFactory` 接口
- 实现 `ParcelFactory` 类统一创建包裹
- 所有需要创建包裹的地方注入 `IParcelFactory`

**第4步：添加影分身防线**

在 `TechnicalDebtComplianceTests` 添加测试：
```csharp
[Fact]
public void ParcelCreation_ShouldNotHaveDuplicateImplementations()
{
    // 检测是否有多个包裹创建服务
    var types = AllTypes
        .That().ResideInNamespace("ZakYip.WheelDiverterSorter")
        .And().HaveNameMatching(".*Parcel.*Factory.*")
        .GetTypes();
    
    // 只允许一个包裹工厂
    Assert.True(types.Count() <= 1, 
        $"发现 {types.Count()} 个包裹工厂实现，只允许1个");
}
```

**预计工作量**：需要审计后确定，可能涉及5-10个文件

---

### PR-5: TD-048（CI/CD流程重建）

**目标**：重建CI/CD流程以匹配当前架构

**关键文件**：
- `.github/workflows/` 目录下的所有workflow文件

**待实施内容**：
1. 审计现有CI/CD工作流
2. 设计新的CI/CD架构
3. 实施新的workflow文件
4. 添加PR质量检查门控（构建、测试、CodeQL等）

**优先级**：🟡 中（基础设施工作）

---

### PR-6: TD-050（主文档更新）

**目标**：更新主文档以反映架构变更

**待更新文档**：
- `README.md` - 反映新架构（默认真实硬件）
- `ARCHITECTURE_PRINCIPLES.md` - 更新架构原则
- `docs/guides/` 下的各类指南

**优先级**：🟢 低（文档工作）

---

### PR-7: TD-051 & TD-052（集成测试）

**目标**：补充缺失的集成测试

**TD-051: SensorActivationWorker 集成测试**
- 状态转换测试
- SafeExecutionService 异常隔离测试
- 传感器启动/停止行为测试

**TD-052: PassThroughAllAsync 集成测试**
- 所有活动摆轮接收命令测试
- 部分失败场景测试
- 健康状态更新测试

**优先级**：🟢 低（测试工作）

---

## 📚 重要参考信息

### 代码规范要点

1. **不使用 DateTime.Now/UtcNow**：统一通过 `ISystemClock` 获取时间
2. **后台任务使用 SafeExecutionService**：防止异常导致进程崩溃
3. **线程安全容器**：跨线程共享集合使用 `ConcurrentDictionary` 等
4. **可空引用类型**：启用 `Nullable=enable`，不新增 `#nullable disable`
5. **DTO 使用 record**：只读数据优先使用 `record` / `record struct`
6. **API 响应使用 ApiResponse<T>**：统一响应格式

### 架构原则

1. **默认真实硬件**：系统默认使用真实硬件，不通过配置开关
2. **仿真模式判断**：通过 `IRuntimeProfile.IsSimulationMode` 判断
3. **配置API化**：业务配置通过API端点管理，不使用appsettings.json
4. **分层架构**：Host → Application → Core/Execution/Ingress/Drivers
5. **硬件韧性**：任何硬件异常不阻塞系统启动

### 提交规范

- 使用中文提交消息
- 小而频繁的提交（每完成一个功能点就提交）
- 提交前确保编译通过：`dotnet build`
- 使用 `report_progress` 工具提交和推送

### 文档更新规则

每个PR完成后必须更新：
1. `docs/RepositoryStructure.md` - 技术债索引表
2. `docs/TechnicalDebtLog.md` - 技术债详细记录
3. 相关的代码注释和XML文档

---

## 🔍 排查问题指南

### 编译错误

如果遇到编译错误：
1. 检查是否有字段/属性被删除但仍被引用
2. 使用 `grep -r "UseHardwareDriver\|UseHardwareSensor\|UseSimulation" src/` 搜索残留引用
3. 检查测试文件是否需要更新

### 运行时错误

如果遇到运行时错误：
1. 检查DI注册是否正确
2. 检查配置默认值是否设置
3. 查看日志确认异常来源

### 测试失败

如果测试失败：
1. 检查测试是否使用了已删除的字段
2. 更新测试Mock对象
3. 更新测试断言逻辑

---

## 📞 需要帮助？

如有问题，请查阅：
- `docs/RepositoryStructure.md` - 仓库结构和架构说明
- `docs/TechnicalDebtLog.md` - 技术债详细记录
- `copilot-instructions.md` - Copilot编码规范
- 本PR的提交历史：`git log --oneline copilot/resolve-technical-debt`

---

**文档版本**：1.0  
**创建时间**：2025-12-09  
**维护人**：ZakYip Development Team
