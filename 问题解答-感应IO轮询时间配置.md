# 问题解答：感应IO轮询时间配置

## 用户问题

> 为什么现在仍无法在 `/api/hardware/leadshine/sensors` 中配置轮询时间？  
> 当前感应IO的轮询时间是在哪里配置的？

## 简短回答

**功能已完整实现！您可以通过 `/api/hardware/leadshine/sensors` API 配置轮询时间。**

## 详细说明

### 1. 当前感应IO轮询时间的配置位置

感应IO的轮询时间采用**两级配置机制**：

#### ① 传感器级别配置（最高优先级）

**API 端点：** `PUT /api/hardware/leadshine/sensors`

**配置方式：** 在请求体中设置 `pollingIntervalMs` 字段

**示例：**

```json
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
    }
  ]
}
```

上述配置将传感器 1 的轮询间隔设置为 20ms。

#### ② 全局默认值（当传感器未配置时使用）

**默认值：** 10ms（硬编码在 `LeadshineSensorFactory` 中）

**使用方式：** 将 `pollingIntervalMs` 设置为 `null` 或不设置该字段

**示例：**

```json
{
  "sensors": [
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

上述配置将传感器 2 的轮询间隔设置为默认值 10ms。

### 2. 完整的配置流程

#### 步骤 1: 查看当前配置

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
        "bitNumber": 0,
        "pollingIntervalMs": 10,
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
    ],
    "version": 1,
    "createdAt": "2025-12-10T10:00:00Z",
    "updatedAt": "2025-12-10T10:00:00Z"
  }
}
```

#### 步骤 2: 更新配置

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
      "pollingIntervalMs": 15,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    },
    {
      "sensorId": 2,
      "sensorName": "摆轮1前感应IO",
      "ioType": "WheelFront",
      "bitNumber": 1,
      "boundWheelNodeId": "WHEEL-1",
      "pollingIntervalMs": 20,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    }
  ]
}
```

**注意事项：**
- 必须包含所有传感器的完整配置（不是增量更新）
- 配置立即生效，无需重启服务
- 正在运行的分拣任务不受影响

#### 步骤 3: 验证配置生效

**方法 1：查看 API 返回**

再次调用 `GET /api/hardware/leadshine/sensors`，检查 `pollingIntervalMs` 字段。

**方法 2：查看系统日志**

系统在启动或重新加载传感器配置时会输出：

```
[INF] 成功创建雷赛传感器 1，类型: ParcelCreation，输入位: 0，轮询间隔: 15ms (独立配置)
[INF] 成功创建雷赛传感器 2，类型: WheelFront，输入位: 1，轮询间隔: 20ms (独立配置)
```

**方法 3：观察系统性能**

- CPU 占用率：轮询间隔越短，CPU 占用越高
- 响应速度：轮询间隔越短，包裹检测响应越快

### 3. 建议的轮询间隔设置

| 轮询间隔 | 适用场景 | CPU 占用 | 检测精度 |
|---------|---------|---------|---------|
| **5-10ms** | 快速移动包裹，高速分拣线 | 高 | 高精度 |
| **10-20ms** | 标准速度场景（**推荐**） | 中等 | 标准精度 |
| **20-50ms** | 低速场景，降低CPU占用 | 低 | 较低精度 |

**针对不同传感器的推荐配置：**

- **创建包裹感应IO：** 10ms（需要快速响应）
- **摆轮前感应IO：** 10-15ms（需要精确触发）
- **锁格感应IO：** 15-20ms（可适当放宽）

### 4. 常见问题

#### Q1: 为什么之前不知道可以配置？

**A:** 主要原因：
1. 缺少专门的配置文档说明
2. Swagger API 文档中该字段不够突出
3. 该字段是可选的（`int?`），容易被忽略

**解决方案：** 现在已补充完整的配置文档（见下方链接）。

#### Q2: 轮询间隔设置多少合适？

**A:** 建议从 10ms 开始，根据实际情况调整：
- **包裹移动速度快** → 缩短轮询间隔（5-10ms）
- **检测精度要求高** → 缩短轮询间隔（5-10ms）
- **CPU 资源紧张** → 延长轮询间隔（20-30ms）
- **系统稳定优先** → 使用推荐值（10-20ms）

#### Q3: 不同传感器可以设置不同的轮询间隔吗？

**A:** 可以！这正是该功能的优势。您可以：
- 为关键传感器设置较短的轮询间隔
- 为非关键传感器设置较长的轮询间隔
- 平衡系统整体的 CPU 占用和响应速度

#### Q4: 配置后需要重启服务吗？

**A:** 不需要！配置更新后立即生效，无需重启服务。

#### Q5: 如何判断当前轮询间隔是否合适？

**A:** 观察以下指标：
- **包裹漏检率：** 如果频繁漏检 → 缩短轮询间隔
- **CPU 占用率：** 如果 CPU 占用过高 → 延长轮询间隔
- **响应延迟：** 如果分拣动作延迟明显 → 缩短轮询间隔
- **系统稳定性：** 如果系统不稳定 → 可能轮询间隔过短

### 5. 技术实现说明

#### 数据模型

**文件：** `src/Core/.../LineModel/Configuration/Models/SensorConfiguration.cs`

```csharp
public class SensorIoEntry
{
    // ... 其他字段 ...
    
