# 场景 H：长时间运行稳定性（增强版）
# Scenario H: Long-Run Stability Enhancement

## 场景概述 / Scenario Overview

场景 H 是一个长时间稳定性测试场景，用于验证系统在**2-4 小时持续运行**、**万级包裹处理量**的情况下的稳定性、资源使用和性能表现。重点监控 SafeExecutor 可靠性、日志量控制、内存稳定性和 CPU 使用率。

Scenario H is a long-run stability test scenario designed to validate system stability, resource usage, and performance under **2-4 hours of continuous operation** with **10,000+ parcels**. Focus on monitoring SafeExecutor reliability, log volume control, memory stability, and CPU utilization.

## 场景参数 / Scenario Parameters

| 参数 | 值 | 说明 |
|------|-----|------|
| **运行时长** | 2-4 小时 | 可配置，默认 2 小时 |
| **包裹总数** | 10,000 - 50,000 件 | 根据运行时长调整 |
| **包裹频率** | 500 件/分钟 | 稳定负载（间隔 120ms） |
| **摩擦因子范围** | 0.85 - 1.15 | 现实摩擦（±15%） |
| **掉包概率** | 3% | 现实掉包率 |
| **线速** | 1000 mm/s | 标准传送带速度 |
| **监控采样间隔** | 60 秒 | 每分钟输出统计 |
| **异常口** | 999 | 默认异常格口ID |

## 测试目标 / Test Objectives

该场景验证系统在长时间运行下的以下特性：

### 1. SafeExecutor 可靠性 🛡️

**验证点**：
- 所有后台服务在长时间运行中无崩溃
- SafeExecutor 正确捕获并记录异常
- 无未捕获异常导致进程退出

**监控指标**：
- `safe_executor_executions_total{result}` - SafeExecutor 执行次数（成功/失败）
- `safe_executor_exceptions_caught_total{exception_type}` - 捕获的异常类型统计
- 进程存活时间（Process Uptime）

**预期行为**：
- ✅ 进程持续运行，无意外退出
- ✅ SafeExecutor 捕获异常但系统继续运行
- ✅ 日志记录所有捕获的异常，包含详细上下文

### 2. 日志量控制 📝

**验证点**：
- 日志去重机制有效，重复日志被抑制
- 日志文件大小增长可控
- 日志清理服务正常工作

**监控指标**：
- `log_deduplication_suppressed_total` - 日志去重抑制次数
- `log_files_size_bytes` - 日志文件总大小
- `log_cleanup_runs_total` - 日志清理运行次数
- 日志文件大小增长速率（MB/小时）

**预期行为**：
- ✅ 日志文件大小增长线性且可控（< 500MB/小时）
- ✅ 重复日志被去重（抑制率 > 50%）
- ✅ 旧日志文件按配置自动清理（默认保留 3 天）

### 3. 内存稳定性 💾

**验证点**：
- 无内存泄漏（内存增长稳定在合理范围）
- 工作集（WorkingSet）稳定
- GC 活动频率正常

**监控指标**：
- `process_working_set_bytes` - 进程工作集大小
- `process_private_memory_bytes` - 进程私有内存大小
- `dotnet_total_memory_bytes` - .NET 托管内存大小
- `dotnet_gc_collections_count{generation}` - GC 回收次数
- `dotnet_gc_pause_seconds` - GC 暂停时间

**预期行为**：
- ✅ 工作集在启动后 30 分钟内稳定（增长 < 20%）
- ✅ 无明显内存泄漏趋势（线性增长斜率接近 0）
- ✅ GC Gen2 回收频率合理（< 1次/分钟）
- ✅ GC 暂停时间短（P95 < 100ms）

### 4. CPU 稳定性 🖥️

**验证点**：
- CPU 使用率稳定
- 无 CPU 热点导致性能下降
- 线程池健康

**监控指标**：
- `process_cpu_seconds_total` - 进程 CPU 使用时间
- `threadpool_num_threads` - 线程池线程数
- `threadpool_pending_work_items` - 线程池待处理任务数
- CPU 使用率（%）

**预期行为**：
- ✅ CPU 使用率稳定（平均 < 50%，峰值 < 80%）
- ✅ 无 CPU 100% 持续占用
- ✅ 线程池线程数合理（< 100）
- ✅ 线程池无积压（待处理任务 < 10）

