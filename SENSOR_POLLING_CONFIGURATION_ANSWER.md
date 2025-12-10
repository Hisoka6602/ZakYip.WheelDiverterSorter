# 感应IO轮询时间配置功能说明

## 问题

用户提问："为什么现在仍无法在 `/api/hardware/leadshine/sensors` 中配置轮询时间？当前感应IO的轮询时间是在哪里配置的？"

## 答案

### 简短回答

**功能已完整实现！** 用户实际上**可以**通过 `/api/hardware/leadshine/sensors` API 配置轮询时间。

### 详细说明

#### 1. 功能实现状态

✅ **数据模型支持完整**
- `SensorConfiguration.SensorIoEntry` 类已有 `PollingIntervalMs` 字段（可选 int?）
- 字段包含完整的 XML 文档注释和使用说明
- 支持 5-50ms 范围的配置

✅ **API 端点完全支持**
- GET `/api/hardware/leadshine/sensors` - 返回包含 `pollingIntervalMs` 的配置
- PUT `/api/hardware/leadshine/sensors` - 接受包含 `pollingIntervalMs` 的配置
- POST `/api/hardware/leadshine/sensors/reset` - 重置所有配置（包括轮询时间）

✅ **业务逻辑完整实现**
- `LeadshineSensorFactory` 在创建传感器时读取 `PollingIntervalMs` 配置
- 如果传感器未配置，使用全局默认值 10ms
- 配置存储在 LiteDB 数据库，支持持久化和热更新

✅ **日志输出清晰**
- 创建传感器时输出轮询间隔配置来源（独立配置/全局默认）

#### 2. 配置方式

**两级配置机制：**

##### 传感器级别配置（最高优先级）

通过 API 为每个传感器单独设置轮询间隔：

```bash
PUT /api/hardware/leadshine/sensors
Content-Type: application/json

{
  "sensors": [
    {
      "sensorId": 1,
      "sensorName": "创建包裹感应IO",
      "ioType": "ParcelCreation",
      "bitNumber": 0,
      "pollingIntervalMs": 20,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    },
    {
      "sensorId": 2,
      "sensorName": "摆轮1前感应IO",
      "ioType": "WheelFront",
      "bitNumber": 1,
      "boundWheelNodeId": "WHEEL-1",
      "pollingIntervalMs": null,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    }
  ]
}
```

**字段说明：**
- `pollingIntervalMs: 20` - 传感器 1 使用独立配置的 20ms 轮询间隔
- `pollingIntervalMs: null` - 传感器 2 使用全局默认值 10ms

##### 全局默认值

- **默认值：** 10ms
- **位置：** 硬编码在 `LeadshineSensorFactory` 构造函数参数中
- **适用范围：** 所有未单独配置 `pollingIntervalMs` 的传感器

#### 3. 建议的轮询间隔

| 轮询间隔 | 适用场景 | CPU 占用 | 检测精度 |
|---------|---------|---------|---------|
| 5-10ms | 快速移动包裹，高速分拣线 | 高 | 高精度 |
| 10-20ms | 标准速度场景（推荐） | 中等 | 标准精度 |
| 20-50ms | 低速场景，降低CPU占用 | 低 | 较低精度 |

#### 4. 验证功能

已通过以下验证确认功能完整：

✅ `SensorIoEntry` 类包含 `PollingIntervalMs` 字段  
✅ API GET 端点返回 `SensorConfiguration` 类型（包含该字段）  
✅ API PUT 端点接受 `SensorConfiguration` 类型（包含该字段）  
✅ `LeadshineSensorFactory` 正确读取并使用该字段  
✅ 构建成功，无编译错误

## 可能导致混淆的原因

1. **文档缺失：** 之前没有专门的文档说明如何配置轮询时间
2. **Swagger 注释不够清晰：** API 端点的 Swagger 文档可能没有明确说明 `pollingIntervalMs` 字段
3. **字段可为 null：** `pollingIntervalMs` 是可选字段（int?），可能让用户以为不支持配置

## 改进措施

### 1. 新增文档

已创建详细的配置指南：
- **文件：** `docs/guides/SENSOR_IO_POLLING_CONFIGURATION.md`
- **内容：** 完整的配置说明、API 示例、常见问题解答

### 2. 更新文档索引

已在以下文档中添加新指南的链接：
- `docs/DOCUMENTATION_INDEX.md`
- `docs/RepositoryStructure.md`

### 3. 未来建议

- ✅ 补充 Swagger 注释，明确说明 `pollingIntervalMs` 字段的作用
- ✅ 在 API 响应示例中展示 `pollingIntervalMs` 的用法
- ✅ 在 `README.md` 中添加快速链接到配置指南

## 相关文件

### 数据模型
- `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Models/SensorConfiguration.cs`
  - 第 215-227 行：`PollingIntervalMs` 字段定义

### API 端点
- `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/HardwareConfigController.cs`
  - 第 285-308 行：GET `/api/hardware/leadshine/sensors`
  - 第 318-370 行：PUT `/api/hardware/leadshine/sensors`
  - 第 378-406 行：POST `/api/hardware/leadshine/sensors/reset`

### 业务逻辑
- `src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Sensors/LeadshineSensorFactory.cs`
  - 第 74 行：读取 `PollingIntervalMs` 配置
  - 第 76-83 行：传递给传感器实例
  - 第 86-92 行：日志输出轮询间隔来源

- `src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Sensors/LeadshineSensor.cs`
  - 构造函数参数：`int pollingIntervalMs = 10`
  - 第 ___ 行：使用轮询间隔执行检测循环

### 文档
- `docs/guides/SENSOR_IO_POLLING_CONFIGURATION.md` - **新增**
- `docs/DOCUMENTATION_INDEX.md` - **已更新**
- `docs/RepositoryStructure.md` - **已更新**

## 总结

功能已完整实现，用户可以立即通过以下步骤配置轮询时间：

1. **查看当前配置：** `GET /api/hardware/leadshine/sensors`
2. **更新轮询时间：** `PUT /api/hardware/leadshine/sensors`（在请求体中设置 `pollingIntervalMs` 字段）
3. **验证生效：** 查看日志或再次调用 GET API 确认

详细使用说明请参考：[感应IO轮询时间配置指南](./docs/guides/SENSOR_IO_POLLING_CONFIGURATION.md)

---

**调查时间：** 2025-12-10  
**调查人员：** GitHub Copilot Agent  
**结论：** 功能完整可用，已补充文档说明
