# TCP 消息接收机制说明

## 概述

本文档说明 TCP 客户端如何从上游 RuleEngine 接收格口分配通知。

## 架构设计

### 消息流程

```
┌─────────────┐         ┌──────────────┐         ┌──────────────┐
│ 本地系统     │         │   TCP        │         │ 上游         │
│ (WDS)       │         │   连接       │         │ RuleEngine   │
└──────┬──────┘         └──────┬───────┘         └──────┬───────┘
       │                       │                        │
       │  1. 创建包裹           │                        │
       ├──────────────────────►│                        │
       │  ParcelId: 123456789  │                        │
       │                       │                        │
       │  2. 发送包裹检测通知    │                        │
       │  NotifyParcelDetected │  3. 转发通知           │
       ├──────────────────────►├───────────────────────►│
       │  {ParcelId: 123456789}│  {ParcelId: 123456789} │
       │                       │                        │
       │                       │  4. 上游决定格口        │
       │                       │  (根据规则引擎)         │
       │                       │                        │
       │                       │  5. 推送格口分配        │
       │  6. 接收格口分配       │◄───────────────────────┤
       │◄──────────────────────┤  {ParcelId: 123456789, │
       │  触发 ChuteAssignment │   ChuteId: 5}          │
       │  Received 事件        │                        │
       │                       │                        │
       │  7. 执行分拣           │                        │
       │  导向格口 5            │                        │
       │                       │                        │
```

## 实现细节

### 1. 后台接收循环

TcpRuleEngineClient 在连接建立后，启动一个后台任务持续监听来自服务器的消息：

```csharp
private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested && IsConnected)
    {
        // 读取数据
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
        
        // 处理消息
        ProcessMessagesInBuffer(messageBuffer);
    }
}
```

### 2. 消息格式支持

支持两种消息格式：

#### 格式 1: 换行符分隔
```json
{"ParcelId":123456789,"ChuteId":5,"NotificationTime":"2024-01-01T10:00:00Z"}
{"ParcelId":123456790,"ChuteId":3,"NotificationTime":"2024-01-01T10:00:01Z"}
```

#### 格式 2: 连续 JSON 对象
```json
{"ParcelId":123456789,"ChuteId":5,"NotificationTime":"2024-01-01T10:00:00Z"}{"ParcelId":123456790,"ChuteId":3,"NotificationTime":"2024-01-01T10:00:01Z"}
```

### 3. JSON 消息提取

使用智能解析器从缓冲区中提取完整的 JSON 对象：

```csharp
private static List<string> ExtractJsonMessages(string text)
{
    // 遍历文本，跟踪花括号平衡
    // 处理字符串转义
    // 提取完整的 JSON 对象
}
```

**关键特性**：
- ✅ 正确处理嵌套的花括号
- ✅ 跳过字符串内的特殊字符
- ✅ 处理转义序列 (`\"`, `\\`)
- ✅ 支持多条消息在同一缓冲区

### 4. 事件触发

解析成功后触发 `ChuteAssignmentReceived` 事件：

```csharp
private void ProcessReceivedMessage(string messageJson)
{
    var notification = JsonSerializer.Deserialize<ChuteAssignmentNotificationEventArgs>(messageJson);
    
    if (notification != null)
    {
        // 触发事件
        OnChuteAssignmentReceived(notification);
    }
}
```

## 协议对比

| 协议 | 接收机制 | 消息格式 | 优点 | 缺点 |
|------|---------|---------|------|------|
| **TCP** | 后台循环 | JSON (换行或连续) | 低延迟、高吞吐 | 需要手动解析 |
| **MQTT** | MQTT 客户端事件 | JSON | 轻量级、QoS保证 | 需要 Broker |
| **SignalR** | Hub 方法注册 | JSON | 自动重连、类型安全 | 较高开销 |

## 线程安全

### 资源保护
- 使用 `CancellationTokenSource` 控制后台任务生命周期
- 在 `Dispose` 时正确停止后台任务
- 使用超时避免无限等待

### 并发处理
- 后台任务独立运行，不阻塞主线程
- 事件触发在后台任务线程中，订阅者需注意线程安全

## 错误处理

### 连接断开
```csharp
if (bytesRead == 0)
{
    Logger.LogWarning("TCP连接已被服务器关闭");
    _isConnected = false;
    break;
}
```

### JSON 解析失败
```csharp
catch (JsonException ex)
{
    Logger.LogError(ex, "解析TCP消息时发生JSON异常: {Message}", messageJson);
}
```

### IO 异常
```csharp
catch (IOException ex)
{
    Logger.LogWarning(ex, "TCP读取数据时发生IO异常");
    _isConnected = false;
    break;
}
```

## 配置更新

当通过 `PUT /api/communication/config/persisted` 更新配置时：

1. **断开当前连接**：`UpstreamConnectionManager` 调用 `_client.DisconnectAsync()`
2. **停止接收循环**：后台任务检测到连接断开并退出
3. **使用新配置重连**：连接循环使用新配置重新建立连接
4. **启动新接收循环**：新连接建立后自动启动新的接收任务

## 性能特性

- **低延迟**: 直接 TCP 通信，无中间层
- **高吞吐**: 支持连续消息无间隔传输
- **缓冲机制**: 使用 8KB 默认缓冲区，可配置
- **智能解析**: 只在完整消息到达时才解析，避免浪费

## 测试覆盖

✅ 单条消息接收  
✅ 多条消息接收（5条）  
✅ 连接超时  
✅ 连接断开重连  
✅ 资源清理

## 使用示例

```csharp
// 创建 TCP 客户端
var client = new TcpRuleEngineClient(logger, options, systemClock);

// 订阅格口分配事件
client.ChuteAssignmentReceived += (sender, notification) =>
{
    Console.WriteLine($"收到包裹 {notification.ParcelId} 的格口分配: {notification.ChuteId}");
    // 执行分拣逻辑...
};

// 连接到服务器（自动启动接收循环）
await client.ConnectAsync();

// 发送包裹检测通知
await client.NotifyParcelDetectedAsync(parcelId);

// 等待接收格口分配（通过事件）...

// 断开连接（自动停止接收循环）
await client.DisconnectAsync();
```

## 相关文档

- [PR42_PARCEL_FIRST_SPECIFICATION.md](pr-summaries/PR42_PARCEL_FIRST_SPECIFICATION.md) - Parcel-First 流程规范
- [PR38_IMPLEMENTATION_SUMMARY.md](pr-summaries/PR38_IMPLEMENTATION_SUMMARY.md) - 连接管理实现
- [Communication/README.md](../src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/README.md) - 通信层文档

## 版本历史

- **v1.0.0** (2024-11-24): 初始实现
  - 添加后台接收循环
  - 实现 JSON 消息提取
  - 支持两种消息格式
  - 添加完整的测试覆盖
