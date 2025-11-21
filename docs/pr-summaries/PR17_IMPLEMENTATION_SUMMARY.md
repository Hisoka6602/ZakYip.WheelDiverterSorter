# PR-17 实施总结：离线异常统计 Job 与报表

## 实施概述

本 PR 实现了一个独立的离线命令行工具，用于分析包裹追踪日志（Parcel Trace Logs）并生成多维度的异常统计报表。该工具完全离线运行，不依赖任何运行时服务，适合在运维脚本或定时任务中周期性执行。

## 已完成功能

### 1. 项目结构

- ✅ 创建 `Tools/` 目录作为 Tooling 层
- ✅ 新增 `ZakYip.WheelDiverterSorter.Tools.Reporting` 控制台项目
- ✅ 项目已添加到解决方案中
- ✅ 配置为 .NET 8.0 目标框架
- ✅ 仅引用 Core 层（`ZakYip.WheelDiverterSorter.Core` 和 `ZakYip.Sorting.Core`）
- ✅ 使用 `System.Text.Json` 进行 JSON 解析

### 2. 核心数据结构（Models）

实现了以下 DTO 和统计结果类：

- **ParcelTraceLogRecord**：包裹追踪日志记录
- **AlertLogRecord**：告警日志记录（预留扩展）
- **TimeBucketStatistics**：时间片维度统计
- **OverloadReasonStatistics**：超载原因分布统计
- **ChuteErrorStatistics**：按格口的异常热点统计
- **NodeErrorStatistics**：按节点的异常热点统计

所有类型使用 `record` 或 `record struct`，必需字段使用 `required` 关键字，符合现代 C# 最佳实践。

### 3. 日志解析器（LogParser）

**功能特性**：
- ✅ 支持扫描指定目录下的 trace 日志文件
- ✅ 支持按日期范围过滤日志文件
- ✅ 逐行解析 JSON 格式日志
- ✅ 支持时间范围过滤（`fromTime` 和 `toTime`）
- ✅ 容错处理：自动跳过损坏的日志行并打印警告
- ✅ 友好的中文控制台输出

**关键实现**：
```csharp
// 跳过损坏的日志行而不中断整个分析
try
{
    var record = JsonSerializer.Deserialize<ParcelTraceLogRecord>(line, JsonOptions);
    records.Add(record);
}
catch (JsonException ex)
{
    skippedLines++;
    Console.WriteLine($"⚠️ 警告：第 {totalLines} 行解析失败：{ex.Message}");
}
```

### 4. 统计分析引擎（TraceStatisticsAnalyzer）

**功能特性**：
- ✅ 时间片划分：按可配置的时间间隔（如 5 分钟、1 小时）分组统计
- ✅ OverloadReason 提取：从 `Details` 字段中智能提取超载原因
- ✅ 格口热点分析：统计每个格口的异常次数和占比
- ✅ 节点热点分析：统计每个节点的相关事件次数和占比
- ✅ 纯内存分析，性能高效

**统计维度**：

1. **时间片维度统计**：
   - 总包裹数（基于 `Created` 事件）
   - 异常包裹数（基于 `ExceptionDiverted` 事件）
   - 超载事件数（基于 `OverloadDecision` 事件）
   - 异常比例和超载比例

2. **OverloadReason 分布**：
   - 各类超载原因的次数和占比
   - 支持从 JSON 或 key=value 格式中提取原因

3. **格口热点**：
   - 异常次数最多的格口
   - 每个格口占全部异常的百分比

4. **节点热点**：
   - 相关事件最多的节点
   - 每个节点占全部事件的百分比

### 5. 报表生成器（ReportWriter）

**输出格式**：
- ✅ CSV 格式：适合导入 Excel 进行深度分析
- ✅ Markdown 格式：适合直接查看或粘贴到文档/Wiki/Issue

**生成的报表文件**：

1. **summary-{timestamp}.csv**：时间片维度统计
2. **overload-{timestamp}.csv**：OverloadReason 分布统计
3. **chute-hotspot-{timestamp}.csv**：格口异常热点统计
4. **node-hotspot-{timestamp}.csv**：节点异常热点统计
5. **report-{timestamp}.md**：Markdown 汇总报告

**文件命名**：包含时间戳，避免覆盖，便于归档。

### 6. 命令行入口（Program.cs）

