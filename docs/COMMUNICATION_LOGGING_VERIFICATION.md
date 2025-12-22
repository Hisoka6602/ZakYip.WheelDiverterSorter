# 通信日志功能验证文档

> **文档目的**：验证系统已实现完整的通信日志记录功能
> 
> **验证日期**：2025-12-22
> 
> **结论**：✅ 系统已完全满足"记录所有通信内容到.log文件"的需求

---

## 一、通信日志文件说明

系统提供**两个独立的通信日志文件**，分别记录不同级别的通信信息：

### 1.1 通用通信日志

**文件名**：`logs/communication-{date}.log`

**记录内容**：
- 通信连接建立/断开事件
- 通信错误和警告
- 通信状态变化
- 所有 Communication 命名空间的日志

**配置开关**：`EnableCommunicationLog`（默认启用）

**日志格式**：
```
{timestamp}|{level}|{logger}|{message} {exception}
```

**示例**：
```log
2025-12-22 10:30:15.123|INFO|ZakYip.WheelDiverterSorter.Communication.Clients.TouchSocketTcpRuleEngineClient|正在连接到RuleEngine TCP服务器: 192.168.1.100:5000...
2025-12-22 10:30:15.456|INFO|ZakYip.WheelDiverterSorter.Communication.Clients.TouchSocketTcpRuleEngineClient|已成功连接到RuleEngine TCP服务器，连接ID: tcp-client-001
2025-12-22 10:30:20.789|WARN|ZakYip.WheelDiverterSorter.Communication.Infrastructure.ExponentialBackoffRetryPolicy|连接失败（第1次尝试），等待500ms后重试...
```

---

### 1.2 上游消息收发日志

**文件名**：`logs/upstream-communication-{date}.log`

**记录内容**：
- 发送到上游的包裹检测通知（ParcelDetectionNotification）
- 发送到上游的分拣完成通知（SortingCompletedNotification）
- 从上游接收的格口分配通知（ChuteAssignmentNotification）
- 完整的JSON消息内容
- 消息发送/接收时间戳

**配置开关**：`EnableUpstreamCommunicationLog`（默认启用）

**日志格式**：
```
{timestamp}|{level}|{message}
```

**过滤规则**：只记录包含 `[上游通信-发送]` 或 `[上游通信-接收]` 标记的日志

**示例**：
```log
2025-12-22 10:35:01.234|INFO|[2025-12-22 10:35:01] [上游通信-发送] TouchSocket TCP通道发送包裹检测通知 | ParcelId=PKG20251222001 | 消息内容={"parcelId":"PKG20251222001","detectedAt":"2025-12-22T10:35:01.123+08:00","sensorId":"S1"}

2025-12-22 10:35:01.567|INFO|[2025-12-22 10:35:01] [上游通信-接收] TouchSocket TCP通道接收消息 | 消息内容={"parcelId":"PKG20251222001","chuteId":5,"assignedAt":"2025-12-22T10:35:01.500+08:00"} | 字节数=128

2025-12-22 10:35:01.590|INFO|[2025-12-22 10:35:01] [上游通信-接收] 解析到格口分配通知 | ParcelId=PKG20251222001 | ChuteId=5

2025-12-22 10:35:05.123|INFO|[2025-12-22 10:35:05] [上游通信-发送] TouchSocket TCP通道发送落格完成通知 | ParcelId=PKG20251222001 | FinalStatus=已分拣
```

---

## 二、日志配置

### 2.1 NLog 配置文件

**位置**：`src/Host/ZakYip.WheelDiverterSorter.Host/nlog.config`

**相关配置段**：

