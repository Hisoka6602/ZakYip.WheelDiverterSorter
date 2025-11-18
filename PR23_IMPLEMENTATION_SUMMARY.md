# PR-23 实现总结：可观测性 / 审计 / 性能护栏一体化

## 概述

本PR为 SortingPipeline 建立了统一的可观测方案，实现了主链路最小必要日志 + 可开关的详细诊断、单件审计Trace（JSONL）可回放、异常告警流，并提供了离线分析工具和性能基准测试框架。

## 已完成功能

### 1. Core 层：诊断枚举 + 审计/告警抽象

#### DiagnosticsLevel 枚举
- **位置**: `ZakYip.WheelDiverterSorter.Core/LineModel/Enums/DiagnosticsLevel.cs`
- **级别**:
  - `None (0)`: 关闭诊断，仅保留必要错误日志
  - `Basic (1)`: 基本诊断，记录关键状态与异常信息
  - `Verbose (2)`: 详细诊断，记录完整流水线关键步骤

#### ParcelTraceEventArgs 增强
- **位置**: `ZakYip.WheelDiverterSorter.Core/LineModel/Tracing/ParcelTraceEventArgs.cs`
- **新增字段**:
  - `TargetChuteId`: 目标格口ID
  - `ActualChuteId`: 实际落格ID（若已知）
- **用途**: 完整记录包裹的目标和实际落格信息，便于审计回放

#### Alert 抽象（已存在）
- `AlertSeverity`: Info/Warning/Critical 三级告警
- `AlertRaisedEventArgs`: 告警事件载荷
- `IAlertSink`: 告警接收器接口

### 2. Infrastructure 层（Observability）：日志落地 + 自清理

#### FileAlertSink
- **位置**: `ZakYip.WheelDiverterSorter.Observability/FileAlertSink.cs`
- **功能**: 
  - 将告警事件写入 `alerts-yyyyMMdd.log`（JSONL格式）
  - 每行一条 JSON，包含 AlertCode、Severity、Message、RaisedAt 等字段
  - 写入失败不影响主业务流程

#### FileBasedParcelTraceSink 增强
- **位置**: `ZakYip.WheelDiverterSorter.Observability/Tracing/FileBasedParcelTraceSink.cs`
- **改进**:
  - 改为直接写入 JSONL 格式（`parcel-trace-yyyyMMdd.log`）
  - 支持新增的 TargetChuteId 和 ActualChuteId 字段
  - 每行一条 JSON，便于工具解析

#### AlertHistoryService
- **位置**: `ZakYip.WheelDiverterSorter.Observability/AlertHistoryService.cs`
- **功能**:
  - 内存保留最近50条告警
  - 提供 GetRecentCriticalAlerts 方法供 Health 端点使用
  - 实现 IAlertSink 接口，可与其他 Sink 组合使用

#### LogCleanupHostedService（已存在）
- 自动清理过期日志文件（*.log）
- 支持按天数保留（默认14天）和总大小限制
- 自动处理 parcel-trace 和 alerts 日志

### 3. Execution 层：Pipeline 埋点

#### DiagnosticsOptions
- **位置**: `ZakYip.WheelDiverterSorter.Execution/DiagnosticsOptions.cs`
- **配置项**:
  - `Level`: 诊断级别（默认 Basic）
  - `NormalParcelSamplingRate`: 正常件抽样比例（默认 0.1，即10%）

#### TracingMiddleware 增强
- **位置**: `ZakYip.WheelDiverterSorter.Execution/Pipeline/Middlewares/TracingMiddleware.cs`
- **功能**:
  - 支持三级诊断控制：
    - `None`: 跳过所有追踪
    - `Basic`: 异常件全记录 + 正常件10%抽样
    - `Verbose`: 全部记录
  - 自动识别异常件和Overload件，无条件审计
  - 添加 TargetChuteId 和 ActualChuteId 到追踪事件

#### 其他中间件统一埋点
- **OverloadEvaluationMiddleware**: 记录 OverloadEvaluated 阶段
- **RoutePlanningMiddleware**: 记录 RoutePlanned 阶段和节点降级决策
- **PathExecutionMiddleware**: 记录 Diverted 和 ExceptionDiverted 阶段

### 4. Host 层：Health 端点增强

#### HealthController 扩展
- **位置**: `ZakYip.WheelDiverterSorter.Host/Controllers/HealthController.cs`
- **新增字段**（在 `/health/line` 响应中）:
  - `DiagnosticsLevel`: 当前诊断级别
  - `ConfigVersion`: 配置版本标识（使用 ConfigName）
  - `DegradationMode`: 降级模式（已存在）
  - `RecentCriticalAlerts`: 最近5条 Critical 告警摘要

### 5. Tools 层：离线报表分析

