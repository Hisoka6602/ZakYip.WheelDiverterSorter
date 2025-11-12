# 性能优化指南

本文档详细说明了摆轮分拣系统中实施的性能优化措施。

## 概述

系统性能目标：
- **吞吐量**: 500-1000 包裹/分钟 (8-17 RPS)
- **延迟**: P95 < 500ms, P99 < 1000ms
- **分拣时长**: < 100ms (核心操作)
- **错误率**: < 5%
- **可用性**: 99.9%

## 实施的优化措施

### 1. 性能基准测试 (BenchmarkDotNet)

#### 位置
`ZakYip.WheelDiverterSorter.Benchmarks/`

#### 功能
- **PathGenerationBenchmarks**: 测试路径生成性能
  - 单段路径生成
  - 多段路径生成
  - 批量路径生成
  - 未知格口处理
  
- **PathExecutionBenchmarks**: 测试路径执行性能
  - 单段路径执行
  - 多段路径执行
  - 并发批量执行

#### 运行方法

```bash
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release
```

#### 输出指标
- **Mean**: 平均执行时间
- **Error**: 测量误差
- **StdDev**: 标准差
- **Gen0/Gen1/Gen2**: GC 代数统计
- **Allocated**: 内存分配量

#### 性能目标
| 操作 | 目标时间 | 备注 |
|-----|---------|------|
| 路径生成 (单段) | < 0.5ms | 最简单场景 |
| 路径生成 (两段) | < 1ms | 常见场景 |
| 路径生成 (批量100) | < 100ms | 批处理 |
| 路径执行 (单段) | < 50ms | 模拟执行 |
| 路径执行 (两段) | < 100ms | 模拟执行 |

### 2. 性能指标监控 (Metrics)

#### 位置
`ZakYip.WheelDiverterSorter.Host/Services/SorterMetrics.cs`

#### 指标类型

##### 计数器 (Counters)
- `sorter.requests.total`: 分拣请求总数
- `sorter.requests.success`: 成功次数
- `sorter.requests.failure`: 失败次数
- `sorter.path_generation.total`: 路径生成次数
- `sorter.path_execution.total`: 路径执行次数

##### 直方图 (Histograms)
- `sorter.requests.duration`: 请求处理时长分布
- `sorter.path_generation.duration`: 路径生成时长分布
- `sorter.path_execution.duration`: 路径执行时长分布

##### 计量器 (Gauges)
- `sorter.requests.active`: 当前活跃请求数

#### 集成方式

```csharp
// Program.cs
builder.Services.AddMetrics();
builder.Services.AddSingleton<SorterMetrics>();

// 在服务中使用
public class MyService
{
    private readonly SorterMetrics _metrics;
    
    public MyService(SorterMetrics metrics)
    {
        _metrics = metrics;
    }
    
    public async Task SortAsync()
    {
        _metrics.RecordSortingRequest();
        var sw = Stopwatch.StartNew();
        
        try
        {
            // ... 执行分拣 ...
            _metrics.RecordSortingSuccess(sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            _metrics.RecordSortingFailure(sw.Elapsed.TotalMilliseconds);
        }
    }
}
```

#### 导出指标

系统使用 .NET 的标准 Metrics API (System.Diagnostics.Metrics)，兼容以下导出器：

- **Prometheus**: 使用 `OpenTelemetry.Exporter.Prometheus.AspNetCore`
- **OpenTelemetry**: 使用 `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- **Console**: 使用 `OpenTelemetry.Exporter.Console` (调试用)

### 3. 路径缓存 (Path Caching)

#### 位置
`ZakYip.WheelDiverterSorter.Host/Services/CachedSwitchingPathGenerator.cs`

#### 功能
- 使用 IMemoryCache 缓存已生成的路径
- LRU (Least Recently Used) 淘汰策略
- 可配置的缓存过期时间 (默认5分钟)
- 支持缓存失效 (配置变更时)

#### 配置

```json
{
  "Performance": {
    "EnablePathCaching": true,
    "PathCacheDurationMinutes": 5
  }
}
```

#### 缓存命中率

通过日志可以查看缓存命中情况：
- 命中: `路径缓存命中: CHUTE_A`
- 未命中: `路径缓存未命中: CHUTE_A`

#### 缓存失效

当配置发生变更时，应主动失效相关缓存：

```csharp
// 失效单个格口缓存
cachedGenerator.InvalidateCache("CHUTE_A");

