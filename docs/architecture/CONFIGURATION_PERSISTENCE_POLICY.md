# 配置持久化策略 (Configuration Persistence Policy)

## 目标

确保所有系统配置都能够持久化存储，避免"仅内存存储"的配置方式，保证系统重启后配置不丢失。

## 核心原则

### 1. 所有配置必须持久化

**规则**: 任何新增的配置类型都必须同时实现持久化机制。

**禁止行为**:
- 不允许创建"仅内存存储"的配置
- 不允许使用 `static` 字段存储可变配置
- 不允许使用临时文件存储配置而不通过仓储模式

**实施要求**:
```csharp
// ✅ 正确：使用仓储模式持久化配置
public interface IMyConfigurationRepository
{
    MyConfiguration Get();
    void Update(MyConfiguration configuration);
    void InitializeDefault(DateTime? currentTime = null);
}

// ❌ 错误：仅内存存储
public class MyConfigurationManager
{
    private static MyConfiguration _config = new();  // ❌ 重启后丢失
}
```

### 2. 统一使用 Repository 模式

**规则**: 所有配置的持久化必须通过 Repository 接口实现。

**仓储模式规范**:

1. **接口定义** (Application 层 / Core 层)
   ```csharp
   public interface IXxxConfigurationRepository
   {
       XxxConfiguration Get();
       void Update(XxxConfiguration configuration);
       void InitializeDefault(DateTime? currentTime = null);
   }
   ```

2. **实现类** (Infrastructure 层 / Core 层)
   ```csharp
   public class LiteDbXxxConfigurationRepository : IXxxConfigurationRepository, IDisposable
   {
       private readonly LiteDatabase _database;
       private readonly ILiteCollection<XxxConfiguration> _collection;
       
       // 实现接口方法...
   }
   ```

3. **域模型** (Core 层)
   ```csharp
   public sealed record class XxxConfiguration
   {
       [BsonId]
       public int Id { get; init; }
       
       public string ConfigName { get; init; } = "xxx";
       public int Version { get; init; } = 1;
       
       // 配置字段...
       
       public DateTime CreatedAt { get; init; }
       public DateTime UpdatedAt { get; init; }
       
       public static XxxConfiguration GetDefault() { ... }
       public (bool IsValid, string? ErrorMessage) Validate() { ... }
   }
   ```

### 3. 配置必须包含元数据

**规则**: 所有持久化配置必须包含以下元数据字段：

- `Id`: 数据库主键（LiteDB 自动生成）
- `ConfigName`: 配置名称（固定值，用于唯一标识）
- `Version`: 配置版本号
- `CreatedAt`: 创建时间
- `UpdatedAt`: 最后更新时间

**实施要求**:
```csharp
public sealed record class MyConfiguration
{
    [BsonId]
    public int Id { get; init; }
    
    public string ConfigName { get; init; } = "my_config";
    public int Version { get; init; } = 1;
    
    // 注意：时间戳字段必须由仓储实现通过 ISystemClock.LocalNow 赋值，禁止直接使用 DateTime.Now/DateTime.UtcNow
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
```

### 4. 配置必须支持验证

**规则**: 所有配置类必须提供 `Validate()` 方法，用于验证配置的有效性。

**实施要求**:
```csharp
public (bool IsValid, string? ErrorMessage) Validate()
{
    if (PollingIntervalMs < 50 || PollingIntervalMs > 1000)
    {
        return (false, "轮询间隔必须在 50-1000 毫秒之间");
    }
    
    // 其他验证逻辑...
    
    return (true, null);
}
```

### 5. 配置必须提供默认值

**规则**: 所有配置类必须提供静态 `GetDefault()` 方法，返回合理的默认配置。

