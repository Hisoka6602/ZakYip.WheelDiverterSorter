# 包裹创建时上游通知未发送的详细诊断

## 问题现象

**症状**：
- ✅ 系统能正常分拣包裹（说明系统在 Running 状态）
- ❌ 包裹创建时没有向上游发送检测通知
- ✅ 调用 `POST /api/communication/test-parcel` 能正常发送

## 诊断策略

由于包裹能分拣，说明系统状态正常，问题出在**上游通知发送逻辑的某个环节**。

## 诊断步骤

### 步骤 1: 启用详细日志（必须）

在 `appsettings.json` 中启用以下日志：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ZakYip.WheelDiverterSorter.Execution.Orchestration": "Debug",
      "ZakYip.WheelDiverterSorter.Communication": "Information",
      "ZakYip.WheelDiverterSorter.Ingress": "Debug"
    }
  }
}
```

**重启服务使日志配置生效**。

### 步骤 2: 触发包裹并观察日志

触发一个包裹（通过传感器或模拟），然后在日志中按顺序查找以下关键日志：

#### 日志检查点 1: 包裹检测事件

```
[调试] [事件处理] 收到 ParcelDetected 事件: ParcelId=xxx, SensorId=xxx
```

- ✅ **如果有**：传感器事件正常触发
- ❌ **如果没有**：问题在传感器层，检查传感器配置和 IO 轮询

#### 日志检查点 2: 包裹创建触发

```
[信息] [ParcelCreation触发] 检测到包裹创建传感器触发，开始处理包裹: ParcelId=xxx, SensorId=xxx
```

- ✅ **如果有**：进入了 `ProcessParcelAsync` 流程
- ❌ **如果没有**：可能是传感器类型配置错误（检查是否为 WheelFront 而非 ParcelCreation）

#### 日志检查点 3: 包裹实体创建

```
[跟踪] [PR-42 Parcel-First] 本地创建包裹: ParcelId=xxx, CreatedAt=xxx
```

- ✅ **如果有**：包裹实体已创建
- ❌ **如果没有**：在状态验证阶段被拒绝，检查系统状态

#### 日志检查点 4: 上游通知发送

这是**最关键的检查点**：

```
[信息] [PR-42 Parcel-First] 发送上游包裹检测通知: ParcelId=xxx, SentAt=xxx, ClientType=xxx, ClientFullName=xxx, IsConnected=xxx
```

**场景分析**：

##### 场景 A: 完全没有这条日志

**可能原因**：
1. 在 `SendUpstreamNotificationAsync` 之前就返回了
2. 在 `DetermineTargetChuteAsync` 调用 `SendUpstreamNotificationAsync` 之前抛异常

**检查方法**：
- 查看日志中是否有异常堆栈
- 检查 `_createdParcels.ContainsKey(parcelId)` 是否返回 false
- 查找日志：`[PR-42 Invariant Violation] 尝试为不存在的包裹`

**修复方法**：
- 如果有 Invariant Violation 日志，说明包裹创建和通知之间有时序问题
- 检查代码中是否有异步并发问题

##### 场景 B: 有发送日志，但没有成功/失败的后续日志

**可能原因**：
- `_upstreamClient.SendAsync()` 调用阻塞或超时
- 底层通信库异常未被捕获

**检查方法**：
- 查看是否有后续日志：
  ```
  [信息] 包裹 xxx 已成功发送检测通知到上游系统
  ```
  或
  ```
  [错误] 包裹 xxx 无法发送检测通知到上游系统
  ```

**修复方法**：
- 检查上游连接状态
- 检查网络连接
- 增加超时日志

##### 场景 C: 显示发送成功，但上游未收到

```
[信息] 包裹 xxx 已成功发送检测通知到上游系统 (ClientType=xxx, IsConnected=True)
```

**可能原因**：
1. **Server 模式**：没有客户端连接，广播到空列表
2. **Client 模式**：发送到错误的地址或上游未正确接收
3. 消息格式不匹配

**检查方法（Server 模式）**：

查找以下日志：

```
[信息] [服务端模式-适配器] 转换NotifyParcelDetectedAsync为BroadcastParcelDetectedAsync: ParcelId=xxx, ServerIsRunning=True, ConnectedClientsCount=0
```

**关键字段**：`ConnectedClientsCount`

- **如果为 0**：没有上游客户端连接，消息无处发送 ⬅️ **这是最可能的原因**
- **如果 > 0**：检查广播日志

**检查广播日志**：

```
[信息] [服务端模式-广播-成功] 已向客户端 xxx 广播包裹检测通知: ParcelId=xxx
```

或错误日志：

```
[警告] [服务端模式-广播失败] 向客户端 xxx 广播包裹检测通知失败
```

### 步骤 3: 检查连接模式和客户端数量

```bash
GET /api/communication/status
```

**重点检查**：

```json
{
  "mode": "Tcp",
  "connectionMode": "Server",  // 确认是 Server 还是 Client
  "isConnected": true,
  "connectedClients": [        // Server 模式特有
    {
      "clientId": "...",
      "clientAddress": "192.168.1.100:12345",
      "connectedAt": "...",
      "connectionDurationSeconds": 300
    }
  ]
}
```

**分析**：

| 连接模式 | 期望状态 | 如果不符合期望 |
|---------|---------|---------------|
| **Client** | `isConnected: true` | 上游服务器未启动或网络不通 |
| **Server** | `connectedClients` 列表非空 | 上游客户端未连接到本服务器 |

### 步骤 4: 确认客户端类型

在发送日志中查看 `ClientType` 和 `ClientFullName`：

```
ClientType=ServerModeClientAdapter
ClientFullName=ZakYip.WheelDiverterSorter.Communication.Adapters.ServerModeClientAdapter
```

**如果是 `ServerModeClientAdapter`**（Server 模式）：
- 必须有上游客户端连接才能发送
- 检查上游系统是否配置为 Client 模式并连接到本服务器

**如果是其他类型**（Client 模式）：
- `TcpRuleEngineClient` / `TouchSocketTcpRuleEngineClient`
- `SignalRRuleEngineClient`
- `MqttRuleEngineClient`

检查 `IsConnected` 状态。

## 常见问题和解决方案

### 问题 1: Server 模式下没有客户端连接

**现象**：
```
[信息] [服务端模式-适配器] ... ConnectedClientsCount=0
```

**原因**：
- 上游系统未配置为 Client 模式
- 上游系统配置的服务器地址不正确
- 网络防火墙阻止连接
- 上游系统未启动

**解决方法**：

1. **确认上游系统配置**：
   - 上游必须配置为 **Client 模式**
   - 上游的 `TcpServer` 地址必须指向本系统的 IP 和端口
   - 例如：`"TcpServer": "192.168.1.50:8000"`

2. **检查本系统服务器配置**：
   ```bash
   GET /api/communication/config/persisted
   ```
   
   确认：
   ```json
   {
     "mode": "Tcp",
     "connectionMode": "Server",  // 本系统是 Server
     "tcp": {
       "tcpServer": "0.0.0.0:8000"  // 监听端口
     }
   }
   ```

3. **测试网络连接**：
   ```bash
   # 从上游系统测试连接
   telnet 192.168.1.50 8000
   
   # 或
   nc -zv 192.168.1.50 8000
   ```

4. **查看服务器启动日志**：
   ```
   [信息] [服务端模式] TCP服务器已启动，监听 0.0.0.0:8000
   ```

5. **监控客户端连接事件**：
   ```
   [信息] [服务端模式-客户端连接] 客户端已连接: xxx from 192.168.1.100:54321
   ```

### 问题 2: Client 模式下连接失败

**现象**：
```
[错误] 包裹 xxx 无法发送检测通知到上游系统。连接失败或上游不可用。ClientType=TcpRuleEngineClient
```

**原因**：
- 上游服务器地址配置错误
- 上游服务器未启动
- 网络不通

**解决方法**：

1. **检查配置**：
   ```bash
   GET /api/communication/config/persisted
   ```
   
   确认：
   ```json
   {
     "mode": "Tcp",
     "connectionMode": "Client",  // 本系统是 Client
     "tcp": {
       "tcpServer": "192.168.1.200:9000"  // 上游服务器地址
     }
   }
   ```

2. **测试连接**：
   ```bash
   GET /api/communication/test
   ```

3. **查看连接日志**：
   ```
   [警告] 连接失败，2000ms 后重试: ...
   ```

### 问题 3: 包裹创建了但通知未发送

**现象**：
- 有 `[PR-42 Parcel-First] 本地创建包裹` 日志
- 没有 `[PR-42 Parcel-First] 发送上游包裹检测通知` 日志

**可能原因**：
1. 在 `DetermineTargetChuteAsync` 调用之前就返回了
2. 在 `SendUpstreamNotificationAsync` 内部第一个检查失败

**检查日志**：
```
[错误] [PR-42 Invariant Violation] 尝试为不存在的包裹 xxx 发送上游通知
```

**如果有此日志**：
- 说明包裹在 `_createdParcels` 字典中不存在
- 可能的时序问题或并发问题
- 需要检查代码逻辑

### 问题 4: 消息格式不匹配

**现象**：
- 发送成功
- 上游收到消息但无法解析

**检查消息格式**：

```csharp
// ParcelDetectedMessage 格式
{
  "ParcelId": 1234567890,
  "DetectedAt": "2025-12-13T10:00:00+08:00",
  "MessageType": "ParcelDetected"
}
```

**确认上游期望格式**：
- 字段名称是否匹配
- 时间格式是否匹配
- 是否需要额外字段

## 快速诊断脚本

```bash
#!/bin/bash

