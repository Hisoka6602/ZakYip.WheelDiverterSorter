# 高负载性能测试指南
# High-Load Performance Testing Guide

本文档详细介绍如何进行高负载场景测试（500-1000包裹/分钟）、压力测试、稳定性测试以及性能瓶颈分析。

This document provides detailed instructions on how to perform high-load scenario testing (500-1000 parcels/minute), stress testing, stability testing, and performance bottleneck analysis.

## 目录 | Table of Contents

1. [概述 | Overview](#概述--overview)
2. [性能目标 | Performance Targets](#性能目标--performance-targets)
3. [BenchmarkDotNet测试 | BenchmarkDotNet Tests](#benchmarkdotnet测试--benchmarkdotnet-tests)
4. [k6负载测试 | k6 Load Tests](#k6负载测试--k6-load-tests)
5. [CI/CD集成 | CI/CD Integration](#cicd集成--cicd-integration)
6. [性能瓶颈分析 | Performance Bottleneck Analysis](#性能瓶颈分析--performance-bottleneck-analysis)
7. [故障排查 | Troubleshooting](#故障排查--troubleshooting)

## 概述 | Overview

本系统的性能测试框架包含两个主要组件：

The performance testing framework includes two main components:

1. **BenchmarkDotNet** - .NET微基准测试框架，用于精确测量代码性能
2. **k6** - 现代负载测试工具，用于端到端系统级性能测试

## 性能目标 | Performance Targets

### 吞吐量要求 | Throughput Requirements

| 场景 | 包裹数/分钟 | 请求数/秒 | 说明 |
|------|------------|-----------|------|
| 正常负载 | 500 | 8.33 | 日常运营负载 |
| 高负载 | 1000 | 16.67 | 高峰时段负载 |
| 峰值负载 | 1500 | 25 | 短期峰值 |
| 极限测试 | 2000+ | 33+ | 压力测试 |

### 延迟要求 | Latency Requirements

| 指标 | 目标值 | 说明 |
|------|--------|------|
| P95延迟 | < 500ms | 95%的请求 |
| P99延迟 | < 1000ms | 99%的请求 |
| 平均延迟 | < 200ms | 平均响应时间 |

### 可靠性要求 | Reliability Requirements

| 指标 | 目标值 | 说明 |
|------|--------|------|
| 错误率 | < 5% | 正常负载下 |
| 可用性 | > 99% | 持续运行 |
| 并发度 | 100+ | 同时处理的包裹数 |

## BenchmarkDotNet测试 | BenchmarkDotNet Tests

### 1. 高负载场景测试

**测试文件**: `ZakYip.WheelDiverterSorter.Benchmarks/HighLoadBenchmarks.cs`

#### 运行所有高负载测试

```bash
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release -- --filter *HighLoadBenchmarks*
```

#### 测试场景说明

| 测试方法 | 描述 | 负载级别 |
|---------|------|---------|
| `Load_500ParcelsPerMinute` | 模拟500包裹/分钟 | 正常负载 |
| `Load_1000ParcelsPerMinute` | 模拟1000包裹/分钟 | 高负载 |
| `Load_PeakLoad_1500ParcelsPerMinute` | 模拟1500包裹/分钟 | 峰值负载 |
| `EndToEnd_500ParcelsPerMinute` | 端到端完整流程 | 正常负载 |
| `ConcurrentExecution_HighLoad` | 并发执行测试 | 高并发 |
| `BatchPathGeneration_100Paths` | 批量路径生成 | 批处理 |
| `BatchPathGeneration_500Paths` | 大批量路径生成 | 批处理 |
| `BatchPathGeneration_1000Paths` | 超大批量路径生成 | 批处理 |
| `MixedLoad_GenerationAndExecution` | 混合负载测试 | 混合 |
| `StressTest_ExtremeLoad` | 极限压力测试 | 压力测试 |

#### 运行特定测试

```bash
# 仅运行500包裹/分钟测试
dotnet run -c Release -- --filter *Load_500ParcelsPerMinute*

# 仅运行端到端测试
dotnet run -c Release -- --filter *EndToEnd*

# 仅运行压力测试
dotnet run -c Release -- --filter *StressTest*
```

#### 解读结果

BenchmarkDotNet会输出以下关键指标：

- **Mean**: 平均执行时间 - 评估典型性能
- **Error**: 误差范围 - 评估测量精度
- **StdDev**: 标准差 - 评估性能稳定性
- **Gen0/Gen1/Gen2**: GC统计 - 评估内存压力
- **Allocated**: 内存分配量 - 评估资源使用

**性能目标**:
- 路径生成: < 1ms (平均)
- 路径执行: < 100ms (平均，包含模拟硬件延迟)
- 内存分配: 尽可能少，避免频繁GC

### 2. 性能瓶颈分析测试

**测试文件**: `ZakYip.WheelDiverterSorter.Benchmarks/PerformanceBottleneckBenchmarks.cs`

#### 运行瓶颈分析

```bash
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release -- --filter *PerformanceBottleneckBenchmarks*
```

#### 瓶颈分析类别

| 类别 | 测试内容 | 关注点 |
|------|---------|--------|
| 数据库访问 | 读写性能 | I/O延迟 |
| 路径生成 | 算法性能 | CPU使用 |
| 路径执行 | 并发性能 | 锁竞争 |
| 内存分配 | GC压力 | 内存管理 |
| 端到端流程 | 整体性能 | 系统瓶颈 |
| 错误处理 | 异常路径 | 容错性能 |

#### 使用场景

1. **性能优化前**: 确定优化重点
2. **性能优化后**: 验证优化效果
3. **定期检查**: 监控性能退化
4. **新功能开发**: 评估性能影响

### 3. 导出和分析结果

```bash
# 导出为HTML报告
dotnet run -c Release -- --filter *HighLoadBenchmarks* --exporters html

# 导出为JSON和Markdown
dotnet run -c Release -- --filter *HighLoadBenchmarks* --exporters json,markdown

# 导出为CSV（用于Excel分析）
dotnet run -c Release -- --filter *HighLoadBenchmarks* --exporters csv
```

结果文件位置: `BenchmarkDotNet.Artifacts/results/`

## k6负载测试 | k6 Load Tests

### 1. 安装k6

#### macOS
```bash
brew install k6
```

#### Linux
```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

#### Windows
```powershell
choco install k6
```

### 2. 高负载测试 (新)

**测试文件**: `performance-tests/high-load-test.js`

#### 测试场景

该测试包含4个并发场景：

1. **500包裹/分钟场景** (5分钟)
   - 恒定速率: 8请求/秒
   - 验证基础负载性能

2. **1000包裹/分钟场景** (5分钟)
   - 恒定速率: 17请求/秒
   - 验证高负载性能

3. **渐进式压力测试** (10分钟)
   - 从500逐步增加到2000包裹/分钟
   - 识别系统极限

4. **稳定性测试** (30分钟)
   - 600包裹/分钟持续运行
   - 验证长期稳定性

#### 运行测试

```bash
# 确保应用程序正在运行
cd ZakYip.WheelDiverterSorter.Host
dotnet run --configuration Release &

# 运行高负载测试
cd performance-tests
k6 run high-load-test.js

# 使用自定义URL
k6 run -e BASE_URL=http://your-server:5000 high-load-test.js

# 导出结果
k6 run --out json=high-load-results.json high-load-test.js
```

#### 性能阈值

测试会自动检查以下阈值：

| 场景 | P95延迟 | 错误率 |
|------|---------|--------|
| 500ppm | < 400ms | < 2% |
| 1000ppm | < 500ms | < 5% |
| ramping | < 800ms | < 10% |
| stability | < 500ms | < 3% |

### 3. 其他k6测试

#### 冒烟测试
```bash
k6 run performance-tests/smoke-test.js
```

#### 负载测试
```bash
k6 run performance-tests/load-test.js
```

#### 压力测试
```bash
k6 run performance-tests/stress-test.js
```

### 4. 结果分析

k6会输出以下关键指标：

```
✓ http_req_duration....: avg=150ms  min=50ms  med=120ms  max=800ms  p(90)=250ms p(95)=350ms
✓ http_req_failed......: 2.50%  ✓ 125  ✗ 4875
✓ http_reqs............: 5000   16.666667/s
✓ iterations...........: 5000   16.666667/s
✓ successful_sorts.....: 4875   counter
✓ failed_sorts.........: 125    counter
✓ sorting_duration.....: avg=80ms   min=20ms  med=75ms   max=200ms  p(95)=95ms
```

**关键指标解释**:
- `http_req_duration`: HTTP请求总时长
- `http_req_failed`: 请求失败率
- `http_reqs`: 每秒请求数 (RPS)
- `successful_sorts/failed_sorts`: 成功/失败的分拣操作
- `sorting_duration`: 分拣操作时长

## CI/CD集成 | CI/CD Integration

### 自动化工作流

**配置文件**: `.github/workflows/performance-testing.yml`

### 触发方式

1. **手动触发**: 在GitHub Actions中选择测试类型
   - benchmark: BenchmarkDotNet测试
   - k6-smoke: k6冒烟测试
   - k6-load: k6负载测试
   - k6-stress: k6压力测试
   - k6-high-load: k6高负载测试
   - all: 所有测试

2. **定期执行**: 每周日凌晨2点自动运行

3. **PR触发**: 对核心代码的PR自动运行轻量级测试

### 查看结果

1. 进入GitHub Actions页面
2. 选择 "Performance Testing" 工作流
3. 下载测试结果工件 (Artifacts)
4. 查看测试摘要

### 性能回归检测

工作流会自动保存测试结果，可用于：
- 对比不同版本的性能
- 识别性能退化
- 追踪性能改进

## 性能瓶颈分析 | Performance Bottleneck Analysis

### 1. 识别瓶颈

运行瓶颈分析基准测试：

```bash
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release -- --filter *PerformanceBottleneckBenchmarks*
```

### 2. 常见瓶颈及优化建议

#### 数据库访问瓶颈

**症状**:
- `DatabaseRead_*` 测试耗时较长
- `DatabaseWrite_*` 测试耗时较长

**优化方案**:
- 添加缓存层（如Redis）
- 批量读写操作
- 数据库索引优化
- 连接池配置

#### 路径生成瓶颈

**症状**:
- `PathGeneration_*` 测试耗时较长
- CPU使用率高

**优化方案**:
- 路径缓存
- 算法优化
- 并行处理
- 预计算常用路径

#### 路径执行瓶颈

**症状**:
- `PathExecution_*` 测试耗时较长
- 并发测试性能差

**优化方案**:
- 优化锁策略
- 异步执行
- 资源池化
- 并发控制优化

#### 内存分配瓶颈

**症状**:
- Gen0/Gen1/Gen2频繁回收
- Allocated值很高

**优化方案**:
- 对象池化
- 减少临时对象创建
- 使用Span<T>和Memory<T>
- 值类型优化

### 3. 性能分析工具

#### dotnet-trace
```bash
# 收集性能追踪
dotnet trace collect --process-id <PID> --duration 00:00:30

# 分析追踪文件
dotnet trace report trace.nettrace
```

#### dotnet-counters
```bash
# 实时监控性能计数器
dotnet counters monitor --process-id <PID>
```

#### BenchmarkDotNet内置分析
```bash
# 启用内存诊断器
dotnet run -c Release -- --memory

# 启用线程诊断器
dotnet run -c Release -- --threading
```

## 故障排查 | Troubleshooting

### 高错误率

**检查项**:
1. 查看应用日志
2. 验证数据库连接
3. 检查配置数据
4. 确认网络连接

**解决方案**:
```bash
# 检查服务器日志
cd ZakYip.WheelDiverterSorter.Host
dotnet run --configuration Release --verbose

# 验证配置
cat appsettings.json
```

### 高延迟

**检查项**:
1. 数据库查询性能
2. 路径生成算法
3. 锁竞争情况
4. 网络延迟

**解决方案**:
```bash
# 运行瓶颈分析
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release -- --filter *Bottleneck*

# 分析慢查询
# 检查数据库日志
```

### 资源耗尽

**检查项**:
1. CPU使用率
2. 内存使用
3. 线程池状态
4. 连接池状态

**监控命令**:
```bash
# 监控资源使用
top -p <PID>

# 监控.NET计数器
dotnet counters monitor --process-id <PID>

# 内存分析
dotnet dump collect --process-id <PID>
dotnet dump analyze <dump-file>
```

### k6测试失败

**常见问题**:
1. 服务器未启动
2. 端口冲突
3. 配置数据缺失
4. 超时设置不当

**解决方案**:
```bash
# 检查服务器状态
curl http://localhost:5000/health

# 检查端口占用
lsof -i :5000

# 增加超时时间
k6 run --http-timeout 30s performance-tests/high-load-test.js
```

## 最佳实践 | Best Practices

### 1. 测试环境

- 使用专用测试环境
- 保持环境一致性
- 隔离其他负载
- 记录环境配置

### 2. 测试执行

- 预热系统（运行前先执行小规模测试）
- 多次运行取平均值
- 记录测试条件
- 保存测试结果

### 3. 结果分析

- 关注趋势而非绝对值
- 对比历史数据
- 识别异常值
- 验证假设

### 4. 持续改进

- 定期运行性能测试
- 设置性能目标
- 跟踪性能指标
- 及时优化瓶颈

## 参考资料 | References

- [BenchmarkDotNet文档](https://benchmarkdotnet.org/articles/overview.html)
- [k6文档](https://k6.io/docs/)
- [.NET性能最佳实践](https://learn.microsoft.com/en-us/dotnet/framework/performance/)
- [GitHub Actions文档](https://docs.github.com/en/actions)

## 支持 | Support

如有问题或建议，请提交Issue或联系开发团队。

For questions or suggestions, please submit an issue or contact the development team.
