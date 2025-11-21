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

## 🔬 仿真长跑模式 | Simulation Long-Run Mode

### 什么是长跑模式 | What is Long-Run Mode

长跑模式用于验证在高负载、摩擦抖动、随机掉包的情况下，系统能够持续稳定运行，且错分计数始终为 0。

Long-run mode validates that the system can run continuously under high load, friction variations, and random packet drops while maintaining zero mis-sorts.

### 启动长跑仿真 | Start Long-Run Simulation

```bash
# 使用长跑配置文件
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run --configuration Release -- --Simulation:IsLongRunMode=true --Simulation:LongRunDuration=01:00:00

# 或使用专用配置文件
dotnet run --configuration Release --Simulation:UseConfigFile=appsettings.LongRun.json
```

### 配置参数 | Configuration Parameters

在 `appsettings.LongRun.json` 中配置以下参数：

```json
{
  "Simulation": {
    "IsLongRunMode": true,              // 启用长跑模式
    "LongRunDuration": "01:00:00",      // 运行时长（1小时）
    "MaxLongRunParcels": 10000,         // 最大包裹数
    "MetricsPushIntervalSeconds": 30,   // 统计输出间隔（秒）
    "FailFastOnMisSort": false,         // 错分时是否立即退出
    "IsEnableRandomFriction": true,     // 启用随机摩擦
    "IsEnableRandomDropout": true       // 启用随机掉包
  }
}
```

### 启动监控栈 | Start Monitoring Stack

#### 使用 Docker Compose

```bash
# 启动 Prometheus + Grafana 监控栈
docker-compose -f docker-compose.monitoring.yml up -d

# 验证服务状态
docker-compose -f docker-compose.monitoring.yml ps

# 查看日志
docker-compose -f docker-compose.monitoring.yml logs -f
```

#### 非 Docker 方案

1. **启动 Prometheus**

```bash
# 修改 monitoring/prometheus/prometheus.yml，添加仿真端点
scrape_configs:
  - job_name: 'simulation'
    static_configs:
      - targets: ['localhost:9091']
    metrics_path: '/metrics'
    scrape_interval: 10s

# 启动 Prometheus
prometheus --config.file=monitoring/prometheus/prometheus.yml
```

2. **启动 Grafana**

```bash
# 安装并启动 Grafana
# macOS: brew install grafana && brew services start grafana
# Linux: sudo systemctl start grafana-server

# 访问 http://localhost:3000
# 默认账号: admin / admin
```

### Grafana 监控面板 | Grafana Monitoring

#### 访问 Grafana

1. 打开浏览器访问 `http://localhost:3000`
2. 使用默认账号登录：`admin` / `admin`
3. 添加 Prometheus 数据源：`Configuration` → `Data Sources` → `Add data source` → `Prometheus`
4. URL: `http://prometheus:9090` (Docker) 或 `http://localhost:9090` (本地)

#### 关键指标查询 | Key Metrics Queries

**1. 错分监控（应始终为 0）**

```promql
# 错分总数（应始终为 0）
simulation_mis_sort_total

# 错分增长率（应为 0）
rate(simulation_mis_sort_total[5m])
```

**2. 包裹状态分布**

```promql
# 按状态统计包裹数
simulation_parcel_total

# 成功分拣到目标格口
simulation_parcel_total{status="SortedToTargetChute"}

# 超时包裹
simulation_parcel_total{status="Timeout"}

# 掉包
simulation_parcel_total{status="Dropped"}

# 执行错误
simulation_parcel_total{status="ExecutionError"}
```

**3. 超时和掉包趋势**

```promql
# 超时率（每分钟）
rate(simulation_parcel_total{status="Timeout"}[1m]) * 60

# 掉包率（每分钟）
rate(simulation_parcel_total{status="Dropped"}[1m]) * 60

# 错误率（每分钟）
rate(simulation_parcel_total{status="ExecutionError"}[1m]) * 60
```

**4. 行程时间分布**

```promql
# P50 行程时间
histogram_quantile(0.50, rate(simulation_travel_time_seconds_bucket[5m]))

# P95 行程时间
histogram_quantile(0.95, rate(simulation_travel_time_seconds_bucket[5m]))

# P99 行程时间
histogram_quantile(0.99, rate(simulation_travel_time_seconds_bucket[5m]))

# 平均行程时间
rate(simulation_travel_time_seconds_sum[5m]) / 
rate(simulation_travel_time_seconds_count[5m])
```

**5. 随摩擦配置变化的行程时间**

在长跑模式下，系统每 1000 个包裹会切换一次场景（不同的摩擦配置），可以观察行程时间的分布变化。

```promql
# 行程时间热力图
increase(simulation_travel_time_seconds_bucket[1m])
```

### 验收标准 | Acceptance Criteria

✅ **仿真运行时 Prometheus 能正确抓取指标**
- 访问 http://localhost:9091/metrics 能看到 `simulation_*` 指标
- Prometheus Target 状态为 UP

✅ **Grafana 显示 simulation_mis_sort_total == 0**
- 在整个长跑过程中，错分计数保持为 0
- 如果出现错分，仿真会打印醒目警告

✅ **Timeout / Dropped 指标符合预期**
- 在启用随机掉包时，会有一定比例的 Dropped 状态
- 超时率在合理范围内（取决于摩擦配置）

✅ **行程时间分布随摩擦配置变化**
- 可以观察到每个场景批次的行程时间差异
- P95/P99 行程时间在不同批次有所不同

### 快速测试 | Quick Test

```bash
# 1. 启动监控栈
docker-compose -f docker-compose.monitoring.yml up -d

# 2. 运行 5 分钟长跑测试
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run -c Release -- \
  --Simulation:IsLongRunMode=true \
  --Simulation:LongRunDuration=00:05:00 \
  --Simulation:MaxLongRunParcels=1000

# 3. 查看 Prometheus 指标
curl http://localhost:9091/metrics | grep simulation_

# 4. 访问 Grafana
open http://localhost:3000
```

### 错分保护机制 | Mis-Sort Protection

如果检测到错分（`simulation_mis_sort_total > 0`）：

1. **日志记录**：记录 ERROR 级别日志
2. **控制台警告**：打印醒目的中文警告信息（红色高亮）
3. **Fail-Fast**：如果配置 `FailFastOnMisSort=true`，程序将立即退出（`Environment.Exit(1)`）

```
╔════════════════════════════════════════════════════════════╗
║                    ⚠️  严重错误  ⚠️                        ║
╠════════════════════════════════════════════════════════════╣
║  检测到包裹错分！                                          ║
║  包裹ID: 123456789                                         ║
║  目标格口: 5                                               ║
║  实际格口: 3                                               ║
║                                                            ║
║  当前错分总数: 1                                           ║
╚════════════════════════════════════════════════════════════╝
```

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
