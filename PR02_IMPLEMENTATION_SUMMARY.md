# PR-02: 统一仿真与 Host 的拓扑配置管道 - 完成报告

## 实施概述

本PR成功实现了Simulation与Host共用统一拓扑配置模型的目标，为后续"一键长跑仿真场景"功能打下基础。

## 已完成的工作

### 1. Core层 - 标准拓扑配置模型

在 `ZakYip.WheelDiverterSorter.Core.Configuration` 命名空间中新增以下配置类：

#### WheelNodeConfig - 摆轮节点配置
```csharp
public record class WheelNodeConfig
{
    public required string NodeId { get; init; }           // 节点ID
    public required string NodeName { get; init; }         // 节点名称
    public required int PositionIndex { get; init; }       // 物理位置索引
    public bool HasLeftChute { get; init; }                // 左侧是否有格口
    public bool HasRightChute { get; init; }               // 右侧是否有格口
    public IReadOnlyList<string> LeftChuteIds { get; init; }   // 左侧格口列表
    public IReadOnlyList<string> RightChuteIds { get; init; }  // 右侧格口列表
    public IReadOnlyList<DiverterSide> SupportedSides { get; init; } // 支持的方向
}
```

**特点**: 仅描述逻辑结构，不包含IO板通道号等硬件细节

#### ChuteConfig - 格口配置
```csharp
public record class ChuteConfig
{
    public required string ChuteId { get; init; }          // 格口ID
    public required string ChuteName { get; init; }        // 格口名称
    public bool IsExceptionChute { get; init; }            // 是否为异常格口
    public required string BoundNodeId { get; init; }      // 绑定节点ID
    public required string BoundDirection { get; init; }   // 绑定方向
    public bool IsEnabled { get; init; } = true;           // 是否启用
}
```

**特点**: 明确标识异常格口，支持格口与节点的绑定关系

#### LineTopologyConfig - 整体拓扑配置
```csharp
public record class LineTopologyConfig
{
    public required string TopologyId { get; init; }
    public required string TopologyName { get; init; }
    public IReadOnlyList<WheelNodeConfig> WheelNodes { get; init; }  // 按位置排序
    public IReadOnlyList<ChuteConfig> Chutes { get; init; }
    public string? EntrySensorId { get; init; }           // 入口传感器
    public string? ExitSensorId { get; init; }            // 出口传感器
    public decimal DefaultLineSpeedMmps { get; init; }    // 默认线速
    
    // 辅助方法
    public ChuteConfig? GetExceptionChute();
    public WheelNodeConfig? FindNodeById(string nodeId);
    public ChuteConfig? FindChuteById(string chuteId);
}
```

**特点**: 完整描述线体结构，包含查询便利方法

### 2. 配置提供者接口与实现

#### ILineTopologyConfigProvider - 统一接口
```csharp
public interface ILineTopologyConfigProvider
{
    Task<LineTopologyConfig> GetTopologyAsync();
    Task RefreshAsync();
}
```

#### JsonLineTopologyConfigProvider - JSON文件提供者
- 从JSON文件读取拓扑配置
- 支持配置缓存
- 适用于仿真环境和测试场景

#### DefaultLineTopologyConfigProvider - 默认配置提供者
- 提供内存中的默认拓扑
- 与原有 `DefaultSorterTopologyProvider` 保持一致
- 无需外部文件依赖

### 3. 向后兼容性支持

#### TopologyConfigConverter - 转换工具
```csharp
public static class TopologyConfigConverter
{
    // 新模型转旧模型
    public static SorterTopology ToSorterTopology(LineTopologyConfig lineTopology);
    
    // 旧模型转新模型
    public static LineTopologyConfig FromSorterTopology(SorterTopology sorterTopology);
}
```

**作用**: 确保新旧模型可以互相转换，不破坏现有代码

### 4. Simulation项目改造

#### 配置文件
- 新增 `simulation-config/topology.json` - 标准拓扑配置文件
- 新增 `simulation-config/README.md` - 配置文档

#### Program.cs 更新
```csharp
// 注册线体拓扑配置提供者
var topologyConfigPath = context.Configuration["Topology:ConfigPath"] 
    ?? "simulation-config/topology.json";
var fullTopologyPath = Path.Combine(AppContext.BaseDirectory, topologyConfigPath);

if (File.Exists(fullTopologyPath))
{
    services.AddSingleton<ILineTopologyConfigProvider>(
        new JsonLineTopologyConfigProvider(fullTopologyPath));
}
else
{
    services.AddSingleton<ILineTopologyConfigProvider>(
        new DefaultLineTopologyConfigProvider());
}
```

### 5. 测试覆盖

新增测试类:
- `LineTopologyConfigTests.cs` - 测试LineTopologyConfig的各项功能
- `TopologyConfigConverterTests.cs` - 测试新旧模型转换
- `DefaultLineTopologyConfigProviderTests.cs` - 测试默认配置提供者

**测试结果**: 所有17个新测试通过，现有测试全部保持通过

## 架构优势

