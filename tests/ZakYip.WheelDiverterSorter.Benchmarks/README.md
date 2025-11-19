# ZakYip.WheelDiverterSorter 性能基准测试

本项目使用 BenchmarkDotNet 进行性能基准测试，用于测量和优化系统的关键代码路径。

## 运行基准测试

### 运行所有基准测试

```bash
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release
```

### 运行特定基准测试类

```bash
# 仅运行路径生成基准测试
dotnet run -c Release --filter *PathGenerationBenchmarks*

# 仅运行路径执行基准测试
dotnet run -c Release --filter *PathExecutionBenchmarks*
```

### 运行特定基准测试方法

```bash
dotnet run -c Release --filter *GeneratePath_SingleSegment*
```

## 基准测试类

### PathGenerationBenchmarks

测试路径生成器的性能：

- `GeneratePath_SingleSegment`: 生成单段路径（最简单场景）
- `GeneratePath_TwoSegments`: 生成两段路径（常见场景）
- `GeneratePath_Unknown`: 处理未知格口（错误场景）
- `GeneratePath_Batch100`: 批量生成100个路径

### PathExecutionBenchmarks

测试路径执行器的性能：

- `ExecutePath_SingleSegment`: 执行单段路径
- `ExecutePath_TwoSegments`: 执行两段路径
- `ExecutePath_Batch10`: 批量执行10个路径

### OverloadPolicyBenchmarks

测试超载策略评估性能：

- `Evaluate_Normal`: 正常场景评估
- `Evaluate_Moderate`: 预警拥堵场景评估
- `Evaluate_Severe`: 严重拥堵场景评估
- `Evaluate_OverCapacity`: 超容量场景评估
- `Evaluate_Timeout`: 超时场景评估
- `Evaluate_Batch100`: 批量评估100次
- `Evaluate_Batch1000`: 批量评估1000次（高频场景）
- `CreatePolicy`: 策略创建开销测试

### HighLoadBenchmarks

测试高负载场景性能（500-1000包裹/分钟）：

- `Load_500ParcelsPerMinute`: 模拟500包裹/分钟
- `Load_1000ParcelsPerMinute`: 模拟1000包裹/分钟
- `Load_PeakLoad_1500ParcelsPerMinute`: 峰值负载
- `EndToEnd_500ParcelsPerMinute`: 端到端性能测试
- `ConcurrentExecution_HighLoad`: 高并发执行测试
- `BatchPathGeneration_*`: 批量路径生成测试
- `MixedLoad_GenerationAndExecution`: 混合负载测试

### PerformanceBottleneckBenchmarks

性能瓶颈分析基准测试：

- **数据库访问**: `DatabaseRead_*`, `DatabaseWrite_*`
- **路径生成**: `PathGeneration_*`
- **路径执行**: `PathExecution_*`
- **内存分配**: `MemoryAllocation_*`
- **端到端**: `EndToEnd_*`
- **错误处理**: `ErrorHandling_*`

## 理解基准测试结果

BenchmarkDotNet 会输出以下指标：

- **Mean**: 平均执行时间
- **Error**: 误差范围
- **StdDev**: 标准差
- **Gen0/Gen1/Gen2**: GC 回收统计
- **Allocated**: 分配的内存量

### 性能目标

基于高吞吐量分拣系统的需求（500-1000包裹/分钟）：

- 路径生成：< 1ms（目标 < 0.5ms）
- 路径执行：< 100ms（包含硬件响应时间）
- 批量处理：线性增长，无明显性能下降
- 内存分配：尽可能少的GC压力

## 持续监控

建议：

1. 在CI/CD中定期运行基准测试
2. 比较不同版本的性能差异
3. 在优化前后运行基准测试以验证改进
4. 使用 BenchmarkDotNet 的导出功能生成报告

## 导出结果

BenchmarkDotNet 会在 `BenchmarkDotNet.Artifacts` 目录中生成详细报告：

- HTML报告：`results/*.html`
- Markdown报告：`results/*.md`
- CSV数据：`results/*.csv`

## 注意事项

1. **必须在 Release 模式下运行**：Debug 模式会严重影响性能测量
2. **关闭其他应用程序**：减少系统干扰
3. **多次运行**：BenchmarkDotNet 会自动进行多次迭代以获得准确结果
4. **不要过早优化**：先确定性能瓶颈，再进行针对性优化
