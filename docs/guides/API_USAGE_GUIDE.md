# API 使用教程

## 概述

本文档提供摆轮分拣系统API的完整使用指南，包括接口说明、示例请求和最佳实践。

## 目录

- [访问API文档](#访问api文档)
- [环境配置](#环境配置)
- [路由配置管理](#路由配置管理)
- [调试分拣功能](#调试分拣功能)
- [错误处理](#错误处理)
- [使用Postman](#使用postman)

## 访问API文档

### Swagger UI

系统集成了交互式API文档，启动服务后可通过以下地址访问：

```
http://localhost:5000/swagger
```

Swagger UI提供：
- 📋 完整的API接口列表
- 📝 详细的参数说明和示例
- 🧪 交互式测试功能（Try it out）
- 📖 数据模型定义

### OpenAPI规范

可以通过以下URL获取OpenAPI JSON规范：

```
http://localhost:5000/swagger/v1/swagger.json
```

## 环境配置

### 基础URL

开发环境：`http://localhost:5000`  
生产环境：根据实际部署情况配置

### 请求头

所有POST和PUT请求需要设置：
```
Content-Type: application/json
```

## 路由配置管理

路由配置定义了包裹从入口到指定格口的摆轮动作序列。

### 数据模型

#### 摆轮角度（DiverterAngle）

支持的摆轮角度：
- `0` - 直行（0度）
- `30` - 30度偏转
- `45` - 45度偏转
- `90` - 90度偏转（直角分拣）

#### 路由配置请求（RouteConfigRequest）

```json
{
  "chuteId": "CHUTE-01",           // 格口标识（必填）
  "diverterConfigurations": [       // 摆轮配置列表（必填）
    {
      "diverterId": "DIV-001",      // 摆轮设备ID（必填）
      "targetAngle": 45,             // 目标角度（必填）
      "sequenceNumber": 1            // 顺序号，从1开始（必填）
    }
  ],
  "isEnabled": true                  // 是否启用（默认true）
}
```

**重要约束：**
- 顺序号（sequenceNumber）必须从1开始
- 顺序号必须连续，不能跳过
- 同一配置中顺序号不能重复

### 1. 获取所有路由配置

**请求：**
```bash
GET /api/config/routes
```

**响应示例：**
```json
[
  {
    "id": 1,
    "chuteId": "CHUTE-01",
    "diverterConfigurations": [
      {
        "diverterId": "DIV-001",
        "targetAngle": 45,
        "sequenceNumber": 1
      },
      {
        "diverterId": "DIV-002",
        "targetAngle": 30,
        "sequenceNumber": 2
      }
    ],
    "isEnabled": true,
    "createdAt": "2025-11-12T16:30:00Z",
    "updatedAt": "2025-11-12T16:30:00Z"
  }
]
```

**使用curl：**
```bash
curl -X GET "http://localhost:5000/api/config/routes"
```

### 2. 获取指定格口的路由配置

**请求：**
```bash
GET /api/config/routes/{chuteId}
```

**参数：**
- `chuteId` - 格口标识（路径参数）

**示例：**
```bash
curl -X GET "http://localhost:5000/api/config/routes/CHUTE-01"
```

### 3. 创建路由配置

**请求：**
```bash
POST /api/config/routes
Content-Type: application/json
```

**请求体示例：**
```json
{
  "chuteId": "CHUTE-01",
  "diverterConfigurations": [
    {
      "diverterId": "DIV-001",
      "targetAngle": 45,
      "sequenceNumber": 1
    },
    {
      "diverterId": "DIV-002",
      "targetAngle": 30,
      "sequenceNumber": 2
    },
    {
      "diverterId": "DIV-003",
      "targetAngle": 45,
      "sequenceNumber": 3
    }
  ],
  "isEnabled": true
}
```

**使用curl：**
```bash
curl -X POST "http://localhost:5000/api/config/routes" \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE-01",
    "diverterConfigurations": [
      {
        "diverterId": "DIV-001",
        "targetAngle": 45,
        "sequenceNumber": 1
      },
      {
        "diverterId": "DIV-002",
        "targetAngle": 30,
        "sequenceNumber": 2
      }
    ],
    "isEnabled": true
  }'
```

**响应：**
- 成功：`201 Created`，返回创建的配置
- 失败：
  - `400 Bad Request` - 参数验证失败
  - `409 Conflict` - 配置已存在

### 4. 更新路由配置

**请求：**
```bash
PUT /api/config/routes/{chuteId}
Content-Type: application/json
```

**特点：**
- 支持热更新，配置立即生效
- 无需重启服务

**示例：**
```bash
curl -X PUT "http://localhost:5000/api/config/routes/CHUTE-01" \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE-01",
    "diverterConfigurations": [
      {
        "diverterId": "DIV-001",
        "targetAngle": 90,
        "sequenceNumber": 1
      }
    ],
    "isEnabled": true
  }'
```

### 5. 删除路由配置

**请求：**
```bash
DELETE /api/config/routes/{chuteId}
```

**示例：**
```bash
curl -X DELETE "http://localhost:5000/api/config/routes/CHUTE-01"
```

**响应：**
- 成功：`204 No Content`
- 失败：`404 Not Found` - 配置不存在

## 调试分拣功能

调试接口用于手动触发包裹分拣流程，测试摆轮路径执行。

### 调试分拣请求

**请求：**
```bash
POST /api/debug/sort
Content-Type: application/json
```

**请求体：**
```json
{
  "parcelId": "PKG001",          // 包裹标识（必填）
  "targetChuteId": "CHUTE-01"    // 目标格口标识（必填）
}
```

**响应示例（成功）：**
```json
{
  "parcelId": "PKG001",
  "targetChuteId": "CHUTE-01",
  "isSuccess": true,
  "actualChuteId": "CHUTE-01",
  "message": "分拣成功：包裹 PKG001 已送达格口 CHUTE-01",
  "failureReason": null,
  "pathSegmentCount": 3
}
```

**响应示例（失败）：**
```json
{
  "parcelId": "PKG001",
  "targetChuteId": "CHUTE-99",
  "isSuccess": false,
  "actualChuteId": "UNKNOWN",
  "message": "分拣失败",
  "failureReason": "未找到格口 CHUTE-99 的路由配置",
  "pathSegmentCount": 0
}
```

**使用curl：**
```bash
curl -X POST "http://localhost:5000/api/debug/sort" \
  -H "Content-Type: application/json" \
  -d '{
    "parcelId": "PKG001",
    "targetChuteId": "CHUTE-01"
  }'
```

## 错误处理

### HTTP状态码

| 状态码 | 说明 |
|--------|------|
| 200 OK | 请求成功 |
| 201 Created | 资源创建成功 |
| 204 No Content | 删除成功（无响应体）|
| 400 Bad Request | 请求参数错误 |
| 404 Not Found | 资源不存在 |
| 409 Conflict | 资源冲突（如重复创建）|
| 500 Internal Server Error | 服务器内部错误 |

### 错误响应格式

```json
{
  "message": "错误描述信息"
}
```

### 常见错误

#### 1. 参数验证失败
```json
{
  "message": "格口ID不能为空"
}
```

**解决方法：** 检查请求参数是否完整且符合要求

#### 2. 配置已存在
```json
{
  "message": "格口 CHUTE-01 的配置已存在，请使用PUT方法更新"
}
```

**解决方法：** 使用PUT请求更新配置，或先删除现有配置

#### 3. 配置不存在
```json
{
  "message": "格口 CHUTE-01 的配置不存在"
}
```

**解决方法：** 检查格口ID是否正确，或先创建配置

#### 4. 顺序号不连续
```json
{
  "message": "顺序号必须连续"
}
```

**解决方法：** 确保sequenceNumber从1开始且连续（1, 2, 3...）

## 使用Postman

### 导入Postman集合

1. 下载 `postman_collection.json` 文件
2. 打开Postman应用
3. 点击"Import"按钮
4. 选择下载的JSON文件
5. 集合导入成功，包含所有API接口

### 配置环境变量

在Postman中设置环境变量：
```
baseUrl = http://localhost:5000
```

### 测试流程

1. **创建路由配置**
   - 使用"创建路由配置"请求
   - 修改chuteId和摆轮配置
   - 发送请求

2. **验证配置**
   - 使用"获取所有路由配置"查看创建的配置
   - 或使用"根据格口ID获取路由配置"查看特定配置

3. **测试分拣**
   - 使用"调试分拣"请求
   - 输入包裹ID和目标格口ID
   - 查看分拣结果

4. **更新配置**
   - 使用"更新路由配置"修改摆轮动作
   - 再次测试分拣验证更新

5. **清理**
   - 使用"删除路由配置"删除测试数据

## 最佳实践

### 1. 配置管理

- ✅ 在生产环境前，先在开发环境充分测试路由配置
- ✅ 使用有意义的格口ID命名（如：CHUTE-01、CHUTE-02）
- ✅ 保持摆轮ID与物理设备一致
- ✅ 记录配置变更历史

### 2. 调试与测试

- ✅ 使用调试接口验证新配置
- ✅ 测试各种分拣场景（成功、失败、边界情况）
- ✅ 监控分拣结果和错误信息

### 3. 错误处理

- ✅ 始终检查HTTP状态码
- ✅ 解析错误响应中的message字段
- ✅ 实现重试机制（针对5xx错误）

### 4. 性能优化

- ✅ 使用GET请求获取配置时考虑缓存
- ✅ 批量操作时控制请求频率
- ✅ 监控API响应时间

## 技术支持

如有问题或建议，请联系：
- 邮箱：support@example.com
- 项目地址：https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter

## 分拣模式配置

系统支持三种分拣模式，可以通过 API 动态切换，配置立即生效无需重启。

### 分拣模式说明

1. **正式分拣模式 (Formal)** - 默认模式
   - 由上游 Sorting.RuleEngine 给出格口分配
   - 适用于正常生产环境
   - 系统启动时默认使用此模式

2. **指定落格分拣模式 (FixedChute)**
   - 所有包裹（异常除外）都将发送到指定的固定格口
   - 适用于测试或特殊场景
   - 需要配置 `fixedChuteId` 参数

3. **循环格口落格模式 (RoundRobin)**
   - 包裹依次分拣到可用格口列表中的格口
   - 适用于负载均衡或测试场景
   - 需要配置 `availableChuteIds` 参数

### 1. 获取当前分拣模式

**请求：**
```bash
GET /api/config/system/sorting-mode
```

**响应示例：**
```json
{
  "sortingMode": "Formal",
  "fixedChuteId": null,
  "availableChuteIds": []
}
```

**使用curl：**
```bash
curl -X GET "http://localhost:5000/api/config/system/sorting-mode"
```

### 2. 切换到正式分拣模式

**请求：**
```bash
PUT /api/config/system/sorting-mode
Content-Type: application/json
```

**请求体：**
```json
{
  "sortingMode": "Formal"
}
```

**使用curl：**
```bash
curl -X PUT "http://localhost:5000/api/config/system/sorting-mode" \
  -H "Content-Type: application/json" \
  -d '{"sortingMode": "Formal"}'
```

**响应：**
```json
{
  "sortingMode": "Formal",
  "fixedChuteId": null,
  "availableChuteIds": []
}
```

### 3. 切换到指定落格模式

**请求体：**
```json
{
  "sortingMode": "FixedChute",
  "fixedChuteId": 1
}
```

**使用curl：**
```bash
curl -X PUT "http://localhost:5000/api/config/system/sorting-mode" \
  -H "Content-Type: application/json" \
  -d '{"sortingMode": "FixedChute", "fixedChuteId": 1}'
```

**响应：**
```json
{
  "sortingMode": "FixedChute",
  "fixedChuteId": 1,
  "availableChuteIds": []
}
```

**注意：** `fixedChuteId` 必须是已在路由配置中存在的格口ID，否则会返回 400 错误。

### 4. 切换到循环格口模式

**请求体：**
```json
{
  "sortingMode": "RoundRobin",
  "availableChuteIds": [1, 2, 3, 4, 5, 6]
}
```

**使用curl：**
```bash
curl -X PUT "http://localhost:5000/api/config/system/sorting-mode" \
  -H "Content-Type: application/json" \
  -d '{"sortingMode": "RoundRobin", "availableChuteIds": [1, 2, 3, 4, 5, 6]}'
```

**响应：**
```json
{
  "sortingMode": "RoundRobin",
  "fixedChuteId": null,
  "availableChuteIds": [1, 2, 3, 4, 5, 6]
}
```

### 常见错误

#### FixedChute 模式未提供格口ID
```json
{
  "message": "指定落格分拣模式下，固定格口ID必须配置且大于0"
}
```

**解决方法：** 在请求中添加 `fixedChuteId` 参数

#### RoundRobin 模式未提供格口列表
```json
{
  "message": "循环格口落格模式下，必须配置至少一个可用格口"
}
```

**解决方法：** 在请求中添加 `availableChuteIds` 数组参数

#### 无效的分拣模式值
```json
{
  "message": "分拣模式值无效，仅支持：Formal（正常）、FixedChute（指定落格）、RoundRobin（循环落格）"
}
```

**解决方法：** 检查 `sortingMode` 参数值是否正确

## 更新日志

### v1.1.0 (2025-11-19)
- ✨ 新增分拣模式配置 API
- ✨ 支持三种分拣模式：正式、指定落格、循环格口
- ✨ 增强 PanelSimulation 仿真模式安全保护
- 🔒 仿真端点在非仿真模式下返回明确错误，不再抛出异常

### v1.0.0 (2025-11-12)
- ✨ 初始版本
- ✨ 实现路由配置管理API
- ✨ 实现调试分拣API
- ✨ 集成Swagger/OpenAPI文档
- ✨ 提供Postman集合
