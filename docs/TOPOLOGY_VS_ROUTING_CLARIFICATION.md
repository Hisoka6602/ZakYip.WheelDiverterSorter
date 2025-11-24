# 线体拓扑配置与路由配置说明

## 概述

在摆轮分拣系统中，**线体拓扑配置**（Line Topology Configuration）和**路由配置**（Route Configuration）是两个不同但相关的概念。本文档旨在阐明两者的区别、作用范围和使用场景，帮助用户正确配置系统。

---

## 线体拓扑配置（Line Topology Configuration）

### API端点
- **GET** `/api/config/line-topology` - 获取线体拓扑配置
- **PUT** `/api/config/line-topology` - 更新线体拓扑配置

### 作用范围
线体拓扑配置描述了分拣线的**物理结构**，定义了：

1. **摆轮节点（Wheel Nodes）**
   - 摆轮的物理位置和顺序
   - 每个摆轮节点的ID、名称、位置索引
   - 摆轮是否支持左侧和右侧分拣

2. **格口（Chutes）**
   - 所有格口的基本信息（ID、名称）
   - 格口绑定到哪个摆轮节点的哪个方向
   - 格口的落格偏移量（用于精确时间计算）
   - 格口是否为异常口

3. **线体段（Line Segments）**
   - 节点之间的物理连接关系
   - 每段的长度和速度
   - 用于计算包裹到达时间和超时阈值

4. **传感器配置**
   - 入口传感器ID
   - 出口传感器ID
   - 默认线速度

### 配置示例

```json
{
  "topologyName": "标准线体拓扑",
  "description": "包含3个摆轮和10个格口的标准配置",
  "wheelNodes": [
    {
      "nodeId": "WHEEL-1",
      "nodeName": "第一摆轮",
      "positionIndex": 1,
      "hasLeftChute": true,
      "hasRightChute": true
    },
    {
      "nodeId": "WHEEL-2",
      "nodeName": "第二摆轮",
      "positionIndex": 2,
      "hasLeftChute": true,
      "hasRightChute": true
    }
  ],
  "chutes": [
    {
      "chuteId": "CHUTE-001",
      "chuteName": "A区01号口",
      "isExceptionChute": false,
      "boundNodeId": "WHEEL-1",
      "boundDirection": "Left",
      "dropOffsetMm": 500.0,
      "isEnabled": true
    },
    {
      "chuteId": "CHUTE-002",
      "chuteName": "A区02号口",
      "isExceptionChute": false,
      "boundNodeId": "WHEEL-1",
      "boundDirection": "Right",
      "dropOffsetMm": 500.0,
      "isEnabled": true
    }
  ],
  "lineSegments": [
    {
      "segmentId": "ENTRY-TO-WHEEL1",
      "fromNodeId": "ENTRY",
      "toNodeId": "WHEEL-1",
      "lengthMm": 5000.0,
      "nominalSpeedMmPerSec": 1000.0,
      "description": "入口到第一个摆轮"
    }
  ],
  "entrySensorId": "SENSOR-ENTRY",
  "exitSensorId": "SENSOR-EXIT",
  "defaultLineSpeedMmps": 1000.0
}
```

### 何时使用
- 初始系统安装时配置物理线体结构
- 增加、移除或重新配置摆轮时
- 修改格口物理布局时
- 调整线体长度或速度参数时
- **变更频率**: 较低，通常只在硬件变更时修改

---

## 路由配置（Route Configuration）

### API端点
- **GET** `/api/config/routes` - 获取所有路由配置
- **GET** `/api/config/routes/{chuteId}` - 获取特定格口的路由配置
- **POST** `/api/config/routes` - 创建新的路由配置
- **PUT** `/api/config/routes/{chuteId}` - 更新路由配置
- **DELETE** `/api/config/routes/{chuteId}` - 删除路由配置
- **GET** `/api/config/routes/export` - 导出路由配置
- **POST** `/api/config/routes/import` - 导入路由配置

### 作用范围
路由配置描述了包裹如何**逻辑分拣**到各个格口，定义了：

1. **摆轮切换序列（Diverter Configurations）**
   - 包裹分拣到某个格口时，哪些摆轮需要切换方向
   - 每个摆轮的切换顺序
   - 每个摆轮段的长度、速度和容错时间

