# 性能基线记录 (Performance Baseline)

本文档记录了 ZakYip.WheelDiverterSorter 系统的性能基线数据，用于后续性能优化的对比和验收。

## 测试环境

### 硬件配置
- **CPU**: 待测试时记录
- **内存**: 待测试时记录
- **磁盘**: 待测试时记录

### 软件配置
- **.NET 版本**: .NET 8.0
- **操作系统**: 待测试时记录
- **BenchmarkDotNet 版本**: 0.15.6

## 基准测试结果

### 1. 路径生成性能 (Path Generation Benchmarks)

#### 测试场景
- `PathGeneration_Simple`: 生成单段路径（最简单场景）
- `PathGeneration_Complex`: 生成复杂路径（5段）
- `PathGeneration_Consecutive100`: 连续生成100个相同路径
- `PathGeneration_Alternating100`: 交替生成100个不同路径

#### 性能目标
- **路径生成**: < 1ms（目标 < 0.5ms）
- **批量处理**: 线性增长，无明显性能下降
- **内存分配**: 尽可能少的GC压力

#### 基线数据
```
// 运行命令
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release --filter *PathGenerationBenchmarks*

// 结果将在首次运行后填写
待补充
```

### 2. Overload 策略评估性能 (Overload Policy Benchmarks)

#### 测试场景
- `Evaluate_Normal`: 正常场景评估
- `Evaluate_Warning`: 预警拥堵场景
- `Evaluate_Severe`: 严重拥堵场景
- `Evaluate_OverCapacity`: 超容量场景
- `Evaluate_Timeout`: 超时场景
- `Evaluate_Batch100`: 批量评估100次
- `Evaluate_Batch1000`: 批量评估1000次

#### 性能目标
- **单次评估**: < 100μs
- **批量评估**: 线性增长
- **内存分配**: 接近零分配（使用 in 参数传递 struct）

#### 基线数据
```
// 运行命令
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release --filter *OverloadPolicyBenchmarks*

// 结果将在首次运行后填写
待补充
```

### 3. 高负载场景性能 (High Load Benchmarks)

#### 测试场景
- `Load_500ParcelsPerMinute`: 模拟500包裹/分钟负载
- `Load_1000ParcelsPerMinute`: 模拟1000包裹/分钟负载
- `Load_PeakLoad_1500ParcelsPerMinute`: 峰值负载1500包裹/分钟
- `EndToEnd_500ParcelsPerMinute`: 端到端性能（包含路径执行）

#### 性能目标
- **500包裹/分钟**: 平均处理时间 < 120ms/包裹
- **1000包裹/分钟**: 平均处理时间 < 60ms/包裹
- **峰值负载**: 系统不崩溃，可降级处理

#### 基线数据
```
// 运行命令
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release --filter *HighLoadBenchmarks*

// 结果将在首次运行后填写
待补充
```

### 4. 性能瓶颈分析 (Performance Bottleneck Benchmarks)

#### 测试场景
- 数据库访问性能
- 路径生成性能
- 路径执行性能
- 内存分配和GC压力
- 端到端性能分析

#### 基线数据
```
// 运行命令
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release --filter *PerformanceBottleneckBenchmarks*

// 结果将在首次运行后填写
待补充
```

## 运行时性能指标

### dotnet-counters 监控数据

#### 测试方法
```bash
# 启动应用
cd ZakYip.WheelDiverterSorter.Host
dotnet run -c Release

# 在另一个终端监控（需要先找到进程ID）
ps aux | grep WheelDiverterSorter
cd Tools/Profiling
./counters-monitor.sh -p <PID> -o baseline-metrics.csv
```

#### 关键指标基线

| 指标 | 目标值 | 基线值 | 备注 |
|------|--------|--------|------|
| CPU使用率 | < 70% | 待测试 | 正常负载下 |
| 工作集内存 | < 500 MB | 待测试 | 稳定运行后 |
| GC堆大小 | < 200 MB | 待测试 | 稳定运行后 |
| Gen0 GC次数 | < 100/分钟 | 待测试 | 正常负载 |
| Gen1 GC次数 | < 10/分钟 | 待测试 | 正常负载 |
| Gen2 GC次数 | < 1/分钟 | 待测试 | 正常负载 |
| 内存分配速率 | < 50 MB/s | 待测试 | 正常负载 |
| GC时间占比 | < 10% | 待测试 | 正常负载 |
| 线程池线程数 | < 50 | 待测试 | 正常负载 |
| 线程池队列长度 | < 10 | 待测试 | 正常负载 |
| 异常数量 | < 10/分钟 | 待测试 | 排除正常业务异常 |

### dotnet-trace 采样数据

#### 测试方法
```bash
# 对运行中的应用进行30秒采样
cd Tools/Profiling
./trace-sampling.sh -p <PID> -d 30 -o baseline-trace.nettrace

# 分析追踪文件
# - 使用 PerfView (Windows)
# - 使用 Visual Studio 性能分析器
# - 使用 speedscope.app (跨平台)
```