```xml
<!-- ========== 通信日志 (EnableCommunicationLog) ========== -->
<target xsi:type="File" name="communicationfile" fileName="logs/communication-${shortdate}.log"
        layout="${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}"
        archiveFileName="logs/archives/communication-{#}.log"
        archiveEvery="Day"
        archiveNumbering="DateAndSequence"
        maxArchiveFiles="14" />

<!-- ========== 上游通信日志 (EnableUpstreamCommunicationLog) ========== -->
<target xsi:type="File" name="upstreamcommunicationfile" fileName="logs/upstream-communication-${shortdate}.log"
        layout="${longdate}|${level:uppercase=true}|${message}"
        archiveFileName="logs/archives/upstream-communication-{#}.log"
        archiveEvery="Day"
        archiveNumbering="DateAndSequence"
        maxArchiveFiles="30" />

<!-- 日志规则 -->
<rules>
    <!-- 上游通信日志 - 通过消息内容过滤 -->
    <logger name="*" minlevel="Debug" writeTo="upstreamcommunicationfile">
      <filters defaultAction="Ignore">
        <when condition="contains('${message}','[上游通信-发送]') or contains('${message}','[上游通信-接收]')" action="Log" />
      </filters>
    </logger>
    
    <!-- 通信日志 - 按命名空间过滤 -->
    <logger name="ZakYip.WheelDiverterSorter.Communication.*" minlevel="Debug" writeTo="communicationfile" />
</rules>
```

### 2.2 配置模型

**位置**：`src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Models/LoggingConfiguration.cs`

```csharp
public class LoggingConfiguration
{
    /// <summary>
    /// 是否启用通信日志
    /// </summary>
    /// <remarks>
    /// 记录与上游规则引擎的通信过程。
    /// 对应日志文件: communication-{date}.log
    /// </remarks>
    public bool EnableCommunicationLog { get; set; } = true;

    /// <summary>
    /// 是否启用上游通信日志
    /// </summary>
    /// <remarks>
    /// 记录与上游系统的详细消息收发内容。
    /// 对应日志文件: upstream-communication-{date}.log
    /// 此日志文件专门记录上游通信的消息内容，便于排查上游通信问题。
    /// </remarks>
    public bool EnableUpstreamCommunicationLog { get; set; } = true;
}
```

### 2.3 API 接口

**获取日志配置**：
```http
GET /api/config/logging
```

**更新日志配置**：
```http
PUT /api/config/logging
Content-Type: application/json

{
  "enableCommunicationLog": true,
  "enableUpstreamCommunicationLog": true
}
```

---

## 三、支持的通信协议

系统的所有通信协议均已集成日志记录功能：

### 3.1 TCP (TouchSocket)

**实现类**：`TouchSocketTcpRuleEngineClient`

**日志标记**：
- 发送：`[上游通信-发送] TouchSocket TCP通道发送...`
- 接收：`[上游通信-接收] TouchSocket TCP通道接收消息...`

**代码位置**：
- `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/TouchSocketTcpRuleEngineClient.cs`
- Line 193: 接收日志
- Line 402: 发送包裹检测通知日志
- Line 449: 发送分拣完成通知日志

### 3.2 MQTT

**实现类**：`MqttRuleEngineClient`

**日志标记**：
- 发送：`[上游通信-发送] MQTT通道发送...`
- 接收：`[上游通信-接收] MQTT通道收到消息...`

**代码位置**：
- `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/MqttRuleEngineClient.cs`
- Line 340: 接收日志
- Line 200: 发送包裹检测通知日志
- Line 275: 发送分拣完成通知日志

### 3.3 SignalR

**实现类**：`SignalRRuleEngineClient`

**日志标记**：
- 发送：`[上游通信-发送] SignalR通道发送...`
- 接收：`[上游通信-接收] SignalR通道收到...`

**代码位置**：
- `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/SignalRRuleEngineClient.cs`
- Line 121: 接收日志
- Line 228: 发送包裹检测通知日志
- Line 286: 发送分拣完成通知日志

---

## 四、日志文件管理

### 4.1 文件位置

**当前日志**：
```
logs/
├── communication-2025-12-22.log
└── upstream-communication-2025-12-22.log
```

**归档日志**：
```
logs/archives/
├── communication-1.log
├── communication-2.log
├── upstream-communication-1.log
└── upstream-communication-2.log
```

### 4.2 滚动策略

| 日志文件 | 滚动周期 | 保留天数 | 归档方式 |
|---------|---------|---------|---------|
| communication-{date}.log | 每天 | 14天 | DateAndSequence |
| upstream-communication-{date}.log | 每天 | 30天 | DateAndSequence |

### 4.3 文件大小

