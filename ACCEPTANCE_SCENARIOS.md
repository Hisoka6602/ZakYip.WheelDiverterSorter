# 验收场景文档 / Acceptance Scenarios Documentation

本文档描述了用于系统验收测试的标准仿真场景，特别是场景 E 长跑仿真的详细说明。

This document describes the standard simulation scenarios for system acceptance testing, with special focus on Scenario E long-run simulation.

---

## 目录 / Table of Contents

- [场景 E：长跑仿真与 Observability 验收](#场景-e长跑仿真与-observability-验收)
  - [场景概述](#场景概述)
  - [场景参数](#场景参数)
  - [拓扑配置](#拓扑配置)
  - [如何启动](#如何启动)
  - [监控指标](#监控指标)
  - [验收标准](#验收标准)
  - [Grafana Dashboard](#grafana-dashboard)
  - [故障排查](#故障排查)

---

## 场景 E：长跑仿真与 Observability 验收

### 场景概述

场景 E 是一个标准的长时间仿真场景，用于验证系统在高密度、持续运行环境下的稳定性和正确性。该场景模拟了真实生产环境中的高负载情况。

**Scenario E** is a standard long-run simulation scenario designed to validate system stability and correctness under high-density, continuous operation. It simulates real production high-load conditions.

### 场景参数

| 参数 | 值 | 说明 |
|------|-----|------|
| **摆轮数量** | 10 台 | 10 wheel diverters |
| **格口配置** | 1-10 正常，11 异常口 | Chutes 1-10 normal, 11 exception |
| **包裹数量** | 1000 个（可配置）| 1000 parcels (configurable) |
| **包裹间隔** | 300ms | 300ms between parcels |
| **线速** | 1000 mm/s (1 m/s) | Belt speed |
| **分拣模式** | RoundRobin | Round-robin distribution |
| **摩擦因子** | 0.95 - 1.05 | Friction variation ±5% |
| **掉包概率** | 0% | No dropout |
| **预计行程时间** | ~120 秒 | ~2 minutes from entry to exception |

### 拓扑配置

场景 E 使用的拓扑配置具有以下特点：

**Topology Configuration Features:**

1. **10 台摆轮，中间长度不一致**
   - 每个摆轮之间的输送线段长度不同
   - 长度范围：800mm - 1500mm
   - 在 `InMemoryRouteConfigurationRepository` 中配置

   ```csharp
   var segmentLengths = new[] { 800, 1200, 1500, 900, 1100, 1300, 1000, 1400, 950, 1250 };
   ```

2. **异常口在末端**
   - ChuteId = 11（第 11 号格口）
   - 位于所有摆轮的最后
   - 用于处理高密度包裹和异常情况

3. **高密度流量处理**
   - 最小安全头距：300mm / 300ms
   - 违反头距的包裹自动路由到异常口
   - 支持并发处理多个包裹

### 如何启动

#### 方法一：使用一键脚本（推荐）

**Linux/macOS:**
```bash
cd /path/to/ZakYip.WheelDiverterSorter
./monitoring/run-scenario-e-longrun.sh
```

**Windows PowerShell:**
```powershell
cd C:\path\to\ZakYip.WheelDiverterSorter
.\monitoring\run-scenario-e-longrun.ps1
```

**自定义参数:**
```bash
# 指定包裹数量
PARCEL_COUNT=500 ./monitoring/run-scenario-e-longrun.sh

# 指定运行时长（5分钟）
LONG_RUN_DURATION="00:05:00" ./monitoring/run-scenario-e-longrun.sh

# 不启动监控栈
START_MONITORING=false ./monitoring/run-scenario-e-longrun.sh
```

#### 方法二：手动启动

**1. 启动监控栈（可选）**
```bash
docker-compose -f docker-compose.monitoring.yml up -d
```

**2. 运行场景 E 仿真**
```bash
cd ZakYip.WheelDiverterSorter.Simulation

dotnet run -c Release -- \
  --Simulation:IsLongRunMode=true \
  --Simulation:ParcelCount=1000 \
  --Simulation:LineSpeedMmps=1000 \
  --Simulation:ParcelInterval=00:00:00.300 \
  --Simulation:SortingMode=RoundRobin \
  --Simulation:ExceptionChuteId=11 \
  --Simulation:IsEnableRandomFriction=true \
  --Simulation:FrictionModel:MinFactor=0.95 \
  --Simulation:FrictionModel:MaxFactor=1.05 \
  --Simulation:MinSafeHeadwayMm=300 \
  --Simulation:MinSafeHeadwayTime=00:00:00.300 \
  --Simulation:DenseParcelStrategy=RouteToException \
  --Simulation:MetricsPushIntervalSeconds=30 \
  --Simulation:IsEnableVerboseLogging=false \
  --Simulation:IsPauseAtEnd=false
```

### 监控指标

场景 E 暴露以下 Prometheus 指标用于监控和验收：

#### 核心业务指标 / Core Business Metrics

| 指标名称 | 类型 | 说明 | 验收要求 |
|---------|------|------|---------|
| `sorting_total_parcels` | Counter | 总处理包裹数 | 应等于创建的包裹数 |
| `sorting_failed_parcels_total` | Counter (labeled) | 失败包裹数，按原因分类 | 记录失败原因分布 |
| `sorting_success_latency_seconds` | Histogram | 成功包裹从入口到落格的延迟 | P95 < 180s |
| `system_state_changes_total` | Counter (labeled) | 状态机状态切换计数 | 监控状态转换 |

**失败原因标签 (reason):**
- `upstream_timeout`: 上游超时
- `ttl_failure`: TTL 失败（已废弃，使用 upstream_timeout）
- `topology_unreachable`: 拓扑不可达
- `sensor_fault`: 传感器故障
- `dropped`: 掉包
- `execution_error`: 执行错误
- `ruleengine_timeout`: 规则引擎超时
- `wrong_chute`: 错分（**必须为 0**）

#### 仿真专用指标 / Simulation-Specific Metrics

| 指标名称 | 类型 | 说明 | 验收要求 |
|---------|------|------|---------|
| `simulation_parcel_total` | Counter (labeled) | 按状态分类的包裹总数 | 统计各状态分布 |
| `simulation_mis_sort_total` | Counter | 错分总数 | **必须为 0** |
| `simulation_travel_time_seconds` | Histogram | 包裹行程时间分布 | 监控行程时间 |

#### 高密度包裹指标 / Dense Parcel Metrics

| 指标名称 | 类型 | 说明 |
|---------|------|------|
| `simulation_dense_parcel_total` | Counter (labeled) | 高密度包裹总数 |
| `simulation_dense_parcel_routed_to_exception_total` | Counter (labeled) | 路由到异常口的包裹数 |
| `simulation_dense_parcel_headway_time_seconds` | Histogram | 头距时间分布 |
| `simulation_dense_parcel_headway_distance_mm` | Histogram | 头距距离分布 |

### 验收标准

#### ✅ 必须满足 / Must Pass

1. **零错分要求 (Zero Mis-Sort Requirement)**
   ```promql
   simulation_mis_sort_total == 0
   ```
   - 所有包裹必须被正确分拣或标记为失败
   - 不允许将包裹送到错误的格口

2. **包裹数量一致性 (Parcel Count Consistency)**
   ```promql
   sorting_total_parcels == <创建的包裹数>
   ```
   - 处理的包裹总数应等于创建的包裹数

3. **成功率合理 (Reasonable Success Rate)**
   ```promql
   rate(simulation_parcel_total{status="SortedToTargetChute"}[5m]) / 
   rate(simulation_parcel_total[5m]) > 0.7
   ```
   - 在正常摩擦条件下，成功率应 > 70%
   - 场景 E 预期成功率：85%-95%

4. **延迟可接受 (Acceptable Latency)**
   ```promql
   histogram_quantile(0.95, rate(sorting_success_latency_seconds_bucket[5m])) < 180
   ```
   - P95 延迟应 < 180 秒（3 分钟）
   - P50 延迟应 < 120 秒（2 分钟）

5. **高密度包裹正确处理 (Correct Dense Parcel Handling)**
   ```promql
   simulation_dense_parcel_routed_to_exception_total > 0
   ```
   - 高密度包裹应被正确识别并路由到异常口
   - 验证高密度检测机制工作正常

#### 📊 应当观察 / Should Observe

1. **状态转换正常 (Normal State Transitions)**
   - 监控 `system_state_changes_total` 确保状态转换合理
   - 不应出现异常的状态循环

2. **失败原因分布合理 (Reasonable Failure Distribution)**
   - 大多数失败应来自高密度路由到异常口
   - 不应有大量 `execution_error` 或 `sensor_fault`

3. **行程时间分布稳定 (Stable Travel Time Distribution)**
   - 行程时间应在一个合理范围内
   - 不应有极端异常值（除非是预期的异常口路由）

### Grafana Dashboard

#### 如何查看 Dashboard

1. **访问 Grafana**
   - URL: http://localhost:3000
   - 默认账号: admin / admin

2. **导入 Dashboard（首次使用）**
   - 进入 Dashboard → Import
   - 上传文件: `monitoring/grafana/dashboards/wheel-diverter-sorter.json`
   - 或使用已配置的自动加载（provisioning）

3. **查看场景 E 关键面板**

#### 关键面板说明 / Key Panel Descriptions

**1. 错分监控面板 (Mis-Sort Monitor)**
```promql
simulation_mis_sort_total
```
- **显示类型**: Stat Panel
- **阈值**: 0 = 绿色，>= 1 = 红色
- **验收要求**: 必须始终为 0

**2. 包裹状态分布 (Parcel Status Distribution)**
```promql
sum by (status) (increase(simulation_parcel_total[5m]))
```
- **显示类型**: Pie Chart
- **用途**: 查看各状态包裹的分布比例

**3. 成功率趋势 (Success Rate Trend)**
```promql
# 每分钟成功分拣的包裹数
rate(simulation_parcel_total{status="SortedToTargetChute"}[5m]) * 60

# 成功率百分比
rate(simulation_parcel_total{status="SortedToTargetChute"}[5m]) / 
rate(simulation_parcel_total[5m]) * 100
```
- **显示类型**: Time Series Graph
- **验收要求**: 成功率应 > 70%

**4. 延迟分位数 (Latency Quantiles)**
```promql
# P50
histogram_quantile(0.50, rate(sorting_success_latency_seconds_bucket[5m]))

# P95
histogram_quantile(0.95, rate(sorting_success_latency_seconds_bucket[5m]))

# P99
histogram_quantile(0.99, rate(sorting_success_latency_seconds_bucket[5m]))
```
- **显示类型**: Time Series Graph
- **验收要求**: P95 < 180s, P50 < 120s

**5. 失败原因分布 (Failure Reason Distribution)**
```promql
sum by (reason) (increase(sorting_failed_parcels_total[5m]))
```
- **显示类型**: Bar Gauge
- **用途**: 查看失败包裹的原因分布

**6. 高密度包裹监控 (Dense Parcel Monitoring)**
```promql
# 每分钟路由到异常口的包裹数
rate(simulation_dense_parcel_routed_to_exception_total[5m]) * 60

# 头距时间 P50/P95
histogram_quantile(0.50, rate(simulation_dense_parcel_headway_time_seconds_bucket[5m]))
histogram_quantile(0.95, rate(simulation_dense_parcel_headway_time_seconds_bucket[5m]))
```
- **显示类型**: Time Series Graph
- **用途**: 监控高密度包裹处理情况

**7. 状态转换热力图 (State Transition Heatmap)**
```promql
rate(system_state_changes_total[5m])
```
- **显示类型**: Heatmap
- **用途**: 观察状态机转换模式

### 指标阈值定义 / Metric Threshold Definitions

#### 可上线标准 (Production Ready)

系统在以下条件下可视为"可上线"：

| 指标 | 阈值 | 说明 |
|------|------|------|
| `simulation_mis_sort_total` | = 0 | 零错分 |
| 成功率 | > 85% | 高成功率 |
| P95 延迟 | < 150s | 低延迟 |
| P99 延迟 | < 180s | 极端情况可接受 |
| 高密度识别率 | > 95% | 准确识别高密度包裹 |

#### 表现异常标准 (Abnormal Performance)

出现以下情况时，表明系统表现异常：

| 指标 | 阈值 | 说明 |
|------|------|------|
| `simulation_mis_sort_total` | > 0 | **严重**: 出现错分 |
| 成功率 | < 70% | **警告**: 成功率过低 |
| P95 延迟 | > 200s | **警告**: 延迟过高 |
| `execution_error` 比例 | > 5% | **警告**: 执行错误过多 |
| `sensor_fault` 比例 | > 5% | **警告**: 传感器故障过多 |

### 故障排查

#### 问题：simulation_mis_sort_total > 0

**原因分析:**
- 路径规划算法错误
- 摆轮控制逻辑错误
- 传感器数据不准确

**排查步骤:**
1. 查看仿真日志，搜索 "错分" 或 "MisSort"
2. 检查错分包裹的详细信息（目标格口 vs 实际格口）
3. 审查路径生成和执行代码

#### 问题：成功率 < 70%

**原因分析:**
- 高密度包裹过多导致路由到异常口
- 摩擦因子设置过大
- 超时设置过短

**排查步骤:**
1. 检查高密度包裹比例: `simulation_dense_parcel_routed_to_exception_total`
2. 查看失败原因分布: `sorting_failed_parcels_total`
3. 调整配置参数或优化高密度处理策略

#### 问题：P95 延迟 > 200s

**原因分析:**
- 输送线长度配置过长
- 线速过慢
- 包裹在某些节点卡住

**排查步骤:**
1. 查看延迟分布直方图
2. 检查 `simulation_travel_time_seconds_bucket` 的分布
3. 审查拓扑配置和线速设置

#### 问题：Metrics 端点无响应

**原因分析:**
- 端口 9091 被占用
- 长跑模式未启用
- Metrics 服务器启动失败

**排查步骤:**
```bash
# 检查端口占用
lsof -i:9091  # Linux/macOS
netstat -ano | findstr :9091  # Windows

# 检查配置
grep "IsLongRunMode" appsettings*.json

# 查看仿真启动日志
# 应该看到: "Prometheus metrics 端点已启动: http://localhost:9091/metrics"
```

#### 问题：Prometheus 未抓取到指标

**原因分析:**
- Prometheus 配置错误
- 网络连接问题
- Metrics 端点未启动

**排查步骤:**
1. 访问 Prometheus Targets: http://localhost:9090/targets
2. 检查 `simulation` job 状态
3. 手动访问 metrics 端点: `curl http://localhost:9091/metrics`
4. 检查 `monitoring/prometheus/prometheus.yml` 配置

---

## 其他场景

有关其他仿真场景（A、B、C、D、HD-1、HD-2 等），请参见：
- [SCENARIO_E_DOCUMENTATION.md](SCENARIO_E_DOCUMENTATION.md) - 场景 E 基础版本
- [LONG_RUN_SIMULATION_IMPLEMENTATION.md](LONG_RUN_SIMULATION_IMPLEMENTATION.md) - 长跑模式实现细节
- [test-all-simulations.sh](test-all-simulations.sh) - 所有场景的测试脚本

---

## 相关文档

- [PROMETHEUS_GUIDE.md](PROMETHEUS_GUIDE.md) - Prometheus 指标详细文档
- [GRAFANA_DASHBOARD_GUIDE.md](GRAFANA_DASHBOARD_GUIDE.md) - Grafana 仪表板使用指南
- [PERFORMANCE_TESTING_QUICKSTART.md](PERFORMANCE_TESTING_QUICKSTART.md) - 性能测试快速开始
- [monitoring/README.md](monitoring/README.md) - 监控基础设施说明

---

**文档版本 / Document Version**: 1.0  
**最后更新 / Last Updated**: 2025-11-17  
**状态 / Status**: ✅ 完成 / Completed
