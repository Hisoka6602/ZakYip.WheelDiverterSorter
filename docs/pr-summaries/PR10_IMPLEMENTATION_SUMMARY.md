# PR-10 实现总结：包裹追踪日志（Parcel Trace Logging）

## 实现概述

本 PR 成功实现了完整的包裹审计追踪日志系统，为每个包裹建立从入口到出口的完整时间线记录。

## 实现内容

### 1. Core 层：抽象定义

**文件位置**：`ZakYip.WheelDiverterSorter.Core/Tracing/`

#### 新增文件：
- `IParcelTraceSink.cs` - 追踪日志写入接口
- `ParcelTraceEventArgs.cs` - 追踪事件数据模型

**设计原则**：
- 仅定义写入接口，不提供查询功能
- 异常安全：WriteAsync 必须捕获所有异常
- 结构化数据：包含 ItemId、Stage、Source、Details 等关键字段

### 2. Observability 层：基础设施实现

**文件位置**：`ZakYip.WheelDiverterSorter.Observability/Tracing/`

#### 新增文件：
1. **FileBasedParcelTraceSink.cs** (实现 IParcelTraceSink)
   - 使用 ILogger 写入日志
   - 结构化 JSON 格式输出
   - 异常捕获与降级处理

2. **ILogCleanupPolicy.cs** - 日志清理策略接口

3. **DefaultLogCleanupPolicy.cs** - 默认清理策略实现
   - 按保留天数删除旧文件
   - 按总大小上限删除最旧文件
   - 中文日志记录清理操作

4. **LogCleanupHostedService.cs** - 后台清理服务
   - 启动时立即执行一次清理
   - 定期执行（默认每 24 小时）
   - 优雅关闭支持

5. **LogCleanupOptions.cs** - 配置选项
   - LogDirectory: 日志根目录
   - RetentionDays: 保留天数（默认 14 天）
   - MaxTotalSizeMb: 大小上限（默认 1024 MB）
   - CleanupIntervalHours: 清理间隔（默认 24 小时）

6. **ObservabilityServiceExtensions.cs** (扩展)
   - AddParcelTraceLogging() - 注册追踪服务
   - AddLogCleanup() - 注册清理服务

### 3. Host 层：集成与配置

#### 修改文件：

**Program.cs**
- 注册 IParcelTraceSink 服务
- 注册 LogCleanupHostedService
- 绑定配置选项

**appsettings.json**
- 新增 LogCleanup 配置节
- 配置保留策略和清理间隔

**nlog.config**
- 新增 parceltrace target
- 专用路径：logs/trace/parcel-trace-{date}.log
- JSON 格式输出
- 每日滚动，保留 14 份归档

### 4. Execution 层：埋点实现

**修改文件**：`ParcelSortingOrchestrator.cs`

#### 追踪点：
1. **Created** - 包裹在入口光电创建时
2. **UpstreamAssigned** - 上游系统返回目标格口
3. **RoutePlanned** - 路径规划完成
4. **OverloadDecision** - 触发超载策略（入口或路由阶段）
5. **Diverted** - 正常落格成功
6. **ExceptionDiverted** - 异常口落格

每个追踪点包含：
- ItemId：包裹唯一标识
- OccurredAt：UTC 时间戳
- Stage：事件阶段名称
- Source：事件来源（Ingress/Upstream/Execution/OverloadPolicy）
- Details：阶段关键参数（JSON 格式字符串）

### 5. 测试

**文件位置**：`ZakYip.WheelDiverterSorter.Observability.Tests/`

#### 新增测试文件：
1. **FileBasedParcelTraceSinkTests.cs** (6 个测试)
   - 正常写入测试
   - 空字段处理测试
   - 异常不传播测试
   - 异常记录 Warning 测试
   - 构造函数参数验证

