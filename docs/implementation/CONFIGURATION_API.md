# 配置管理 API 文档

## 概述

本系统提供了基于 LiteDB 的配置管理功能，支持通过 API 端点动态管理格口到摆轮的路由配置，无需重启应用即可生效（热更新）。

## API 端点

### 1. 获取所有路由配置

**请求:**
```http
GET /api/config/routes
```

**响应:**
```json
[
  {
    "id": 1,
    "chuteId": "CHUTE_A",
    "diverterConfigurations": [
      {
        "diverterId": "D1",
        "targetAngle": 30,
        "sequenceNumber": 1
      },
      {
        "diverterId": "D2",
        "targetAngle": 45,
        "sequenceNumber": 2
      }
    ],
    "isEnabled": true,
    "createdAt": "2025-11-12T08:38:27.097Z",
    "updatedAt": "2025-11-12T08:38:27.097Z"
  }
]
```

### 2. 获取指定格口的路由配置

**请求:**
```http
GET /api/config/routes/{chuteId}
```

**示例:**
```bash
curl -X GET http://localhost:5000/api/config/routes/CHUTE_A
```

**响应:**
```json
{
  "id": 1,
  "chuteId": "CHUTE_A",
  "diverterConfigurations": [
    {
      "diverterId": "D1",
      "targetAngle": 30,
      "sequenceNumber": 1
    }
  ],
  "isEnabled": true,
  "createdAt": "2025-11-12T08:38:27.097Z",
  "updatedAt": "2025-11-12T08:38:27.097Z"
}
```

### 3. 创建新的路由配置

**请求:**
```http
POST /api/config/routes
Content-Type: application/json
```

**请求体:**
```json
{
  "chuteId": "CHUTE_D",
  "diverterConfigurations": [
    {
      "diverterId": "D2",
      "targetAngle": 45,
      "sequenceNumber": 1
    },
    {
      "diverterId": "D3",
      "targetAngle": 90,
      "sequenceNumber": 2
    }
  ],
  "isEnabled": true
}
```

**示例:**
```bash
curl -X POST http://localhost:5000/api/config/routes \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE_D",
    "diverterConfigurations": [
      {"diverterId": "D2", "targetAngle": 45, "sequenceNumber": 1},
      {"diverterId": "D3", "targetAngle": 90, "sequenceNumber": 2}
    ],
    "isEnabled": true
  }'
```

**响应:**
- **201 Created**: 配置创建成功
- **400 Bad Request**: 请求验证失败
- **409 Conflict**: 格口配置已存在

### 4. 更新现有路由配置（热更新）

**请求:**
```http
PUT /api/config/routes/{chuteId}
Content-Type: application/json
```

**请求体:**
```json
{
  "chuteId": "CHUTE_A",
  "diverterConfigurations": [
    {
      "diverterId": "D1",
      "targetAngle": 45,
      "sequenceNumber": 1
    }
  ],
  "isEnabled": true
}
```

**示例:**
```bash
curl -X PUT http://localhost:5000/api/config/routes/CHUTE_A \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE_A",
    "diverterConfigurations": [
      {"diverterId": "D1", "targetAngle": 45, "sequenceNumber": 1}
    ],
    "isEnabled": true
  }'
```

**响应:**
- **200 OK**: 配置更新成功，立即生效（热更新）
- **400 Bad Request**: 请求验证失败

**注意:** 配置更新后立即生效，无需重启应用。

### 5. 删除路由配置

**请求:**
```http
DELETE /api/config/routes/{chuteId}
```

**示例:**
```bash
curl -X DELETE http://localhost:5000/api/config/routes/CHUTE_D
```

**响应:**
- **204 No Content**: 删除成功
- **404 Not Found**: 配置不存在

## 数据模型

### DiverterConfigRequest

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| diverterId | string | 是 | 摆轮标识或设备ID |
| targetAngle | DiverterAngle | 是 | 目标摆轮角度（0, 30, 45, 90） |
| sequenceNumber | int | 是 | 段的顺序号，从1开始 |

