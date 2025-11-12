# 系统配置管理指南

## 概述

系统配置管理功能允许您通过 LiteDB 数据库和 REST API 灵活管理系统级配置参数，支持热重载，无需重启服务即可生效。

## 配置项说明

### 可配置参数

| 参数名称 | 类型 | 默认值 | 范围/说明 |
|---------|------|--------|----------|
| `exceptionChuteId` | string | `CHUTE_EXCEPTION` | 异常格口ID，用于处理分拣失败或无法分配格口的包裹 |
| `mqttDefaultPort` | int | `1883` | MQTT默认端口（1-65535） |
| `tcpDefaultPort` | int | `8888` | TCP默认端口（1-65535） |
| `chuteAssignmentTimeoutMs` | int | `10000` | 格口分配超时时间（1000-60000毫秒） |
| `requestTimeoutMs` | int | `5000` | 请求超时时间（1000-60000毫秒） |
| `retryCount` | int | `3` | 重试次数（0-10） |
| `retryDelayMs` | int | `1000` | 重试延迟（100-10000毫秒） |
| `enableAutoReconnect` | bool | `true` | 是否启用自动重连 |

### 配置存储

所有配置存储在 LiteDB 数据库中，默认路径为 `Data/routes.db`。系统配置使用独立的集合 `SystemConfiguration`，与路由配置分离。

## API 使用

### 1. 获取当前系统配置

**请求：**
```bash
GET /api/config/system
```

**响应示例：**
```json
{
  "id": 1,
  "exceptionChuteId": "CHUTE_EXCEPTION",
  "mqttDefaultPort": 1883,
  "tcpDefaultPort": 8888,
  "chuteAssignmentTimeoutMs": 10000,
  "requestTimeoutMs": 5000,
  "retryCount": 3,
  "retryDelayMs": 1000,
  "enableAutoReconnect": true,
  "version": 1,
  "createdAt": "2025-11-12T18:00:00.000Z",
  "updatedAt": "2025-11-12T18:00:00.000Z"
}
```

### 2. 获取配置模板

获取默认配置模板，可用作配置文件的参考。

**请求：**
```bash
GET /api/config/system/template
```

**响应示例：**
```json
{
  "exceptionChuteId": "CHUTE_EXCEPTION",
  "mqttDefaultPort": 1883,
  "tcpDefaultPort": 8888,
  "chuteAssignmentTimeoutMs": 10000,
  "requestTimeoutMs": 5000,
  "retryCount": 3,
  "retryDelayMs": 1000,
  "enableAutoReconnect": true
}
```

### 3. 更新系统配置（热重载）

更新配置后立即生效，无需重启服务。版本号自动递增。

**请求：**
```bash
PUT /api/config/system
Content-Type: application/json

{
  "exceptionChuteId": "CUSTOM_EXCEPTION",
  "mqttDefaultPort": 1884,
  "tcpDefaultPort": 9999,
  "chuteAssignmentTimeoutMs": 15000,
  "requestTimeoutMs": 8000,
  "retryCount": 5,
  "retryDelayMs": 2000,
  "enableAutoReconnect": true
}
```

**响应示例：**
```json
{
  "id": 1,
  "exceptionChuteId": "CUSTOM_EXCEPTION",
  "mqttDefaultPort": 1884,
  "tcpDefaultPort": 9999,
  "chuteAssignmentTimeoutMs": 15000,
  "requestTimeoutMs": 8000,
  "retryCount": 5,
  "retryDelayMs": 2000,
  "enableAutoReconnect": true,
  "version": 2,
  "createdAt": "2025-11-12T18:00:00.000Z",
  "updatedAt": "2025-11-12T18:05:00.000Z"
}
```

### 4. 重置为默认配置

将系统配置重置为默认值。

**请求：**
```bash
POST /api/config/system/reset
```

**响应：**
返回重置后的配置（与获取配置相同的格式）。

## 使用示例

### cURL 示例

#### 获取配置
```bash
curl http://localhost:5000/api/config/system
```

#### 更新配置
```bash
curl -X PUT http://localhost:5000/api/config/system \
  -H "Content-Type: application/json" \
  -d '{
    "exceptionChuteId": "CUSTOM_EXCEPTION",
    "mqttDefaultPort": 1884,
    "tcpDefaultPort": 9999,
    "chuteAssignmentTimeoutMs": 15000,
    "requestTimeoutMs": 8000,
    "retryCount": 5,
    "retryDelayMs": 2000,
    "enableAutoReconnect": true
  }'
```

#### 重置配置
```bash
curl -X POST http://localhost:5000/api/config/system/reset
```

### PowerShell 示例

#### 获取配置
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/config/system" -Method Get
```

#### 更新配置
```powershell
$body = @{
    exceptionChuteId = "CUSTOM_EXCEPTION"
    mqttDefaultPort = 1884
    tcpDefaultPort = 9999
    chuteAssignmentTimeoutMs = 15000
    requestTimeoutMs = 8000
    retryCount = 5
    retryDelayMs = 2000
    enableAutoReconnect = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/config/system" `
    -Method Put `
    -Body $body `
    -ContentType "application/json"
```

## 配置验证

系统会自动验证配置参数的有效性：

### 验证规则

- **异常格口ID**: 不能为空
- **MQTT端口**: 必须在 1-65535 之间
- **TCP端口**: 必须在 1-65535 之间
- **格口分配超时**: 必须在 1000-60000 毫秒之间
- **请求超时**: 必须在 1000-60000 毫秒之间
- **重试次数**: 必须在 0-10 之间
- **重试延迟**: 必须在 100-10000 毫秒之间

### 验证错误示例

如果提供无效值，API 将返回 400 Bad Request：

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "MqttDefaultPort": [
      "MQTT默认端口必须在1-65535之间"
    ]
  }
}
```

## 热重载机制

配置更新后，系统会立即从数据库读取最新配置：

1. **ParcelSortingOrchestrator**: 每次处理包裹时读取最新的异常格口ID和超时时间
2. **版本控制**: 每次更新配置时，版本号自动递增
3. **时间戳**: 自动维护创建时间和更新时间

## 环境迁移

### 导出配置

使用 API 获取当前配置并保存：

```bash
curl http://localhost:5000/api/config/system > system-config.json
```

### 导入配置

在新环境中更新配置：

```bash
curl -X PUT http://localhost:5000/api/config/system \
  -H "Content-Type: application/json" \
  -d @system-config.json
```

### 数据库迁移

直接复制 `Data/routes.db` 文件到新环境即可，包含所有路由配置和系统配置。

## 最佳实践

1. **版本控制**: 建议将配置模板提交到版本控制系统
2. **环境差异**: 为不同环境（开发、测试、生产）维护不同的配置
3. **监控版本**: 定期检查配置版本号，确保配置同步
4. **备份配置**: 定期备份 LiteDB 数据库文件
5. **验证测试**: 更新配置后，验证系统行为是否符合预期

## 故障排查

### 配置未生效

1. 检查 API 返回的 version 是否增加
2. 查看应用日志确认配置更新
3. 验证数据库文件路径是否正确

### 数据库访问问题

1. 确保数据目录存在且有写权限
2. 检查是否有其他进程占用数据库文件
3. 验证数据库文件未损坏

### 验证失败

1. 参考验证规则检查参数范围
2. 确保所有必填字段都已提供
3. 检查 JSON 格式是否正确

## 参考资料

- [路由配置管理](CONFIGURATION_API.md)
- [API 使用指南](API_USAGE_GUIDE.md)
- [Swagger 文档](http://localhost:5000/swagger)