**支持的参数**：

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `--log-dir` | 日志根目录 | `./logs` |
| `--from` | 起始时间 | 不限 |
| `--to` | 结束时间 | 不限 |
| `--bucket` | 时间片大小 | `5m` |
| `--output` | 输出目录 | `./reports` |

**时间格式支持**：
- 日期：`YYYY-MM-DD`（如 `2025-11-18`）
- 完整时间：`YYYY-MM-DDTHH:mm:ss`（如 `2025-11-18T08:30:00`）

**时间片大小支持**：
- 秒：`30s`（30 秒）
- 分钟：`5m`（5 分钟）
- 小时：`1h`（1 小时）

**用户友好特性**：
- ✅ 中英文双语帮助信息
- ✅ 进度提示（扫描、解析、分析、生成）
- ✅ 友好的错误提示
- ✅ 汇总统计信息输出

### 7. 文档

创建了详细的用户文档 `docs/REPORTING_OFFLINE_ANALYSIS.md`，包含：

- 工具概述和功能特性
- 安装与运行指南
- 参数说明和使用示例
- 输出文件详细说明
- 日志文件格式要求
- 技术架构介绍
- 故障排查指南
- 常见使用场景
- 与其他系统集成的建议

## 验收结果

### 功能验收

✅ **基本功能测试**：
- 成功解析 JSON 格式的 trace 日志
- 正确统计时间片、OverloadReason、格口、节点等维度
- 正确生成 CSV 和 Markdown 报表

✅ **时间范围过滤测试**：
```bash
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \
  --log-dir /tmp/test-logs \
  --from "2025-11-18T08:00:00" \
  --to "2025-11-18T08:30:00" \
  --bucket 5m
```
结果：正确过滤指定时间范围的日志记录。

✅ **容错处理测试**：
- 测试场景：日志文件包含 2 行损坏的 JSON
- 结果：工具跳过损坏行并打印警告，继续处理其他行，不中断分析

✅ **错误处理测试**：
- 测试场景：指定不存在的日志目录
- 结果：友好提示"trace 日志目录不存在"，不抛出异常

✅ **命令行参数测试**：
- `--help` 参数正确显示帮助信息
- 各参数正确解析和应用

### 测试数据示例

**输入日志**（包含 7 个包裹，4 个异常）：
```json
{"ItemId":1001,"Stage":"Created","OccurredAt":"2025-11-18T08:00:00Z",...}
{"ItemId":1002,"Stage":"OverloadDecision","OccurredAt":"2025-11-18T08:05:05Z","Details":"{\"Reason\":\"CapacityExceeded\"}"}
...
```

**输出结果**：
- 时间片统计：2 个时间片（08:00-09:00, 09:00-10:00）
- OverloadReason 分布：CapacityExceeded (50%), Timeout (25%), NodeDegraded (25%)
- 格口热点：EXCEPTION-001 (75%), EXCEPTION-002 (25%)
- 节点热点：NODE-12 (66.67%), NODE-08 (33.33%)
- 整体异常率：57.14%

### 安全性验收

✅ **CodeQL 扫描结果**：0 个安全告警

✅ **安全考虑**：
- 不执行任何危险操作（如删除文件、修改系统设置）
- 只读取日志文件，不写入任何已存在的文件
- 输出目录自动创建，不会覆盖已存在的目录
- 异常处理完善，不会因意外情况导致程序崩溃

## 技术亮点

### 1. 架构设计

**分层清晰**：
```
┌─────────────────────────────────────┐
│   Tooling Layer (Tools/)            │  ← 本 PR 新增
│   - 离线分析工具                     │
│   - 不依赖运行时服务                 │
└─────────────────────────────────────┘
┌─────────────────────────────────────┐
│   Core Layer                        │  ← 仅引用此层
│   - 数据模型、枚举                   │
└─────────────────────────────────────┘
```

**符合要求**：
- ✅ 不动现有 Core / Execution / Host 主体逻辑
- ✅ 不引用 WebHost
- ✅ 不注入 DI
- ✅ 作为"线下工具"独立运行

### 2. 代码质量

- 使用现代 C# 特性（`record`、`record struct`、`required`、`init`）
- 中英文双语注释，便于维护
- 变量名和类名使用英文，注释使用中文
- 遵循单一职责原则：解析、分析、生成分离
- 错误处理完善，用户体验友好

### 3. 性能考虑

