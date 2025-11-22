# 运行前健康检查（Pre-Run Health Check）

## 概述

运行前健康检查是一个新增的健康检查端点，用于在系统启动后、开始实际分拣前验证所有关键配置是否就绪。该功能确保系统具备基本可运行条件，避免因配置不完整导致的运行时故障。

## API 端点

### GET /health/prerun

**描述**: 验证系统基础运行条件是否就绪

**响应状态码**:
- `200 OK`: 系统配置完整，可以开始运行
- `503 Service Unavailable`: 系统配置不完整，不可运行

**响应格式**:

```json
{
  "overallStatus": "Healthy" | "Unhealthy",
  "checks": [
    {
      "name": "ExceptionChuteConfigured",
      "status": "Healthy" | "Unhealthy",
      "message": "异常口 999 已配置且存在于拓扑中"
    },
    {
      "name": "PanelIoConfigured",
      "status": "Healthy" | "Unhealthy",
      "message": "面板 IO 配置完整且有效"
    },
    {
      "name": "LineTopologyValid",
      "status": "Healthy" | "Unhealthy",
      "message": "拓扑配置完整，共 2 个摆轮节点，5 个格口，所有路径可达"
    },
    {
      "name": "LineSegmentsLengthAndSpeedValid",
      "status": "Healthy" | "Unhealthy",
      "message": "所有 3 个线体段的长度与速度配置有效"
    }
  ]
}
```

## 检查项目

### 1. 异常口配置检查（ExceptionChuteConfigured）

**检查内容**:
- 异常口ID是否已配置且大于0
- 异常口是否存在于线体拓扑的格口配置中
- 异常口是否有有效的可达路径

**失败场景**:
- 系统配置未初始化
- 异常口ID未配置或配置为无效值（<=0）
- 异常口不存在于线体拓扑中
- 异常口无可达路径

**示例消息**:
- ✅ `"异常口 999 已配置且存在于拓扑中"`
- ❌ `"系统配置未初始化"`
- ❌ `"异常口ID未配置或配置为无效值"`
- ❌ `"异常口 999 不存在于线体拓扑中"`

### 2. 面板 IO 配置检查（PanelIoConfigured）

**检查内容**:
- 开始按钮 IO 是否已配置
- 停止按钮 IO 是否已配置
- 急停按钮 IO 是否已配置
- 开始按钮灯 IO 是否已配置
- 停止按钮灯 IO 是否已配置
- 连接状态灯 IO 是否已配置
- 三色灯（红、黄、绿）IO 是否已配置
- 所有 IO 位配置是否在有效范围内（0-1023）

**失败场景**:
- 面板启用但缺少必需的 IO 配置
- IO 位配置超出有效范围
- 配置验证失败（如轮询间隔、防抖时间不合法）

**示例消息**:
- ✅ `"面板功能未启用，跳过检查"` （面板未启用视为健康）
- ✅ `"面板 IO 配置完整且有效"`
- ❌ `"缺少面板 IO 配置：开始按钮 IO、急停按钮 IO"`
- ❌ `"面板配置验证失败：轮询间隔必须在 50-1000 毫秒之间"`

### 3. 摆轮拓扑完整性检查（LineTopologyValid）

**检查内容**:
- 线体拓扑是否已配置
- 是否至少配置了一个摆轮节点
- 入口到首个摆轮是否有有效的线体段
- 所有配置的格口是否都有可达路径

**失败场景**:
- 线体拓扑未配置
- 拓扑中未配置任何摆轮节点
- 找不到从入口到首个摆轮的路径
- 存在无可达路径的格口

**示例消息**:
- ✅ `"拓扑配置完整，共 2 个摆轮节点，5 个格口，所有路径可达"`
- ❌ `"线体拓扑未配置"`
- ❌ `"拓扑中未配置任何摆轮节点"`
- ❌ `"找不到从入口到首个摆轮 WHEEL-1 的路径"`
- ❌ `"以下格口无可达路径：100、200、300"`

### 4. 线体长度与线速度合法性检查（LineSegmentsLengthAndSpeedValid）