echo "=== 1. 检查系统状态 ==="
curl -s http://localhost:5000/api/system/status | jq '{currentState, isStarted}'

echo ""
echo "=== 2. 检查通信配置 ==="
curl -s http://localhost:5000/api/communication/config/persisted | jq '{mode, connectionMode, tcpServer: .tcp.tcpServer}'

echo ""
echo "=== 3. 检查连接状态 ==="
curl -s http://localhost:5000/api/communication/status | jq '{isConnected, mode, connectionMode, connectedClients: (.connectedClients // [] | length)}'

echo ""
echo "=== 4. 测试通信连接 ==="
curl -s -X POST http://localhost:5000/api/communication/test | jq '{success, message, responseTimeMs}'

echo ""
echo "=== 5. 发送测试包裹 ==="
curl -s -X POST http://localhost:5000/api/communication/test-parcel \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "TEST-99999"}' | jq '{success, message}'
```

## 日志追踪完整示例

### 正常流程（应该看到的日志）

```
[调试] [事件处理] 收到 ParcelDetected 事件: ParcelId=123456, SensorId=1
[信息] [ParcelCreation触发] 检测到包裹创建传感器触发，开始处理包裹: ParcelId=123456, SensorId=1
[开始] 开始处理包裹: ParcelId=123456, SensorId=1
[调试] [步骤 1/5] 创建本地包裹实体
[跟踪] [PR-42 Parcel-First] 本地创建包裹: ParcelId=123456, CreatedAt=2025-12-13T10:00:00+08:00
[调试] [步骤 2/5] 验证系统状态
[调试] [步骤 3/5] 拥堵检测与超载评估
[调试] [步骤 4/5] 确定目标格口
[信息] [PR-42 Parcel-First] 发送上游包裹检测通知: ParcelId=123456, SentAt=..., ClientType=ServerModeClientAdapter, IsConnected=True
[信息] [服务端模式-适配器] 转换NotifyParcelDetectedAsync为BroadcastParcelDetectedAsync: ParcelId=123456, ServerIsRunning=True, ConnectedClientsCount=2
[信息] [服务端模式-广播-成功] 已向客户端 client-001 广播包裹检测通知: ParcelId=123456
[信息] [服务端模式-广播-成功] 已向客户端 client-002 广播包裹检测通知: ParcelId=123456
[信息] [服务端模式-广播-完成] BroadcastParcelDetectedAsync: ParcelId=123456, SuccessCount=2
[信息] [服务端模式-适配器] BroadcastParcelDetectedAsync调用完成: ParcelId=123456
[信息] 包裹 123456 已成功发送检测通知到上游系统 (ClientType=ServerModeClientAdapter, IsConnected=True)
[信息] [目标格口确定] 包裹 123456 的目标格口: 5
[调试] [步骤 5/5] 生成队列任务并入队
[信息] [入队完成] 包裹 123456 的 3 个任务已加入队列，目标格口=5
```

### 异常流程（Server模式无客户端连接）

```
[调试] [事件处理] 收到 ParcelDetected 事件: ParcelId=123456, SensorId=1
[信息] [ParcelCreation触发] 检测到包裹创建传感器触发，开始处理包裹: ParcelId=123456, SensorId=1
[开始] 开始处理包裹: ParcelId=123456, SensorId=1
[调试] [步骤 1/5] 创建本地包裹实体
[跟踪] [PR-42 Parcel-First] 本地创建包裹: ParcelId=123456
[调试] [步骤 2/5] 验证系统状态
[调试] [步骤 3/5] 拥堵检测与超载评估
[调试] [步骤 4/5] 确定目标格口
[信息] [PR-42 Parcel-First] 发送上游包裹检测通知: ParcelId=123456, ClientType=ServerModeClientAdapter, IsConnected=True
[信息] [服务端模式-适配器] 转换NotifyParcelDetectedAsync为BroadcastParcelDetectedAsync: ParcelId=123456, ServerIsRunning=True, ConnectedClientsCount=0  ⬅️ 关键：无客户端
[信息] [服务端模式-广播-完成] BroadcastParcelDetectedAsync: ParcelId=123456, SuccessCount=0, FailedCount=0, RemainingClientsCount=0  ⬅️ 广播到0个客户端
[信息] [服务端模式-适配器] BroadcastParcelDetectedAsync调用完成: ParcelId=123456
[信息] 包裹 123456 已成功发送检测通知到上游系统  ⬅️ 技术上"成功"，但实际无人接收
[信息] [目标格口确定] 包裹 123456 的目标格口: 5
```

## 总结

**最可能的原因**（根据您的描述）：

1. ✅ 系统在 Running 状态（包裹能分拣）
2. ✅ 代码逻辑正确（调用了 `SendUpstreamNotificationAsync`）
3. ✅ 通信层工作正常（test-parcel 能发）
4. ❌ **Server 模式下没有上游客户端连接** ⬅️ 最可能

**验证方法**：
```bash
curl http://localhost:5000/api/communication/status | jq '.connectedClients'
```

**如果返回 `null` 或空数组** `[]`，说明没有客户端连接，需要：
1. 配置上游系统为 Client 模式
2. 上游系统的 TcpServer 地址指向本系统
3. 确保网络连通

**如果返回客户端列表**，则继续检查广播日志中的成功/失败计数。

---

**文档版本**: 1.0  
**创建时间**: 2025-12-13  
**维护者**: ZakYip Development Team
