# EMC硬件资源分布式锁（雷赛专用）

## 功能概述

EMC硬件资源分布式锁是为雷赛(Leadshine)的EMC驱动硬件设计的协调机制。由于EMC驱动硬件是共享的，可以被多个项目实例连接，当某个实例执行重置操作（冷重置或热重置）时，其他使用相同EMC硬件的进程实例也会受到影响。

本功能提供了一套完整的通知机制，确保：
1. 实例在执行重置前通知其他实例
2. 其他实例接收通知并做好准备
3. 实例之间协调，避免冲突

## 核心特性

- ✅ **多通信协议支持**：支持TCP、SignalR、MQTT三种通信方式
- ✅ **发布/订阅模式**：使用事件驱动的通知机制
- ✅ **超时保护**：防止死锁，支持请求超时
- ✅ **自动响应**：自动处理某些类型的锁请求
- ✅ **灵活配置**：支持实例ID、超时时间等配置

## 工作流程

### 1. 标准重置流程

```
发起实例                      其他实例1                     其他实例2
   |                             |                             |
   |--- RequestLock ------------>|                             |
   |                             |                             |
   |                             |--- Acknowledge ------------>|
   |                             |                             |
   |<--- Acknowledge ------------|                             |
   |                             |                             |
   |<--- Ready ------------------|                             |
   |                             |                             |
   |<--- Ready -----------------------------------|             |
   |                             |                             |
   |--- 执行重置操作 -------->   |                             |
   |                             |                             |
   |--- ResetComplete ---------->|                             |
   |                             |                             |
   |                             |--- 恢复使用EMC ---------->  |
   |                             |                             |
```

### 2. 事件类型说明

| 事件类型 | 描述 | 发送方 | 接收方 |
|---------|------|--------|--------|
| **RequestLock** | 请求锁（准备执行重置） | 发起实例 | 其他所有实例 |
| **Acknowledge** | 确认收到通知 | 其他实例 | 发起实例 |
| **Ready** | 已停止使用EMC，可以执行重置 | 其他实例 | 发起实例 |
| **ColdReset** | 冷重置通知（硬件重启） | 发起实例 | 其他所有实例 |
| **HotReset** | 热重置通知（软件重置） | 发起实例 | 其他所有实例 |
| **ReleaseLock** | 释放锁 | 发起实例 | 其他所有实例 |
| **ResetComplete** | 重置完成，可以恢复使用 | 发起实例 | 其他所有实例 |

## 配置说明

### appsettings.json 配置示例

```json
{
  "EmcLock": {
    "Enabled": true,
    "InstanceId": "WDS_Instance_01",
    "CommunicationMode": 1,
    "TcpServer": "192.168.1.100:9000",
    "SignalRHubUrl": "http://192.168.1.100:5001/emclock",
    "MqttBroker": "192.168.1.100",
    "MqttPort": 1883,
    "MqttTopicPrefix": "emc/lock",
    "DefaultTimeoutMs": 5000,
    "HeartbeatIntervalMs": 3000,
    "AutoReconnect": true,
    "ReconnectIntervalMs": 5000
  }
}
```

### 配置项说明

| 配置项 | 类型 | 必填 | 默认值 | 说明 |
|-------|------|------|--------|------|
| `Enabled` | bool | 是 | false | 是否启用EMC分布式锁 |
| `InstanceId` | string | 否 | 自动生成 | 实例唯一标识符 |
| `CommunicationMode` | enum | 是 | Tcp | 通信方式：0=Http, 1=Tcp, 2=SignalR, 3=Mqtt |
| `TcpServer` | string | TCP模式必填 | localhost:9000 | TCP服务器地址（host:port） |
| `SignalRHubUrl` | string | SignalR模式必填 | - | SignalR Hub URL |
| `MqttBroker` | string | MQTT模式必填 | localhost | MQTT Broker地址 |
| `MqttPort` | int | MQTT模式必填 | 1883 | MQTT端口 |
| `MqttTopicPrefix` | string | 否 | emc/lock | MQTT主题前缀 |
| `DefaultTimeoutMs` | int | 否 | 5000 | 默认超时时间（毫秒） |
| `HeartbeatIntervalMs` | int | 否 | 3000 | 心跳间隔（毫秒） |
| `AutoReconnect` | bool | 否 | true | 是否自动重连 |
| `ReconnectIntervalMs` | int | 否 | 5000 | 重连间隔（毫秒） |

## 使用示例

### 1. 注册服务

```csharp
// Program.cs 或 Startup.cs
services.AddEmcResourceLock(configuration);
```

### 2. 注入并使用