    /// <summary>
    /// 传感器轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 设置此传感器的独立轮询周期。
    /// 如果为 null，则使用全局默认值 (10ms)。
    /// 建议范围：5ms - 50ms
    /// </remarks>
    public int? PollingIntervalMs { get; set; }
}
```

#### API 端点

**文件：** `src/Host/.../Controllers/HardwareConfigController.cs`

```csharp
// 获取配置
[HttpGet("leadshine/sensors")]
public ActionResult<ApiResponse<SensorConfiguration>> GetLeadshineSensorIoConfig()

// 更新配置
[HttpPut("leadshine/sensors")]
public ActionResult<ApiResponse<SensorConfiguration>> UpdateLeadshineSensorIoConfig(
    [FromBody] SensorConfiguration request)

// 重置配置
[HttpPost("leadshine/sensors/reset")]
public ActionResult<ApiResponse<SensorConfiguration>> ResetLeadshineSensorIoConfig()
```

#### 业务逻辑

**文件：** `src/Ingress/.../Sensors/LeadshineSensorFactory.cs`

```csharp
public IEnumerable<ISensor> CreateSensors()
{
    // ...
    foreach (var config in sensorConfigs.Where(s => s.IsEnabled))
    {
        // 使用传感器独立的轮询间隔，如果未配置则使用全局默认值
        var pollingIntervalMs = config.PollingIntervalMs ?? _defaultPollingIntervalMs;
        
        var sensor = new LeadshineSensor(
            // ...
            pollingIntervalMs);
        
        _logger.LogInformation(
            "成功创建雷赛传感器 {SensorId}，轮询间隔: {PollingIntervalMs}ms{Source}",
            config.SensorId,
            pollingIntervalMs,
            config.PollingIntervalMs.HasValue ? " (独立配置)" : " (全局默认)");
    }
}
```

### 6. 相关资源

#### 详细文档

- **配置指南：** [docs/guides/SENSOR_IO_POLLING_CONFIGURATION.md](docs/guides/SENSOR_IO_POLLING_CONFIGURATION.md)
- **问题答复：** [SENSOR_POLLING_CONFIGURATION_ANSWER.md](SENSOR_POLLING_CONFIGURATION_ANSWER.md)
- **系统配置：** [docs/guides/SYSTEM_CONFIG_GUIDE.md](docs/guides/SYSTEM_CONFIG_GUIDE.md)

#### 功能演示

运行演示脚本（需要系统正在运行）：

```bash
./demo_sensor_polling_config.sh
```

#### Swagger 文档

系统启动后访问：`http://localhost:5000/swagger`

查找 **硬件配置** → **感应IO配置** 相关端点。

## 总结

**功能已完整实现且可立即使用！**

您可以通过以下步骤配置感应IO的轮询时间：

1. 查看当前配置：`GET /api/hardware/leadshine/sensors`
2. 更新配置：`PUT /api/hardware/leadshine/sensors`（设置 `pollingIntervalMs` 字段）
3. 验证生效：查看 API 返回或系统日志

如有任何疑问，请参考详细配置指南：[docs/guides/SENSOR_IO_POLLING_CONFIGURATION.md](docs/guides/SENSOR_IO_POLLING_CONFIGURATION.md)

---

**文档创建时间：** 2025-12-10  
**维护团队：** ZakYip Development Team