#### LogParser 扩展
- **位置**: `Tools/ZakYip.WheelDiverterSorter.Tools.Reporting/Analyzers/LogParser.cs`
- **新增方法**:
  - `ScanAlertLogFiles`: 扫描 alerts-*.log 文件
  - `ParseAlertLogFiles`: 解析 JSONL 格式的告警日志

#### ReportWriter 扩展
- **位置**: `Tools/ZakYip.WheelDiverterSorter.Tools.Reporting/Writers/ReportWriter.cs`
- **新增方法**:
  - `WriteAlertReport`: 生成告警分析报表
  - 输出文件：
    - `alerts-statistics-{timestamp}.csv`: 告警统计（按严重程度、按代码）
    - `alerts-detail-{timestamp}.csv`: 告警详情列表
    - `alerts-report-{timestamp}.md`: Markdown 格式报表

#### Program.cs 集成
- 自动扫描和解析 alert 日志
- 在汇总信息中显示告警事件数
- 同时生成 parcel trace 和 alert 报表

### 6. Benchmarks（已存在，无需修改）
- **位置**: `ZakYip.WheelDiverterSorter.Benchmarks`
- 已有基准测试：
  - PathGenerationBenchmarks
  - OverloadPolicyBenchmarks
  - HighLoadBenchmarks
  - PerformanceBottleneckBenchmarks
- 已有文档：`docs/PERFORMANCE_BASELINE.md`

## 使用说明

### 配置诊断级别

在 `appsettings.json` 中配置：

```json
{
  "Diagnostics": {
    "Level": "Basic",
    "NormalParcelSamplingRate": 0.1
  }
}
```

### 配置日志清理

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

### 运行离线报表工具

```bash
# 分析所有日志
cd Tools/ZakYip.WheelDiverterSorter.Tools.Reporting
dotnet run

# 分析指定时间范围
dotnet run -- --from 2025-11-01 --to 2025-11-18 --bucket 1h

# 自定义输出目录
dotnet run -- --log-dir /var/logs/wheel-diverter --output ./my-reports
```

### 查看系统健康状态

```bash
curl http://localhost:5000/health/line
```

响应包含：
- 当前诊断级别
- 配置版本
- 降级模式
- 最近 Critical 告警

## 验收要点

### 可观测性
- ✅ DiagnosticsLevel = None 时仅输出错误日志，Trace 文件仅记录异常件
- ✅ DiagnosticsLevel = Verbose 时主链路关键阶段都有 Trace 记录
- ✅ Basic 模式下异常件全记录，正常件10%抽样

### 审计与回放
- ✅ 异常件可通过 parcel-trace 里的 Stage 序列复盘完整经历
- ✅ 离线分析工具能读取日志生成 CSV 和 Markdown 报表
- ✅ JSONL 格式便于工具解析和回放

### 告警流
- ✅ FileAlertSink 写入 alerts-*.log
- ✅ AlertHistoryService 保留最近告警用于 Health 端点
- ✅ 告警分析工具能统计和展示告警趋势

### 长期运行安全
- ✅ LogCleanupHostedService 自动清理过期日志
- ✅ 日志清理行为记录中文运行日志

### 性能护栏
- ✅ Benchmarks 项目可运行（已存在）
- ✅ PERFORMANCE_BASELINE.md 文档已存在
- ✅ 本 PR 不涉及核心逻辑变更，性能无明显影响

## 分层设计总结

| 层级 | 职责 | 新增/修改文件 |
|------|------|--------------|
| **Core** | 接口、枚举、事件模型 | DiagnosticsLevel.cs, ParcelTraceEventArgs.cs |
| **Observability (Infrastructure)** | 文件落地、自清理策略 | FileAlertSink.cs, AlertHistoryService.cs, FileBasedParcelTraceSink.cs |
| **Execution** | 埋点调用、诊断控制 | DiagnosticsOptions.cs, TracingMiddleware.cs, 其他中间件 |
| **Host** | 状态暴露 API | HealthController.cs |
| **Tools** | 离线分析、报表 | LogParser.cs, ReportWriter.cs, Program.cs |

## 后续优化建议

1. **性能基线更新**: 运行 Benchmarks 并更新 PERFORMANCE_BASELINE.md 中的实际数据
2. **集成测试**: 添加端到端测试验证日志记录和清理功能
3. **实时监控**: 考虑接入 Prometheus/Grafana 展示实时告警趋势
4. **告警通道**: 实现企业微信、钉钉等 IAlertSink 实现
5. **配置版本管理**: 考虑在 SystemConfiguration 中添加 ConfigVersion 字段

## 注意事项

- 所有中文提示在注释中，日志字段保持英文便于工具解析
- 写入失败不影响主业务流程（所有 Sink 实现都捕获异常）
- 诊断级别可通过配置文件动态调整（需重启应用）
- 日志文件名格式固定：`parcel-trace-yyyyMMdd.log` 和 `alerts-yyyyMMdd.log`