**检查内容**:
- 所有线体段的长度（LengthMm）是否大于0
- 所有线体段的标称速度（NominalSpeedMmPerSec）是否大于0
- 特别验证入口到首个摆轮路径上的线体段配置

**失败场景**:
- 存在长度<=0的线体段
- 存在速度<=0的线体段
- 关键路径段配置无效

**示例消息**:
- ✅ `"所有 3 个线体段的长度与速度配置有效"`
- ❌ `"线体拓扑未配置，无法检查线体段"`
- ❌ `"发现 2 个非法线体段配置：SEG-1(长度=-100mm)、SEG-2(速度=0mm/s)"`
- ❌ `"关键路径段 SEG-ENTRY-WHEEL1 配置无效（长度=-100mm，速度=0mm/s）"`

## 使用场景

### 1. 系统上线前验证

在系统首次部署或配置变更后，运维/工程人员可以调用该端点验证配置完整性：

```bash
curl http://localhost:5000/health/prerun
```

### 2. 容器编排平台就绪检查

可配置为 Kubernetes readiness probe，确保 Pod 在配置完整前不接收流量：

```yaml
readinessProbe:
  httpGet:
    path: /health/prerun
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 10
```

### 3. CI/CD 流程验证

在自动化部署流程中，可通过该端点验证部署后的配置状态：

```bash
#!/bin/bash
response=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health/prerun)
if [ $response -eq 200 ]; then
  echo "✅ Pre-run health check passed"
  exit 0
else
  echo "❌ Pre-run health check failed"
  exit 1
fi
```

## 技术实现

### 架构设计

```
┌─────────────────────────────────────┐
│     HealthController (Host)         │
│  GET /health/prerun                 │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  IPreRunHealthCheckService          │
│  (Application Layer)                │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  ISafeExecutionService              │
│  (异常隔离)                          │
└─────────────────────────────────────┘
```

### 关键特性

1. **异常安全**: 所有检查逻辑都通过 `ISafeExecutionService` 包裹，确保单个检查失败不影响其他检查
2. **分层架构**: 业务逻辑在 Application 层，Host 层仅做薄壳调用
3. **可扩展性**: 易于添加新的检查项目
4. **中文消息**: 所有错误消息使用中文，便于现场运维

### 服务注册

服务已自动注册到 DI 容器中：

```csharp
// HealthCheckServiceExtensions.cs
services.AddSingleton<IPreRunHealthCheckService, PreRunHealthCheckService>();
```

## 测试

项目包含完整的单元测试覆盖（16个测试用例）：

```bash
dotnet test --filter "FullyQualifiedName~PreRunHealthCheckServiceTests"
```

测试覆盖：
- ✅ 异常口检查：4个测试用例
- ✅ 面板IO检查：3个测试用例
- ✅ 拓扑完整性检查：4个测试用例
- ✅ 线体段配置检查：3个测试用例
- ✅ 综合检查：2个测试用例

## 未来扩展

以下检查项目可在后续版本中添加：

1. **上游超时配置检查**（UpstreamTimeoutConfigured）
   - 验证上游超时配置是否存在
   - 验证超时时间是否合理
   
2. **驱动器基础配置检查**（DriverConfigValid）
   - 验证驱动器连接配置
   - 验证驱动器参数有效性
   
3. **数据库连接检查**（DatabaseConnected）
   - 验证LiteDB数据库连接
   - 验证数据库文件权限

## 相关文档

- [健康检查架构设计](./HEALTH_CHECK_ARCHITECTURE.md)
- [系统配置指南](./SYSTEM_CONFIG_GUIDE.md)
- [面板配置指南](./PANEL_CONFIG_GUIDE.md)
- [拓扑配置指南](./TOPOLOGY_CONFIG_GUIDE.md)

## 更新日志

### v1.0.0 (2025-11-22)
- ✅ 实现运行前健康检查服务
- ✅ 添加 /health/prerun 端点
- ✅ 实现 4 项核心检查（异常口、面板IO、拓扑、线体段）
- ✅ 添加 16 个单元测试
- ✅ 完成文档编写
