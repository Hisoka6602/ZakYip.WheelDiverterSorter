# 队列性能测试使用指南

## 快速开始

### 运行快速性能评估（推荐）

```bash
cd tests/ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release -- --quick-queue-test
```

**输出示例**：
```
==================================================
队列性能快速评估
==================================================

【1. 基本操作性能测试】
  入队 100000 次: 125.81 ms
  平均每次入队: 1.26 μs
  出队 100000 次: 58.78 ms
  平均每次出队: 0.59 μs

【2. 300包裹/秒场景测试】
  300包裹入队 + 150包裹出队: 0.31 ms
  平均每包裹处理时间: 0.00 ms
  是否满足300包裹/秒: 是 ✓

... (更多测试结果)
```

**测试时间**：约2-3秒

### 运行完整BenchmarkDotNet测试（耗时较长）

```bash
cd tests/ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release --filter "*QueuePerformanceBenchmarks.SingleEnqueue*"
```

**测试时间**：约1-2分钟（单个测试）

## 测试项说明

### 快速评估测试包含：

1. **基本操作性能**
   - 测试100,000次入队/出队操作
   - 评估单次操作的平均耗时

2. **300包裹/秒场景**
   - 模拟每秒300个包裹的实际负载
   - 验证是否满足性能需求

3. **6个摆轮并发**
   - 模拟6个Position同时工作
   - 测试并发锁竞争情况

4. **时钟访问性能**
   - 测试ISystemClock.LocalNow的开销
   - 对比DateTime.Now的性能

5. **批量修改操作**
   - 测试UpdateAffectedParcelsToStraight性能
   - 测试RemoveAllTasksForParcel性能

6. **1分钟持续运行**
   - 模拟18,000个包裹（300/秒 × 60秒）
   - 评估实际吞吐量

7. **1小时运行预估**
   - 基于10秒测试预估1小时性能
   - 评估是否能处理1,080,000个包裹

### BenchmarkDotNet详细测试

提供更精确的性能指标，包括：
- 内存分配（Memory Diagnoser）
- GC统计（Gen0/Gen1/Gen2）
- 置信区间（Confidence Interval）
- 标准差（Standard Deviation）

## 性能指标解读

### 性能目标
- **需求**：300包裹/秒
- **每包裹可用时间**：3.33 ms

### 测试结果对比

| 场景 | 实际性能 | 性能余量 | 结论 |
|------|---------|----------|------|
| 300包裹/秒 | 0.001 ms/包裹 | 3,330倍 | ✓ 通过 |
| 1分钟持续 | 1,466,646 包裹/秒 | 4,889倍 | ✓ 优异 |
| 1小时预估 | 0.01 分钟 | 6,000倍 | ✓ 充足 |

### 性能瓶颈阈值

| 操作 | 当前性能 | 瓶颈阈值 | 状态 |
|------|---------|----------|------|
| 入队 | 1.26 μs | >1000 μs | ✓ 正常 |
| 出队 | 0.59 μs | >1000 μs | ✓ 正常 |
| 时钟访问 | 0.04 ns | >1000 ns | ✓ 正常 |
| 批量修改 | 2.31 ms/300任务 | >100 ms | ✓ 正常 |

## 故障排查

### 如果性能测试失败

1. **确认环境**：
   ```bash
   dotnet --version  # 应为.NET 8.0+
   ```

2. **清理并重建**：
   ```bash
   dotnet clean
   dotnet build -c Release
   ```

3. **检查依赖**：
   ```bash
   dotnet restore
   ```

### 如果测试结果与预期不符

1. **检查CPU负载**：测试期间其他程序占用CPU
2. **检查内存**：确保有足够的可用内存
3. **关闭其他程序**：避免资源竞争
4. **多次运行**：取平均值

## 实际场景诊断

如果现场感觉性能慢，按以下步骤诊断：

### 1. 运行快速性能测试
```bash
dotnet run -c Release -- --quick-queue-test
```

**如果测试结果正常**（>1,000包裹/秒），说明队列本身无问题。

### 2. 检查容差配置

查看 `ConveyorSegmentConfiguration.TimeToleranceMs`：
```bash
# 检查数据库配置
sqlite3 your_database.db "SELECT * FROM ConveyorSegmentConfiguration;"
```

**症状**：容差太小导致频繁超时

**解决**：调整容差为 `实际传输时间 × 1.5`

### 3. 分析日志

搜索超时事件：
```bash
grep "超时" logs/*.log | wc -l
```

**如果超时频率 > 1%**，说明容差配置不当。

### 4. 使用性能分析工具

**推荐工具**：
- JetBrains dotTrace（性能分析）
- JetBrains dotMemory（内存分析）
- Visual Studio Profiler

**关注点**：
- 摆轮驱动执行时间
- 数据库IO时间
- 网络通信时间
- 日志输出时间

## 相关文档

- [QUEUE_PERFORMANCE_REPORT.md](./QUEUE_PERFORMANCE_REPORT.md) - 详细性能评估报告
- [../../docs/CORE_ROUTING_LOGIC.md](../../docs/CORE_ROUTING_LOGIC.md) - 核心路由逻辑说明
- [../../docs/guides/SYSTEM_CONFIG_GUIDE.md](../../docs/guides/SYSTEM_CONFIG_GUIDE.md) - 系统配置指南

## 常见问题

### Q: 为什么快速测试和BenchmarkDotNet结果不同？

A: BenchmarkDotNet执行预热（warmup）和多次迭代，结果更精确但耗时更长。快速测试适合日常检查，BenchmarkDotNet适合深度分析。

### Q: 测试结果显示性能优异，为什么现场还是慢？

A: 队列本身不是瓶颈。应检查：
1. 容差配置（最可能）
2. 硬件响应时间
3. 网络延迟
4. 数据库IO
5. 日志开销

### Q: 如何优化队列性能？

A: **不需要优化**。当前性能已有4,889倍余量。应优先优化其他组件。

### Q: 时钟访问会影响性能吗？

A: **不会**。测试显示时钟访问开销可忽略（<0.001%）。

---

**最后更新**：2025-12-27
