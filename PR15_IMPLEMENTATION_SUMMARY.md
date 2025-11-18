# PR-15: 业务异常趋势监控与告警钩子（线体级）实现总结

## Implementation Summary - Business Anomaly Monitoring and Alert Hook System

### 概述 (Overview)

本PR实现了线体级的业务异常趋势监控和告警钩子系统，不仅监控硬件状态，还监控业务指标（异常口比例、超载占比、上游超时等），提供统一的告警接口，方便后续接入企业微信、钉钉、邮件等通道。

This PR implements a line-level business anomaly monitoring and alert hook system that monitors not only hardware status but also business metrics (exception chute ratio, overload percentage, upstream timeouts, etc.), providing a unified alert interface for future integration with WeChat Work, DingTalk, email, and other channels.

---

## 核心实现 (Core Implementation)

### 1. Core层：告警级别与事件模型 (Alert Levels and Event Model)

#### AlertSeverity (告警严重程度枚举)
```csharp
public enum AlertSeverity
{
    Info = 0,      // 信息级 - 通知性消息
    Warning = 1,   // 警告级 - 需要关注但不紧急
    Critical = 2   // 严重级 - 需要立即处理
}
```

**特性:**
- 使用 `Description` 特性提供中文描述
- 三级告警分类符合企业运维标准

#### AlertRaisedEventArgs (告警事件参数)
```csharp
public record struct AlertRaisedEventArgs
{
    public required string AlertCode { get; init; }
    public required AlertSeverity Severity { get; init; }
    public required string Message { get; init; }
    public required DateTimeOffset RaisedAt { get; init; }
    
    // 可选字段
    public string? LineId { get; init; }
    public string? ChuteId { get; init; }
    public int? NodeId { get; init; }
    public Dictionary<string, object>? Details { get; init; }
}
```

**设计亮点:**
- 使用 `record struct` 确保值类型语义和不可变性
- 必填字段使用 `required` 关键字
- 可选字段支持灵活扩展（线体ID、格口ID、节点ID等）
- Details 字典支持附加任意结构化数据

#### IAlertSink (告警接收器接口)
```csharp
public interface IAlertSink
{
    Task WriteAlertAsync(AlertRaisedEventArgs alertEvent, 
                         CancellationToken cancellationToken = default);
}
```

**设计考虑:**
- 异步接口，避免阻塞主业务流程
- 支持取消令牌，便于优雅关闭
- 单一职责：只负责接收告警，不关心具体实现

---

### 2. Application/Execution层：异常趋势分析器 (Anomaly Detector)

#### IAnomalyDetector (异常检测器接口)
```csharp
public interface IAnomalyDetector
{
    void RecordSortingResult(string targetChuteId, bool isExceptionChute);
    void RecordOverload(string reason);
    void RecordUpstreamTimeout();
    Task CheckAnomalyTrendsAsync(CancellationToken cancellationToken = default);
    void ResetStatistics();
}
```

#### AnomalyDetector (实现类)

**监控指标:**

1. **异常格口比例 (Exception Chute Ratio)**
   - 监控窗口：最近 5 分钟
   - 告警阈值：> 15%
   - 告警级别：Warning
   - 告警代码：`EXCEPTION_CHUTE_RATIO_HIGH`
   - 最小样本数：20 个包裹

2. **超载事件激增 (Overload Spike)**
   - 监控窗口：前后 2.5 分钟对比
   - 告警阈值：> 2x 增长
   - 告警级别：Warning
   - 告警代码：`OVERLOAD_SPIKE`
   - 检测 RouteOverload 和 CapacityExceeded 原因

3. **上游超时比例 (Upstream Timeout Ratio)**
   - 监控窗口：最近 5 分钟
   - 告警阈值：> 10%
   - 告警级别：Critical
   - 告警代码：`UPSTREAM_TIMEOUT_HIGH`
   - 最小样本数：20 个包裹

**核心特性:**

- **滑动时间窗口**: 使用 Queue 数据结构实现 5 分钟滑动窗口
- **自动清理过期数据**: 每次记录时自动清理超出窗口的数据
- **告警冷却机制**: 10 分钟冷却期，避免告警风暴
- **线程安全**: 使用 `lock` 保护内部数据结构
- **异步告警写入**: 使用 `Task.Run` 避免阻塞检测逻辑
- **异常处理**: 捕获所有异常，确保不影响主业务流程

---

### 3. Infrastructure/Observability层：日志告警接收器 (Log Alert Sink)

#### LogAlertSink (实现类)

```csharp
public class LogAlertSink : IAlertSink
{
    private readonly ILogger<LogAlertSink> _logger;
    private readonly PrometheusMetrics? _metrics;
    
    public async Task WriteAlertAsync(AlertRaisedEventArgs alertEvent, 
                                      CancellationToken cancellationToken = default)
    {
        // 1. 序列化为 JSON
        // 2. 根据严重程度使用不同日志级别
        // 3. 记录 Prometheus 指标
    }
}
```

**日志输出格式:**

