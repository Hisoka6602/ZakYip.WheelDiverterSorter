# 离线异常统计分析工具使用指南

## 概述

本工具用于离线分析包裹追踪日志（Parcel Trace Logs），生成异常口统计报表。它不依赖运行中的系统实例，只读取已生成的日志文件，适合在运维机或测试机上周期性运行。

## 主要功能

1. **时间维度统计**：按可配置的时间片（如 5 分钟、1 小时）统计包裹处理情况
2. **OverloadReason 分析**：统计各类超载原因的分布
3. **格口热点分析**：识别异常率最高的格口
4. **节点热点分析**：识别问题最多的摆轮节点

## 工具特性

- ✅ **离线分析**：不影响线上实例运行
- ✅ **时间范围过滤**：可指定分析的起止时间
- ✅ **灵活配置**：时间片大小可自定义
- ✅ **多格式输出**：同时生成 CSV 和 Markdown 报表
- ✅ **容错处理**：自动跳过损坏的日志行，不会因个别错误而中断

## 安装与运行

### 前置要求

- .NET 8.0 SDK 或更高版本

### 构建项目

```bash
cd /path/to/ZakYip.WheelDiverterSorter
dotnet build Tools/ZakYip.WheelDiverterSorter.Tools.Reporting
```

### 运行工具

```bash
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \
  [--log-dir <日志目录>] \
  [--from <起始时间>] \
  [--to <结束时间>] \
  [--bucket <时间片大小>] \
  [--output <输出目录>]
```

## 参数说明

| 参数 | 说明 | 默认值 | 示例 |
|------|------|--------|------|
| `--log-dir` | 日志根目录路径 | `./logs` | `--log-dir /var/logs/wheel-diverter` |
| `--from` | 起始时间 | 不限 | `--from 2025-11-01` 或 `--from "2025-11-01T08:00:00"` |
| `--to` | 结束时间 | 不限（到最新） | `--to 2025-11-18` 或 `--to "2025-11-18T18:00:00"` |
| `--bucket` | 时间片大小 | `5m`（5 分钟） | `--bucket 1h`（1 小时）、`--bucket 30s`（30 秒） |
| `--output` | 报表输出目录 | `./reports` | `--output /tmp/reports` |

### 时间格式说明

- **日期格式**：`YYYY-MM-DD`（如 `2025-11-18`）
- **完整时间格式**：`YYYY-MM-DDTHH:mm:ss`（如 `2025-11-18T08:30:00`）
- **时间片大小**：
  - `Ns`：N 秒（如 `30s` 表示 30 秒）
  - `Nm`：N 分钟（如 `5m` 表示 5 分钟）
  - `Nh`：N 小时（如 `1h` 表示 1 小时）

## 使用示例

### 示例 1：分析所有日志，按 1 小时分片

```bash
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \
  --bucket 1h
```

这将分析 `./logs/trace/` 目录下的所有日志文件，按 1 小时时间片统计，结果输出到 `./reports/` 目录。

### 示例 2：分析指定日期范围

```bash
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \
  --from "2025-11-01" \
  --to "2025-11-18" \
  --bucket 5m
```

只分析 2025 年 11 月 1 日到 18 日之间的日志。

### 示例 3：自定义所有参数

```bash
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \
  --log-dir "/var/logs/wheel-diverter" \
  --from "2025-11-01T00:00:00" \
  --to "2025-11-18T23:59:59" \
  --bucket "1h" \
  --output "./my-reports"
```

完整指定所有参数，适合集成到定时任务或运维脚本中。

### 示例 4：查看帮助信息

```bash
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting --help
```

## 输出文件说明

工具运行后，会在输出目录生成以下文件（文件名包含时间戳）：

### 1. `summary-<timestamp>.csv`

**时间片维度统计**，包含以下字段：

| 字段 | 说明 |
|------|------|
| `BucketStart` | 时间片起始时间 |
| `BucketEnd` | 时间片结束时间 |
| `TotalParcels` | 总包裹数（基于 Created 事件） |
| `ExceptionParcels` | 异常包裹数（ExceptionDiverted 事件） |
| `OverloadEvents` | 超载事件数（OverloadDecision 事件） |
| `ExceptionRatio` | 异常比例（0-1） |
| `OverloadRatio` | 超载比例（0-1） |

**示例**：
```csv
BucketStart,BucketEnd,TotalParcels,ExceptionParcels,OverloadEvents,ExceptionRatio,OverloadRatio
2025-11-18T08:00:00.0000000+08:00,2025-11-18T09:00:00.0000000+08:00,1200,25,30,0.0208,0.0250
```

