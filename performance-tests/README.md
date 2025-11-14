# 性能测试指南

本目录包含使用 k6 进行性能和负载测试的脚本。

## 前置条件

### 安装 k6

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

或从 https://k6.io/docs/getting-started/installation 下载安装包

## 测试场景

### 1. 冒烟测试（Smoke Test）

**目的**: 快速验证系统基本功能是否正常

```bash
k6 run smoke-test.js
```

**特点**:
- 单个虚拟用户
- 运行1分钟
- 测试所有已知格口
- 验证错误处理

**使用场景**: 
- 代码变更后的快速验证
- 部署前的基础检查

### 2. 负载测试（Load Test）

**目的**: 验证系统在预期负载下的性能

```bash
k6 run load-test.js
```

**负载模式**:
- 逐步增加: 10 → 50 → 100 虚拟用户
- 总时长约7分钟
- 模拟真实包裹到达间隔（60-160ms）

**性能目标**:
- 95%请求延迟 < 500ms
- 错误率 < 10%
- 分拣操作 < 100ms

**使用场景**:
- 评估系统正常运行能力
- 验证是否满足 500-1000 包裹/分钟的需求

### 3. 压力测试（Stress Test）

**目的**: 找到系统极限和破坏点

```bash
k6 run stress-test.js
```

**负载模式**:
- 快速增加: 50 → 100 → 200 → 300 → 400 → 500 虚拟用户
- 总时长约12分钟
- 最小请求间隔

**使用场景**:
- 确定系统最大容量
- 识别性能瓶颈
- 验证系统在极端负载下的行为

### 4. 高负载测试（High-Load Test）⭐ 新增

**目的**: 验证系统在500-1000包裹/分钟负载下的性能和稳定性

```bash
k6 run high-load-test.js
```

**测试场景**:
1. **500包裹/分钟场景** - 持续5分钟
   - 恒定速率: 8请求/秒
   - 验证基础负载性能
   
2. **1000包裹/分钟场景** - 持续5分钟
   - 恒定速率: 17请求/秒
   - 验证高负载性能
   
3. **渐进式压力测试** - 持续10分钟
   - 从500逐步增加到2000包裹/分钟
   - 识别系统极限
   
4. **稳定性测试** - 持续30分钟
   - 600包裹/分钟持续运行
   - 验证长期稳定性

**性能阈值**:
- 500ppm场景: P95延迟 < 400ms, 错误率 < 2%
- 1000ppm场景: P95延迟 < 500ms, 错误率 < 5%
- 压力场景: P95延迟 < 800ms, 错误率 < 10%
- 稳定性场景: P95延迟 < 500ms, 错误率 < 3%

**使用场景**:
- 验证系统是否满足500-1000包裹/分钟的业务需求
- 评估系统在持续高负载下的稳定性
- 识别性能瓶颈和优化方向
- 压力测试找到系统极限

**输出指标**:
- HTTP请求时长 (P95, P99)
- 错误率（按场景分类）
- 分拣操作时长
- 吞吐量
- 成功/失败计数

## 自定义配置

### 修改基础URL

```bash
k6 run -e BASE_URL=http://your-server:port load-test.js
```

### 调整虚拟用户数

编辑脚本中的 `options.stages` 数组

### 修改性能阈值

编辑脚本中的 `options.thresholds` 对象

## 理解测试结果

k6 会输出以下关键指标：

### 请求指标
- `http_req_duration`: HTTP请求时长
  - `p(95)`: 95%的请求在该时间内完成
  - `p(99)`: 99%的请求在该时间内完成
- `http_req_failed`: 请求失败率
- `http_reqs`: 每秒请求数（RPS）

### 自定义指标
- `errors`: 错误率
- `sorting_duration`: 分拣操作时长（仅 load-test.js）

### 虚拟用户
- `vus`: 当前虚拟用户数
- `vus_max`: 最大虚拟用户数

## 性能基准

基于高吞吐量分拣系统需求（500-1000包裹/分钟）：

| 指标 | 目标值 | 备注 |
|-----|--------|------|
| 吞吐量 | 8-17 RPS | 500-1000包裹/分钟 |
| P95延迟 | < 500ms | 95%请求 |
| P99延迟 | < 1000ms | 99%请求 |
| 错误率 | < 5% | 正常运行 |
| 分拣时长 | < 100ms | 核心操作 |

## 持续监控

建议将性能测试集成到 CI/CD 流程：

```bash
# 在每次发布前运行冒烟测试
k6 run --quiet smoke-test.js

# 定期（如每周）运行完整负载测试
k6 run --out json=results.json load-test.js
```

## 故障排查

### 高错误率
- 检查服务器日志
- 验证数据库连接
- 确认配置数据完整性

### 高延迟
- 检查数据库查询性能
- 分析路径生成算法
- 验证并发控制机制

### 资源耗尽
- 监控 CPU/内存使用
- 检查线程池配置
- 验证连接池设置

## 与 BenchmarkDotNet 的配合

1. 使用 BenchmarkDotNet 优化单个组件
2. 使用 k6 验证系统级性能
3. 对比优化前后的端到端性能

## 扩展阅读

- [k6 官方文档](https://k6.io/docs/)
- [k6 最佳实践](https://k6.io/docs/testing-guides/test-types/)
- [k6 性能阈值](https://k6.io/docs/using-k6/thresholds/)