```
[ALERT] EXCEPTION_CHUTE_RATIO_HIGH | Severity=Warning | Message=异常格口比例过高... | JSON={...}
[ALERT-WARNING] EXCEPTION_CHUTE_RATIO_HIGH: 异常格口比例过高...
```

**集成点:**
- 结构化日志输出（JSON格式）
- 可通过 NLog 或 Serilog 路由到专门的 `alert.log` 文件
- 可选的 Prometheus 指标记录

---

### 4. Observability层：Prometheus 告警指标

#### 新增指标

1. **sorting_alerts_total{severity, code}** (Counter)
   - 告警总数，按严重程度和代码分类
   - 标签：
     - `severity`: Info/Warning/Critical
     - `code`: 告警代码（如 EXCEPTION_CHUTE_RATIO_HIGH）

2. **sorting_last_alert_timestamp{severity, code}** (Gauge)
   - 最近一次告警时间（Unix时间戳）
   - 标签同上
   - 可用于计算告警间隔、检测告警频率

**使用示例 (PromQL):**

```promql
# 最近 1 小时的告警总数
increase(sorting_alerts_total[1h])

# 严重告警率
rate(sorting_alerts_total{severity="Critical"}[5m])

# 告警频率（每分钟）
rate(sorting_alerts_total[1m]) * 60

# 最近一次告警距今时间
time() - sorting_last_alert_timestamp
```

---

## 测试覆盖 (Test Coverage)

### 单元测试 (Unit Tests)

#### Core 层测试 (7 tests)
- `AlertSeverityTests`: 枚举值、描述特性、字符串表示
- `AlertRaisedEventArgsTests`: 必填/可选字段、记录相等性

#### Execution 层测试 (14 tests)
- `AnomalyDetectorTests`:
  - 构造函数参数校验
  - 数据记录方法
  - 异常趋势检测（高/正常比例）
  - 冷却机制
  - 统计重置
  - 异常处理

#### Observability 层测试 (10 tests)
- `LogAlertSinkTests`:
  - 不同严重程度的日志级别
  - 可选字段的 JSON 序列化
  - Prometheus 指标记录
  - 异常处理（不中断主流程）

### 集成测试 (Integration Tests) (5 tests)

#### AlertFlowIntegrationTests
- 端到端告警流程（从检测到接收）
- 多种告警场景：
  - 高异常口比例 → Warning 告警
  - 高上游超时比例 → Critical 告警
  - 多重异常同时触发
  - 正常场景不触发告警
- 自定义 AlertSink 验证告警数据完整性

**测试总结:**
- **总计 36 个测试，全部通过**
- 覆盖所有关键路径
- 包含正常场景和异常场景
- 验证线程安全和异步行为

---

## 使用指南 (Usage Guide)

### 1. 服务注册 (Service Registration)

```csharp
// Program.cs 或 Startup.cs
services.AddSingleton<IAlertSink, LogAlertSink>();
services.AddSingleton<IAnomalyDetector, AnomalyDetector>();
services.AddSingleton<PrometheusMetrics>(); // 已存在
```

### 2. 业务代码集成 (Business Code Integration)

```csharp
public class ParcelSortingOrchestrator
{
    private readonly IAnomalyDetector _anomalyDetector;
    
    public async Task ProcessSortingAsync(SortOrder sortOrder)
    {
        // 执行分拣逻辑...
        
        // 记录结果
        bool isExceptionChute = IsExceptionChute(resultChute);
        _anomalyDetector.RecordSortingResult(resultChute, isExceptionChute);
        
        // 如果是超载
        if (isOverload)
        {
            _anomalyDetector.RecordOverload(overloadReason);
        }
        
        // 如果上游超时
        if (isUpstreamTimeout)
        {
            _anomalyDetector.RecordUpstreamTimeout();
        }
    }
}
```

### 3. 周期性异常检测 (Periodic Anomaly Detection)

