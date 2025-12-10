# 感应IO轮询时间配置指南

## 概述

本指南说明如何通过 API 配置感应IO的轮询间隔时间。轮询间隔决定了系统检测传感器状态变化的频率，直接影响包裹检测的响应速度和系统CPU占用。

## 功能特性

- ✅ 支持为每个传感器单独配置轮询间隔
- ✅ 支持全局默认轮询间隔（10ms）
- ✅ 配置立即生效，无需重启服务
- ✅ 配置持久化存储在 LiteDB 数据库中

## 配置层级

系统采用**两级配置**机制：

### 1. 传感器级别配置（最高优先级）

为单个传感器指定独立的轮询间隔。

**配置路径：** `SensorConfiguration.Sensors[].PollingIntervalMs`

**API 端点：** `PUT /api/hardware/leadshine/sensors`

**示例：** 为创建包裹感应IO设置 20ms 轮询间隔

```json
{
  "sensors": [
    {
      "sensorId": 1,
      "sensorName": "创建包裹感应IO",
      "ioType": "ParcelCreation",
      "ioPointId": 0,
      "pollingIntervalMs": 20,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    }
  ]
}
```

### 2. 全局默认值（当传感器未配置时使用）

**默认值：** 10ms

**适用范围：** 所有未单独配置 `pollingIntervalMs` 的传感器

**说明：** 如果传感器的 `pollingIntervalMs` 字段为 `null` 或未设置，系统将使用全局默认值 10ms。

## 建议的轮询间隔设置

| 轮询间隔 | 适用场景 | CPU 占用 | 检测精度 |
|---------|---------|---------|---------|
| **5-10ms** | 快速移动包裹，高速分拣线 | 高 | 高精度 |
| **10-20ms** | 标准速度场景（推荐） | 中等 | 标准精度 |
| **20-50ms** | 低速场景，降低CPU占用 | 低 | 较低精度 |

**推荐配置：**
- 创建包裹感应IO：10ms（需要快速响应）
- 摆轮前感应IO：10-15ms（需要精确触发）
- 锁格感应IO：15-20ms（可适当放宽）

## API 使用示例

### 1. 获取当前感应IO配置

查看所有感应IO的当前配置，包括轮询间隔。

```bash
GET /api/hardware/leadshine/sensors
```

**响应示例：**

```json
{
  "success": true,
  "data": {
    "id": 1,
    "sensors": [
      {
        "sensorId": 1,
        "sensorName": "创建包裹感应IO",
        "ioType": "ParcelCreation",
        "ioPointId": 0,
        "pollingIntervalMs": 10,
        "triggerLevel": "ActiveHigh",
        "isEnabled": true
      },
      {
        "sensorId": 2,
        "sensorName": "摆轮1前感应IO",
        "ioType": "WheelFront",
        "ioPointId": 1,
        "boundWheelNodeId": "WHEEL-1",
        "pollingIntervalMs": null,
        "triggerLevel": "ActiveHigh",
        "isEnabled": true
      }
    ],
    "version": 1,
    "createdAt": "2025-12-10T10:00:00Z",
    "updatedAt": "2025-12-10T10:00:00Z"
  },
  "message": null,
  "timestamp": "2025-12-10T11:30:00Z"
}
```

**字段说明：**
- `pollingIntervalMs: 10` - 传感器使用独立配置的 10ms 轮询间隔
- `pollingIntervalMs: null` - 传感器使用全局默认值 10ms

### 2. 更新单个传感器的轮询间隔

```bash
PUT /api/hardware/leadshine/sensors
Content-Type: application/json

{
  "sensors": [
    {
      "sensorId": 1,
      "sensorName": "创建包裹感应IO",
      "ioType": "ParcelCreation",
      "ioPointId": 0,
      "pollingIntervalMs": 15,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    },
    {
      "sensorId": 2,
      "sensorName": "摆轮1前感应IO",
      "ioType": "WheelFront",
      "ioPointId": 1,
      "boundWheelNodeId": "WHEEL-1",
      "pollingIntervalMs": null,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    }
  ]
}
```

**注意事项：**
- 必须包含所有传感器的完整配置（更新是替换式的，不是增量式的）
- 设置 `pollingIntervalMs: null` 表示使用全局默认值
- 配置立即生效，正在运行的分拣任务不受影响

### 3. 批量设置所有传感器使用相同轮询间隔