// 失效所有缓存
cachedGenerator.InvalidateAllCache();
```

### 4. 对象池 (Object Pooling)

#### 位置
`ZakYip.WheelDiverterSorter.Host/Services/OptimizedSortingService.cs`

#### 实现
- 使用 `ArrayPool<T>` 减少数组分配
- 避免短生命周期对象的频繁创建
- 减少 GC 压力

#### 示例

```csharp
// 使用 ArrayPool 租借数组
var buffer = ArrayPool<char>.Shared.Rent(size);
try
{
    // 使用 buffer
}
finally
{
    // 归还到池中
    ArrayPool<char>.Shared.Return(buffer);
}
```

#### 配置

```json
{
  "Performance": {
    "EnableObjectPooling": true
  }
}
```

### 5. 负载测试 (k6)

#### 位置
`performance-tests/`

#### 测试脚本

1. **smoke-test.js**: 冒烟测试
   - 单虚拟用户
   - 验证基本功能
   - 用于快速回归测试

2. **load-test.js**: 负载测试
   - 逐步增加负载 (10→50→100 VUs)
   - 模拟真实场景
   - 验证性能目标

3. **stress-test.js**: 压力测试
   - 快速增加负载 (50→500 VUs)
   - 找到系统极限
   - 识别性能瓶颈

#### 运行方法

```bash
# 确保系统运行中
cd ZakYip.WheelDiverterSorter.Host
dotnet run

# 在另一个终端运行测试
cd performance-tests

# 冒烟测试 (1分钟)
k6 run smoke-test.js

# 负载测试 (7分钟)
k6 run load-test.js

# 压力测试 (12分钟)
k6 run stress-test.js

# 自定义基础URL
k6 run -e BASE_URL=http://your-server:port load-test.js
```

#### 性能阈值

| 测试类型 | P95延迟 | 错误率 | 备注 |
|---------|---------|--------|------|
| 冒烟测试 | < 200ms | < 1% | 基本功能验证 |
| 负载测试 | < 500ms | < 10% | 预期负载 |
| 压力测试 | < 1000ms | < 20% | 极限负载 |

## 性能监控最佳实践

### 1. 持续监控

在生产环境中启用指标收集：

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("ZakYip.WheelDiverterSorter");
        metrics.AddPrometheusExporter();
    });
```

### 2. 告警配置

在 Grafana/Prometheus 中配置告警：

```yaml
# 示例 Prometheus 告警规则
groups:
  - name: sorter_alerts
    rules:
      - alert: HighErrorRate
        expr: rate(sorter_requests_failure[5m]) / rate(sorter_requests_total[5m]) > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "分拣错误率过高"
          
      - alert: HighLatency
        expr: histogram_quantile(0.95, sorter_requests_duration) > 500
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "分拣延迟过高"
```

### 3. 定期基准测试

建议频率：
- **每日**: 冒烟测试 (CI/CD)
- **每周**: 负载测试
- **每月**: 压力测试
- **重大变更前**: 完整性能测试套件

### 4. 性能分析工具

当发现性能问题时，使用以下工具：

#### dotnet-trace
```bash
# 收集性能跟踪
dotnet-trace collect --process-id <PID> --duration 00:00:30

# 分析 .nettrace 文件
# 使用 PerfView、Visual Studio 或 speedscope.app
```

#### dotnet-counters
```bash
# 实时监控性能计数器
dotnet-counters monitor --process-id <PID>
```

#### dotnet-dump
```bash
# 捕获内存转储
dotnet-dump collect --process-id <PID>

# 分析转储
dotnet-dump analyze <dump-file>
```

## 优化检查清单

### 代码优化
- [ ] 使用 BenchmarkDotNet 测量热点代码
- [ ] 减少不必要的对象分配
- [ ] 使用 ArrayPool 和对象池
- [ ] 避免同步阻塞调用
- [ ] 使用 Span<T> 和 Memory<T>
- [ ] 优化字符串操作 (StringBuilder, 字符串驻留)

### 缓存优化
- [ ] 启用路径缓存
- [ ] 配置合理的缓存过期时间
- [ ] 实现缓存失效策略
- [ ] 监控缓存命中率

### 并发优化
- [ ] 使用并发控制服务
- [ ] 配置合理的并发限制
- [ ] 实现摆轮资源锁
- [ ] 避免死锁和竞态条件

### 数据库优化
- [ ] 为常用查询创建索引
- [ ] 使用连接池
- [ ] 实现查询结果缓存
- [ ] 避免 N+1 查询问题

### 监控优化
- [ ] 配置性能指标收集
- [ ] 设置合理的告警阈值
- [ ] 集成 Grafana 仪表板
- [ ] 启用分布式追踪 (OpenTelemetry)

## 故障排查

### 性能下降
1. 检查 CPU/内存使用率
2. 查看 GC 停顿时间
3. 分析慢查询日志
4. 检查缓存命中率
5. 运行性能基准测试对比

### 高延迟
1. 使用 dotnet-trace 分析热点
2. 检查是否有阻塞调用
3. 验证数据库查询性能
4. 检查网络延迟
5. 分析锁竞争情况

### 内存泄漏
1. 使用 dotnet-dump 捕获转储
2. 分析大对象堆
3. 检查事件订阅是否取消
4. 验证缓存是否正确淘汰
5. 检查静态字段引用

## 参考资源

- [BenchmarkDotNet 文档](https://benchmarkdotnet.org/articles/overview.html)
- [.NET Metrics API](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics)
- [k6 负载测试](https://k6.io/docs/)
- [.NET 性能最佳实践](https://learn.microsoft.com/en-us/dotnet/core/extensions/performance-best-practices)
- [Memory<T> 和 Span<T>](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/)