```csharp
public class AnomalyMonitoringWorker : BackgroundService
{
    private readonly IAnomalyDetector _anomalyDetector;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _anomalyDetector.CheckAnomalyTrendsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

### 4. 扩展自定义 AlertSink (Custom Alert Sink)

```csharp
// 企业微信告警示例
public class WeChatWorkAlertSink : IAlertSink
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    
    public async Task WriteAlertAsync(AlertRaisedEventArgs alertEvent, 
                                      CancellationToken cancellationToken = default)
    {
        var message = new
        {
            msgtype = "markdown",
            markdown = new
            {
                content = $"## {alertEvent.Severity} 告警\n" +
                          $"**代码**: {alertEvent.AlertCode}\n" +
                          $"**消息**: {alertEvent.Message}\n" +
                          $"**时间**: {alertEvent.RaisedAt:yyyy-MM-dd HH:mm:ss}"
            }
        };
        
        await _httpClient.PostAsJsonAsync(_webhookUrl, message, cancellationToken);
    }
}
```

---

## 验收检查 (Acceptance Criteria)

### ✅ 已完成 (Completed)

1. **Core 层**
   - ✅ AlertSeverity 枚举（Info/Warning/Critical）
   - ✅ AlertRaisedEventArgs 事件参数
   - ✅ IAlertSink 接口

2. **Application/Execution 层**
   - ✅ IAnomalyDetector 接口
   - ✅ AnomalyDetector 实现
   - ✅ 异常口比例监控（>15%）
   - ✅ 超载激增检测（2x 增长）
   - ✅ 上游超时监控（>10%）
   - ✅ 告警冷却机制（10分钟）
   - ✅ 结构化日志输出

3. **Infrastructure/Observability 层**
   - ✅ LogAlertSink 实现
   - ✅ JSON 结构化日志输出
   - ✅ 可扩展设计（无外部推送绑定）

4. **Prometheus 指标**
   - ✅ sorting_alerts_total{severity, code}
   - ✅ sorting_last_alert_timestamp{severity, code}

5. **测试**
   - ✅ 36 个单元测试和集成测试
   - ✅ 全部测试通过
   - ✅ 覆盖所有关键场景

6. **安全扫描**
   - ✅ CodeQL 扫描：0 个告警

### 🔄 待人工验证 (Manual Validation Needed)

1. **场景验证**
   - 人为制造异常场景（提升异常口比例）
   - 在日志中确认告警输出
   - 在 Prometheus 中确认指标增加
   - 验证不影响主业务流程

2. **性能验证**
   - 监控 AnomalyDetector 的内存使用
   - 验证 Queue 清理逻辑的效果
   - 确认异步告警写入不阻塞主流程

3. **NLog/Serilog 配置**
   - 配置 alert.log 文件路由
   - 验证日志轮转和归档

---

## 技术亮点 (Technical Highlights)

1. **最小化侵入**: 所有新代码都是新增文件，不修改现有代码
2. **单一职责**: 每个组件职责明确，易于维护和测试
3. **可扩展设计**: IAlertSink 接口支持多种实现（日志、企业微信、钉钉等）
4. **线程安全**: 使用 lock 保护共享状态
5. **异步优先**: 避免阻塞主业务流程
6. **异常隔离**: 告警失败不影响主业务
7. **滑动窗口**: 自动清理过期数据，避免内存泄漏
8. **告警冷却**: 防止告警风暴
9. **结构化日志**: JSON 格式，便于日志分析
10. **全面测试**: 36 个测试，覆盖所有场景

---

## 后续扩展建议 (Future Enhancements)

1. **外部通道集成**
   - 企业微信 Webhook
   - 钉钉机器人
   - SMTP 邮件
   - Slack/Teams

2. **告警配置化**
   - 从配置文件读取阈值
   - 动态调整监控窗口
   - 启用/禁用特定告警

3. **告警聚合**
   - 合并相似告警
   - 按时间段汇总
   - 生成告警报告

4. **告警历史**
   - 持久化告警记录
   - 提供查询 API
   - 告警统计面板

5. **智能告警**
   - 基于机器学习的异常检测
   - 自适应阈值调整
   - 预测性告警

---

## 文件清单 (File List)

### 新增文件 (New Files)

**Core 层:**
- `ZakYip.WheelDiverterSorter.Core/Enums/AlertSeverity.cs`
- `ZakYip.WheelDiverterSorter.Core/Events/AlertRaisedEventArgs.cs`
- `ZakYip.WheelDiverterSorter.Core/IAlertSink.cs`

**Execution 层:**
- `ZakYip.WheelDiverterSorter.Execution/IAnomalyDetector.cs`
- `ZakYip.WheelDiverterSorter.Execution/AnomalyDetector.cs`

**Observability 层:**
- `ZakYip.WheelDiverterSorter.Observability/LogAlertSink.cs`

**测试文件:**
- `ZakYip.WheelDiverterSorter.Core.Tests/AlertSeverityTests.cs`
- `ZakYip.WheelDiverterSorter.Core.Tests/AlertRaisedEventArgsTests.cs`
- `ZakYip.WheelDiverterSorter.Execution.Tests/AnomalyDetectorTests.cs`
- `ZakYip.WheelDiverterSorter.Execution.Tests/AlertFlowIntegrationTests.cs`
- `ZakYip.WheelDiverterSorter.Observability.Tests/LogAlertSinkTests.cs`

### 修改文件 (Modified Files)

- `ZakYip.WheelDiverterSorter.Observability/PrometheusMetrics.cs` (+38 lines)
  - 新增 sorting_alerts_total Counter
  - 新增 sorting_last_alert_timestamp Gauge
  - 新增 RecordAlert() 方法

**统计:**
- 新增文件：12 个
- 修改文件：1 个
- 新增代码：1541 行
- 测试覆盖：36 个测试

---

## 总结 (Summary)

PR-15 成功实现了业务异常趋势监控与告警钩子系统，提供了统一的告警接口和扩展点。实现遵循现有代码风格，使用最小化修改，确保不影响主业务流程。所有测试通过，CodeQL 安全扫描无告警。系统已准备好投入使用和后续扩展。