### RouteConfigRequest

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| chuteId | string | 是 | 目标格口标识 |
| diverterConfigurations | DiverterConfigRequest[] | 是 | 摆轮配置列表 |
| isEnabled | bool | 否 | 是否启用此配置（默认true） |

### DiverterAngle 枚举

| 值 | 说明 |
|----|------|
| 0 | 0度 - 直行通过 |
| 30 | 30度 - 小角度分流 |
| 45 | 45度 - 中角度分流 |
| 90 | 90度 - 大角度分流 |

## 验证规则

1. **格口ID不能为空**
2. **摆轮配置不能为空**
3. **摆轮ID不能为空**
4. **顺序号必须从1开始**
5. **顺序号必须连续**（如：1, 2, 3...）
6. **顺序号不能重复**

## 热更新特性

配置更改后立即生效，无需重启应用：

1. **创建新配置**: 新格口立即可用于分拣
2. **更新配置**: 格口的路由立即按新配置执行
3. **删除配置**: 格口立即不可用，分拣将失败

## 使用示例

### 示例1: 添加新格口

```bash
# 1. 创建新格口 CHUTE_E
curl -X POST http://localhost:5000/api/config/routes \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE_E",
    "diverterConfigurations": [
      {"diverterId": "D1", "targetAngle": 30, "sequenceNumber": 1},
      {"diverterId": "D4", "targetAngle": 90, "sequenceNumber": 2}
    ],
    "isEnabled": true
  }'

# 2. 立即测试新格口（无需重启）
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG001", "targetChuteId": "CHUTE_E"}'
```

### 示例2: 修改现有格口

```bash
# 1. 修改 CHUTE_A 的配置
curl -X PUT http://localhost:5000/api/config/routes/CHUTE_A \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE_A",
    "diverterConfigurations": [
      {"diverterId": "D2", "targetAngle": 90, "sequenceNumber": 1}
    ],
    "isEnabled": true
  }'

# 2. 新配置立即生效
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG002", "targetChuteId": "CHUTE_A"}'
```

### 示例3: 禁用格口

```bash
# 1. 禁用格口
curl -X PUT http://localhost:5000/api/config/routes/CHUTE_B \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE_B",
    "diverterConfigurations": [
      {"diverterId": "D1", "targetAngle": 0, "sequenceNumber": 1}
    ],
    "isEnabled": false
  }'

# 2. 禁用后的格口无法使用
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG003", "targetChuteId": "CHUTE_B"}'
# 返回: 失败，格口未配置
```

## 数据存储

配置数据存储在 LiteDB 数据库中：
- 数据库位置: `Data/routes.db`
- 数据库类型: LiteDB (NoSQL)
- 配置表: `ChuteRoutes`

## 安全性

- 所有用户输入在日志记录前都经过清理，防止日志注入攻击
- 配置验证确保数据完整性
- 使用结构化日志记录

## 错误处理

| 错误代码 | 说明 |
|---------|------|
| 400 Bad Request | 请求验证失败（缺少必填字段、顺序号不连续等） |
| 404 Not Found | 请求的配置不存在 |
| 409 Conflict | 配置已存在（创建时） |
| 500 Internal Server Error | 服务器内部错误 |

## 分拣模式配置 API

### 1. 获取当前分拣模式配置

**请求:**
```http
GET /api/config/system/sorting-mode
```

**响应:**
```json
{
  "sortingMode": "Formal",
  "fixedChuteId": null,
  "availableChuteIds": []
}
```

**分拣模式说明:**
- `Formal`: 正式分拣模式（默认），由上游 Sorting.RuleEngine 给出格口分配
- `FixedChute`: 指定落格分拣模式，所有包裹（异常除外）都将发送到指定的固定格口
- `RoundRobin`: 循环格口落格模式，包裹依次分拣到可用格口列表中的格口

