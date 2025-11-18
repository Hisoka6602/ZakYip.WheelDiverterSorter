# PR-10: 包裹追踪日志（Parcel Trace Logging）

## 概述

本功能为每个包裹建立完整的"时间线"审计日志，从入口光电创建到最终落格或异常口的全过程追踪。

### 关键特性

- **完整生命周期追踪**：记录包裹从创建到完成的所有关键节点
- **独立日志文件**：使用专用的 JSON 格式日志文件，便于分析
- **自动清理**：支持基于天数和大小的自动日志清理，防止磁盘占满
- **异常安全**：追踪日志写入失败不会影响主业务流程
- **高性能**：异步写入，不阻塞分拣流程

## 日志事件类型

追踪日志包含以下关键阶段：

### 1. Created（创建）
- **触发点**：入口光电检测到包裹
- **来源**：Ingress
- **详情**：传感器信息

### 2. UpstreamAssigned（上游分配）
- **触发点**：RuleEngine 返回目标格口
- **来源**：Upstream
- **详情**：目标格口 ID、响应延迟、分配状态

### 3. RoutePlanned（路径规划）
- **触发点**：完成摆轮路径规划
- **来源**：Execution
- **详情**：目标格口、路径段数量、预计耗时

### 4. OverloadDecision（超载决策）
- **触发点**：触发超载策略（入口或路由阶段）
- **来源**：OverloadPolicy
- **详情**：超载原因、拥堵等级、时间预算

### 5. Diverted（正常落格）
- **触发点**：包裹成功分拣到目标格口
- **来源**：Execution
- **详情**：实际格口、目标格口

### 6. ExceptionDiverted（异常落格）
- **触发点**：包裹被送往异常口
- **来源**：Execution
- **详情**：实际格口、异常原因（超载/失败/超时等）

## 日志文件

### 文件路径与格式

追踪日志存储在独立目录：

```
logs/
  trace/
    parcel-trace-2025-01-18.log          # 当天日志
  archives/
    trace/
      parcel-trace-2025-01-17-001.log    # 归档日志
      parcel-trace-2025-01-16-001.log
```

### 日志格式

每条日志为一行 JSON，包含以下字段：

```json
{
  "ItemId": 123456789,
  "BarCode": null,
  "OccurredAt": "2025-01-18T03:45:12.345Z",
  "Stage": "Created",
  "Source": "Ingress",
  "Details": "传感器检测到包裹"
}
```

### 滚动策略

- **每日滚动**：每天生成新文件
- **保留期限**：默认保留 14 天
- **自动归档**：旧文件自动移至 `archives/trace/` 目录
- **大小限制**：日志总大小超过 1GB 时，自动删除最旧文件

## 配置

### appsettings.json

```json
{
  "LogCleanup": {
    "LogDirectory": "logs",
    "RetentionDays": 14,
    "MaxTotalSizeMb": 1024,
    "CleanupIntervalHours": 24
  }
}
```

### 配置说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `LogDirectory` | string | "logs" | 日志根目录路径 |
| `RetentionDays` | int | 14 | 日志保留天数 |
| `MaxTotalSizeMb` | int | 1024 | 日志总大小上限（MB） |
| `CleanupIntervalHours` | int | 24 | 清理任务执行间隔（小时） |

## 使用场景

### 1. 排查包裹异常

当包裹未能正常落格时，可通过追踪日志快速定位问题：

```bash
# 查找特定包裹的所有事件
grep '"ItemId":123456789' logs/trace/parcel-trace-*.log

# 查找所有超载异常
grep '"Stage":"OverloadDecision"' logs/trace/parcel-trace-*.log

# 查找所有异常落格
grep '"Stage":"ExceptionDiverted"' logs/trace/parcel-trace-*.log
```

### 2. 分析超时包裹

```bash
# 查找超时相关的超载决策
grep '"Stage":"OverloadDecision"' logs/trace/parcel-trace-*.log | grep "Timeout"
```

### 3. 统计异常率

```bash
# 统计当天异常包裹数量
grep -c '"Stage":"ExceptionDiverted"' logs/trace/parcel-trace-$(date +%Y-%m-%d).log

# 统计当天总包裹数量
grep -c '"Stage":"Created"' logs/trace/parcel-trace-$(date +%Y-%m-%d).log
```