### 2. `overload-<timestamp>.csv`

**OverloadReason 分布统计**，包含以下字段：

| 字段 | 说明 |
|------|------|
| `Reason` | 超载原因（如 Timeout、CapacityExceeded 等） |
| `Count` | 该原因出现次数 |
| `Percent` | 占总超载事件的百分比 |

**示例**：
```csv
Reason,Count,Percent
CapacityExceeded,150,60.00
Timeout,80,32.00
NodeDegraded,20,8.00
```

### 3. `chute-hotspot-<timestamp>.csv`

**按格口的异常热点统计**，包含以下字段：

| 字段 | 说明 |
|------|------|
| `ChuteId` | 格口 ID |
| `ExceptionCount` | 该格口的异常次数 |
| `Percent` | 占全部异常的百分比 |

**示例**：
```csv
ChuteId,ExceptionCount,Percent
CHUTE-001,50,25.00
CHUTE-005,40,20.00
CHUTE-010,30,15.00
```

### 4. `node-hotspot-<timestamp>.csv`

**按节点的异常热点统计**，包含以下字段：

| 字段 | 说明 |
|------|------|
| `NodeId` | 节点 ID |
| `EventCount` | 该节点相关的异常/超载事件次数 |
| `Percent` | 占全部事件的百分比 |

**示例**：
```csv
NodeId,EventCount,Percent
NODE-12,80,40.00
NODE-08,60,30.00
NODE-15,30,15.00
```

### 5. `report-<timestamp>.md`

**Markdown 格式的汇总报告**，包含上述所有统计的可视化表格，适合直接查看或粘贴到文档、Wiki、Issue 中。

## 日志文件要求

工具读取的日志文件应满足以下条件：

1. **文件位置**：`<log-dir>/trace/parcel-trace-*.log`
2. **文件格式**：每行一条 JSON 记录
3. **JSON 格式**：

```json
{
  "ItemId": 123456789,
  "BarCode": null,
  "OccurredAt": "2025-11-18T03:45:12.345Z",
  "Stage": "Created",
  "Source": "Ingress",
  "Details": "传感器检测到包裹"
}
```

### 关键字段

| 字段 | 说明 | 示例值 |
|------|------|--------|
| `ItemId` | 包裹 ID（必需） | `123456789` |
| `OccurredAt` | 事件时间（必需） | `"2025-11-18T03:45:12.345Z"` |
| `Stage` | 事件阶段（必需） | `"Created"`, `"ExceptionDiverted"`, `"OverloadDecision"` |
| `Source` | 事件来源（必需） | `"Ingress"`, `"Execution"`, `"OverloadPolicy"` |
| `Details` | 详细信息（可选，可能包含 JSON） | `"{\"Reason\":\"Timeout\"}"` |

## 技术架构

### 项目结构

```
Tools/ZakYip.WheelDiverterSorter.Tools.Reporting/
├── Models/                      # 数据模型（DTOs）
│   ├── ParcelTraceLogRecord.cs  # 日志记录 DTO
│   ├── TimeBucketStatistics.cs  # 时间片统计结果
│   ├── OverloadReasonStatistics.cs
│   ├── ChuteErrorStatistics.cs
│   └── NodeErrorStatistics.cs
├── Analyzers/                   # 分析引擎
│   ├── LogParser.cs             # 日志解析器
│   └── TraceStatisticsAnalyzer.cs # 统计分析器
├── Writers/                     # 报表生成器
│   └── ReportWriter.cs          # CSV & Markdown 生成
└── Program.cs                   # 命令行入口
```

### 依赖项

- **ZakYip.WheelDiverterSorter.Core**：公共枚举和类型定义
- **ZakYip.Sorting.Core**：OverloadReason 枚举
- **System.Text.Json**：JSON 解析（.NET 标准库）

### 设计原则

1. **不依赖运行时服务**：不注入 DI，不依赖 Host / Execution 层
2. **纯内存分析**：一次性加载所有记录到内存进行分析
3. **容错优先**：解析失败不中断整体流程
4. **可扩展性**：统计维度可方便扩展

## 参考资料

- [PR-10: 包裹追踪日志实现文档](../PR10_PARCEL_TRACE_LOGGING.md)
- [系统架构文档](../README.md)
- [Overload 策略文档](../PR08_OVERLOAD_IMPLEMENTATION_SUMMARY.md)
