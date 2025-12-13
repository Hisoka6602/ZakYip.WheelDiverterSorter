# 上游通知故障排查指南

## 问题现象

**症状**：创建包裹时没有向上游发送信息，但调用 `POST /api/communication/test-parcel` 能正常发送。

## 根本原因分析

### 1. 包裹创建的两种方式

系统中创建包裹有两种方式：

#### 方式一：正常生产流程（传感器触发）

```
传感器硬件触发
    ↓
ParcelDetectionService 轮询检测
    ↓
触发 ParcelDetected 事件
    ↓
SortingOrchestrator.OnParcelDetected() 事件处理器
    ↓
ProcessParcelAsync() - 创建包裹
    ↓
SendUpstreamNotificationAsync() - 发送上游通知
    ↓
_upstreamClient.SendAsync(new ParcelDetectedMessage { ... })
```

**关键前提条件**：
- ✅ 系统必须处于 **Running** 状态
- ✅ `SortingServicesInitHostedService` 必须已启动
- ✅ `ISortingOrchestrator.StartAsync()` 必须已调用
- ✅ `ISensorEventProvider.StartAsync()` 必须已启动传感器轮询
- ✅ 传感器配置正确且硬件连接正常

#### 方式二：测试端点（直接调用）

```
POST /api/communication/test-parcel
    ↓
CommunicationController.SendTestParcel()
    ↓
[Client模式] _upstreamClient.SendAsync(...)
或
[Server模式] server.BroadcastChuteAssignmentAsync(...)
```

**关键前提条件**：
- ✅ 系统必须处于 **Ready** 或 **Faulted** 状态（不能是 Running）
- ✅ 上游连接已建立（Client模式）或服务器已启动（Server模式）

### 2. 为什么 test-parcel 能发但正常流程不能发？

**原因**：`test-parcel` 端点**绕过了传感器事件流程**，直接调用底层通信接口。

| 特征 | 正常流程 | test-parcel 端点 |
|------|---------|------------------|
| **触发方式** | 传感器硬件触发事件 | HTTP API 直接调用 |
| **需要系统状态** | Running | Ready/Faulted |
| **需要传感器服务** | ✅ 是 | ❌ 否 |
| **需要事件订阅** | ✅ 是 | ❌ 否 |
| **创建包裹实体** | ✅ 是 | ❌ 否（仅测试通信） |
| **完整分拣流程** | ✅ 是 | ❌ 否 |

## 故障排查步骤

### 步骤 1: 检查系统状态

```bash
GET /api/system/status
```

**期望结果**：
```json
{
  "currentState": "Running",  // 必须是 Running
  "isStarted": true
}
```

**如果不是 Running 状态**：
- 传感器事件不会被处理
- `OnParcelDetected` 不会被调用
- 包裹不会被创建，也不会发送上游通知

**解决方法**：通过面板或 API 启动系统到 Running 状态。

### 步骤 2: 检查传感器服务是否启动

查看日志中是否有以下启动消息：

```
[信息] 正在启动传感器事件监听...
[信息] 传感器事件监听已启动
[信息] 分拣编排服务已启动
```

**如果没有这些日志**：
- `SortingServicesInitHostedService` 可能未注册
- 启动过程可能失败
- 检查应用启动日志和异常

### 步骤 3: 检查传感器配置

```bash
GET /api/hardware/sensors
```

**检查要点**：
- 是否有配置 `ParcelCreation` 类型的传感器
- 传感器 ID 是否正确
- IO 地址是否正确
- 轮询间隔是否合理

### 步骤 4: 检查上游连接状态

```bash
GET /api/communication/status
```

**期望结果**：
```json
{
  "isConnected": true,
  "mode": "Tcp",  // 或 SignalR/MQTT
  "connectionMode": "Client",  // 或 Server
  "errorMessage": null
}
```

**如果未连接**：
- Client模式：检查上游服务器地址和端口
- Server模式：检查是否有客户端连接
- 检查网络连接
- 查看通信日志

### 步骤 5: 启用详细日志

在 `appsettings.json` 中设置：

```json
{
  "Logging": {
    "LogLevel": {
      "ZakYip.WheelDiverterSorter.Execution.Orchestration": "Debug",
      "ZakYip.WheelDiverterSorter.Ingress": "Debug",
      "ZakYip.WheelDiverterSorter.Communication": "Debug"
    }
  }
}
```

**重点关注的日志消息**：

1. **传感器触发日志**：
   ```
   [调试] 收到 ParcelDetected 事件: ParcelId=xxx, SensorId=xxx
   ```

2. **包裹创建日志**：
   ```
   [信息] [ParcelCreation触发] 检测到包裹创建传感器触发
   [跟踪] [PR-42 Parcel-First] 本地创建包裹: ParcelId=xxx
   ```