**示例:**
```bash
curl -X GET http://localhost:5000/api/config/system/sorting-mode
```

### 2. 更新分拣模式配置

**请求:**
```http
PUT /api/config/system/sorting-mode
Content-Type: application/json
```

**示例 1: 设置为正式分拣模式**
```json
{
  "sortingMode": "Formal"
}
```

```bash
curl -X PUT http://localhost:5000/api/config/system/sorting-mode \
  -H "Content-Type: application/json" \
  -d '{"sortingMode": "Formal"}'
```

**示例 2: 设置为固定格口模式**
```json
{
  "sortingMode": "FixedChute",
  "fixedChuteId": 1
}
```

```bash
curl -X PUT http://localhost:5000/api/config/system/sorting-mode \
  -H "Content-Type: application/json" \
  -d '{"sortingMode": "FixedChute", "fixedChuteId": 1}'
```

**示例 3: 设置为循环格口模式**
```json
{
  "sortingMode": "RoundRobin",
  "availableChuteIds": [1, 2, 3, 4, 5, 6]
}
```

```bash
curl -X PUT http://localhost:5000/api/config/system/sorting-mode \
  -H "Content-Type: application/json" \
  -d '{"sortingMode": "RoundRobin", "availableChuteIds": [1, 2, 3, 4, 5, 6]}'
```

**响应:**
- **200 OK**: 配置更新成功
- **400 Bad Request**: 请求参数无效（如：FixedChute 模式未提供 fixedChuteId，RoundRobin 模式未提供 availableChuteIds）
- **500 Internal Server Error**: 服务器内部错误

**验证规则:**
- `FixedChute` 模式必须提供 `fixedChuteId` 且大于 0
- `RoundRobin` 模式必须提供至少一个 `availableChuteIds`，且所有格口 ID 必须大于 0
- 分拣模式配置立即生效，无需重启服务

## 异常路由策略配置 API

### 1. 获取异常路由策略

**请求:**
```http
GET /api/config/exception-policy
```

**响应:**
```json
{
  "exceptionChuteId": 999,
  "upstreamTimeoutMs": 10000,
  "retryOnTimeout": false,
  "retryCount": 0,
  "retryDelayMs": 1000,
  "useExceptionOnTopologyUnreachable": true,
  "useExceptionOnTtlFailure": true
}
```

**字段说明:**
- `exceptionChuteId`: 异常格口ID，当包裹无法正常分拣时路由到此格口
- `upstreamTimeoutMs`: **等待上游 RuleEngine 推送结果的超时时间（毫秒）**
  - 默认值：10000 (10秒)
  - 取值范围：1000-60000 (1-60秒)
  - **此配置支持热更新**：修改后立即对新包裹生效
- `retryOnTimeout`: 超时后是否重试
- `retryCount`: 重试次数
- `retryDelayMs`: 重试延迟（毫秒）
- `useExceptionOnTopologyUnreachable`: 拓扑不可达时是否使用异常格口
- `useExceptionOnTtlFailure`: TTL失败时是否使用异常格口

**示例:**
```bash
curl -X GET http://localhost:5000/api/config/exception-policy
```

### 2. 更新异常路由策略（热更新）

**请求:**
```http
PUT /api/config/exception-policy
Content-Type: application/json
```

**请求体:**
```json
{
  "exceptionChuteId": 999,
  "upstreamTimeoutMs": 15000,
  "retryOnTimeout": false,
  "retryCount": 0,
  "retryDelayMs": 1000,
  "useExceptionOnTopologyUnreachable": true,
  "useExceptionOnTtlFailure": true
}
```

**示例:**
```bash
curl -X PUT http://localhost:5000/api/config/exception-policy \
  -H "Content-Type: application/json" \
  -d '{
    "exceptionChuteId": 999,
    "upstreamTimeoutMs": 15000,
    "retryOnTimeout": false,
    "retryCount": 0,
    "retryDelayMs": 1000,
    "useExceptionOnTopologyUnreachable": true,
    "useExceptionOnTtlFailure": true
  }'
```