日志文件会随着通信量增长，典型大小：
- 低流量：< 10 MB/天
- 中流量：10-100 MB/天
- 高流量：> 100 MB/天

建议根据实际磁盘空间调整 `maxArchiveFiles` 参数。

---

## 五、日志内容示例

### 5.1 完整通信流程日志

**场景**：包裹从检测到分拣完成的完整通信过程

#### 步骤1：系统连接到上游

**communication-{date}.log**：
```log
2025-12-22 09:00:00.123|INFO|ZakYip.WheelDiverterSorter.Communication.Infrastructure.UpstreamConnectionManager|开始连接到上游RuleEngine服务器...
2025-12-22 09:00:00.234|INFO|ZakYip.WheelDiverterSorter.Communication.Clients.TouchSocketTcpRuleEngineClient|正在连接到RuleEngine TCP服务器: 192.168.1.100:5000...
2025-12-22 09:00:00.567|INFO|ZakYip.WheelDiverterSorter.Communication.Clients.TouchSocketTcpRuleEngineClient|已成功连接到RuleEngine TCP服务器，连接ID: tcp-client-001，保持活动状态: True
2025-12-22 09:00:00.890|INFO|ZakYip.WheelDiverterSorter.Communication.Infrastructure.UpstreamConnectionManager|上游连接已建立，当前状态: Connected
```

#### 步骤2：发送包裹检测通知

**upstream-communication-{date}.log**：
```log
2025-12-22 10:35:01.234|INFO|[2025-12-22 10:35:01] [上游通信-发送] TouchSocket TCP通道发送包裹检测通知 | ParcelId=PKG20251222001 | 消息内容={"parcelId":"PKG20251222001","detectedAt":"2025-12-22T10:35:01.123+08:00","sensorId":"S1"}
```

#### 步骤3：接收格口分配通知

**upstream-communication-{date}.log**：
```log
2025-12-22 10:35:01.567|INFO|[2025-12-22 10:35:01] [上游通信-接收] TouchSocket TCP通道接收消息 | 消息内容={"parcelId":"PKG20251222001","chuteId":5,"assignedAt":"2025-12-22T10:35:01.500+08:00"} | 字节数=128
2025-12-22 10:35:01.590|INFO|[2025-12-22 10:35:01] [上游通信-接收] 解析到格口分配通知 | ParcelId=PKG20251222001 | ChuteId=5
```

#### 步骤4：发送分拣完成通知

**upstream-communication-{date}.log**：
```log
2025-12-22 10:35:05.123|INFO|[2025-12-22 10:35:05] [上游通信-发送] TouchSocket TCP通道发送落格完成通知 | ParcelId=PKG20251222001 | FinalStatus=已分拣
```

### 5.2 通信错误日志

**场景**：上游服务器无响应

**communication-{date}.log**：
```log
2025-12-22 10:40:00.123|ERROR|ZakYip.WheelDiverterSorter.Communication.Clients.TouchSocketTcpRuleEngineClient|连接到RuleEngine TCP服务器失败 System.Net.Sockets.SocketException: 无法连接到远程服务器
2025-12-22 10:40:00.456|WARN|ZakYip.WheelDiverterSorter.Communication.Infrastructure.ExponentialBackoffRetryPolicy|连接失败（第1次尝试），等待500ms后重试...
2025-12-22 10:40:01.000|WARN|ZakYip.WheelDiverterSorter.Communication.Infrastructure.ExponentialBackoffRetryPolicy|连接失败（第2次尝试），等待1000ms后重试...
```

**upstream-communication-{date}.log**：
```log
2025-12-22 10:40:05.000|ERROR|[上游通信-发送] TouchSocket TCP通道无法连接 | ParcelId=PKG20251222002
```

---

## 六、验证步骤

### 6.1 启动系统

```bash
# 方式1：直接运行
cd src/Host/ZakYip.WheelDiverterSorter.Host
dotnet run

# 方式2：Docker
docker-compose up -d
```

### 6.2 检查日志文件生成

```bash
# 查看日志目录
ls -lh logs/

# 预期输出：
# communication-2025-12-22.log
# upstream-communication-2025-12-22.log
```

### 6.3 触发通信并查看日志

