# PR-09: 系统健康检查与自检指南

## 概述

PR-09为摆轮分拣系统引入了标准化的健康检查端点和启动自检管线，用于验证系统组件（驱动器、上游系统、配置）的健康状态。

## API端点

### 1. `/healthz` - 进程级健康检查

**用途**: 用于Kubernetes/负载均衡器的存活检查  
**方法**: GET  
**返回**: 简单的健康状态

**响应示例**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-18T10:30:00Z"
}
```

**HTTP状态码**:
- `200 OK`: 进程正常运行
- `503 ServiceUnavailable`: 进程内部错误

**特点**:
- 不依赖驱动自检结果
- 只要进程能响应就认为健康
- 轻量级，响应快速

---

### 2. `/health/line` - 线体级健康检查

**用途**: 详细的系统健康状态查询  
**方法**: GET  
**返回**: 完整的自检报告和系统状态

**响应示例**:
```json
{
  "systemState": "Ready",
  "isSelfTestSuccess": true,
  "lastSelfTestAt": "2025-11-18T10:00:00Z",
  "drivers": [
    {
      "driverName": "RelayWheelDiverter-D001",
      "isHealthy": true,
      "errorCode": null,
      "errorMessage": null,
      "checkedAt": "2025-11-18T10:00:01Z"
    }
  ],
  "upstreams": [
    {
      "endpointName": "RuleEngine-Default",
      "isHealthy": true,
      "errorCode": null,
      "errorMessage": null,
      "checkedAt": "2025-11-18T10:00:02Z"
    }
  ],
  "config": {
    "isValid": true,
    "errorMessage": null
  },
  "summary": {
    "currentCongestionLevel": "Normal",
    "recommendedCapacityParcelsPerMinute": null
  }
}
```

**HTTP状态码**:
- `200 OK`: 系统状态为Ready/Running且自检成功
- `503 ServiceUnavailable`: 系统状态为Faulted/EmergencyStop或自检失败

**详细字段说明**:
- `systemState`: 当前系统状态（Booting/Ready/Running/Paused/Faulted/EmergencyStop）
- `isSelfTestSuccess`: 最近一次自检是否成功
- `lastSelfTestAt`: 最近一次自检时间
- `drivers`: 驱动器健康状态列表
  - `driverName`: 驱动器名称
  - `isHealthy`: 是否健康
  - `errorCode`: 错误代码（如果不健康）
  - `errorMessage`: 中文错误消息
  - `checkedAt`: 检查时间
- `upstreams`: 上游系统健康状态列表
  - `endpointName`: 端点名称
  - `isHealthy`: 是否健康
  - `errorCode`: 错误代码（如果不健康）
  - `errorMessage`: 中文错误消息
  - `checkedAt`: 检查时间
- `config`: 配置健康状态
  - `isValid`: 配置是否有效
  - `errorMessage`: 错误消息（如果无效）
- `summary`: 系统概要信息（可选）
  - `currentCongestionLevel`: 当前拥堵级别（从PR-08读取）
  - `recommendedCapacityParcelsPerMinute`: 推荐产能

---

## 自检内容

系统启动时会自动执行以下自检项目：

### 1. 驱动器自检
- 检查摆轮驱动器是否可访问
- 检查IO板读写功能
- 验证关键设备驱动器状态
- 使用"安全读"方式，不触发实际动作

### 2. 上游系统检查
- 检查RuleEngine连接状态
- 验证API通道连通性
- 执行轻量级连通性测试

### 3. 配置验证
- 验证系统配置有效性
- 检查异常格口ID配置
- 验证线体拓扑配置
- 检查路由配置（可选）

---

## 自检失败排查步骤

### 驱动器自检失败

**常见错误**:
- `DRIVER_ERROR`: 驱动器访问失败
- `CANCELLED`: 自检操作被取消

**排查步骤**:
1. 检查硬件连接是否正常
2. 验证驱动器配置是否正确
3. 检查设备是否上电
4. 查看详细日志获取更多信息

### 上游系统检查失败

**常见错误**:
- `NOT_CONNECTED`: 上游连接未建立
- `CHECK_ERROR`: 健康检查失败

**排查步骤**:
1. 检查网络连接
2. 验证RuleEngine服务是否运行
3. 检查配置中的连接信息
4. 确认防火墙规则

### 配置验证失败

**常见错误**:
- "系统配置未找到或为空"
- "异常格口ID配置无效"
- "配置验证异常"

**排查步骤**:
1. 检查配置文件是否存在
2. 验证配置格式是否正确
3. 确认必要的配置项已设置
4. 检查数据库连接（如使用LiteDB）

---

## 系统状态转换

自检结果会影响系统状态转换：

```
启动: Booting
  ↓ (自检成功)
状态: Ready
  ↓ (自检失败)