**实施要求**:
```csharp
public static MyConfiguration GetDefault()
{
    return new MyConfiguration
    {
        ConfigName = "my_config",
        Version = 1,
        // 设置合理的默认值...
        // 注意：CreatedAt 和 UpdatedAt 应由仓储层通过 ISystemClock.LocalNow 赋值
        // 例如：repository.Save(config) 时设置时间戳
    };
}
```

## 统一 IO 配置规范

### IO 配置模型

所有 IO 相关配置必须使用统一的 `IoPointConfiguration` 模型或包含以下核心字段：

- **BoardId / ModuleId**: 板卡/模块标识
- **ChannelNumber**: 通道编号（0-1023）
- **IoType**: IO 类型（Input/Output）
- **TriggerLevel**: 电平语义（ActiveHigh/ActiveLow）
- **Description**: 用途描述（可选）

**示例**:
```csharp
public sealed record class IoPointConfiguration
{
    public required string Name { get; init; }
    public string? BoardId { get; init; }
    public required int ChannelNumber { get; init; }
    public required IoType Type { get; init; }
    public required TriggerLevel TriggerLevel { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
}
```

### 电平语义统一

所有 IO 配置必须使用 `TriggerLevel` 枚举明确电平语义：

```csharp
public enum TriggerLevel
{
    /// <summary>高电平有效（常开按键/输出1有效）</summary>
    ActiveHigh = 0,
    
    /// <summary>低电平有效（常闭按键/输出0有效）</summary>
    ActiveLow = 1
}
```

**禁止行为**:
- 不允许使用 `bool` 表示电平（不明确语义）
- 不允许使用 `int` 直接表示高/低电平（容易混淆）

## 现有配置类型

### 已完全实现持久化的配置

| 配置类型 | 域模型 | 仓储接口 | 仓储实现 |
|---------|--------|---------|---------|
| 系统配置 | `SystemConfiguration` | `ISystemConfigurationRepository` | `LiteDbSystemConfigurationRepository` |
| 面板配置 | `PanelConfiguration` | `IPanelConfigurationRepository` | `LiteDbPanelConfigurationRepository` |
| IO联动配置 | `IoLinkageConfiguration` | `IIoLinkageConfigurationRepository` | `LiteDbIoLinkageConfigurationRepository` |
| 传感器配置 | `SensorConfiguration` | `ISensorConfigurationRepository` | `LiteDbSensorConfigurationRepository` |
| 通讯配置 | `CommunicationConfiguration` | `ICommunicationConfigurationRepository` | `LiteDbCommunicationConfigurationRepository` |
| 路由配置 | `ChuteRouteConfiguration` | `IRouteConfigurationRepository` | `LiteDbRouteConfigurationRepository` |
| 驱动配置 | `DriverConfiguration` | `IDriverConfigurationRepository` | `LiteDbDriverConfigurationRepository` |

### 配置文件位置

- **域模型**: `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/`
- **仓储接口**: `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/`
- **仓储实现**: `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/`

## 新增配置流程

### 步骤 1: 定义域模型

在 `Core` 层定义配置的域模型：

```csharp
// src/Core/.../Configuration/MyConfiguration.cs
public sealed record class MyConfiguration
{
    [BsonId]
    public int Id { get; init; }
    public string ConfigName { get; init; } = "my_config";
    public int Version { get; init; } = 1;
    
    // 配置字段...
    
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    
    public static MyConfiguration GetDefault() { ... }
    public (bool IsValid, string? ErrorMessage) Validate() { ... }
}
```

### 步骤 2: 定义仓储接口

```csharp
// src/Core/.../Configuration/IMyConfigurationRepository.cs
public interface IMyConfigurationRepository
{
    MyConfiguration Get();
    void Update(MyConfiguration configuration);
    void InitializeDefault(DateTime? currentTime = null);
}
```

### 步骤 3: 实现仓储