```bash
PUT /api/hardware/leadshine/sensors
Content-Type: application/json

{
  "sensors": [
    {
      "sensorId": 1,
      "sensorName": "创建包裹感应IO",
      "ioType": "ParcelCreation",
      "ioPointId": 0,
      "pollingIntervalMs": 20,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    },
    {
      "sensorId": 2,
      "sensorName": "摆轮1前感应IO",
      "ioType": "WheelFront",
      "ioPointId": 1,
      "boundWheelNodeId": "WHEEL-1",
      "pollingIntervalMs": 20,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    },
    {
      "sensorId": 3,
      "sensorName": "格口1锁格感应IO",
      "ioType": "ChuteLock",
      "ioPointId": 2,
      "boundChuteId": "CHUTE-001",
      "pollingIntervalMs": 20,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    }
  ]
}
```

### 4. 重置为默认配置

```bash
POST /api/hardware/leadshine/sensors/reset
```

**响应：**

重置后所有传感器的 `pollingIntervalMs` 将被设置为 `null`，使用全局默认值 10ms。

## 验证配置生效

配置更新后，可以通过以下方式验证：

### 1. 查看日志

系统在创建传感器时会输出日志：

```
[INF] 成功创建雷赛传感器 1，类型: ParcelCreation，输入位: 0，轮询间隔: 20ms (独立配置)
[INF] 成功创建雷赛传感器 2，类型: WheelFront，输入位: 1，轮询间隔: 10ms (全局默认)
```

### 2. 观察系统性能

- **CPU 占用率：** 轮询间隔越短，CPU 占用越高
- **响应速度：** 轮询间隔越短，包裹检测响应越快

### 3. 使用 GET API 确认

```bash
GET /api/hardware/leadshine/sensors
```

检查响应中的 `pollingIntervalMs` 字段值。

## 常见问题

### Q1: 为什么我的配置没有生效？

**A:** 请检查以下几点：
1. 确认 API 调用返回成功（200 OK）
2. 确认使用的是 PUT 方法而不是 POST
3. 确认请求体包含了所有传感器的完整配置
4. 重启系统后重新验证（虽然理论上不需要重启）

### Q2: 轮询间隔设置多少合适？

**A:** 根据以下因素决定：
- **包裹移动速度：** 速度越快，轮询间隔应该越短
- **检测精度要求：** 精度要求越高，轮询间隔应该越短
- **CPU 资源：** CPU 资源紧张时，可以适当增大轮询间隔
- **推荐起点：** 从 10ms 开始，根据实际情况调整

### Q3: 轮询间隔可以设置为 0 吗？

**A:** 不建议。虽然技术上可以设置较小的值，但过小的轮询间隔会导致：
- CPU 占用率过高
- 可能导致系统响应性下降
- 对硬件性能要求更高
- **建议最小值：** 5ms

### Q4: 不同传感器可以设置不同的轮询间隔吗？

**A:** 可以！这正是该功能的优势。您可以：
- 为关键传感器（如创建包裹感应IO）设置较短的轮询间隔（5-10ms）
- 为非关键传感器设置较长的轮询间隔（20-30ms）
- 平衡系统整体的CPU占用和响应速度

### Q5: 如何判断当前轮询间隔是否合适？

**A:** 观察以下指标：
- **包裹漏检率：** 如果频繁漏检，需要缩短轮询间隔
- **CPU 占用率：** 如果 CPU 占用过高，可以适当延长轮询间隔
- **响应延迟：** 如果分拣动作延迟明显，需要缩短轮询间隔
- **系统稳定性：** 如果系统不稳定，可能是轮询间隔过短

## 技术实现说明

### 工作原理

1. **配置存储：** `SensorConfiguration` 存储在 LiteDB 数据库中
2. **配置读取：** `LeadshineSensorFactory` 在创建传感器时读取配置
3. **轮询逻辑：** 每个 `LeadshineSensor` 实例在自己的循环中使用配置的轮询间隔
4. **热更新：** 配置更新后，新创建的传感器实例将使用新配置

### 相关代码

- **配置模型：** `src/Core/.../LineModel/Configuration/Models/SensorConfiguration.cs`
- **API 端点：** `src/Host/.../Controllers/HardwareConfigController.cs`
- **工厂类：** `src/Ingress/.../Sensors/LeadshineSensorFactory.cs`
- **传感器实现：** `src/Ingress/.../Sensors/LeadshineSensor.cs`

## 相关文档

- [系统配置管理指南](./SYSTEM_CONFIG_GUIDE.md)
- [API 使用教程](./API_USAGE_GUIDE.md)
- [硬件配置管理 API](../swagger) - Swagger 文档

---

**最后更新：** 2025-12-10  
**维护团队：** ZakYip Development Team
