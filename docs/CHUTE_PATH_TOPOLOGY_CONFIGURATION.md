# 格口路径拓扑配置说明

## 概述

**格口路径拓扑配置**（Chute Path Topology Configuration）是一个新的配置概念，用于描述从入口到各个格口的完整路径拓扑结构。与现有的线体拓扑配置不同，本配置通过**引用已配置的ID**来组织路径关系，而不是重复定义物理结构。

---

## 拓扑结构示例

```
      格口B     格口D     格口F
        ↑         ↑         ↑
入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(默认异常口)
  ↓     ↓         ↓         ↓
传感器  格口A      格口C     格口E
```

---

## API端点

- **GET** `/api/config/chute-path-topology` - 获取格口路径拓扑配置
- **PUT** `/api/config/chute-path-topology` - 更新格口路径拓扑配置

---

## 配置结构

格口路径拓扑配置通过引用其他配置中已定义的ID来组织路径关系：

| 配置元素 | 引用来源 | 说明 |
|---------|---------|------|
| `EntrySensorId` | 感应IO配置 (`SensorConfiguration`) | 入口传感器，类型必须是 `ParcelCreation` |
| `DiverterNodes[].DiverterId` | 摆轮设备配置 (`WheelDiverterConfiguration`) | 摆轮设备ID |
| `DiverterNodes[].SegmentId` | 线体拓扑配置 (`LineTopologyConfig.LineSegments`) | 线体段ID |
| `DiverterNodes[].FrontSensorId` | 感应IO配置 (`SensorConfiguration`) | 摆轮前感应IO，类型必须是 `WheelFront` |
| `ExceptionChuteId` | 格口ID | 默认异常格口 |

---

## 配置示例

```json
{
  "topologyName": "标准格口路径拓扑",
  "description": "3摆轮6格口的标准配置",
  "entrySensorId": 1,
  "diverterNodes": [
    {
      "diverterId": 1,
      "diverterName": "摆轮D1",
      "positionIndex": 1,
      "segmentId": 1,
      "frontSensorId": 2,
      "leftChuteIds": [2],
      "rightChuteIds": [1],
      "remarks": "第一个摆轮，格口A在右侧，格口B在左侧"
    },
    {
      "diverterId": 2,
      "diverterName": "摆轮D2",
      "positionIndex": 2,
      "segmentId": 2,
      "frontSensorId": 3,
      "leftChuteIds": [4],
      "rightChuteIds": [3],
      "remarks": "第二个摆轮，格口C在右侧，格口D在左侧"
    },
    {
      "diverterId": 3,
      "diverterName": "摆轮D3",
      "positionIndex": 3,
      "segmentId": 3,
      "frontSensorId": 4,
      "leftChuteIds": [6],
      "rightChuteIds": [5],
      "remarks": "第三个摆轮，格口E在右侧，格口F在左侧"
    }
  ],
  "exceptionChuteId": 999
}
```

> **注意**：线速（`defaultLineSpeedMmps`）已从拓扑配置中移除，线速现在在线体段配置（`LineSegmentConfig.SpeedMmPerSec`）中定义。

---

## 导入导出

### JSON导出

```
GET /api/config/chute-path-topology/export/json
```

导出当前配置为JSON文件，可用于备份或迁移。

### CSV导出

```
GET /api/config/chute-path-topology/export/csv
```

导出当前配置为CSV文件，便于在Excel等工具中查看和编辑。

CSV格式说明：
- `#` 开头的行为配置元数据注释
- 列头: `DiverterId,DiverterName,PositionIndex,SegmentId,FrontSensorId,LeftChuteIds,RightChuteIds,Remarks`
- `LeftChuteIds` 和 `RightChuteIds` 使用分号分隔多个值

### JSON导入

```
POST /api/config/chute-path-topology/import/json
Content-Type: multipart/form-data
```

从JSON文件导入配置，将覆盖当前配置。

### CSV导入

```
POST /api/config/chute-path-topology/import/csv?topologyName={name}&entrySensorId={id}&exceptionChuteId={id}&description={desc}
Content-Type: multipart/form-data
```

从CSV文件导入摆轮节点配置，需要提供拓扑元数据参数。

---

## 模拟测试

### 模拟包裹分拣路径

```
POST /api/config/chute-path-topology/simulate
Content-Type: application/json

{
  "targetChuteId": 1,
  "lineSpeedMmps": 1000,
  "defaultSegmentLengthMm": 5000,
  "simulateTimeout": false,
  "timeoutExtraDelayMs": 5000,
  "simulateParcelLoss": false,
  "parcelLossAtDiverterIndex": 1,
  "routingRequestDelayMs": 50,
  "sensorDetectionDelayMs": 10,
  "diverterActionDelayMs": 100
}
```

用于测试拓扑配置是否正确，模拟包裹从入口到指定格口的完整分拣过程。

**请求参数说明:**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| targetChuteId | long | 是 | - | 目标格口ID |
| lineSpeedMmps | decimal | 否 | 1000 | 线体速度（毫米/秒） |
| defaultSegmentLengthMm | double | 否 | 5000 | 默认线体段长度（毫米） |
| simulateTimeout | bool | 否 | false | 是否模拟超时场景 |
| timeoutExtraDelayMs | int | 否 | 5000 | 超时额外延迟（毫秒） |
| simulateParcelLoss | bool | 否 | false | 是否模拟丢包场景 |
| parcelLossAtDiverterIndex | int | 否 | 1 | 丢包位置（第几个摆轮） |