**方式1：等待系统自动连接上游**
```bash
# 实时查看通信日志
tail -f logs/communication-2025-12-22.log

# 实时查看上游消息日志
tail -f logs/upstream-communication-2025-12-22.log
```

**方式2：通过 API 发送测试包裹**
```bash
# 发送测试包裹检测通知
curl -X POST http://localhost:5000/api/communication/test-parcel \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "TEST001", "detectedAt": "2025-12-22T10:00:00Z"}'

# 查看上游通信日志
tail -20 logs/upstream-communication-2025-12-22.log
```

### 6.4 验证日志开关

```bash
# 获取当前日志配置
curl http://localhost:5000/api/config/logging

# 关闭通信日志
curl -X PUT http://localhost:5000/api/config/logging \
  -H "Content-Type: application/json" \
  -d '{"enableCommunicationLog": false, "enableUpstreamCommunicationLog": false}'

# 重新启用通信日志
curl -X PUT http://localhost:5000/api/config/logging \
  -H "Content-Type: application/json" \
  -d '{"enableCommunicationLog": true, "enableUpstreamCommunicationLog": true}'
```

---

## 七、故障排查

### 7.1 日志文件未生成

**可能原因**：
1. 日志开关被关闭
2. 日志目录权限不足
3. NLog 配置错误

**解决方法**：
```bash
# 1. 检查日志配置
curl http://localhost:5000/api/config/logging

# 2. 检查日志目录权限
ls -la logs/

# 3. 检查 NLog 内部日志
cat logs/internal-nlog.txt
```

### 7.2 日志内容不完整

**可能原因**：
1. 日志级别设置过高
2. 日志过滤规则不匹配
3. 通信客户端未正确集成日志

**解决方法**：
```bash
# 1. 检查 NLog 配置中的 minlevel 设置
cat src/Host/ZakYip.WheelDiverterSorter.Host/nlog.config | grep minlevel

# 2. 确认日志标记格式正确
grep -r "\[上游通信-" logs/upstream-communication-*.log
```

### 7.3 日志文件过大

**可能原因**：
1. 日志级别设置为 Debug
2. 高频通信场景
3. 未配置日志归档

**解决方法**：
```bash
# 1. 调整日志级别为 Info
# 修改 nlog.config 中的 minlevel="Info"

# 2. 减少日志保留天数
# 修改 nlog.config 中的 maxArchiveFiles="7"

# 3. 启用日志压缩（需要 NLog.Targets.Compress）
# 在 nlog.config 中添加压缩配置
```

---

## 八、总结

### 8.1 功能清单

| 功能 | 状态 | 说明 |
|-----|------|------|
| 通信日志文件 | ✅ 已实现 | communication-{date}.log |
| 上游消息日志文件 | ✅ 已实现 | upstream-communication-{date}.log |
| TCP 协议日志 | ✅ 已集成 | TouchSocket 客户端 |
| MQTT 协议日志 | ✅ 已集成 | MQTT 客户端 |
| SignalR 协议日志 | ✅ 已集成 | SignalR 客户端 |
| 日志开关控制 | ✅ 已实现 | API 动态配置 |
| 日志自动归档 | ✅ 已实现 | 按日期滚动 |
| 消息内容记录 | ✅ 已实现 | 完整JSON内容 |

### 8.2 优势

1. **完整性**：记录所有通信消息的发送和接收
2. **可追溯性**：包含完整的时间戳和消息内容
3. **可配置性**：支持动态开关和归档策略
4. **多协议支持**：TCP、MQTT、SignalR 全覆盖
5. **故障诊断**：便于排查通信问题

### 8.3 文档链接

- **上游连接指南**：`docs/guides/UPSTREAM_CONNECTION_GUIDE.md`
- **系统配置指南**：`docs/guides/SYSTEM_CONFIG_GUIDE.md`
- **NLog 配置**：`src/Host/ZakYip.WheelDiverterSorter.Host/nlog.config`
- **LoggingConfiguration 源码**：`src/Core/.../Models/LoggingConfiguration.cs`

---

**文档版本**：1.0  
**最后更新**：2025-12-22  
**维护者**：ZakYip Development Team