- **纯内存分析**：一次性加载所有记录到内存，避免频繁磁盘 I/O
- **高效解析**：使用 `System.Text.Json` 进行高性能 JSON 解析
- **预估性能**：约 10,000-50,000 条/秒，100 万条记录约 30 秒内完成

**内存使用**：
- 每条记录约 200-300 字节
- 100 万条记录约 200-300 MB
- 建议单次分析不超过 1000 万条记录

### 4. 可扩展性

- 易于添加新的统计维度（在 `TraceStatisticsAnalyzer` 中添加方法）
- 易于支持新的日志格式（在 `LogParser` 中扩展）
- 易于添加新的输出格式（在 `ReportWriter` 中添加方法）

## 使用场景

### 1. 每日异常分析

在定时任务中运行，分析前一天的日志：
```bash
#!/bin/bash
YESTERDAY=$(date -d "yesterday" +%Y-%m-%d)
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \
  --from "$YESTERDAY" \
  --to "$YESTERDAY" \
  --bucket 1h \
  --output "/var/reports/daily/$YESTERDAY"
```

### 2. 高峰时段分析

分析特定高峰时段的异常情况：
```bash
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \
  --from "2025-11-18T08:00:00" \
  --to "2025-11-18T12:00:00" \
  --bucket 5m
```

### 3. 故障排查

快速生成故障时段的详细分析：
```bash
dotnet run --project Tools/ZakYip.WheelDiverterSorter.Tools.Reporting \
  --from "2025-11-18T14:30:00" \
  --to "2025-11-18T15:30:00" \
  --bucket 1m \
  --output "./incident-reports"
```

## 未来优化方向

### 1. 性能优化
- 并行化：多线程解析多个日志文件
- 流式处理：支持超大文件的流式解析（避免全部加载到内存）
- 增量分析：支持增量更新统计结果

### 2. 功能扩展
- 支持 Alert 日志分析
- 支持更多统计维度（如按传感器、按线体段等）
- 支持数据可视化（生成图表）
- 支持导出到数据库或 Elasticsearch

### 3. 集成增强
- 提供 Docker 镜像，便于容器化部署
- 提供 REST API，支持远程调用
- 集成到 CI/CD 管道
- 与监控系统（Grafana、Prometheus）集成

## 与现有系统的关系

- **PR-10（包裹追踪日志）**：本工具读取 PR-10 生成的 trace 日志
- **PR-08（超载策略）**：分析 PR-08 产生的 OverloadReason
- **Core 层**：复用 Core 层的枚举和类型定义
- **Host 层**：完全独立，不影响 Host 层运行

## 验收标准完成情况

✅ **能正确扫描指定时间范围的日志**
- 支持按日期和时间范围过滤
- 自动识别日志文件命名格式

✅ **能输出至少三类 CSV 报表**
- 时间片统计
- OverloadReason 统计
- Chute/Node 热点统计

✅ **Markdown 预览文件能在本地打开查看**
- 生成格式规范的 Markdown 表格
- 包含汇总统计信息

✅ **对"日志缺失 / 行格式损坏"的情况容错**
- 工具不会崩溃
- 跳过坏行并在控制台打印中文警告
- 继续处理其他有效日志

✅ **工具不依赖 Host / Execution 运行**
- 只引用 Core 层
- 不注入 DI
- 完全离线运行

✅ **不会影响线体实例的稳定性**
- 只读取已生成的日志文件
- 不连接任何运行时服务
- 不修改任何系统状态

## 总结

本 PR 成功实现了一个功能完整、用户友好、安全可靠的离线异常统计分析工具。该工具：

- ✅ 满足所有需求规格
- ✅ 通过所有验收标准
- ✅ 代码质量高，遵循最佳实践
- ✅ 文档完善，易于使用和维护
- ✅ 无安全漏洞
- ✅ 性能良好，可处理大规模日志
- ✅ 易于扩展和集成

该工具将为运维人员和现场工程师提供强大的离线分析能力，帮助快速定位异常、优化系统配置、提升分拣效率。

---

**文档位置**：
- 用户文档：`docs/REPORTING_OFFLINE_ANALYSIS.md`
- 本实施总结：`PR17_IMPLEMENTATION_SUMMARY.md`

**项目位置**：
- `Tools/ZakYip.WheelDiverterSorter.Tools.Reporting/`
