# 配置迁移指南：从 appsettings.json 到 API 配置

## 概述

根据新的架构要求，**所有业务配置必须通过 API 端点管理**，不允许在 `appsettings.json` 中配置。

`appsettings.json` 应该仅保留基础设施配置（日志、数据库路径等），业务配置统一通过 API 端点进行管理和持久化。

## 简化后的 appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    },
    "RetentionDays": 3
  },
  
  "RouteConfiguration": {
    "DatabasePath": "Data/routes.db"
  },
  
  "LogCleanup": {
    "LogDirectory": "logs",
    "RetentionDays": 14,
    "MaxTotalSizeMb": 1024,
    "CleanupIntervalHours": 24
  }
}
```

## 配置迁移对照表

### 1. 驱动配置 (Driver)

**旧配置位置**：`appsettings.json` → `Driver`  
**新配置方式**：API 端点  
**控制器**：`DriverConfigController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取驱动配置 | `GET /api/config/driver` | GET |
| 更新驱动配置 | `PUT /api/config/driver` | PUT |

**示例**：
```bash
# 获取当前驱动配置
curl -X GET http://localhost:5000/api/config/driver

# 更新驱动配置
curl -X PUT http://localhost:5000/api/config/driver \
  -H "Content-Type: application/json" \
  -d '{
    "useHardwareDriver": false,
    "vendorType": "Leadshine",
    "cardNo": 0,
    "diverters": [
      {
        "diverterId": 1,
        "outputStartBit": 0,
        "feedbackInputBit": 10
      }
    ]
  }'
```

---

### 2. 传感器配置 (Sensor)

**旧配置位置**：`appsettings.json` → `Sensor`  
**新配置方式**：API 端点  
**控制器**：`SensorConfigController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取传感器配置 | `GET /api/config/sensors` | GET |
| 更新传感器配置 | `PUT /api/config/sensors` | PUT |
| 获取单个传感器 | `GET /api/config/sensors/{sensorId}` | GET |

**示例**：
```bash
# 获取所有传感器配置
curl -X GET http://localhost:5000/api/config/sensors

# 更新传感器配置
curl -X PUT http://localhost:5000/api/config/sensors \
  -H "Content-Type: application/json" \
  -d '{
    "useHardwareSensor": false,
    "vendorType": "Mock",
    "sensors": [
      {
        "sensorId": "SENSOR_PE_01",
        "sensorType": "Photoelectric",
        "inputBit": 0,
        "isEnabled": true
      }
    ]
  }'
```

---

### 3. 规则引擎通信配置 (RuleEngineConnection)

**旧配置位置**：`appsettings.json` → `RuleEngineConnection`  
**新配置方式**：API 端点  
**控制器**：`CommunicationController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取通信配置 | `GET /api/config/communication` | GET |
| 更新通信配置 | `PUT /api/config/communication` | PUT |
| 获取连接状态 | `GET /api/config/communication/status` | GET |
| 测试连接 | `POST /api/config/communication/test` | POST |

**示例**：
```bash
# 获取通信配置
curl -X GET http://localhost:5000/api/config/communication

# 更新通信配置
curl -X PUT http://localhost:5000/api/config/communication \
  -H "Content-Type: application/json" \
  -d '{
    "mode": "Http",
    "httpApi": "http://localhost:5000/api/sorting/chute",
    "timeoutMs": 5000,
    "retryCount": 3,
    "enableAutoReconnect": true
  }'
```

---

### 4. 并发控制配置 (Concurrency)

**旧配置位置**：`appsettings.json` → `Concurrency`  
**新配置方式**：API 端点（包含在系统配置中）  
**控制器**：`SystemConfigController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取系统配置（含并发） | `GET /api/config/system` | GET |
| 更新系统配置（含并发） | `PUT /api/config/system` | PUT |

**示例**：
```bash
# 获取系统配置（包含并发控制）
curl -X GET http://localhost:5000/api/config/system

# 更新并发控制配置
curl -X PUT http://localhost:5000/api/config/system \
  -H "Content-Type: application/json" \
  -d '{
    "concurrency": {
      "maxConcurrentParcels": 10,
      "parcelQueueCapacity": 100,
      "diverterLockTimeoutMs": 5000
    }
  }'
```

---

### 5. 性能优化配置 (Performance)

**旧配置位置**：`appsettings.json` → `Performance`  
**新配置方式**：API 端点（包含在系统配置中）  
**控制器**：`SystemConfigController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取系统配置（含性能） | `GET /api/config/system` | GET |
| 更新系统配置（含性能） | `PUT /api/config/system` | PUT |

