# PR-41: 性能基线与指标定义

## 概述

本文档定义了系统的性能基线指标和可接受范围，用于监控和评估系统性能表现。

## 核心性能指标

### 1. 包裹处理时延 (Parcel Processing Latency)

**定义**: 从包裹进入系统（入口传感器检测）到分拣完成（落格确认）的总时间。

**Prometheus 指标**: `sorter_parcel_e2e_latency_seconds`

**关键百分位数**:
- **P50 (中位数)**: 正常情况下 50% 的包裹应在此时间内完成分拣
- **P95**: 95% 的包裹应在此时间内完成分拣
- **P99**: 99% 的包裹应在此时间内完成分拣

**可接受范围** (基于系统配置和硬件能力):

| 场景 | P50 | P95 | P99 | 备注 |
|------|-----|-----|-----|------|
| 低负载 (< 500包/分) | < 3s | < 5s | < 8s | 理想条件 |
| 中负载 (500-800包/分) | < 5s | < 10s | < 15s | 正常生产负载 |
| 高负载 (800-1000包/分) | < 8s | < 15s | < 25s | 峰值负载 |
| 超载 (> 1000包/分) | < 15s | < 30s | < 60s | 需要节流 |

**告警阈值**:
- ⚠️ Warning: P95 > 15s
- 🚨 Critical: P99 > 30s

### 2. 执行循环耗时 (Execution Loop Duration)

**定义**: 系统执行管线每个循环周期的耗时。

**Prometheus 指标**: `sorter_execution_loop_duration_seconds`

**可接受范围**:
- **平均值**: < 50ms
- **最大值**: < 500ms (偶发峰值)
- **P95**: < 100ms

**告警阈值**:
- ⚠️ Warning: 平均值 > 100ms
- 🚨 Critical: 平均值 > 200ms 或 P95 > 500ms

### 3. CPU 使用率 (CPU Usage)

**定义**: 进程的 CPU 使用率（百分比）。

**Prometheus 指标**: `sorter_cpu_usage_percent`

**可接受范围**:
- **正常运行**: < 60%
- **高负载**: 60-80%
- **告警**: > 80%

**告警阈值**:
- ⚠️ Warning: 持续 5 分钟 > 70%
- 🚨 Critical: 持续 5 分钟 > 85%

### 4. 内存使用 (Memory Usage)

**定义**: 进程使用的私有内存（字节）。

**Prometheus 指标**:
- `sorter_memory_usage_bytes`: 私有内存
- `sorter_working_set_bytes`: 工作集
- `sorter_managed_heap_bytes`: 托管堆

**可接受范围** (假设 8GB 可用内存):
- **私有内存**: < 2GB
- **工作集**: < 3GB
- **托管堆**: < 1GB

**内存泄漏检测**:
- 在长时间运行（> 4小时）后，内存增长应趋于平稳
- 持续线性增长 > 50MB/小时 需要调查

**告警阈值**:
- ⚠️ Warning: 私有内存 > 2GB
- 🚨 Critical: 私有内存 > 4GB 或持续增长

### 5. GC 回收频率 (GC Collection Frequency)

**定义**: 垃圾回收器各代的回收次数。

**Prometheus 指标**: `sorter_gc_collection_total{generation="gen0|gen1|gen2"}`

**可接受范围** (每小时):
- **Gen0**: < 3600 次 (平均每秒 < 1次)
- **Gen1**: < 360 次 (平均每 10 秒 < 1次)
- **Gen2**: < 36 次 (平均每分钟 < 1次)

**优化目标**:
- 减少临时对象分配以降低 Gen0 回收频率
- 避免长期引用导致的 Gen2 回收

**告警阈值**:
- ⚠️ Warning: Gen2 回收 > 100次/小时
- 🚨 Critical: Gen2 回收 > 300次/小时

## 性能测试场景

### 基准测试 (Baseline Test)

**目的**: 建立性能基线