3. **上游通知日志**：
   ```
   [信息] [PR-42 Parcel-First] 发送上游包裹检测通知: ParcelId=xxx
   [信息] 包裹 xxx 已成功发送检测通知到上游系统
   ```

### 步骤 6: 验证事件订阅

在代码中确认：

```csharp
// SortingOrchestrator 构造函数
_sensorEventProvider.ParcelDetected += OnParcelDetected;  // 第182行
```

这个订阅在构造函数中完成，如果 `SortingOrchestrator` 正确注册到 DI 容器，事件订阅应该正常工作。

### 步骤 7: 手动触发传感器测试

如果使用模拟传感器，可以通过 API 手动触发：

```bash
# 具体端点取决于实现，检查 Host/Controllers 中是否有传感器测试端点
POST /api/sensors/trigger
{
  "sensorId": 1
}
```

## 常见问题和解决方案

### 问题 1: 系统未启动到 Running 状态

**现象**：
- test-parcel 能发送
- 传感器触发但没有包裹创建
- 日志中没有 "ParcelDetected 事件" 消息

**原因**：`OnParcelDetected` 事件处理器中检查系统状态，非 Running 状态会拒绝处理。

**解决**：
1. 检查 `SystemStateManager.CurrentState`
2. 通过面板或 API 启动系统
3. 确认系统状态转换到 Running

### 问题 2: 传感器配置错误

**现象**：
- 系统已 Running
- 硬件触发但没有事件
- 日志中没有传感器触发消息

**原因**：传感器类型配置错误或 IO 地址错误。

**解决**：
1. 检查传感器配置中的 `IoType` 字段
2. 确认是 `ParcelCreation` 类型
3. 验证 IO 地址映射正确

### 问题 3: 上游连接未建立

**现象**：
- 包裹创建成功
- 有 "发送上游包裹检测通知" 日志
- 但显示 "无法发送检测通知到上游系统"

**原因**：上游连接未建立或已断开。

**解决**：
1. 检查上游服务器是否运行
2. 检查网络连接
3. 查看通信配置（地址、端口、协议）
4. 检查 `_upstreamClient.IsConnected` 状态

### 问题 4: 事件订阅未生效

**现象**：
- 传感器触发（硬件层面确认）
- 但 `OnParcelDetected` 从未调用

**原因**：事件订阅链路中断。

**解决**：
1. 确认 `SortingOrchestrator` 已注册到 DI
2. 确认 `SortingServicesInitHostedService` 已启动
3. 检查是否有异常导致订阅失败
4. 验证 `ISensorEventProvider` 实现正确

## 验证清单

使用此清单确认系统配置正确：

- [ ] 系统已启动到 **Running** 状态
- [ ] `SortingServicesInitHostedService` 已在日志中显示启动
- [ ] 传感器服务已启动（日志确认）
- [ ] 至少有一个 `ParcelCreation` 类型的传感器配置
- [ ] 传感器 IO 地址配置正确
- [ ] 上游连接状态为 `IsConnected: true`
- [ ] 日志级别设置为 Debug（用于故障排查）
- [ ] 硬件驱动正常工作（Simulated 或实际硬件）

## 与 test-parcel 端点的对比

| 项目 | 正常流程需要 | test-parcel 需要 |
|------|------------|-----------------|
| 系统状态 | Running | Ready/Faulted |
| 传感器服务 | ✅ | ❌ |
| 事件订阅 | ✅ | ❌ |
| 包裹实体 | ✅ 创建 | ❌ 不创建 |
| 上游连接 | ✅ | ✅ |
| 完整流程 | ✅ | ❌（仅测试通信） |

**结论**：`test-parcel` 是一个**通信测试工具**，它绕过了完整的分拣流程，只测试底层通信能力。如果 test-parcel 能发但正常流程不能发，说明**问题在分拣流程的上层逻辑**（系统状态、传感器服务、事件订阅），而不是通信层本身。

## 推荐的调试流程

1. **确认系统状态** → `GET /api/system/status`
2. **确认传感器配置** → `GET /api/hardware/sensors`
3. **确认上游连接** → `GET /api/communication/status`
4. **启用详细日志** → 修改 `appsettings.json`
5. **启动系统** → 通过面板或 API
6. **触发传感器** → 物理触发或模拟触发
7. **查看日志** → 追踪完整流程
8. **验证上游收到** → 检查上游系统日志

## 相关文档

- [系统配置指南](./guides/SYSTEM_CONFIG_GUIDE.md)
- [上游连接配置](./guides/UPSTREAM_CONNECTION_GUIDE.md)
- [传感器IO轮询配置](./guides/SENSOR_IO_POLLING_CONFIGURATION.md)
- [架构原则](./ARCHITECTURE_PRINCIPLES.md)

---

**文档版本**: 1.0  
**创建时间**: 2025-12-13  
**维护者**: ZakYip Development Team