**示例**：
```bash
# 更新性能配置
curl -X PUT http://localhost:5000/api/config/system \
  -H "Content-Type: application/json" \
  -d '{
    "performance": {
      "enablePathCaching": true,
      "pathCacheDurationMinutes": 5,
      "enableMetrics": true,
      "enableObjectPooling": true
    }
  }'
```

---

### 6. 中段皮带 IO 联动配置 (MiddleConveyorIo)

**旧配置位置**：`appsettings.json` → `MiddleConveyorIo`  
**新配置方式**：API 端点  
**控制器**：`IoLinkageController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取 IO 联动配置 | `GET /api/config/io-linkage` | GET |
| 更新 IO 联动配置 | `PUT /api/config/io-linkage` | PUT |
| 获取单个段配置 | `GET /api/config/io-linkage/{segmentKey}` | GET |
| 更新单个段配置 | `PUT /api/config/io-linkage/{segmentKey}` | PUT |

**示例**：
```bash
# 获取 IO 联动配置
curl -X GET http://localhost:5000/api/config/io-linkage

# 更新 IO 联动配置
curl -X PUT http://localhost:5000/api/config/io-linkage \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "isSimulationMode": false,
    "startOrderStrategy": "UpstreamFirst",
    "segments": [
      {
        "segmentKey": "Middle1",
        "displayName": "中段皮带1",
        "startOutputChannel": 100,
        "priority": 1
      }
    ]
  }'
```

---

### 7. 仿真配置 (Simulation)

**旧配置位置**：`appsettings.json` → `Simulation`  
**新配置方式**：API 端点  
**控制器**：`SimulationConfigController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取仿真配置 | `GET /api/config/simulation` | GET |
| 更新仿真配置 | `PUT /api/config/simulation` | PUT |

**示例**：
```bash
# 获取仿真配置
curl -X GET http://localhost:5000/api/config/simulation

# 更新仿真配置
curl -X PUT http://localhost:5000/api/config/simulation \
  -H "Content-Type: application/json" \
  -d '{
    "parcelCount": 1000,
    "lineSpeedMmps": 1000,
    "parcelIntervalMs": 300,
    "sortingMode": "RoundRobin",
    "exceptionChuteId": 21,
    "isSimulationEnabled": true
  }'
```

---

### 8. 拓扑配置 (TopologyConfiguration)

**旧配置位置**：`appsettings.json` → `TopologyConfiguration`  
**新配置方式**：API 端点（通过路由配置管理）  
**控制器**：`RouteConfigController` 或专门的拓扑控制器

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取拓扑配置 | `GET /api/config/topology` | GET |
| 更新拓扑配置 | `PUT /api/config/topology` | PUT |

**注意**：拓扑配置可能已整合到路由配置中，请查看 `RouteConfigController` 的文档。

---

### 9. 路由配置 (Routes)

**旧配置位置**：数据库或配置文件  
**新配置方式**：API 端点（已完成文件化导入导出）  
**控制器**：`RouteConfigController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取所有路由 | `GET /api/config/routes` | GET |
| 获取单个路由 | `GET /api/config/routes/{chuteId}` | GET |
| 创建路由 | `POST /api/config/routes` | POST |
| 更新路由 | `PUT /api/config/routes/{chuteId}` | PUT |
| 删除路由 | `DELETE /api/config/routes/{chuteId}` | DELETE |
| **导出路由文件** | `GET /api/config/routes/export?format=json` | GET |
| **导入路由文件** | `POST /api/config/routes/import?mode=skip` | POST |

**文件导入导出示例**：
```bash
# 导出为 JSON 文件
curl -X GET http://localhost:5000/api/config/routes/export?format=json \
  -o routes.json

# 导出为 CSV 文件
curl -X GET http://localhost:5000/api/config/routes/export?format=csv \
  -o routes.csv

# 导入 JSON 文件（跳过已存在）
curl -X POST http://localhost:5000/api/config/routes/import?mode=skip \
  -F "file=@routes.json"

# 导入 CSV 文件（全量替换）
curl -X POST http://localhost:5000/api/config/routes/import?mode=replace \
  -F "file=@routes.csv"