### 5. 吞吐量稳定性 📊

**验证点**：
- 每分钟成功分拣包裹数稳定
- 延迟不随时间显著增长
- 错误率保持低水平

**监控指标**：
- `sorting_total_parcels` - 总处理包裹数
- `sorting_success_parcels_total` - 成功分拣包裹数
- `sorting_success_latency_seconds` - 成功包裹延迟直方图
- `sorting_failed_parcels_total{reason}` - 失败包裹数（按原因）

**预期行为**：
- ✅ 吞吐量稳定（500 件/分钟 ±10%）
- ✅ P95 延迟稳定（< 180s）
- ✅ P99 延迟稳定（< 200s）
- ✅ 错误率稳定（< 10%）

## 监控采样计划 / Monitoring Sampling Plan

### 实时监控（每 60 秒）

| 指标分类 | 采样项 | 输出格式 |
|---------|--------|---------|
| **包裹统计** | 总数、成功、失败、错分 | 计数 + 成功率 |
| **内存** | WorkingSet, PrivateMemory, ManagedMemory | MB |
| **CPU** | 使用率、线程数 | %, Count |
| **日志** | 文件大小、去重次数 | MB, Count |
| **GC** | Gen0/1/2 回收次数、暂停时间 | Count, ms |

### 阶段性报告（每 30 分钟）

| 报告项 | 内容 |
|--------|------|
| **性能总结** | 吞吐量、延迟 P50/P95/P99 |
| **资源趋势** | 内存、CPU 增长趋势 |
| **异常汇总** | SafeExecutor 捕获的异常类型和次数 |
| **日志健康** | 日志大小、去重效果 |

### 最终报告（运行结束）

| 报告项 | 内容 |
|--------|------|
| **稳定性评估** | 运行时长、崩溃次数、异常次数 |
| **性能评估** | 平均吞吐量、延迟分布 |
| **资源评估** | 内存泄漏检测、CPU 热点分析 |
| **日志评估** | 日志量、清理效果 |
| **建议** | 优化建议和潜在风险点 |

## 如何运行 / How to Run

### 方法一：使用启动脚本（推荐）

```bash
# Linux/macOS
./performance-tests/run-scenario-h-long-run-stability.sh \
  --duration=7200 \
  --parcels=60000 \
  --monitor

# Windows PowerShell
.\performance-tests\run-scenario-h-long-run-stability.ps1 `
  -Duration 7200 `
  -Parcels 60000 `
  -Monitor
```

**参数说明**：
- `--duration`: 运行时长（秒，默认 7200 = 2 小时）
- `--parcels`: 包裹总数（默认根据时长自动计算）
- `--monitor`: 启用实时监控（每 60 秒输出统计）

### 方法二：手动运行 + 监控脚本

```bash
# 终端 1：启动仿真
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run -c Release -- \
  --Simulation:IsLongRunMode=true \
  --Simulation:LongRunDuration=02:00:00 \
  --Simulation:MaxLongRunParcels=60000 \
  --Simulation:ParcelInterval=00:00:00.120 \
  --Simulation:MetricsPushIntervalSeconds=60 \
  --Simulation:IsEnableRandomFriction=true \
  --Simulation:FrictionModel:MinFactor=0.85 \
  --Simulation:FrictionModel:MaxFactor=1.15 \
  --Simulation:IsEnableRandomDropout=true \
  --Simulation:DropoutModel:DropoutProbabilityPerSegment=0.03 \
  --Simulation:ExceptionChuteId=999 \
  --Simulation:IsPauseAtEnd=false

# 终端 2：启动监控脚本
./performance-tests/monitor-long-run.sh --pid={仿真进程PID}
```

### 方法三：集成测试（短时版）

```bash
cd ZakYip.WheelDiverterSorter.E2ETests
dotnet test --filter "DisplayName~ScenarioH_ShortRun"
# 注意：集成测试版本只运行 5-10 分钟，用于快速验证
```

## 监控脚本使用 / Monitoring Script Usage

### monitor-long-run.sh 功能

```bash
./performance-tests/monitor-long-run.sh --pid={PID} [选项]