```csharp
public class LeadshineDiverterService
{
    private readonly IEmcResourceLockManager _lockManager;
    private readonly ILogger<LeadshineDiverterService> _logger;

    public LeadshineDiverterService(
        IEmcResourceLockManager lockManager,
        ILogger<LeadshineDiverterService> logger)
    {
        _lockManager = lockManager;
        _logger = logger;

        // 订阅EMC锁事件
        _lockManager.EmcLockEventReceived += OnEmcLockEventReceived;
    }

    public async Task InitializeAsync()
    {
        // 连接到锁服务
        var connected = await _lockManager.ConnectAsync();
        if (!connected)
        {
            _logger.LogError("连接到EMC锁服务失败");
        }
    }

    public async Task ResetEmcAsync(ushort cardNo)
    {
        // 1. 请求锁
        var lockAcquired = await _lockManager.RequestLockAsync(cardNo, timeoutMs: 10000);
        if (!lockAcquired)
        {
            _logger.LogWarning("获取EMC锁失败，其他实例可能未准备好");
            return;
        }

        try
        {
            // 2. 发送冷重置通知
            await _lockManager.NotifyColdResetAsync(cardNo);

            // 3. 执行实际的重置操作
            _logger.LogInformation("开始执行EMC冷重置，卡号: {CardNo}", cardNo);
            // ... 执行重置逻辑 ...
            await Task.Delay(2000); // 模拟重置时间

            // 4. 通知重置完成
            await _lockManager.NotifyResetCompleteAsync(cardNo);
            _logger.LogInformation("EMC重置完成，卡号: {CardNo}", cardNo);
        }
        finally
        {
            // 5. 释放锁
            await _lockManager.ReleaseLockAsync(cardNo);
        }
    }

    private async void OnEmcLockEventReceived(object? sender, EmcLockEventArgs e)
    {
        var lockEvent = e.LockEvent;
        _logger.LogInformation(
            "收到EMC锁事件: {Type}, 来自实例: {InstanceId}, 卡号: {CardNo}",
            lockEvent.NotificationType,
            lockEvent.InstanceId,
            lockEvent.CardNo);

        switch (lockEvent.NotificationType)
        {
            case EmcLockNotificationType.ColdReset:
            case EmcLockNotificationType.HotReset:
                // 停止使用EMC硬件
                await StopUsingEmcAsync(lockEvent.CardNo);

                // 发送就绪消息
                await _lockManager.SendReadyAsync(lockEvent.EventId, lockEvent.CardNo);
                break;

            case EmcLockNotificationType.ResetComplete:
                // 重新初始化并恢复使用
                await ReInitializeEmcAsync(lockEvent.CardNo);
                break;
        }
    }

    private async Task StopUsingEmcAsync(ushort cardNo)
    {
        _logger.LogInformation("暂停使用EMC，卡号: {CardNo}", cardNo);
        // ... 停止使用EMC的逻辑 ...
        await Task.CompletedTask;
    }

    private async Task ReInitializeEmcAsync(ushort cardNo)
    {
        _logger.LogInformation("重新初始化EMC，卡号: {CardNo}", cardNo);
        // ... 重新初始化EMC的逻辑 ...
        await Task.CompletedTask;
    }
}
```

## 通信协议详细说明

### TCP协议

- **优点**：性能高，延迟低，适合局域网
- **缺点**：需要自己实现服务器端
- **适用场景**：高性能要求，实例数量较少

**消息格式**：JSON文本，以换行符`\n`分隔

```json
{
  "EventId": "uuid",
  "InstanceId": "WDS_Instance_01",
  "NotificationType": 2,
  "CardNo": 0,
  "Timestamp": "2025-11-14T10:00:00Z",
  "Message": "冷重置即将执行",
  "TimeoutMs": 5000
}
```

### SignalR协议

- **优点**：易于使用，支持自动重连，适合ASP.NET应用
- **缺点**：需要Web服务器
- **适用场景**：与Web应用集成，需要双向实时通信

**Hub方法**：
- `RegisterInstance(string instanceId)` - 注册实例
- `UnregisterInstance(string instanceId)` - 取消注册
- `SendEmcLockEvent(EmcLockEvent lockEvent)` - 发送锁事件
- `ReceiveEmcLockEvent(EmcLockEvent lockEvent)` - 接收锁事件（客户端方法）

### MQTT协议

- **优点**：标准协议，支持QoS，易于扩展
- **缺点**：需要MQTT Broker
- **适用场景**：多实例，跨网络，物联网环境

**主题结构**：
- `emc/lock/card{CardNo}/{NotificationType}` - 锁事件主题
- 例如：`emc/lock/card0/ColdReset`

**QoS级别**：AtLeastOnce (QoS 1)

## 故障排查

### 1. 连接失败

**现象**：无法连接到锁服务

**可能原因**：
- 配置的服务器地址不正确
- 服务器未启动
- 网络不通

**解决方法**：
- 检查配置文件中的服务器地址
- 确认服务器已启动并监听正确端口
- 使用ping或telnet测试网络连通性

### 2. 超时

**现象**：请求锁或发送通知超时

**可能原因**：
- 其他实例未运行或未连接
- 其他实例响应慢
- 网络延迟高

**解决方法**：
- 确认所有实例都已连接到锁服务
- 增加超时时间
- 检查网络延迟

### 3. 重置后实例未恢复

**现象**：重置完成后，实例无法恢复使用EMC

**可能原因**：
- 未收到ResetComplete通知
- 重新初始化失败

**解决方法**：
- 检查日志，确认是否收到通知
- 检查重新初始化逻辑
- 手动重启实例

## 最佳实践

1. **唯一实例ID**：为每个实例配置唯一的InstanceId，便于识别和调试
2. **合理超时**：根据实际情况设置超时时间，避免过短或过长
3. **日志记录**：充分记录锁事件和操作，便于故障排查
4. **错误处理**：妥善处理连接失败、超时等异常情况
5. **优雅退出**：实例退出时断开锁服务连接
6. **测试验证**：在测试环境充分验证多实例协调逻辑

## 相关文档

- [通信层集成文档](COMMUNICATION_INTEGRATION.md)
- [硬件驱动文档](ZakYip.WheelDiverterSorter.Drivers/README.md)
- [系统配置管理指南](SYSTEM_CONFIG_GUIDE.md)

## 技术支持

如有问题，请提交Issue或联系技术支持团队。