2. **DefaultLogCleanupPolicyTests.cs** (6 个测试)
   - 目录不存在测试
   - 删除过期文件测试
   - 超大小限制删除测试
   - 无需清理测试
   - 取消令牌测试
   - 构造函数参数验证

**测试结果**：12/12 通过 ✅

### 6. 文档

**PR10_PARCEL_TRACE_LOGGING.md**
- 功能概述与特性说明
- 日志事件类型详解
- 文件路径与格式说明
- 配置指南
- 使用场景与示例
- 架构设计说明
- 性能考虑
- 故障排查指南

## 验收标准

| 标准 | 状态 | 说明 |
|------|------|------|
| 长时间运行不占满磁盘 | ✅ | LogCleanupHostedService 自动清理 |
| 异常包裹有完整记录 | ✅ | 6 个关键节点全覆盖 |
| 写入失败不影响分拣 | ✅ | FileBasedParcelTraceSink 捕获所有异常 |
| JSON 格式便于分析 | ✅ | NLog JSON 格式输出 |
| 严格分层架构 | ✅ | Core → Observability → Host |
| 单元测试覆盖 | ✅ | 12 个测试全部通过 |
| 安全扫描 | ✅ | CodeQL 0 alerts |

## 技术亮点

### 1. 异常安全设计
```csharp
public ValueTask WriteAsync(ParcelTraceEventArgs eventArgs, ...)
{
    try
    {
        _logger.LogInformation("ParcelTrace {@Trace}", ...);
    }
    catch (Exception ex)
    {
        // 仅记录 Warning，不向上抛出
        _logger.LogWarning(ex, "写入包裹追踪日志失败...");
    }
    return ValueTask.CompletedTask;
}
```

### 2. 自动清理机制
- 双重保护：按天数 + 按大小
- 优先删除最旧文件
- 定时执行 + 启动时执行
- 中文日志便于监控

### 3. 结构化日志
```json
{
  "ItemId": 123456789,
  "OccurredAt": "2025-01-18T03:45:12.345Z",
  "Stage": "RoutePlanned",
  "Source": "Execution",
  "Details": "TargetChuteId=5, SegmentCount=3, EstimatedTimeMs=2500"
}
```

### 4. 依赖注入设计
- 可选依赖：IParcelTraceSink? 允许为 null
- 服务注册：通过扩展方法统一管理
- 配置绑定：Options 模式支持运行时配置

## 使用示例

### 查询特定包裹的完整轨迹
```bash
grep '"ItemId":123456789' logs/trace/parcel-trace-*.log | jq -s 'sort_by(.OccurredAt)'
```

### 统计当天异常率
```bash
# 异常包裹数
grep -c '"Stage":"ExceptionDiverted"' logs/trace/parcel-trace-$(date +%Y-%m-%d).log

# 总包裹数
grep -c '"Stage":"Created"' logs/trace/parcel-trace-$(date +%Y-%m-%d).log
```

### 查找所有超载决策
```bash
grep '"Stage":"OverloadDecision"' logs/trace/parcel-trace-*.log | jq '.Details'
```

## 后续优化方向

### 1. 采样策略（可选）
- 正常包裹采样 1-10%
- 异常包裹 100% 记录
- 可配置采样率

### 2. 结构化查询（可选）
- 集成 ElasticSearch 或 Loki
- 提供 Grafana 可视化
- 实时告警支持

### 3. 性能优化（可选）
- 异步批量写入
- 内存缓冲区
- 压缩归档

## 总结

PR-10 成功实现了完整的包裹追踪日志系统，具备以下特点：

✅ **完整性**：覆盖包裹生命周期所有关键节点
✅ **安全性**：异常隔离，不影响主流程
✅ **可维护性**：自动清理，防止磁盘占满
✅ **可观测性**：JSON 格式，便于分析和排障
✅ **可测试性**：12 个单元测试，100% 通过
✅ **文档完善**：使用指南、配置说明、故障排查齐全

该实现严格遵循分层架构，代码质量高，可直接用于生产环境。