### 4. 使用 jq 分析 JSON 日志

```bash
# 提取所有超载包裹的 ItemId
grep '"Stage":"OverloadDecision"' logs/trace/parcel-trace-*.log | jq -r '.ItemId'

# 按来源统计事件数量
cat logs/trace/parcel-trace-*.log | jq -r '.Source' | sort | uniq -c

# 查看特定包裹的完整时间线
grep '"ItemId":123456789' logs/trace/parcel-trace-*.log | jq -s 'sort_by(.OccurredAt)'
```

## 架构设计

### 分层架构

```
┌─────────────────────────────────────────┐
│   Host / Execution / Ingress           │  ← 业务层：调用 IParcelTraceSink
│   (ParcelSortingOrchestrator)          │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│   Core Layer                            │  ← 抽象层：定义接口和模型
│   - IParcelTraceSink                    │
│   - ParcelTraceEventArgs                │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│   Observability Layer                   │  ← 实现层：文件写入、清理
│   - FileBasedParcelTraceSink            │
│   - LogCleanupHostedService             │
└─────────────────────────────────────────┘
```

### 关键组件

#### 1. IParcelTraceSink
- **职责**：定义追踪日志写入接口
- **位置**：`ZakYip.WheelDiverterSorter.Core.Tracing`
- **特性**：异步写入、异常安全

#### 2. FileBasedParcelTraceSink
- **职责**：将追踪事件写入日志文件
- **位置**：`ZakYip.WheelDiverterSorter.Observability.Tracing`
- **实现**：使用 ILogger 通过 NLog 写入 JSON 格式日志

#### 3. LogCleanupHostedService
- **职责**：定期清理过期日志文件
- **位置**：`ZakYip.WheelDiverterSorter.Observability.Tracing`
- **策略**：
  - 删除超过保留天数的文件
  - 当总大小超限时，删除最旧文件

## 性能考虑

### 异步写入
- 使用 `ValueTask` 避免不必要的堆分配
- 不阻塞主业务流程

### 异常隔离
- 所有异常在 FileBasedParcelTraceSink 内部捕获
- 写入失败仅记录 Warning 日志，不向上抛出

### 磁盘占用控制
- 自动清理机制防止磁盘占满
- 可配置保留天数和总大小上限

## 扩展与优化

### 未来优化方向

1. **采样策略**
   - 对正常包裹实施采样（如 1% 记录率）
   - 异常包裹 100% 记录
   - 可配置采样率

2. **结构化查询**
   - 可选：将日志导入 ElasticSearch 或 Loki
   - 提供更强大的查询和可视化能力

3. **实时监控**
   - 集成 Grafana 仪表板
   - 实时展示异常率、超载率等指标

## 验收标准

- ✅ 长时间运行后 `logs/` 目录不会无限增长
- ✅ 异常包裹在追踪日志中有完整记录
- ✅ 追踪日志写入失败不影响分拣流程
- ✅ 日志文件格式为 JSON，便于解析
- ✅ 自动清理按配置正常工作

## 故障排查

### 问题：日志文件未生成

**排查步骤**：
1. 检查 `logs/trace/` 目录是否存在（应自动创建）
2. 检查 NLog 配置（`nlog.config`）是否包含 `parceltrace` target
3. 检查 DI 配置是否正确注册 `IParcelTraceSink`

### 问题：日志文件过大

**解决方案**：
1. 调整 `RetentionDays` 减少保留天数
2. 调整 `MaxTotalSizeMb` 降低总大小上限
3. 实施采样策略（未来优化）

### 问题：追踪日志影响性能

**解决方案**：
1. 追踪日志默认为异步写入，不应影响性能
2. 如仍有影响，考虑实施采样策略
3. 检查磁盘 I/O 性能

## 参考资料

- [NLog 配置文档](https://nlog-project.org/config/)
- [jq 命令行 JSON 处理](https://stedolan.github.io/jq/)
- PR-08: 超载策略实现
- PR-09: 健康检查与自检