选项：
  --pid           必需，仿真进程 PID
  --interval      采样间隔（秒，默认 60）
  --output        输出文件路径（默认 ./long-run-monitor-{timestamp}.log）
  --metrics-port  Prometheus 指标端口（默认 5000）
  --grafana-url   Grafana 仪表板 URL（可选）
```

### 监控输出示例

```
[2025-11-19 17:00:00] === 长跑监控报告 ===
运行时长: 00:30:15
包裹统计: 总数=15,075, 成功=14,250 (94.5%), 失败=825, 错分=0 ✅
内存使用: WorkingSet=256MB, Private=312MB, Managed=187MB
CPU 使用率: 42.3% (平均), 线程数=18
日志文件: 总大小=142MB, 去重抑制=1,247次
GC 统计: Gen0=45, Gen1=12, Gen2=2, 暂停时间=P95:34ms

[2025-11-19 17:01:00] === 长跑监控报告 ===
运行时长: 00:31:15
包裹统计: 总数=15,575, 成功=14,720 (94.5%), 失败=855, 错分=0 ✅
内存使用: WorkingSet=258MB (+2MB), Private=314MB (+2MB), Managed=189MB (+2MB)
CPU 使用率: 41.8% (平均), 线程数=18
日志文件: 总大小=148MB (+6MB/min), 去重抑制=1,298次 (+51)
GC 统计: Gen0=47 (+2), Gen1=12 (+0), Gen2=2 (+0), 暂停时间=P95:36ms
```

## 预期结果 / Expected Results

### 稳定性指标 / Stability Metrics

| 指标 | 预期值 | 说明 |
|------|--------|------|
| **进程崩溃** | 0 次 | 必须全程无崩溃 |
| **未捕获异常** | 0 次 | SafeExecutor 应捕获所有异常 |
| **运行时长** | 达到目标时长 | 完整运行无中断 |

### 性能指标 / Performance Metrics

| 指标 | 预期值 | 说明 |
|------|--------|------|
| **吞吐量** | 500 件/分钟 ±10% | 稳定处理速率 |
| **成功率** | > 90% | 包含 3% 掉包 |
| **P95 延迟** | < 180s | 95% 包裹 |
| **P99 延迟** | < 200s | 99% 包裹 |
| **错分率** | 0% | 零错分保证 ✅ |

### 资源指标 / Resource Metrics

| 指标 | 预期值 | 说明 |
|------|--------|------|
| **内存增长** | < 20% / 2小时 | 启动后稳定 |
| **CPU 平均** | < 50% | 持续运行 |
| **CPU 峰值** | < 80% | 偶发峰值 |
| **日志增长** | < 500MB / 小时 | 去重生效 |
| **GC Gen2** | < 1次 / 分钟 | 内存管理良好 |

## 故障排查 / Troubleshooting

### 问题 1：内存持续增长

**症状**：WorkingSet 每小时增长 > 100MB，无稳定趋势

**可能原因**：
- 内存泄漏（对象未释放）
- 缓存无限增长
- 事件订阅未取消

**排查步骤**：
1. 使用 dotMemory 分析内存快照
2. 检查是否有静态集合持续增长
3. 检查事件订阅是否正确取消
4. 检查缓存是否有过期策略

**相关代码检查点**：
- `IRouteTemplateCache` 是否有上限
- `NodeHealthRegistry` 是否清理过期节点
- 事件订阅 `+=` 是否有对应的 `-=`

### 问题 2：CPU 使用率过高

**症状**：CPU 持续 > 70%，影响性能

**可能原因**：
- 热点代码未优化
- 频繁的 GC
- 死循环或死锁
- 线程池饥饿

**排查步骤**：
1. 使用 dotnet-trace 分析 CPU 热点
2. 检查 GC 频率和暂停时间
3. 检查线程池待处理任务数
4. 查看是否有死锁日志

**优化建议**：
- 使用 `readonly struct` 减少分配
- 避免频繁的字符串拼接
- 使用 `ArrayPool<T>` 复用数组
- 异步方法使用 `ValueTask`

### 问题 3：日志文件过大

**症状**：日志文件增长 > 1GB/小时

**可能原因**：
- 日志去重未生效
- 日志级别过低（Debug/Trace）
- 高频错误日志
- 日志清理未运行

**排查步骤**：
1. 检查 `log_deduplication_suppressed_total` 指标
2. 查看日志级别配置（应为 Information 或更高）
3. 统计日志中的高频模式
4. 检查 `LogCleanupHostedService` 是否运行

**优化建议**：
- 确保 `ILogDeduplicator` 正确使用
- 调整日志级别到 Information
- 减少不必要的日志输出
- 配置日志轮转（按大小或时间）

### 问题 4：SafeExecutor 捕获大量异常

**症状**：`safe_executor_exceptions_caught_total` 快速增长

**可能原因**：
- 业务逻辑错误
- 外部依赖不稳定
- 配置错误

**排查步骤**：
1. 查看 `exception_type` 标签，统计异常类型
2. 查看日志中的异常详情和堆栈跟踪
3. 检查异常发生的代码位置
4. 验证配置和外部依赖

**注意**：
- SafeExecutor 捕获异常是正常的（防止崩溃）
- 但如果异常率 > 10%，说明有问题需要修复

## 验收标准 / Acceptance Criteria

✅ **稳定性要求**：
- 进程全程无崩溃
- 无未捕获异常导致进程退出
- SafeExecutor 正确捕获所有异常

✅ **功能要求**：
- `SortedToWrongChuteCount == 0`：零错分
- 成功率 > 90%
- 总包裹数 = 各状态之和

✅ **性能要求**：
- 吞吐量稳定（500 件/分钟 ±10%）
- P95 延迟 < 180s, P99 延迟 < 200s
- 延迟不随时间显著增长

✅ **资源要求**：
- 内存增长 < 20% / 2小时
- CPU 平均 < 50%
- 日志增长 < 500MB/小时
- GC Gen2 < 1次/分钟

## 与其他场景的对比 / Comparison with Other Scenarios

| 场景 | 运行时长 | 包裹数 | 验证重点 | 特点 |
|------|----------|--------|---------|------|
| A-E | 1-5 分钟 | 10-50 | 功能正确性 | 快速验证 |
| F | 5-10 分钟 | 500-1000 | 高负载 + 上游抖动 | 压力测试 |
| G | 3-5 分钟 | 100-500 | 多厂商兼容性 | 架构验证 |
| **H (长跑)** | **2-4 小时** | **10,000-50,000** | **长期稳定性** | **生产级验证** |

## 应用场景 / Use Cases

场景 H 模拟的是生产环境的真实运行状态：

### 1. 生产环境部署前验证 🚀
- 验证系统是否具备 7x24 运行能力
- 发现潜在的内存泄漏和性能问题
- 验证监控和告警是否有效

### 2. 重大变更后回归测试 🔄
- 每次重大变更后运行长跑测试
- 确保变更未引入稳定性问题
- 对比前后性能指标

### 3. 性能基准建立 📊
- 建立系统的性能基线
- 为后续优化提供对比依据
- 识别系统瓶颈

## 相关文件 / Related Files

- 场景定义：`ZakYip.WheelDiverterSorter.Simulation/Scenarios/ScenarioDefinitions.cs::CreateScenarioH()`
- 启动脚本：`performance-tests/run-scenario-h-long-run-stability.sh`
- 监控脚本：`performance-tests/monitor-long-run.sh`
- 配置文件：`ZakYip.WheelDiverterSorter.Simulation/appsettings.LongRun.json`
- SafeExecutor：`ZakYip.WheelDiverterSorter.Observability/Utilities/SafeExecutionService.cs`
- 日志去重：`ZakYip.WheelDiverterSorter.Observability/Utilities/LogDeduplicator.cs`

## 维护建议 / Maintenance Recommendations

- **定期运行**：每周或每次重大变更后运行一次
- **记录基线**：记录每次运行的关键指标，建立历史趋势
- **监控告警**：在 Grafana 中配置长跑监控仪表板和告警
- **分析报告**：每次运行后生成分析报告，识别问题和优化点
- **优化迭代**：根据长跑测试结果持续优化系统性能和稳定性

---

**场景版本：** v1.0  
**创建日期：** 2025-11-19  
**适用版本：** >= PR-39  
**依赖特性：** PR-37 (SafeExecutor, LogDeduplicator), 长跑模式配置