```csharp
// src/Core/.../Configuration/LiteDbMyConfigurationRepository.cs
public class LiteDbMyConfigurationRepository : IMyConfigurationRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<MyConfiguration> _collection;
    
    public LiteDbMyConfigurationRepository(string databasePath)
    {
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<MyConfiguration>("MyConfiguration");
        _collection.EnsureIndex(x => x.ConfigName, unique: true);
    }
    
    public MyConfiguration Get() { ... }
    public void Update(MyConfiguration configuration) { ... }
    public void InitializeDefault(DateTime? currentTime = null) { ... }
    public void Dispose() { _database?.Dispose(); }
}
```

### 步骤 4: 注册到 DI 容器

在 `Host` 层的 `Program.cs` 或服务配置中注册：

```csharp
// src/Host/.../Program.cs
builder.Services.AddSingleton<IMyConfigurationRepository>(sp =>
{
    var dbPath = Path.Combine(configDir, "my_config.db");
    return new LiteDbMyConfigurationRepository(dbPath);
});
```

### 步骤 5: 提供 API 端点

在 `Host` 层创建 Controller：

```csharp
// src/Host/.../Controllers/MyConfigController.cs
[ApiController]
[Route("api/config/my-config")]
public class MyConfigController : ControllerBase
{
    private readonly IMyConfigurationRepository _repository;
    
    [HttpGet]
    public ActionResult<MyConfigResponse> Get() { ... }
    
    [HttpPut]
    public ActionResult<MyConfigResponse> Update([FromBody] MyConfigRequest request) { ... }
}
```

### 步骤 6: 编写测试

创建单元测试验证持久化功能：

```csharp
// tests/.../MyConfigurationRepositoryTests.cs
public class MyConfigurationRepositoryTests
{
    [Fact]
    public void Should_Persist_Configuration_Across_Repository_Instances()
    {
        // 测试配置持久化...
    }
}
```

## Code Review 检查清单

在 Code Review 时，必须检查以下内容：

- [ ] 新增配置是否定义了域模型？
- [ ] 域模型是否使用 `record class` 并标记为 `sealed`？
- [ ] 域模型是否包含必需的元数据字段（Id, ConfigName, Version, CreatedAt, UpdatedAt）？
- [ ] 是否定义了仓储接口？
- [ ] 是否实现了基于 LiteDB 的仓储？
- [ ] 仓储实现是否使用 `Connection=shared` 模式？
- [ ] 是否为 `ConfigName` 创建了唯一索引？
- [ ] 配置类是否提供了 `GetDefault()` 方法？
- [ ] 配置类是否提供了 `Validate()` 方法？
- [ ] 是否在 DI 容器中注册了仓储？
- [ ] 是否提供了 API 端点用于读写配置？
- [ ] 是否编写了单元测试验证持久化？
- [ ] IO 相关配置是否使用了 `TriggerLevel` 枚举？
- [ ] IO 相关配置是否包含必需字段（BoardId, ChannelNumber, IoType, TriggerLevel）？

## 违规处理

任何违反上述规则的修改，均视为**无效修改**，不得合并到主分支。

具体违规行为包括：
1. 新增配置未实现持久化
2. 使用 `static` 字段存储可变配置
3. 配置类缺少必需的元数据字段
4. 配置类未提供验证和默认值方法
5. IO 配置未使用 `TriggerLevel` 明确电平语义
6. 仓储实现未使用 `Connection=shared` 模式
7. 未为 `ConfigName` 创建唯一索引

## 参考实现

参考以下已有实现作为最佳实践：

- **面板配置**: `PanelConfiguration` + `LiteDbPanelConfigurationRepository`
- **IO联动配置**: `IoLinkageConfiguration` + `LiteDbIoLinkageConfigurationRepository`
- **传感器配置**: `SensorConfiguration` + `LiteDbSensorConfigurationRepository`

## 文档维护

本文档应随着系统演进持续更新。任何新增的配置持久化要求或最佳实践都应及时补充到本文档。

---

**文档版本**: 1.0  
**最后更新**: 2025-11-22  
**维护团队**: ZakYip Development Team