#### 热点函数分析

基线热点函数（按CPU时间排序）：

1. 待补充
2. 待补充
3. 待补充

## 性能优化记录

### PR-19: 初始性能基线与优化

#### 优化项目

1. **事件类型优化**
   - 将热路径事件类从 `class` 改为 `readonly record struct`
   - 优化的类型：
     * PathExecutionFailedEventArgs
     * PathSegmentExecutionFailedEventArgs
     * PathSwitchedEventArgs
     * SortOrderCreatedEventArgs
     * ParcelScannedEventArgs
     * DiverterDirectionChangedEventArgs
   - **预期收益**: 减少堆分配，降低Gen0 GC次数

2. **路由模板缓存**
   - 添加 `IRouteTemplateCache` 接口和 `DefaultRouteTemplateCache` 实现
   - 创建 `CachedSwitchingPathGenerator` 包装器
   - **预期收益**: 减少数据库访问，提升路径生成速度

3. **方法内联优化**
   - 为小型高频方法添加 `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
   - 优化的方法：
     * `ChuteIdHelper.FormatChuteId`
     * `ChuteIdHelper.ParseChuteId`
   - **预期收益**: 减少函数调用开销

4. **性能测试工具**
   - 添加 `OverloadPolicyBenchmarks` 基准测试
   - 创建 dotnet-trace 和 dotnet-counters 采样脚本
   - **收益**: 建立持续性能监控能力

#### 性能对比

优化前后对比（待测试后填写）：

| 指标 | 优化前 | 优化后 | 提升 |
|------|--------|--------|------|
| 路径生成平均时间 | 待测试 | 待测试 | - |
| Overload评估平均时间 | 待测试 | 待测试 | - |
| Gen0 GC次数/分钟 | 待测试 | 待测试 | - |
| Gen1 GC次数/分钟 | 待测试 | 待测试 | - |
| 内存分配速率 | 待测试 | 待测试 | - |

## 性能验收标准

### 新功能开发

对于新增的功能，必须满足以下性能要求：

1. **路径规划相关**
   - 单次路径生成 < 1ms
   - 缓存命中率 > 80%

2. **Overload 决策**
   - 单次评估 < 100μs
   - 批量评估保持线性增长

3. **主事件流**
   - 事件发布延迟 < 10μs
   - 事件处理延迟 < 100ms

### 重大改动验收

对于涉及以下模块的重大改动，必须重新运行基准测试并对比：

- **路由规划** (Routing / PathPlanner)
- **Overload 策略** (OverloadPolicy)
- **主事件流** (Event Bus / Event Handlers)
- **数据库访问层** (Repository)

#### 验收流程

1. **运行基准测试**
   ```bash
   cd ZakYip.WheelDiverterSorter.Benchmarks
   dotnet run -c Release
   ```

2. **对比关键指标**
   - 如果性能提升 > 10%，记录到本文档
   - 如果性能下降 > 5%，需要在PR中说明原因和权衡

3. **运行运行时监控**
   ```bash
   # 启动应用并监控5分钟
   ./Tools/Profiling/counters-monitor.sh -p <PID> -o pr-xxx-metrics.csv
   
   # 对比 Gen0/Gen1 次数、内存分配速率等指标
   ```

4. **必要时进行采样分析**
   ```bash
   ./Tools/Profiling/trace-sampling.sh -p <PID> -d 60 -o pr-xxx-trace.nettrace
   ```

## 性能监控最佳实践

### 日常监控

1. **定期运行基准测试**（每周或每次重大更新后）
2. **监控生产环境指标**（使用 Prometheus + Grafana）
3. **关注异常指标**（Gen2 GC次数突增、CPU持续高位）

### 性能问题排查

当发现性能问题时：

1. **快速诊断**
   ```bash
   # 实时查看关键指标
   ./Tools/Profiling/counters-monitor.sh -p <PID>
   ```

2. **深度分析**
   ```bash
   # 采样30-60秒
   ./Tools/Profiling/trace-sampling.sh -p <PID> -d 60
   ```

3. **分析热点**
   - 使用 PerfView 或 Visual Studio 分析 .nettrace 文件
   - 找出CPU占用最高的函数
   - 找出内存分配最多的代码路径

4. **针对性优化**
   - 减少不必要的分配
   - 优化算法复杂度
   - 添加缓存
   - 使用对象池

## 更新记录

| 日期 | 版本 | 修改内容 | 修改人 |
|------|------|----------|--------|
| 2025-11-18 | v1.0 | 创建性能基线文档框架 | GitHub Copilot |
| - | - | 待补充首次基准测试数据 | - |

## 参考资料

- [BenchmarkDotNet 文档](https://benchmarkdotnet.org/)
- [.NET 性能诊断](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/)
- [dotnet-trace 工具](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
- [dotnet-counters 工具](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- [性能优化最佳实践](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/performance-best-practices)