**返回结果:**

```json
{
  "success": true,
  "data": {
    "parcelId": "SIM-20241126193000-1",
    "targetChuteId": 1,
    "actualChuteId": 1,
    "isExceptionChute": false,
    "simulationStartTime": "2024-11-26T19:30:00",
    "simulationEndTime": "2024-11-26T19:30:05.250",
    "totalDurationMs": 5250,
    "totalDistanceMm": 5000,
    "diverterCount": 1,
    "isSuccess": true,
    "isParcelLost": false,
    "isTimeout": false,
    "steps": [
      {
        "stepNumber": 1,
        "stepType": "ParcelCreation",
        "description": "包裹在入口传感器创建",
        "nodeName": "入口传感器",
        "durationMs": 0,
        "cumulativeTimeMs": 0,
        "status": "Success"
      },
      {
        "stepNumber": 2,
        "stepType": "RoutingRequest",
        "description": "向上游请求路由",
        "durationMs": 50,
        "cumulativeTimeMs": 50,
        "status": "Success"
      },
      {
        "stepNumber": 3,
        "stepType": "Transit",
        "description": "包裹运输到摆轮D1",
        "nodeId": 1,
        "nodeName": "摆轮D1",
        "durationMs": 5000,
        "cumulativeTimeMs": 5050,
        "status": "Success"
      },
      {
        "stepNumber": 4,
        "stepType": "DiverterAction",
        "description": "摆轮D1执行左转分拣",
        "nodeId": 1,
        "durationMs": 100,
        "cumulativeTimeMs": 5150,
        "status": "Success"
      },
      {
        "stepNumber": 5,
        "stepType": "ChuteArrival",
        "description": "包裹成功落入格口1",
        "nodeId": 1,
        "cumulativeTimeMs": 5150,
        "status": "Success"
      }
    ],
    "summary": "包裹成功分拣到格口1，耗时5250ms"
  }
}
```

**模拟场景:**

1. **正常分拣**: `simulateTimeout=false, simulateParcelLoss=false`
2. **超时场景**: `simulateTimeout=true` - 包裹将被路由到异常格口
3. **丢包场景**: `simulateParcelLoss=true, parcelLossAtDiverterIndex=2` - 在第2个摆轮处模拟丢包

---

## 与其他配置的关系

### 依赖关系图

```
感应IO配置 (SensorConfiguration)
    ├── ParcelCreation 类型 ──→ 格口路径拓扑.EntrySensorId
    └── WheelFront 类型 ──────→ 格口路径拓扑.DiverterNodes[].FrontSensorId

线体拓扑配置 (LineTopologyConfig.LineSegments)
    └── SegmentId ────────────→ 格口路径拓扑.DiverterNodes[].SegmentId
    └── SpeedMmPerSec ────────→ 线速在此定义，不在拓扑中

摆轮设备配置 (WheelDiverterConfiguration)
    └── DiverterId ───────────→ 格口路径拓扑.DiverterNodes[].DiverterId
```

### 配置顺序

1. **第一步**：配置感应IO (`/api/config/sensors`)
2. **第二步**：配置线体段 (`/api/config/line-topology`)
3. **第三步**：配置摆轮设备 (`/api/config/wheel-diverter`)
4. **第四步**：配置格口路径拓扑 (`/api/config/chute-path-topology`)

---

## 验证规则

在更新配置时，系统会进行以下验证：

1. **入口传感器ID验证**
   - 必须在感应IO配置中存在
   - 类型必须是 `ParcelCreation`

2. **摆轮节点验证**
   - 至少需要配置一个摆轮节点
   - 每个节点的 `segmentId` 必须在线体段配置中存在
   - 如果配置了 `frontSensorId`，必须在感应IO配置中存在且类型为 `WheelFront`
   - 每个节点至少配置一侧格口（`leftChuteIds` 或 `rightChuteIds`）
   - `positionIndex` 不能重复

---

## 使用场景

### 场景1：新建分拣线拓扑

1. 先配置感应IO（入口传感器、各摆轮前传感器）
2. 配置线体段（入口到第一摆轮、摆轮之间的线体段）
3. 配置摆轮设备
4. 最后配置格口路径拓扑，引用上述配置的ID

### 场景2：调整格口分布

直接修改格口路径拓扑配置中的 `leftChuteIds` 和 `rightChuteIds`，无需修改其他配置。

### 场景3：增加摆轮

1. 在线体段配置中添加新的线体段
2. 在摆轮设备配置中添加新的摆轮
3. 在感应IO配置中添加新的摆轮前传感器（可选）
4. 在格口路径拓扑中添加新的摆轮节点

---

## 最佳实践

### 1. ID 引用一致性

确保所有引用的ID在对应的配置中已存在，否则更新会失败。

### 2. 位置索引顺序

`positionIndex` 应按物理位置顺序从1开始递增，这有助于路径计算和调试。

### 3. 备注信息

为每个摆轮节点添加清晰的 `remarks`，便于理解配置。

### 4. 异常格口

始终配置一个有效的 `exceptionChuteId`，用于处理无法正常分拣的包裹。

---

## 相关API文档

- [感应IO配置API](/api/config/sensors)
- [线体拓扑配置API](/api/config/line-topology)
- [摆轮设备配置API](/api/config/wheel-diverter)
- [配置管理最佳实践](./CONFIG_MIGRATION_GUIDE.md)

---

**文档版本**: 1.0  
**最后更新**: 2025-11-26  
**维护团队**: ZakYip Development Team
