# 线体拓扑配置说明

## 概述

本目录包含摆轮分拣系统的拓扑配置文件，定义了整条分拣线的物理结构和逻辑关系。

## 配置文件

### topology.json

拓扑配置的主文件，包含以下内容：

- **WheelNodes**: 摆轮节点列表，按物理位置顺序排列
- **Chutes**: 格口配置列表，包括普通格口和异常格口
- **DefaultLineSpeedMmps**: 默认线速（毫米/秒）
- **EntrySensorId**: 入口传感器ID
- **ExitSensorId**: 出口传感器ID

## 配置结构

### WheelNode 配置项

```json
{
  "NodeId": "DIVERTER_A",           // 节点唯一标识
  "NodeName": "摆轮节点A",           // 显示名称
  "PositionIndex": 0,               // 物理位置索引（从0开始）
  "HasLeftChute": true,             // 左侧是否有格口
  "HasRightChute": false,           // 右侧是否有格口
  "LeftChuteIds": ["CHUTE_A1"],     // 左侧关联的格口ID列表
  "RightChuteIds": [],              // 右侧关联的格口ID列表
  "SupportedSides": [               // 支持的分拣方向
    "Straight",                     // 直行
    "Left"                          // 左转
  ],
  "Remarks": "第一个摆轮"            // 备注信息（可选）
}
```

### Chute 配置项

```json
{
  "ChuteId": "CHUTE_A1",            // 格口唯一标识
  "ChuteName": "格口A1",             // 显示名称
  "IsExceptionChute": false,        // 是否为异常格口
  "BoundNodeId": "DIVERTER_A",      // 绑定的节点ID
  "BoundDirection": "Left",         // 绑定的方向（Left/Right/Straight）
  "IsEnabled": true,                // 是否启用
  "Remarks": "备注信息"              // 备注（可选）
}
```

## 使用方式

在仿真程序中，拓扑配置通过 `ILineTopologyConfigProvider` 接口加载：

```csharp
// 使用JSON文件配置
var provider = new JsonLineTopologyConfigProvider("simulation-config/topology.json");
var topology = await provider.GetTopologyAsync();

// 或使用默认配置
var defaultProvider = new DefaultLineTopologyConfigProvider();
var defaultTopology = await defaultProvider.GetTopologyAsync();
```

## 注意事项

1. **节点顺序**: WheelNodes 中的 PositionIndex 必须连续且从0开始
2. **格口绑定**: 每个Chute必须绑定到一个有效的WheelNode
3. **异常格口**: 系统中应该至少有一个IsExceptionChute为true的格口
4. **方向一致性**: Chute的BoundDirection必须与对应节点的SupportedSides匹配

## 与Host配置的关系

此配置文件描述的是**逻辑拓扑**，不包含硬件IO板通道号等物理细节。

Host启动时可以使用相同的LineTopologyConfig模型，但配置来源可能不同：
- Simulation: 从JSON文件读取
- Host: 从LiteDB数据库或API读取

通过统一的配置模型，确保Simulation和Host使用完全一致的拓扑结构。