状态: Faulted
```

**状态说明**:
- `Booting`: 系统正在启动和自检
- `Ready`: 系统就绪，可以开始运行
- `Faulted`: 系统故障，需要处理
- `Running`: 系统正常运行中
- `Paused`: 系统已暂停
- `EmergencyStop`: 触发急停

---

## 与PR-08的关系

健康检查端点可以展示PR-08的拥堵/过载状态，但不依赖PR-08的具体实现：

- `/health/line`的`summary.currentCongestionLevel`字段显示当前拥堵级别
- `/health/line`的`summary.recommendedCapacityParcelsPerMinute`字段显示推荐产能
- 这些字段是可选的，如果PR-08未实现或指标不可用，字段将为null

**注意**: 健康检查只做状态展示，不做决策或控制。

---

## Prometheus指标

PR-09引入以下系统健康指标：

### 1. `system_state`
**类型**: Gauge  
**说明**: 当前系统状态  
**值**:
- 0 = Booting
- 1 = Ready
- 2 = Running
- 3 = Paused
- 4 = Faulted
- 5 = EmergencyStop

**查询示例**:
```promql
system_state
```

### 2. `system_selftest_last_success_timestamp`
**类型**: Gauge  
**说明**: 最近一次自检成功时间（Unix时间戳）  
**查询示例**:
```promql
system_selftest_last_success_timestamp
```

### 3. `system_selftest_failures_total`
**类型**: Counter  
**说明**: 自检失败总次数  
**查询示例**:
```promql
rate(system_selftest_failures_total[5m])
```

---

## 配置选项

在`appsettings.json`中可以配置健康检查功能：

```json
{
  "HealthCheck": {
    "Enabled": true
  }
}
```

**配置说明**:
- `Enabled`: 是否启用健康检查功能（默认true）
  - `true`: 启用自检和健康检查端点
  - `false`: 禁用自检，系统直接进入Ready状态（向后兼容）

---

## 使用建议

### 1. Kubernetes部署

```yaml
livenessProbe:
  httpGet:
    path: /healthz
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/line
    port: 5000
  initialDelaySeconds: 30
  periodSeconds: 30
  failureThreshold: 3
```

### 2. 监控告警

在Prometheus中设置告警规则：

```yaml
groups:
  - name: system_health
    rules:
      - alert: SystemFaulted
        expr: system_state == 4
        for: 1m
        annotations:
          summary: "系统处于故障状态"
          
      - alert: SelfTestFailing
        expr: rate(system_selftest_failures_total[5m]) > 0
        for: 5m
        annotations:
          summary: "系统自检持续失败"
```

### 3. 定期检查

建议设置定期健康检查任务：
- 每分钟检查`/healthz`
- 每5分钟检查`/health/line`
- 监控Prometheus指标变化

---

## 故障恢复

如果系统进入Faulted状态：

1. 调用`/health/line`查看详细错误信息
2. 根据错误类型进行排查（参考上面的排查步骤）
3. 修复问题后，重启系统触发新的自检
4. 验证系统状态是否恢复为Ready

---

## 开发者指南

### 添加新的驱动器自检

1. 实现`IDriverSelfTest`接口：
```csharp
public class MyDriverSelfTest : IDriverSelfTest
{
    public string DriverName => "MyDriver";
    
    public async Task<DriverHealthStatus> RunSelfTestAsync(
        CancellationToken cancellationToken = default)
    {
        // 实现自检逻辑
        return new DriverHealthStatus
        {
            DriverName = DriverName,
            IsHealthy = true,
            CheckedAt = DateTimeOffset.UtcNow
        };
    }
}
```

2. 在DI容器中注册：
```csharp
services.AddSingleton<IDriverSelfTest, MyDriverSelfTest>();
```

### 添加新的上游健康检查

1. 实现`IUpstreamHealthChecker`接口：
```csharp
public class MyUpstreamHealthChecker : IUpstreamHealthChecker
{
    public string EndpointName => "MyUpstream";
    
    public async Task<UpstreamHealthStatus> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        // 实现健康检查逻辑
        return new UpstreamHealthStatus
        {
            EndpointName = EndpointName,
            IsHealthy = true,
            CheckedAt = DateTimeOffset.UtcNow
        };
    }
}
```

2. 在DI容器中注册：
```csharp
services.AddSingleton<IUpstreamHealthChecker, MyUpstreamHealthChecker>();
```

---

## 常见问题

**Q: 自检失败会影响系统启动吗？**  
A: 是的，自检失败会将系统状态设为Faulted，但不会阻止进程启动。系统会记录详细的错误信息供排查。

**Q: 可以禁用自检功能吗？**  
A: 可以，在配置中设置`HealthCheck:Enabled = false`即可禁用。

**Q: 自检包含哪些内容？**  
A: 自检包括驱动器测试、上游系统连通性检查和配置验证三部分。

**Q: `/healthz`和`/health/line`有什么区别？**  
A: `/healthz`是轻量级的进程存活检查，`/health/line`返回详细的系统健康状态和自检报告。

**Q: 自检会触发硬件动作吗？**  
A: 不会，自检使用"安全读"方式，只读取状态，不触发实际动作。

---

## 参考资料

- [系统状态机设计](./PANEL_BUTTON_STATE_MACHINE_IMPLEMENTATION.md)
- [PR-08拥堵检测文档](./PR08_USAGE_GUIDE.md)
- [Prometheus指标指南](./PROMETHEUS_GUIDE.md)
- [系统配置指南](./SYSTEM_CONFIG_GUIDE.md)
