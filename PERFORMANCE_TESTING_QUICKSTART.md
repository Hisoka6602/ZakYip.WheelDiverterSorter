# 性能测试快速入门 | Performance Testing Quick Start

## 🚀 快速开始 | Quick Start

### 1. 运行BenchmarkDotNet测试 | Run BenchmarkDotNet Tests

```bash
# 所有高负载测试
cd ZakYip.WheelDiverterSorter.Benchmarks
dotnet run -c Release -- --filter *HighLoadBenchmarks*

# 瓶颈分析
dotnet run -c Release -- --filter *PerformanceBottleneckBenchmarks*

# 所有性能测试
dotnet run -c Release
```

### 2. 运行k6负载测试 | Run k6 Load Tests

```bash
# 启动应用
cd ZakYip.WheelDiverterSorter.Host
dotnet run --configuration Release &

# 等待启动
sleep 15

# 运行测试
cd ../performance-tests

# 冒烟测试 (1分钟)
k6 run smoke-test.js

# 负载测试 (7分钟)
k6 run load-test.js

# 压力测试 (12分钟)
k6 run stress-test.js

# 高负载测试 (50分钟) ⭐
k6 run high-load-test.js
```

### 3. 在CI/CD中运行 | Run in CI/CD

1. 访问 GitHub Actions
2. 选择 "Performance Testing" 工作流
3. 点击 "Run workflow"
4. 选择测试类型:
   - `benchmark` - BenchmarkDotNet测试
   - `k6-high-load` - 高负载测试
   - `all` - 所有测试

## 📊 性能目标 | Performance Targets

| 场景 | 包裹数/分钟 | 请求数/秒 | P95延迟 | 错误率 |
|------|------------|-----------|---------|--------|
| 正常负载 | 500 | 8.33 | < 400ms | < 2% |
| 高负载 | 1000 | 16.67 | < 500ms | < 5% |
| 峰值负载 | 1500 | 25 | < 800ms | < 10% |
| 极限测试 | 2000+ | 33+ | - | < 20% |

## 📁 文件位置 | File Locations

```
ZakYip.WheelDiverterSorter.Benchmarks/
├── HighLoadBenchmarks.cs              # 高负载测试
├── PerformanceBottleneckBenchmarks.cs # 瓶颈分析
├── PathGenerationBenchmarks.cs        # 路径生成测试
└── PathExecutionBenchmarks.cs         # 路径执行测试

performance-tests/
├── smoke-test.js                      # 冒烟测试
├── load-test.js                       # 负载测试
├── stress-test.js                     # 压力测试
└── high-load-test.js                  # 高负载测试 ⭐

.github/workflows/
└── performance-testing.yml            # CI/CD工作流 ⭐

Documentation/
├── HIGH_LOAD_PERFORMANCE_TESTING.md   # 详细指南 ⭐
└── HIGH_LOAD_PERFORMANCE_TESTING_SUMMARY.md # 实施总结 ⭐
```

## 🔍 测试内容 | Test Coverage

### BenchmarkDotNet (微基准测试)

**HighLoadBenchmarks** - 10个测试:
- ✅ 500/1000/1500包裹/分钟负载
- ✅ 端到端性能测试
- ✅ 并发执行测试
- ✅ 批量处理 (100/500/1000)
- ✅ 混合负载测试
- ✅ 极限压力测试

**PerformanceBottleneckBenchmarks** - 20+个测试:
- ✅ 数据库访问性能
- ✅ 路径生成性能
- ✅ 路径执行性能
- ✅ 内存分配和GC
- ✅ 端到端流程
- ✅ 错误处理性能

### k6 (端到端负载测试)

**high-load-test.js** - 4个场景:
1. 500包裹/分钟 (5分钟)
2. 1000包裹/分钟 (5分钟)
3. 渐进式压力 (500→2000包裹/分钟, 10分钟)
4. 稳定性测试 (600包裹/分钟, 30分钟)

## 📈 查看结果 | View Results

### BenchmarkDotNet结果

```
BenchmarkDotNet.Artifacts/results/
├── *.html   # HTML报告
├── *.md     # Markdown报告
└── *.csv    # CSV数据
```

### k6结果

控制台输出 + JSON文件:
```bash
k6 run --out json=results.json high-load-test.js
```

### CI/CD结果

1. GitHub Actions页面
2. 选择工作流运行
3. 下载Artifacts
4. 查看测试摘要

## 🛠️ 常见问题 | Troubleshooting

### 问题: 端口被占用
```bash
# 查找占用进程
lsof -i :5000

# 终止进程
kill -9 <PID>
```

### 问题: k6未安装
```bash
# macOS
brew install k6

# Linux
sudo apt-get install k6
```

### 问题: 性能测试失败
```bash
# 1. 检查应用是否运行
curl http://localhost:5000/health

# 2. 检查日志
cd ZakYip.WheelDiverterSorter.Host
dotnet run --configuration Release

# 3. 增加超时时间
k6 run --http-timeout 30s high-load-test.js
```

## 📚 详细文档 | Detailed Documentation

- 📘 [HIGH_LOAD_PERFORMANCE_TESTING.md](HIGH_LOAD_PERFORMANCE_TESTING.md) - 完整指南
- 📗 [HIGH_LOAD_PERFORMANCE_TESTING_SUMMARY.md](HIGH_LOAD_PERFORMANCE_TESTING_SUMMARY.md) - 实施总结
- 📙 [performance-tests/README.md](performance-tests/README.md) - k6测试指南

## 🎯 使用场景 | Use Cases

### 开发阶段
```bash
# 优化前 - 识别瓶颈
dotnet run -c Release -- --filter *PerformanceBottleneckBenchmarks*

# 优化后 - 验证效果
dotnet run -c Release -- --filter *HighLoadBenchmarks*
```

### 测试阶段
```bash
# 快速验证
k6 run smoke-test.js

# 完整测试
k6 run high-load-test.js
```

### 发布前
```bash
# 在GitHub Actions中触发完整测试
# 检查所有指标是否达标
```

### 生产监控
```bash
# 定期运行 (CI/CD自动执行)
# 对比历史数据
# 识别性能退化
```

## ⚡ 性能优化建议 | Optimization Tips

基于瓶颈分析结果:

1. **数据库优化**
   - 添加缓存层
   - 批量操作
   - 索引优化

2. **算法优化**
   - 路径缓存
   - 预计算
   - 并行处理

3. **并发优化**
   - 锁策略优化
   - 异步处理
   - 资源池化

4. **内存优化**
   - 对象池化
   - 减少分配
   - GC调优

---

**需要帮助?** 查看详细文档或提交Issue