**条件**:
- 无混沌注入
- 轻度摩擦（0.95-1.05）
- 500 包裹/分钟
- 持续 30 分钟

**预期结果**:
- P95 延迟 < 10s
- P99 延迟 < 15s
- CPU < 50%
- 无内存泄漏

### 压力测试 (Stress Test)

**目的**: 验证高负载性能

**条件**:
- 无混沌注入
- 中度摩擦（0.90-1.10）
- 1000 包裹/分钟
- 持续 2 小时

**预期结果**:
- P95 延迟 < 15s
- P99 延迟 < 25s
- CPU < 70%
- 内存增长 < 100MB/小时

### 混沌测试 (Chaos Test)

**目的**: 验证系统韧性

**条件**:
- 中度混沌注入
- 中度摩擦
- 600 包裹/分钟
- 持续 30 分钟

**预期结果**:
- 系统不崩溃
- SafeExecutor 捕获所有混沌异常
- P99 延迟 < 60s
- 日志量可控（< 1000条/分钟）

## 监控和查询

### Prometheus 查询示例

#### 包裹处理时延百分位数
```promql
# P50
histogram_quantile(0.50, rate(sorter_parcel_e2e_latency_seconds_bucket[5m]))

# P95
histogram_quantile(0.95, rate(sorter_parcel_e2e_latency_seconds_bucket[5m]))

# P99
histogram_quantile(0.99, rate(sorter_parcel_e2e_latency_seconds_bucket[5m]))
```

#### CPU 使用率
```promql
sorter_cpu_usage_percent
```

#### 内存使用
```promql
# MB
sorter_memory_usage_bytes / 1024 / 1024
```

#### GC 回收速率（每分钟）
```promql
rate(sorter_gc_collection_total[1m]) * 60
```

#### 执行循环平均耗时
```promql
rate(sorter_execution_loop_duration_seconds_sum[5m]) / rate(sorter_execution_loop_duration_seconds_count[5m])
```

## Grafana 仪表板

建议创建以下 Grafana 面板:

1. **包裹处理时延**: 显示 P50/P95/P99 曲线
2. **系统资源**: CPU 和内存使用率
3. **GC 统计**: Gen0/Gen1/Gen2 回收频率
4. **执行循环**: 平均和最大耗时
5. **吞吐量**: 包裹处理速率（包/分钟）
6. **错误率**: 失败包裹百分比

## 性能优化建议

### 当 CPU 使用率过高时
1. 使用 BenchmarkDotNet 识别热点代码路径
2. 减少不必要的计算和循环
3. 考虑使用 `Span<T>` 和 `Memory<T>` 减少分配
4. 优化频繁调用的方法

### 当内存使用率过高时
1. 检查是否有对象泄漏（事件订阅、缓存等）
2. 使用对象池减少分配
3. 及时释放大对象
4. 使用 `ArrayPool<T>` 管理临时数组

### 当 GC 频率过高时
1. 减少临时对象分配
2. 避免 `ToList()`, `ToArray()` 等非必要转换
3. 使用 `readonly struct` 和 `in` 参数
4. 考虑使用 `stackalloc` 处理小数组

### 当延迟过高时
1. 检查是否有同步阻塞操作
2. 优化数据库查询和网络调用
3. 调整并发控制参数
4. 检查硬件响应时间

## 性能回归检测

### 持续集成中的性能测试
建议在 CI 流程中定期运行基准测试，并比较结果:

```bash
# 运行基准测试
cd tests/ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release

# 比较结果
# 使用 BenchmarkDotNet 的结果比较功能
```

### 性能回归标准
如果以下任何指标相比基线退化 > 20%，应当调查:
- 包裹处理 P95 延迟
- 执行循环平均耗时
- CPU 使用率
- 内存分配量

## 附录: 硬件配置参考

本性能基线基于以下硬件配置:
- **CPU**: 4 核心 (或等效)
- **内存**: 8 GB RAM
- **存储**: SSD
- **网络**: 1 Gbps

不同硬件配置下，可接受范围需要相应调整。