2. **分拣路径优化**
   - 针对不同格口配置不同的摆轮组合
   - 支持多摆轮顺序切换
   - 可以优化分拣效率

3. **容错参数**
   - 皮带速度和长度
   - 容错时间配置

### 配置示例

```json
{
  "chuteId": 1,
  "chuteName": "A区01号口",
  "diverterConfigurations": [
    {
      "diverterId": 1,
      "targetDirection": "Left",
      "sequenceNumber": 1,
      "segmentLengthMm": 5000.0,
      "segmentSpeedMmPerSecond": 1000.0,
      "segmentToleranceTimeMs": 2000
    },
    {
      "diverterId": 2,
      "targetDirection": "Right",
      "sequenceNumber": 2,
      "segmentLengthMm": 5000.0,
      "segmentSpeedMmPerSecond": 1000.0,
      "segmentToleranceTimeMs": 2000
    }
  ],
  "beltSpeedMmPerSecond": 1000.0,
  "beltLengthMm": 10000.0,
  "toleranceTimeMs": 2000,
  "isEnabled": true
}
```

### 何时使用
- 新增格口分拣规则时
- 优化分拣路径时
- 调整容错时间参数时
- 修改摆轮切换顺序时
- **变更频率**: 较高，业务规则调整时经常修改

---

## 两者关系

### 依赖关系
```
线体拓扑配置（物理结构）
    ↓ 引用
路由配置（逻辑分拣规则）
```

路由配置**引用**线体拓扑配置中定义的：
- 格口ID（ChuteId）
- 摆轮ID（DiverterId）

### 配置顺序
1. **第一步**: 配置线体拓扑（定义物理结构）
2. **第二步**: 配置路由规则（定义分拣逻辑）

### 验证规则
- 路由配置中引用的格口ID必须在线体拓扑中存在
- 路由配置中引用的摆轮ID必须在线体拓扑中存在
- 不能存在完全相同的摆轮方向组合（防止路由冲突）

---

## 常见场景

### 场景1: 新增一个格口

1. **更新线体拓扑配置**
   ```
   PUT /api/config/line-topology
   ```
   添加新的格口配置到 `chutes` 数组

2. **创建路由配置**
   ```
   POST /api/config/routes
   ```
   为新格口创建分拣规则

### 场景2: 优化现有格口的分拣路径

直接**更新路由配置**即可：
```
PUT /api/config/routes/{chuteId}
```
无需修改线体拓扑配置

### 场景3: 更换摆轮硬件

1. **更新线体拓扑配置**（如果摆轮位置或能力发生变化）
2. **检查并更新路由配置**（如果需要调整分拣路径）

### 场景4: 调整线速度

1. **更新线体拓扑配置**中的 `defaultLineSpeedMmps`
2. **更新路由配置**中受影响的 `segmentSpeedMmPerSecond` 和容错时间

---

## 最佳实践

### 配置分离原则
- **物理结构变更** → 修改线体拓扑配置
- **业务规则调整** → 修改路由配置

### 配置备份
- 定期导出线体拓扑和路由配置
- 使用版本控制管理配置文件
- 在生产环境变更前先在测试环境验证

### 热更新支持
- 两种配置都支持热更新，无需重启服务
- 配置修改后立即生效

### 配置导入导出
- 路由配置支持 JSON 和 CSV 格式导入导出
- 适用于批量配置和跨环境迁移

---

## 总结

| 维度 | 线体拓扑配置 | 路由配置 |
|------|------------|---------|
| **描述内容** | 物理结构（摆轮位置、格口布局） | 逻辑分拣规则（摆轮切换序列） |
| **配置对象** | 摆轮节点、格口、线体段 | 格口分拣路径 |
| **变更频率** | 低（硬件变更时） | 高（业务规则调整时） |
| **配置顺序** | 先配置 | 后配置（引用拓扑） |
| **导入导出** | 不支持 | 支持（JSON/CSV） |

**简单记忆**：
- **线体拓扑** = "线体长什么样" = 物理布局
- **路由配置** = "包裹怎么走" = 分拣规则

---

## 相关API文档

- [线体拓扑配置API](./CONFIGURATION_API.md#线体拓扑配置)
- [路由配置API](./CONFIGURATION_API.md#路由配置)
- [配置管理最佳实践](./CONFIG_MIGRATION_GUIDE.md)