**响应:**
- **200 OK**: 配置更新成功，立即生效（热更新）
- **400 Bad Request**: 请求参数无效
  - `upstreamTimeoutMs` 必须在 1000-60000 范围内
  - `exceptionChuteId` 必须大于 0
- **500 Internal Server Error**: 服务器内部错误

**热更新特性说明:**

配置更新后**立即生效**，无需重启应用：

1. **上游超时时间 (upstreamTimeoutMs)**:
   - ✅ 修改后立即生效
   - ✅ 新进入系统的包裹使用新的超时时间
   - ⚠️ 正在等待上游响应的包裹继续使用旧的超时时间
   - 策略：**只对新包裹生效**（保证已在处理中的包裹不受影响）

2. **异常格口 (exceptionChuteId)**:
   - ✅ 修改后立即生效
   - ✅ 所有超时/失败的包裹立即路由到新的异常格口

### 异常类型枚举

系统定义了以下异常类型（`ExceptionType` 枚举）：

| 枚举值 | 值 | 说明 | 触发场景 |
|--------|---|------|----------|
| `Unknown` | 0 | 未知异常 | 未分类的错误 |
| **`UpstreamTimeout`** | 1 | **上游超时** | **RuleEngine 未在 TTL 时间内响应** |
| `PathFailure` | 2 | 路径失败 | 执行路径时发生错误 |
| `NodeDegraded` | 3 | 节点降级 | 路径经过不健康节点 |
| `Overload` | 4 | 系统过载 | 系统拥堵超出处理能力 |
| `TopologyUnreachable` | 5 | 拓扑不可达 | 无可用路径到达目标格口 |
| `TtlFailure` | 6 | TTL失败 | 包裹在系统中停留时间过长 |
| `SensorFault` | 7 | 传感器故障 | 传感器检测异常 |
| `ConfigurationError` | 8 | 配置错误 | 配置无效或缺失 |

### 上游超时工作流程

**推送模型下的超时处理：**

1. **包裹检测**：IO传感器检测到包裹 → 系统创建包裹记录
2. **通知上游**：系统通过 `NotifyParcelDetectedAsync()` 通知 RuleEngine
3. **启动计时器**：开始等待上游推送结果，最长等待 `upstreamTimeoutMs`
4. **接收推送**：
   - ✅ **在 TTL 内收到推送** → 使用推送的格口号进行分拣
   - ❌ **超过 TTL 无推送** → 触发 `UpstreamTimeout` 异常 → 路由到异常格口

**验证示例：**

```bash
# 1. 设置较短的超时时间（5秒）
curl -X PUT http://localhost:5000/api/config/exception-policy \
  -H "Content-Type: application/json" \
  -d '{"exceptionChuteId": 999, "upstreamTimeoutMs": 5000}'

# 2. 模拟包裹检测（如果 RuleEngine 未在 5 秒内推送）
# → 包裹将自动路由到格口 999，异常类型为 UpstreamTimeout

# 3. 调整为正常超时时间（10秒）
curl -X PUT http://localhost:5000/api/config/exception-policy \
  -H "Content-Type: application/json" \
  -d '{"exceptionChuteId": 999, "upstreamTimeoutMs": 10000}'
```

## 注意事项

1. **备份数据**: 在修改生产环境配置前，建议备份数据库文件
2. **测试变更**: 建议在测试环境先验证配置更改
3. **顺序号**: 确保摆轮顺序号正确，这直接影响包裹的分拣路径
4. **角度选择**: 根据实际物理布局选择正确的摆轮角度
5. **默认分拣模式**: 系统启动时默认使用正式分拣模式（Formal），除非通过 API 修改
6. **分拣模式切换**: 分拣模式可以随时切换，配置会立即生效
