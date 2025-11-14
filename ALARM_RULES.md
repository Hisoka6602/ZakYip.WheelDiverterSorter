# 告警规则说明 / Alarm Rules Documentation

本文档说明了轮式分拣系统的告警规则配置和使用方法。

This document describes the alarm rules configuration and usage for the wheel diverter sorter system.

## 告警规则 / Alarm Rules

系统定义了以下6种告警规则：

The system defines the following 6 alarm rules:

### 1. 分拣失败率过高 (P1告警) / High Sorting Failure Rate (P1 Alarm)

- **触发条件 / Trigger Condition**: 分拣失败率 > 5%
- **告警级别 / Alarm Level**: P1 (最高优先级 / Highest Priority)
- **描述 / Description**: 当系统分拣失败率超过5%时触发。需要至少20次分拣操作才会触发检查。
- **处理建议 / Recommended Action**: 立即检查摆轮硬件、传感器和路径配置。

### 2. 队列积压 (P2告警) / Queue Backlog (P2 Alarm)

- **触发条件 / Trigger Condition**: 当前队列长度 > 50个包裹
- **告警级别 / Alarm Level**: P2 (中等优先级 / Medium Priority)
- **描述 / Description**: 当待处理包裹队列积压超过50个包裹时触发。
- **处理建议 / Recommended Action**: 检查系统吞吐量，可能需要增加处理能力或检查是否有阻塞。

### 3. 摆轮故障 (P1告警) / Diverter Fault (P1 Alarm)

- **触发条件 / Trigger Condition**: 摆轮设备报告故障
- **告警级别 / Alarm Level**: P1 (最高优先级 / Highest Priority)
- **描述 / Description**: 当摆轮设备出现故障（如电机堵转、通信失败等）时触发。
- **处理建议 / Recommended Action**: 立即检查相应摆轮的硬件状态和连接。

### 4. 传感器故障 (P2告警) / Sensor Fault (P2 Alarm)

- **触发条件 / Trigger Condition**: 传感器设备报告故障
- **告警级别 / Alarm Level**: P2 (中等优先级 / Medium Priority)
- **描述 / Description**: 当传感器出现故障（如连续读取错误、无响应等）时触发。
- **处理建议 / Recommended Action**: 检查传感器连接和硬件状态，可能需要清洁或更换传感器。

### 5. RuleEngine断线 (P1告警) / RuleEngine Disconnection (P1 Alarm)

- **触发条件 / Trigger Condition**: RuleEngine断线时间 > 1分钟
- **告警级别 / Alarm Level**: P1 (最高优先级 / Highest Priority)
- **描述 / Description**: 当与RuleEngine的连接断开超过1分钟时触发。
- **处理建议 / Recommended Action**: 检查RuleEngine服务状态和网络连接，系统会将包裹发送到异常格口。

### 6. 系统重启 (P3告警) / System Restart (P3 Alarm)

- **触发条件 / Trigger Condition**: 系统启动时
- **告警级别 / Alarm Level**: P3 (低优先级 / Low Priority)
- **描述 / Description**: 系统启动或重启时触发，用于记录系统状态变更。
- **处理建议 / Recommended Action**: 确认是计划内重启，检查系统日志了解重启原因。

## API 接口 / API Endpoints

系统提供以下REST API接口管理告警：

The system provides the following REST API endpoints for alarm management:

### 获取活跃告警 / Get Active Alarms

```http
GET /api/alarms
```

返回所有当前活跃的告警列表。

Returns a list of all currently active alarms.

### 获取分拣失败率 / Get Sorting Failure Rate

```http
GET /api/alarms/sorting-failure-rate
```

返回当前分拣失败率统计。

Returns the current sorting failure rate statistics.

### 确认告警 / Acknowledge Alarm

```http
POST /api/alarms/acknowledge?alarmType={AlarmType}
```

确认指定类型的告警。

Acknowledges an alarm of the specified type.

### 重置统计计数器 / Reset Statistics

```http
POST /api/alarms/reset-statistics
```

重置分拣成功/失败统计计数器。

Resets the sorting success/failure statistics counters.

## 使用示例 / Usage Examples

### 查询活跃告警 / Query Active Alarms

```bash
curl http://localhost:5000/api/alarms
```

### 查询分拣失败率 / Query Sorting Failure Rate

```bash
curl http://localhost:5000/api/alarms/sorting-failure-rate
```

### 确认队列积压告警 / Acknowledge Queue Backlog Alarm

```bash
curl -X POST "http://localhost:5000/api/alarms/acknowledge?alarmType=QueueBacklog"
```

## 集成说明 / Integration Notes

### 在代码中使用 AlarmService / Using AlarmService in Code

AlarmService 已注册为单例服务，可以通过依赖注入使用：

AlarmService is registered as a singleton and can be used via dependency injection:

```csharp
public class MyService
{
    private readonly AlarmService _alarmService;
    
    public MyService(AlarmService alarmService)
    {
        _alarmService = alarmService;
    }
    
    public void ProcessParcel()
    {
        // Report sorting success/failure
        if (success)
        {
            _alarmService.RecordSortingSuccess();
        }
        else
        {
            _alarmService.RecordSortingFailure();
        }
        
        // Report queue length
        _alarmService.UpdateQueueLength(currentQueueSize);
        
        // Report sensor fault
        _alarmService.ReportSensorFault("S1", "No signal detected");
        
        // Clear sensor fault when recovered
        _alarmService.ClearSensorFault("S1");
    }
}
```

## 监控和日志 / Monitoring and Logging

- 所有告警触发都会记录到系统日志中，级别为 Warning
- AlarmMonitoringWorker 后台服务每10秒检查一次告警状态
- 可以通过 API 获取实时告警状态

- All alarm triggers are logged to the system log with Warning level
- AlarmMonitoringWorker background service checks alarm status every 10 seconds
- Real-time alarm status can be retrieved via API

## 未来扩展 / Future Enhancements

计划中的功能：

Planned features:

1. 告警推送到外部系统（邮件、钉钉、企业微信等）
2. 告警历史记录和统计分析
3. 可配置的告警阈值
4. 告警升级机制（未确认的P1告警自动升级）
5. Prometheus告警规则集成

1. Push alarms to external systems (Email, DingTalk, WeChat Work, etc.)
2. Alarm history and statistical analysis
3. Configurable alarm thresholds
4. Alarm escalation mechanism (auto-escalate unacknowledged P1 alarms)
5. Prometheus alerting rules integration