```

---

### 10. 面板配置 (Panel)

**旧配置位置**：数据库  
**新配置方式**：API 端点  
**控制器**：`PanelConfigController`

| 操作 | API 端点 | 方法 |
|-----|---------|-----|
| 获取面板配置 | `GET /api/config/panel` | GET |
| 更新面板配置 | `PUT /api/config/panel` | PUT |
| 重置为默认配置 | `POST /api/config/panel/reset` | POST |

**示例**：
```bash
# 获取面板配置
curl -X GET http://localhost:5000/api/config/panel

# 更新面板配置
curl -X PUT http://localhost:5000/api/config/panel \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "useSimulation": true,
    "pollingIntervalMs": 100,
    "debounceMs": 50
  }'
```

---

## 迁移步骤

### 步骤 1：备份现有配置

```bash
# 备份当前的 appsettings.json
cp appsettings.json appsettings.json.backup

# 备份数据库文件
cp Data/routes.db Data/routes.db.backup
```

### 步骤 2：通过 API 导入现有配置

对于每个配置类型，使用对应的 API 端点将现有配置导入系统：

```bash
# 示例：导入驱动配置
curl -X PUT http://localhost:5000/api/config/driver \
  -H "Content-Type: application/json" \
  -d @driver-config.json

# 示例：导入传感器配置
curl -X PUT http://localhost:5000/api/config/sensors \
  -H "Content-Type: application/json" \
  -d @sensor-config.json

# 类似地导入其他配置...
```

### 步骤 3：替换 appsettings.json

将 `appsettings.json` 替换为简化版本（仅保留基础设施配置）：

```bash
# 使用简化的配置文件
cp docs/appsettings.simplified.json src/Host/ZakYip.WheelDiverterSorter.Host/appsettings.json
```

### 步骤 4：验证配置

重启应用并验证所有配置是否正确加载：

```bash
# 重启应用
dotnet run

# 验证各配置端点
curl http://localhost:5000/api/config/driver
curl http://localhost:5000/api/config/sensors
curl http://localhost:5000/api/config/routes
# ...
```

### 步骤 5：删除备份（可选）

确认一切正常后，可以删除备份文件：

```bash
rm appsettings.json.backup
```

---

## 配置持久化说明

所有通过 API 管理的配置都会持久化到数据库（LiteDB），应用重启后配置不会丢失。

| 配置类型 | 持久化方式 | Repository 接口 |
|---------|----------|----------------|
| 驱动配置 | LiteDB | `IDriverConfigurationRepository` |
| 传感器配置 | LiteDB | `ISensorConfigurationRepository` |
| 路由配置 | LiteDB | `IRouteConfigurationRepository` |
| 面板配置 | LiteDB | `IPanelConfigurationRepository` |
| 通信配置 | LiteDB | `ICommunicationConfigurationRepository` |
| IO 联动配置 | LiteDB | `IIoLinkageConfigurationRepository` |
| 系统配置 | LiteDB | `ISystemConfigurationRepository` |
| 仿真配置 | 静态变量 + 可选持久化 | `SimulationOptions` |

**注意**：仿真配置目前使用静态变量存储运行时更新，如需持久化可按需实现。

---

## 首次启动默认配置

首次启动时，如果数据库中没有配置，系统会使用内置的默认配置。

您也可以通过 API 手动设置初始配置，或使用配置导入功能批量导入。

---

## 常见问题

### Q: 为什么要从 appsettings.json 迁移到 API 配置？

**A**: 
1. **动态配置**：通过 API 可以在运行时动态更新配置，无需重启应用
2. **配置持久化**：配置存储在数据库中，支持版本管理和审计
3. **统一管理**：所有配置通过统一的 API 接口管理，便于自动化和集成
4. **避免冲突**：防止配置文件和数据库配置不一致的问题

### Q: 如果我想要预设默认配置怎么办？

**A**: 您可以：
1. 使用配置文件导入功能批量导入默认配置
2. 在应用启动时通过脚本调用 API 设置默认配置
3. 修改各 Repository 的默认配置逻辑

### Q: 配置文件完全删除会有问题吗？

**A**: 不会。简化后的 `appsettings.json` 仅保留必要的基础设施配置（日志、数据库路径），足以保证应用正常启动。

---

## 相关文档

- [PR-XX 实施说明](./PR-XX_IMPLEMENTATION_SUMMARY.md)
- [路由导入导出 API 文档](../src/Host/Controllers/RouteConfigController.cs)
- [配置 API 文档集合](../src/Host/Controllers/)

---

**文档版本**: 1.0  
**最后更新**: 2025-11-22  
**维护团队**: ZakYip Development Team