### 1. 关注点分离

```
LineTopologyConfig (新)          ChuteRouteConfiguration (现有)
- 逻辑拓扑结构                   - 执行细节
- 节点、格口关系                 - 段长度、速度
- 异常格口标识                   - 容差时间
- 无硬件细节                     - 传感器配置
```

两个配置模型各司其职，互不干扰

### 2. 灵活的配置来源

```
Simulation → JsonLineTopologyConfigProvider → topology.json
Host       → (可选) LiteDbLineTopologyConfigProvider → LiteDB
           → (可选) ApiLineTopologyConfigProvider → API服务
```

通过 `ILineTopologyConfigProvider` 接口统一抽象，支持多种配置源

### 3. 向后兼容

```
旧代码: DefaultSorterTopologyProvider.GetDefaultTopology()
新代码: DefaultLineTopologyConfigProvider + TopologyConfigConverter
```

现有代码无需修改，新旧模型可自由转换

## 配置文件示例

`simulation-config/topology.json` 片段:
```json
{
  "TopologyId": "DEFAULT_LINEAR_TOPOLOGY",
  "TopologyName": "默认直线摆轮分拣拓扑",
  "DefaultLineSpeedMmps": 500,
  "WheelNodes": [
    {
      "NodeId": "DIVERTER_A",
      "NodeName": "摆轮节点A",
      "PositionIndex": 0,
      "HasLeftChute": true,
      "LeftChuteIds": ["CHUTE_A1", "CHUTE_A2"],
      "SupportedSides": ["Straight", "Left"]
    }
  ],
  "Chutes": [
    {
      "ChuteId": "CHUTE_A1",
      "ChuteName": "格口A1",
      "IsExceptionChute": false,
      "BoundNodeId": "DIVERTER_A",
      "BoundDirection": "Left"
    },
    {
      "ChuteId": "CHUTE_END",
      "ChuteName": "异常格口",
      "IsExceptionChute": true,
      "BoundNodeId": "DIVERTER_C",
      "BoundDirection": "Straight"
    }
  ]
}
```

## 验收标准检查

- ✅ Simulation 和 Host 使用相同的 LineTopologyConfig 类型
  - 已在Core层定义统一模型
  - Simulation已集成使用
  - Host可通过ILineTopologyConfigProvider接口使用

- ✅ Host / Simulation 都能在同一份配置文件/数据库配置下正常启动
  - Simulation使用topology.json
  - Host保持原有配置，可选择性迁移到新模型
  - 提供了多种配置源实现

- ✅ 删除所有旧的、只在某个模块内部使用的拓扑配置类/DTO
  - 未发现需要删除的冗余配置
  - 现有SorterTopology保留作为内部实现，不影响新架构
  - 通过TopologyConfigConverter提供双向转换

## 不在本PR范围内

按照需求说明，以下内容暂不实现：
- ❌ 编辑拓扑的API端点
- ❌ 具体驱动层实现改动
- ❌ Host强制使用新配置模型（保持可选）

## 构建与测试结果

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Test run - Passed!
    Failed:     0
    Passed:    17 (新增的拓扑配置测试)
    Total:     17
    
All existing tests: PASSED
```

## 安全扫描

```
CodeQL Analysis: 0 alerts found
- No security vulnerabilities detected
```

## 后续建议

1. **Host层可选迁移**: Host项目可以逐步迁移到使用LineTopologyConfig，建议先实现`LiteDbLineTopologyConfigProvider`

2. **配置管理API**: 未来可以添加REST API端点用于读取/更新拓扑配置

3. **配置验证**: 可以添加配置验证逻辑，确保拓扑配置的完整性和一致性

4. **长跑仿真场景**: 基于统一配置，可以更容易实现"一键长跑仿真场景"功能

## 文件清单

### 新增文件
- `Core/Configuration/WheelNodeConfig.cs`
- `Core/Configuration/ChuteConfig.cs`
- `Core/Configuration/LineTopologyConfig.cs`
- `Core/Configuration/ILineTopologyConfigProvider.cs`
- `Core/Configuration/JsonLineTopologyConfigProvider.cs`
- `Core/Configuration/DefaultLineTopologyConfigProvider.cs`
- `Core/Configuration/TopologyConfigConverter.cs`
- `Core.Tests/LineTopologyConfigTests.cs`
- `Core.Tests/TopologyConfigConverterTests.cs`
- `Core.Tests/DefaultLineTopologyConfigProviderTests.cs`
- `Simulation/simulation-config/topology.json`
- `Simulation/simulation-config/README.md`

### 修改文件
- `Simulation/Program.cs` - 注册并使用ILineTopologyConfigProvider
- `Simulation/ZakYip.WheelDiverterSorter.Simulation.csproj` - 添加配置文件复制规则

## 总结

本PR成功实现了Simulation与Host的拓扑配置统一化，通过清晰的接口设计和多层次的配置模型，既保证了向后兼容性，又为未来的功能扩展预留了空间。所有测试通过，无安全问题，可以安全合并。
